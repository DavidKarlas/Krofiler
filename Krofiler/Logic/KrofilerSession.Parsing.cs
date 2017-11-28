using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Krofiler.Reader;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		ProfilerRunner runner;
		public LargeList<object> AllAllocsAndMoves = new LargeList<object>();
		public List<object> AllRootRegAndUnReg = new List<object>();
		public List<MethodJit> AllMethods = new List<MethodJit>();

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
			Reader.Reader reader;
			try {
				reader = new Reader.Reader(new FileStream(fileToProcess, FileMode.Open, FileAccess.Read, FileShare.Read));
			} catch {
				goto retryOpeningLogfile;
			}

			PointerSize = reader.PointerSize;

			var cancellationToken = cts.Token;
			//We do this here, because roots are reported before HeapStart :(
			//Hopefully with small change in Mono we can fix this
			currentHeapshot = new Heapshot(0, this, AllAllocsAndMoves.Count);
			while (!cancellationToken.IsCancellationRequested) {
				var obj = reader.ReadNext();
				if (obj == null) {
					if (runner != null) {
						if (runner.HasExited)
							break;
						Thread.Sleep(100);
						continue;
					} else {
						break;
					}
				}
				ParsingProgress = reader.Progress;
				var heapObject = obj as HeapObject;
				if (heapObject != null) {
					currentHeapshot.Add(heapObject.Address, heapObject);
					continue;
				}
				if (obj is HeapAlloc) {
					AllAllocsAndMoves.Add(obj);
					continue;
				}
				if (obj is HeapMoves) {
					AllAllocsAndMoves.Add(obj);
					continue;
				}
				var root = obj as Root;
				if (root != null) {
					for (int i = 0; i < root.Addresses.Length; i++) {
						currentHeapshot.Roots[root.Objects[i]] = new RootInfo() {
							Object = root.Addresses[i]
						};
					}
					continue;
				}
				var moreReferences = obj as MoreReferences;
				if (moreReferences != null) {
					currentHeapshot[moreReferences.Address].Refs = currentHeapshot[moreReferences.Address].Refs.Concat(moreReferences.Refs).ToArray();
					currentHeapshot[moreReferences.Address].Offsets = currentHeapshot[moreReferences.Address].Offsets.Concat(moreReferences.Offsets).ToArray();
					continue;
				}
				var classInfo = obj as ClassInfo;
				if (classInfo != null) {
					currentHeapshot.Types.Add(classInfo.Id, classInfo);
					continue;
				}
				if (obj is HeapStart) {
					currentHeapshot.AllocsAndMovesStartPosition = AllAllocsAndMoves.Count;
					continue;
				}
				if (obj is HeapEnd) {
					currentHeapshot.Initialize();
					heapshots.Add(currentHeapshot);
					NewHeapshot?.Invoke(this, currentHeapshot);
					currentHeapshot = new Heapshot(heapshots.Count, this, AllAllocsAndMoves.Count);
					continue;
				}
				if (obj is RootRegister rr) {
					AllRootRegAndUnReg.Add(rr);
				} else if (obj is RootUnregister ur) {
					AllRootRegAndUnReg.Add(ur);
				}else if(obj is MethodJit mj){
					AllMethods.Add(mj);
				}
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

		public string GetMethodName(long methodId)
		{
			throw new NotImplementedException();
		}

		Heapshot currentHeapshot;
		List<Heapshot> heapshots = new List<Heapshot>();

		public Dictionary<long, Tuple<uint, StackFrame, bool>> allocs = new Dictionary<long, Tuple<uint, StackFrame, bool>>(1024);

		public double ParsingProgress { get; private set; }
	}
}

