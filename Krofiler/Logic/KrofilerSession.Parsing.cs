using System;
using System.IO;
using System.Threading;
using HeapShot.Reader;
using MonoDevelop.Profiler;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		ProfilerRunner runner;

		public void ProcessFile()
		{
			int retries = 0;
		retryOpeningLogfile:
			retries++;
			if (retries > 1) {
				if (retries == 4) {
					if (!File.Exists(fileToProcess)) {
						ReportUserError("File doesn't exist.");
					} else if (new FileInfo(fileToProcess).Length == 0) {
						ReportUserError("File is empty.");
					} else {
						ReportUserError("Failed to open/read file.");
					}
				}
				Thread.Sleep(500);
			}
			LogFileReader reader;
			try {
				reader = new LogFileReader(fileToProcess);
			} catch {
				goto retryOpeningLogfile;
			}
			var header = Header.Read(reader);
			if (header == null) {
				goto retryOpeningLogfile;
			}
			TcpPort = header.Port;
			reader.Header = header;
			var cancellationToken = cts.Token;
			var allEvents = new List<Event>();
			while (!cancellationToken.IsCancellationRequested) {
				var buffer = BufferHeader.Read(reader);
				if (buffer == null) {
					if (runner != null) {
						if (runner.HasExited)
							break;
						Thread.Sleep(100);
						continue;
					} else {
						break;
					}
				}
				ParsingProgress = (double)reader.Position / (double)reader.Length;
				reader.BufferHeader = buffer;
				while (!reader.IsBufferEmpty) {
#if DONT_ORDER_BY_TIME
					ProcessEvent(Event.Read(reader));
#else
					allEvents.Add(Event.Read(reader));
#endif
				}
			}
			var allHeapObjects = new List<HeapEvent>();
			foreach (var ev in allEvents) {
				var heapEvent = ev as HeapEvent;
				if (heapEvent != null) {
					if (heapEvent.Type == HeapEvent.EventType.Object) {
						allHeapObjects.Add(heapEvent);
					}
					if (heapEvent.Type == HeapEvent.EventType.End) {
						foreach (var item in allHeapObjects) {
							item.Time = heapEvent.Time - 1;
						}
					}
				}
			}
			foreach (var ev in allEvents.OrderBy(e => e.Time)) {
				ProcessEvent(ev);
			}
			Finished?.Invoke(this);
#if DEBUG
			DoSomeCoolStuff();
#endif
		}

		CancellationTokenSource cts = new CancellationTokenSource();
		Thread parsingThread;
		internal void StartParsing()
		{
			parsingThread = new Thread(new ThreadStart(ProcessFile));
			parsingThread.Start();
		}

		public class AllocStruct
		{
			public StackFrame StackFrame;
			public long Object;
		}


		public string GetTypeName(long id)
		{
			if (classIdToName.ContainsKey(id)) {
				return classIdToName[id];
			} else {
				return "<no name>";
			}
		}


		Heapshot currentHeapshot;
		List<Heapshot> heapshots = new List<Heapshot>();
		public List<GcMoveElement> allMoves = new List<GcMoveElement>();

		public Dictionary<long, string> methodsNames = new Dictionary<long, string>();
		//int maxDepth = 0;
		StackFrame GetStackFrame(long[] backtrace)
		{
			//maxDepth = Math.Max(maxDepth, backtrace.Length);
			//if (backtrace.Length > 100) {
			//	Console.WriteLine("Bt:");
			//	foreach (var v in backtrace)
			//		Console.WriteLine(v + "(" + GetMethod(v) + "),");
			//}
			return rootStackFrame.GetStackFrame(this, backtrace, 0);
		}

		StackFrame rootStackFrame = new StackFrame(null, -1);

		public string GetMethodName(long methodId)
		{
			if (methodId == -1)
				return "[root]";
			if (!methodsNames.ContainsKey(methodId))
				return "Not existing(" + methodId + ").";
			return methodsNames[methodId];
		}

		Dictionary<long, string> classIdToName = new Dictionary<long, string>();
		public Dictionary<long, Tuple<uint, StackFrame>> allocs = new Dictionary<long, Tuple<uint, StackFrame>>();
		public double ParsingProgress { get; private set; }
		List<string> allImagesPaths = new List<string>();

		void ProcessEvent(Event ev)
		{
			var allocEvent = ev as AllocEvent;
			if (allocEvent != null) {
				allocs[allocEvent.Obj] = Tuple.Create((uint)(ev.Time / 1000), GetStackFrame(allocEvent.Backtrace));
				//Console.WriteLine($"A:{allocEvent.Obj} {Helper.Time(ev)}");
				return;
			}
			var gcEvent = ev as MoveGcEvent;
			if (gcEvent != null) {
				for (var i = 0; i < gcEvent.ObjAddr.Length; i += 2) {
					var f = gcEvent.ObjAddr[i];
					var t = gcEvent.ObjAddr[i + 1];
					//Console.WriteLine($"M:{f}->{t} {Helper.Time(ev)}");
					if (allocs.ContainsKey(f))
						allocs[t] = allocs[f];
					allMoves.Add(new GcMoveElement() {
						From = f,
						To = t
					});
				}
				//GC Move events that came within 1 second of last heapshot
				//consider them as they came before heapshot(hacky workaround runtime problem)
				if (heapshots.Any() && gcEvent.Time - 1000000000 < heapshots.Last().endTime)
					heapshots.Last().MovesPosition = allMoves.Count;
				return;
			}
			var methodEvent = ev as MethodEvent;
			if (methodEvent != null) {
				if (methodEvent.Type == MethodEvent.MethodType.Jit) {
					methodsNames.Add(methodEvent.Method, methodEvent.Name);
				}
				return;
			}
			var typeEvent = ev as MetadataEvent;
			if (typeEvent != null) {
				switch (typeEvent.MType) {
					case MetadataEvent.MetaDataType.Class:
						classIdToName[typeEvent.Pointer] = typeEvent.Name;
						break;
					case MetadataEvent.MetaDataType.Image:
						allImagesPaths.Add(typeEvent.Name);
						break;
					case MetadataEvent.MetaDataType.Assembly:
						break;
					case MetadataEvent.MetaDataType.Domain:
						break;
					case MetadataEvent.MetaDataType.Thread:
						break;
					case MetadataEvent.MetaDataType.Context:
						break;
					default:
						break;
				}
				return;
			}
			var heapEvent = ev as HeapEvent;
			if (heapEvent != null) {
				switch (heapEvent.Type) {
					case HeapEvent.EventType.Start:
						Console.WriteLine("Start:" + heapshots.Count);
						currentHeapshot = new Heapshot(this, "Heap " + (heapshots.Count + 1));
						currentHeapshot.startTime = heapEvent.Time;
						break;
					case HeapEvent.EventType.End:
						Console.WriteLine("End:" + heapshots.Count);
						currentHeapshot.endTime = heapEvent.Time;
						heapshots.Add(currentHeapshot);
						NewHeapshot?.Invoke(this, currentHeapshot);
						currentHeapshot = null;
						break;
					case HeapEvent.EventType.Root:
						//for (int i = 0; i < heapEvent.RootRefs.Length; i++) {
						//	currentHeapshot.AddRootRef(heapEvent.RootRefs[i], heapEvent.RootRefTypes[i], heapEvent.RootRefExtraInfos[i]);
						//}
						break;
					case HeapEvent.EventType.Object:
						currentHeapshot.AddObject(heapEvent);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
				return;
			}
		}
	}
}

