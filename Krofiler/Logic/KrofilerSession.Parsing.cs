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
		public LogProcessor processor;

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
			ProcessId = header.ProcessId;
			var cancellationToken = cts.Token;

			try {
				processor = new LogProcessor(fileToProcess, new KrofilerLogEventVisitor(this));
				processor.Process(cancellation, runner != null);
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
		public List<SuperEvent> Descriptions = new List<SuperEvent>();
		public event Action<SuperEvent> CounterSamplesAdded;
		//Dictionary<long, List<Action<object>>> callbacks = new Dictionary<long, List<Action<object>>>();
		//public void RegisterCounterCallback<T>(CounterDescriptionsEvent.CounterDescription description, Action<object> action)
		//{
		//	if (callbacks.TryGetValue(description.Index, out var callbackList)){
		//		callbackList.Add(action);
		//	} else {
		//		callbacks[description.Index] = new List<Action<object>>() { action };
		//	}
		//}

		public event Action<KrofilerSession, TimeSpan, double, double> AllocationsPerSecond;

		class KrofilerLogEventVisitor : LogEventVisitor
		{
			Timer timer;
			public override void VisitCounterDescriptionsEvent(SuperEvent ev)
			{
				session.Descriptions.Add(ev);
				session.CountersDescriptionsAdded?.Invoke();
				if (timer == null)
					timer = new Timer(ProcessingTimer, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1));
			}

			private void ProcessingTimer(object state)
			{
				ProcessAllocationsPerSecond();
			}

			public override void VisitCounterSamplesEvent(SuperEvent ev)
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
			Dictionary<long, ulong> allocationsTracker = new Dictionary<long, ulong>();

			public KrofilerLogEventVisitor(KrofilerSession session)
			{
				this.session = session;
			}

			int lastReportedSecond = -1;
			ulong newestSecond = 0;
			private void ProcessAllocationsPerSecond()
			{
				while (lastReportedSecond + 20 < (int)newestSecond) {
					lastReportedSecond++;
					session.AllocationsPerSecond?.Invoke(session, TimeSpan.FromMilliseconds(lastReportedSecond * 100), allocatedObjectsPerSecond[lastReportedSecond], allocatedBytesPerSecond[lastReportedSecond]);
				}
			}

			//TODO: I'm lazy... lower this to 100 and start from begining of array once end is reached
			uint[] allocatedObjectsPerSecond = new uint[100000];
			uint[] allocatedBytesPerSecond = new uint[100000];
			public override void VisitAllocationEvent(SuperEvent ev)
			{
				var sec = ev.Timestamp / 100000000;
				if (newestSecond < sec)
					newestSecond = sec;
				if ((int)sec <= lastReportedSecond)
					Console.WriteLine("Trouble in paradise!");
				allocatedObjectsPerSecond[sec]++;
				allocatedBytesPerSecond[sec] += (uint)ev.AllocationEvent_ObjectSize;
				allocationsTracker[ev.AllocationEvent_ObjectPointer] = ev.AllocationEvent_FilePointer;
			}

			public override void VisitGCMoveEvent(SuperEvent ev)
			{
				if (allocationsTracker.TryGetValue(ev.GCMoveEvent_OldObjectPointer, out var allocation)) {
					allocationsTracker[ev.GCMoveEvent_NewObjectPointer] = allocation;
					allocationsTracker.Remove(ev.GCMoveEvent_OldObjectPointer);
				} else {
					Console.WriteLine("Scary stuff, moving something that doesn't exist.");
				}
				if (ev.GCMoveEvent_OldObjectPointer2 != 0) {
					if (allocationsTracker.TryGetValue(ev.GCMoveEvent_OldObjectPointer2, out var allocation2)) {
						allocationsTracker[ev.GCMoveEvent_NewObjectPointer2] = allocation2;
						allocationsTracker.Remove(ev.GCMoveEvent_OldObjectPointer2);
					} else {
						Console.WriteLine("Scary stuff, moving something that doesn't exist.");
					}
				}
			}

			int heapshotCounter;
			Heapshot currentHeapshot;
			Stopwatch processingHeapTime;

			public override void VisitHeapBeginEvent(SuperEvent ev)
			{
				Console.WriteLine("CACA2:" + DateTime.Now);
				System.Environment.Exit(2);
				processingHeapTime = Stopwatch.StartNew();
				currentHeapshot = new Heapshot(session, ++heapshotCounter);
			}

			public override void VisitHeapEndEvent(SuperEvent ev)
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

			public override void VisitHeapObjectEvent(SuperEvent ev)
			{
				if (ev.HeapObjectEvent_ObjectSize == 0) {
					//var existingObj = currentHeapshot.ObjectsInfoMap[ev.HeapRootsEvent_ObjectPointer];
					//Array.Resize(ref existingObj.ReferencesTo, existingObj.ReferencesTo.Length + ev.ReferencesTo.Length);
					//Array.Copy(ev.ReferencesTo, 0, existingObj.ReferencesTo, existingObj.ReferencesTo.Length - ev.ReferencesTo.Length, ev.ReferencesTo.Length);
					//Array.Resize(ref existingObj.ReferencesAt, existingObj.ReferencesAt.Length + ev.ReferencesAt.Length);
					//Array.Copy(ev.ReferencesAt, 0, existingObj.ReferencesAt, existingObj.ReferencesAt.Length - ev.ReferencesAt.Length, ev.ReferencesAt.Length);
					return;
				}

				var obj = new ObjectInfo();
				try {
					obj.Allocation = allocationsTracker[ev.HeapObjectEvent_ObjectPointer];
				} catch {
					obj.Allocation = 0;
					Console.WriteLine("OMG:" + session.classIdToName[vtableToClass[ev.HeapObjectEvent_VTablePointer]]);
				}
				obj.ObjAddr = ev.HeapObjectEvent_ObjectPointer;
				obj.TypeId = vtableToClass[ev.HeapObjectEvent_VTablePointer];
				//obj.ReferencesTo = ev.ReferencesTo;
				//obj.ReferencesAt = ev.ReferencesAt;
				if (!currentHeapshot.TypesToObjectsListMap.ContainsKey(obj.TypeId))
					currentHeapshot.TypesToObjectsListMap[obj.TypeId] = new List<ObjectInfo>();
				currentHeapshot.TypesToObjectsListMap[obj.TypeId].Add(obj);

				currentHeapshot.ObjectsInfoMap.Add(ev.HeapObjectEvent_ObjectPointer, obj);
			}

			Dictionary<long, long> vtableToClass = new Dictionary<long, long>();
			public override void VisitVTableLoadEvent(SuperEvent ev)
			{
				vtableToClass[ev.VTableLoadEvent_VTablePointer] = ev.VTableLoadEvent_ClassPointer;
			}

			public override void VisitClassLoadEvent(SuperEvent ev)
			{
				session.classIdToName[ev.ClassLoadEvent_ClassPointer] = ev.ClassLoadEvent_Name;
			}

			public override void VisitHeapRootsEvent(SuperEvent ev)
			{
				if (currentHeapshot != null) {
					var index = rootsEventsBinary.BinarySearch(ev.HeapRootsEvent_AddressPointer);
					if (index < 0) {
						index = ~index;
						if (index == 0) {
							Console.WriteLine($"This should not happen. Root is before any HeapRootsEvent {ev.HeapRootsEvent_AddressPointer}.");
							return;
						}
						var rootReg = rootsEvents[rootsEventsBinary[index - 1]];
						if (rootReg.HeapRootRegisterEvent_RootPointer < ev.HeapRootsEvent_AddressPointer && rootReg.HeapRootRegisterEvent_RootPointer + rootReg.HeapRootRegisterEvent_RootSize >= ev.HeapRootsEvent_AddressPointer) {
							currentHeapshot.Roots[ev.HeapRootsEvent_ObjectPointer] = rootReg;
						} else {
							Console.WriteLine($"This should not happen. Closest root is too small({ev.HeapRootsEvent_AddressPointer}):");
							Console.WriteLine(rootReg);
						}
					} else {
						//We got exact match
						currentHeapshot.Roots[ev.HeapRootsEvent_ObjectPointer] = rootsEvents[ev.HeapRootsEvent_AddressPointer];
					}
				} else {
					//Console.WriteLine("This should not happen. HeapRootsEvent outside HeapshotBegin/End.");
				}
			}

			Dictionary<long, SuperEvent> rootsEvents = new Dictionary<long, SuperEvent>();
			List<long> rootsEventsBinary = new List<long>();

			public override void VisitHeapRootRegisterEvent(SuperEvent ev)
			{
				var index = rootsEventsBinary.BinarySearch(ev.HeapRootRegisterEvent_RootPointer);
				if (index < 0) {//negative index means it's not there
					index = ~index;
					if (index - 1 >= 0) {
						var oneBefore = rootsEvents[rootsEventsBinary[index - 1]];
						if (oneBefore.HeapRootRegisterEvent_RootPointer + oneBefore.HeapRootRegisterEvent_RootSize > ev.HeapRootRegisterEvent_RootPointer) {
							Console.WriteLine("2 HeapRootRegisterEvents overlap:");
							Console.WriteLine(ev);
							Console.WriteLine(oneBefore);
						}
					}
					if (index < rootsEventsBinary.Count) {
						var oneAfter = rootsEvents[rootsEventsBinary[index]];
						if (oneAfter.HeapRootRegisterEvent_RootPointer < ev.HeapRootRegisterEvent_RootPointer + ev.HeapRootRegisterEvent_RootSize) {
							Console.WriteLine("2 HeapRootRegisterEvents overlap:");
							Console.WriteLine(ev);
							Console.WriteLine(oneAfter);
						}
					}
					rootsEventsBinary.Insert(index, ev.HeapRootRegisterEvent_RootPointer);
					rootsEvents.Add(ev.HeapRootRegisterEvent_RootPointer, ev);
				} else {
					Console.WriteLine("2 HeapRootRegisterEvent at same address:");
					Console.WriteLine(ev);
					Console.WriteLine(rootsEvents[ev.HeapRootRegisterEvent_RootPointer]);
					rootsEvents[ev.HeapRootRegisterEvent_RootPointer] = ev;
				}
			}

			public override void VisitHeapRootUnregisterEvent(SuperEvent ev)
			{
				if (rootsEvents.Remove(ev.HeapRootUnregisterEvent_RootPointer)) {
					var index = rootsEventsBinary.BinarySearch(ev.HeapRootUnregisterEvent_RootPointer);
					rootsEventsBinary.RemoveAt(index);
				} else {
					Console.WriteLine("HeapRootUnregisterEvent attempted at address that was not Registred:");
					Console.WriteLine(ev);
				}
			}

			public override void VisitJitEvent(SuperEvent ev)
			{
				session.methodsNames[ev.JitEvent_MethodPointer] = ev.JitEvent_Name;
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
				return processor.ReadString(classIdToName[id]);
			} else {
				return "<no name>";
			}
		}

		public Dictionary<long, ulong> methodsNames = new Dictionary<long, ulong>();

		public string GetMethodName(long methodId)
		{
			if (methodId == -1)
				return "[root]";
			if (!methodsNames.ContainsKey(methodId))
				return "Not existing(" + methodId + ").";
			return processor.ReadString(methodsNames[methodId]);
		}

		Dictionary<long, ulong> classIdToName = new Dictionary<long, ulong>();
		public double ParsingProgress {
			get {
				if (completionSource?.Task?.IsCompleted ?? false)
					return 100;
				try {
					if ((processor.Stream.Length) == 0)
						return 0;
					return (double)processor.Stream.Position / processor.Stream.Length;
				} catch {
					return 0;
				}
			}
		}
		List<string> allImagesPaths = new List<string>();
	}
}

