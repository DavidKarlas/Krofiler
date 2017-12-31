using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Profiler.Log;
using SQLitePCL;

namespace Krofiler
{

	public class MoreReferences
	{
		public ulong FilePointer;
		public MoreReferences More;
	}

	public struct ObjReferences
	{
		public ulong FilePointer;
		public MoreReferences More;

		public ObjReferences(ulong filePointer)
		{
			this.FilePointer = filePointer;
			More = null;
		}
	}

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

		public event Action<SuperEvent> CountersDescriptionsAdded;
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

		class KrofilerLogEventVisitor : LogEventVisitor
		{
			public override void VisitCounterDescriptionsEvent(SuperEvent ev)
			{
				session.Descriptions.Add(ev);
				session.CountersDescriptionsAdded?.Invoke(ev);
			}

			public override void VisitCounterSamplesEvent(SuperEvent ev)
			{
				session.CounterSamplesAdded?.Invoke(ev);
			}

			private KrofilerSession session;
			Dictionary<long, ulong> allocationsTracker = new Dictionary<long, ulong>(24000000);

			public KrofilerLogEventVisitor(KrofilerSession session)
			{
				this.session = session;
				Console.WriteLine($"Start: {DateTime.Now}");
			}

			public override void VisitAllocationEvent(SuperEvent ev)
			{
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

			public override void VisitHeapBeginEvent(SuperEvent ev)
			{
				currentHeapshot = new Heapshot(session, ++heapshotCounter);
				deadObjs = new HashSet<long>(allocationsTracker.Keys);
				Console.WriteLine($"HeapBeginEvent({currentHeapshot.Name}): {DateTime.Now}");
			}

			HashSet<long> deadObjs;
			public override void VisitHeapEndEvent(SuperEvent ev)
			{
				Console.WriteLine($"HeapEndEvent({currentHeapshot.Name}): {DateTime.Now}");
				currentHeapshot.FinishProcessing();
				Console.WriteLine($"Allocations before cleanup({DateTime.Now}):{allocationsTracker.Count}");
				foreach (var key in deadObjs) {
					allocationsTracker.Remove(key);
				}
				deadObjs = null;
				Console.WriteLine($"Allocations after cleanup({DateTime.Now}):{allocationsTracker.Count}");
				session.Heapshots.Add(currentHeapshot);
				session.NewHeapshot?.Invoke(session, currentHeapshot);
				currentHeapshot = null;
				GC.Collect();
			}

			public override void VisitHeapObjectEvent(SuperEvent ev)
			{
				ulong alloc = 0;
				try {
					deadObjs.Remove(ev.HeapObjectEvent_ObjectPointer);
					alloc = allocationsTracker[ev.HeapObjectEvent_ObjectPointer];
				} catch {
				}
				currentHeapshot.Insert(ev.HeapObjectEvent_ObjectPointer,vtableToClass[ev.HeapObjectEvent_VTablePointer], alloc, ev.HeapObjectEvent_ObjectSize);
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
				ProcessNewRoot(ev.HeapRootsEvent_ObjectPointer, ev.HeapRootsEvent_AddressPointer);
				if (ev.HeapRootsEvent_ObjectPointer2 != 0)
					ProcessNewRoot(ev.HeapRootsEvent_ObjectPointer2, ev.HeapRootsEvent_AddressPointer2);
			}

			void ProcessNewRoot(long objAddr, long rootAddr)
			{
				var index = rootsEventsBinary.BinarySearch(rootAddr);
				if (index < 0) {
					index = ~index;
					if (index == 0) {
						Console.WriteLine($"This should not happen. Root is before any HeapRootsEvent {rootAddr}.");
						return;
					}
					var rootReg = rootsEvents[rootsEventsBinary[index - 1]];
					if (rootReg.HeapRootRegisterEvent_RootPointer < rootAddr && rootReg.HeapRootRegisterEvent_RootPointer + rootReg.HeapRootRegisterEvent_RootSize >= rootAddr) {
						currentHeapshot.Roots[objAddr] = rootReg;
					} else {
						Console.WriteLine($"This should not happen. Closest root is too small({rootAddr}):");
						Console.WriteLine(rootReg);
					}
				} else {
					//We got exact match
					currentHeapshot.Roots[objAddr] = rootsEvents[rootAddr];
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
			long oldSize = 0;
			public override void VisitGCResizeEvent(SuperEvent ev)
			{
				if (ev.GCResizeEvent_NewSize == oldSize)
					return;
				oldSize = ev.GCResizeEvent_NewSize;
				session.GCResize?.Invoke(ev.Time, ev.GCResizeEvent_NewSize);
			}
		}
		public event Action<TimeSpan, long> GCResize;

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
					if ((processor?.Stream?.Length ?? 0) == 0)
						return 0;
					return (double)processor.Stream.Position / processor.Stream.Length;
				} catch {
					return 0;
				}
			}
		}
		List<string> allImagesPaths = new List<string>();

		public FileStream GetFileStream(ulong position)
		{
			const ulong fileMask = 0xfful << 56;
			var fileName = (fileMask & position) + ".krof";
			var path = Path.Combine(processor.cacheFolder, fileName);
			var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			stream.Position = (long)(0xfffffffffffffful & position);
			return stream;
		}
	}
}

