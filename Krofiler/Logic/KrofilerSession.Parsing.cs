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

		LogStreamHeader ReadHeader(string mldpFilePath)
		{
			if (!File.Exists(mldpFilePath))
				return null;
			try {
				using (var s = File.OpenRead(mldpFilePath))
				using (var _reader = new LogReader(s, true)) {
					return new LogStreamHeader(_reader);
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
			var header = ReadHeader(fileToProcess);
			if (header == null) {
				goto retryOpeningLogfile;
			}
			TcpPort = header.Port;
			var cancellationToken = cts.Token;

			try {
				using (fileStream = new FileStream(fileToProcess, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					var processor = new LogProcessor(fileStream, new KrofilerLogEventVisitor(this));
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

		public event Action CountersDescriptionsAdded;
		public IReadOnlyList<CounterDescriptionsEvent.CounterDescription> Descriptions;
		public event Action<CounterSamplesEvent> CounterSamplesAdded;
		//Dictionary<long, List<Action<object>>> callbacks = new Dictionary<long, List<Action<object>>>();
		//public void RegisterCounterCallback<T>(CounterDescriptionsEvent.CounterDescription description, Action<object> action)
		//{
		//	if (callbacks.TryGetValue(description.Index, out var callbackList)){
		//		callbackList.Add(action);
		//	} else {
		//		callbacks[description.Index] = new List<Action<object>>() { action };
		//	}
		//}


		class KrofilerLogEventVisitor : LogEventVisitor
		{
			public override void Visit(CounterDescriptionsEvent ev)
			{
				session.Descriptions = ev.Descriptions;
				session.CountersDescriptionsAdded?.Invoke();
			}

			public override void Visit(CounterSamplesEvent ev)
			{
				session.CounterSamplesAdded?.Invoke(ev);
				//foreach (var sample in ev.Samples) {
				//	if (session.callbacks.TryGetValue(sample.Index, out var callbackList))
				//		foreach (var callback in callbackList) {
				//			callback(sample.Value);
				//		}
				//}
			}

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
					} else {
						Console.WriteLine("Scary stuff, moving something that doesn't exist.");
					}
				}
			}

			int heapshotCounter;
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
				try {
					obj.Allocation = allocationsTracker[ev.ObjectPointer];
				} catch {
					obj.Allocation = new AllocationEvent();
					Console.WriteLine("OMG:" + session.classIdToName[vtableToClass[ev.VTablePointer]]);
				}
				obj.ObjAddr = ev.ObjectPointer;
				obj.TypeId = vtableToClass[ev.VTablePointer];
				obj.ReferencesTo = ev.References.Select(r => r.ObjectPointer).ToArray();
				obj.ReferencesAt = ev.References.Select(r => (ushort)r.Offset).ToArray();
				if (!currentHeapshot.TypesToObjectsListMap.ContainsKey(obj.TypeId))
					currentHeapshot.TypesToObjectsListMap[obj.TypeId] = new List<ObjectInfo>();
				currentHeapshot.TypesToObjectsListMap[obj.TypeId].Add(obj);

				currentHeapshot.ObjectsInfoMap.Add(ev.ObjectPointer, obj);
			}

			Dictionary<long, long> vtableToClass = new Dictionary<long, long>();
			public override void Visit(VTableLoadEvent ev)
			{
				vtableToClass[ev.VTablePointer] = ev.ClassPointer;
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
							if (rootReg.RootPointer < root.AddressPointer && rootReg.RootPointer + rootReg.RootSize >= root.AddressPointer) {
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
				var index = rootsEventsBinary.BinarySearch(ev.RootPointer);
				if (index < 0) {//negative index means it's not there
					index = ~index;
					if (index - 1 >= 0) {
						var oneBefore = rootsEvents[rootsEventsBinary[index - 1]];
						if (oneBefore.RootPointer + oneBefore.RootSize > ev.RootPointer) {
							Console.WriteLine("2 HeapRootRegisterEvents overlap:");
							Console.WriteLine(ev);
							Console.WriteLine(oneBefore);
						}
					}
					if (index < rootsEventsBinary.Count) {
						var oneAfter = rootsEvents[rootsEventsBinary[index]];
						if (oneAfter.RootPointer < ev.RootPointer + ev.RootSize) {
							Console.WriteLine("2 HeapRootRegisterEvents overlap:");
							Console.WriteLine(ev);
							Console.WriteLine(oneAfter);
						}
					}
					rootsEventsBinary.Insert(index, ev.RootPointer);
					rootsEvents.Add(ev.RootPointer, ev);
				} else {
					Console.WriteLine("2 HeapRootRegisterEvent at same address:");
					Console.WriteLine(ev);
					Console.WriteLine(rootsEvents[ev.RootPointer]);
					rootsEvents[ev.RootPointer] = ev;
				}
			}

			public override void Visit(HeapRootUnregisterEvent ev)
			{
				if (rootsEvents.Remove(ev.RootPointer)) {
					var index = rootsEventsBinary.BinarySearch(ev.RootPointer);
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

