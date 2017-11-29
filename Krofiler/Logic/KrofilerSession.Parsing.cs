using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Profiler.Log;

namespace Krofiler
{
	public partial class KrofilerSession
	{
		ProfilerRunner runner;
		LogStreamHeader header;

		class HeadReaderLogEventVisitor : LogEventVisitor
		{
			public CancellationTokenSource TokenSource = new CancellationTokenSource();

			public override void VisitBefore(LogEvent ev)
			{
				TokenSource.Cancel();
			}
		}

		LogStreamHeader ReadHeader(string mldpFilePath)
		{
			if (!File.Exists(mldpFilePath))
				return null;
			try {
				using (var s = File.OpenRead(mldpFilePath)) {
					var visitor = new HeadReaderLogEventVisitor();
					var processor = new LogProcessor(s, visitor, null);
					try {
						processor.Process(visitor.TokenSource.Token);
					} catch (OperationCanceledException) {
					}
					return processor.StreamHeader;
				}
			} catch (Exception e) {
				ReportUserError(e.Message, e.ToString());
				return null;
			}
		}

		TaskCompletionSource<bool> completionSource;
		CancellationToken cancellation;
		FileStream fileStream;

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
			header = ReadHeader(fileToProcess);
			if (header == null) {
				goto retryOpeningLogfile;
			}
			TcpPort = header.Port;
			var cancellationToken = cts.Token;

			try {
				using (fileStream = new FileStream(fileToProcess, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					var processor = new LogProcessor(fileStream, new KrofilerLogEventVisitor(this), null);
					processor.Process(cancellation, runner != null);
				}
				if (cancellation.IsCancellationRequested)
					completionSource.SetCanceled();
				else
					completionSource.SetResult(true);
			} catch (Exception e) {
				if (cancellation.IsCancellationRequested)
					completionSource.SetCanceled();
				else
					completionSource.SetException(e);
			}

			Finished?.Invoke(this);
		}

		class KrofilerLogEventVisitor : LogEventVisitor
		{
			private KrofilerSession session;
			Dictionary<long, AllocationEvent> allocationsTracker = new Dictionary<long, AllocationEvent>();

			public KrofilerLogEventVisitor(KrofilerSession session)
			{
				this.session = session;
			}

			public override void Visit(AllocationEvent ev)
			{
				allocationsTracker[ev.ObjectPointer] = ev;
			}

			public override void Visit(GCMoveEvent ev)
			{
				for (int i = 0; i < ev.NewObjectPointers.Count; i++) {
					if (allocationsTracker.TryGetValue(ev.OldObjectPointers[i], out var allocation)) {
						allocationsTracker[ev.NewObjectPointers[i]] = allocation;
						allocationsTracker.Remove(ev.OldObjectPointers[i]);
					}
				}
			}

			int heapshotCounter = 0;
			Heapshot currentHeapshot;
			Stopwatch processingHeapTime;

			public override void Visit(HeapBeginEvent ev)
			{
				processingHeapTime = Stopwatch.StartNew();
				currentHeapshot = new Heapshot(session, ++heapshotCounter);
			}

			public override void Visit(HeapEndEvent ev)
			{
				processingHeapTime.Stop();
				Console.WriteLine($"It took {processingHeapTime.Elapsed} to procees heapshot {heapshotCounter}.");
				var deadAllocations = new HashSet<long>();
				foreach (var key in allocationsTracker.Keys)
					if (!currentHeapshot.ObjectsInfoMap.ContainsKey(key))
						deadAllocations.Add(key);
				foreach (var key in deadAllocations) {
					allocationsTracker.Remove(key);
				}

				session.NewHeapshot?.Invoke(session, currentHeapshot);
				currentHeapshot = null;
			}

			public override void Visit(HeapObjectEvent ev)
			{
				if (ev.ObjectSize == 0) {
					var existingObj = currentHeapshot.ObjectsInfoMap[ev.ObjectPointer];
					//Todo: optimise to not use linq?
					existingObj.ReferencesTo = existingObj.ReferencesTo.Concat(ev.References.Select(r => r.ObjectPointer)).ToArray();
					existingObj.ReferencesAt = existingObj.ReferencesAt.Concat(ev.References.Select(r => (ushort)r.Offset)).ToArray();
					return;
				}

				var obj = new ObjectInfo();
				obj.Allocation = allocationsTracker[ev.ObjectPointer];
				obj.ObjAddr = ev.ObjectPointer;
				obj.TypeId = ev.ClassPointer;
				obj.ReferencesTo = ev.References.Select(r => r.ObjectPointer).ToArray();
				obj.ReferencesAt = ev.References.Select(r => (ushort)r.Offset).ToArray();
				if (!currentHeapshot.TypesToObjectsListMap.ContainsKey(ev.ClassPointer))
					currentHeapshot.TypesToObjectsListMap[ev.ClassPointer] = new List<ObjectInfo>();
				currentHeapshot.TypesToObjectsListMap[ev.ClassPointer].Add(obj);

				currentHeapshot.ObjectsInfoMap.Add(ev.ObjectPointer, obj);
			}

			public override void Visit(ClassLoadEvent ev)
			{
				session.classIdToName[ev.ClassPointer] = ev.Name;
			}

			public override void Visit(HeapRootsEvent ev)
			{
				if (currentHeapshot != null) {
					foreach (var root in ev.Roots) {
						var index = rootsEventsBinary.BinarySearch(root.AddressPointer);
						if (index < 0) {
							index = ~index;
							if (index == 0) {
								Console.WriteLine($"This should not happen. Root is before any HeapRootsEvent {root.AddressPointer}.");
								continue;
							}
							var rootReg = rootsEvents[rootsEventsBinary[index - 1]];
							if (rootReg.Start < root.AddressPointer && rootReg.Start + rootReg.Size >= root.AddressPointer) {
								currentHeapshot.Roots[root.ObjectPointer] = rootReg;
							} else {
								Console.WriteLine($"This should not happen. Closest root is too small({root.AddressPointer}):");
								Console.WriteLine(rootReg);
							}
						} else {
							//We got exact match
							currentHeapshot.Roots[root.ObjectPointer] = rootsEvents[root.AddressPointer];
						}
					}
				} else {
					//Console.WriteLine("This should not happen. HeapRootsEvent outside HeapshotBegin/End.");
				}
			}

			Dictionary<long, HeapRootRegisterEvent> rootsEvents = new Dictionary<long, HeapRootRegisterEvent>();
			List<long> rootsEventsBinary = new List<long>();

			public override void Visit(HeapRootRegisterEvent ev)
			{
				var index = rootsEventsBinary.BinarySearch(ev.Start);
				if (index < 0) {//negative index means it's not there
					index = ~index;
					if (index - 1 >= 0) {
						var oneBefore = rootsEvents[rootsEventsBinary[index - 1]];
						if (oneBefore.Start + oneBefore.Size > ev.Start) {
							Console.WriteLine("2 HeapRootRegisterEvents overlap:");
							Console.WriteLine(ev);
							Console.WriteLine(oneBefore);
						}
					}
					if (index < rootsEventsBinary.Count) {
						var oneAfter = rootsEvents[rootsEventsBinary[index]];
						if (oneAfter.Start < ev.Start + ev.Size) {
							Console.WriteLine("2 HeapRootRegisterEvents overlap:");
							Console.WriteLine(ev);
							Console.WriteLine(oneAfter);
						}
					}
					rootsEventsBinary.Insert(index, ev.Start);
					rootsEvents.Add(ev.Start, ev);
				} else {
					Console.WriteLine("2 HeapRootRegisterEvent at same address:");
					Console.WriteLine(ev);
					Console.WriteLine(rootsEvents[ev.Start]);
					rootsEvents[ev.Start] = ev;
				}
			}

			public override void Visit(HeapRootUnregisterEvent ev)
			{
				if (rootsEvents.Remove(ev.Start)) {
					var index = rootsEventsBinary.BinarySearch(ev.Start);
					rootsEventsBinary.RemoveAt(index);
				} else {
					Console.WriteLine("HeapRootUnregisterEvent attempted at address that was not Registred:");
					Console.WriteLine(ev);
				}
			}

			public override void Visit(JitEvent ev)
			{
				session.methodsNames[ev.MethodPointer] = ev.Name;
			}
		}

		CancellationTokenSource cts = new CancellationTokenSource();
		Thread parsingThread;
		internal Task StartParsing()
		{
			if (completionSource != null)
				return completionSource.Task;
			completionSource = new TaskCompletionSource<bool>();
			parsingThread = new Thread(new ThreadStart(ProcessFile));
			parsingThread.Start();
			return completionSource.Task;
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

		public Dictionary<long, string> methodsNames = new Dictionary<long, string>();

		public string GetMethodName(long methodId)
		{
			if (methodId == -1)
				return "[root]";
			if (!methodsNames.ContainsKey(methodId))
				return "Not existing(" + methodId + ").";
			return methodsNames[methodId];
		}

		Dictionary<long, string> classIdToName = new Dictionary<long, string>();
		public double ParsingProgress {
			get {
				if (completionSource?.Task?.IsCompleted ?? false)
					return 100;
				try {
					if ((fileStream?.Length ?? 0) == 0)
						return 0;
					return (double)fileStream.Position / fileStream.Length;
				} catch {
					return 0;
				}
			}
		}
		List<string> allImagesPaths = new List<string>();
	}
}

