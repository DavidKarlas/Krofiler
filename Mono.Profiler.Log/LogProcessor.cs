// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mono.Profiler.Log
{
	public sealed class LogProcessor
	{
		public Stream Stream { get; }

		LogEventVisitor Visitor;

		public LogStreamHeader StreamHeader { get; private set; }

		ulong minimalTime = ulong.MaxValue;

		bool _used;

		public LogProcessor(Stream stream, LogEventVisitor visitor)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			Stream = stream;
			Visitor = visitor;
		}

		class ObjectPool<T>
		{
			private ConcurrentBag<T> _objects;
			private Func<T> _objectGenerator;

			public ObjectPool(Func<T> objectGenerator)
			{
				if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");
				_objects = new ConcurrentBag<T>();
				_objectGenerator = objectGenerator;
			}

			public T GetObject()
			{
				T item;
				if (_objects.TryTake(out item)) return item;
				return _objectGenerator();
			}

			public void PutObject(T item)
			{
				_objects.Add(item);
			}
		}
		bool live;
		bool fileFinished;
		CancellationToken token;
		ManualResetEvent eventsReady = new ManualResetEvent(false);
		ManualResetEvent needMoreEvents = new ManualResetEvent(false);
		ConcurrentQueue<List<LogEvent>> queue = new ConcurrentQueue<List<LogEvent>>();
		ObjectPool<List<LogEvent>> listsPool = new ObjectPool<List<LogEvent>>(() => new List<LogEvent>());
		public string CacheFolder;
		const int QueueSize = 100;
		public void Process(CancellationToken token, bool live = false)
		{
			if (_used)
				throw new InvalidOperationException("This log processor cannot be reused.");
			var managerThread = new Thread(new ThreadStart(ParsingManager));
			managerThread.Start();
			_used = true;
			this.live = live;
			this.token = token;
			var visitor = Visitor;
			while (!this.token.IsCancellationRequested) {
				if (queue.TryDequeue(out var list)) {
					foreach (var item in list) {
						item.Accept(visitor);
					}
					list.Clear();
					listsPool.PutObject(list);
					if (queue.Count < QueueSize)
						needMoreEvents.Set();
				} else {
					if (queue.IsEmpty && fileFinished)
						return;
					needMoreEvents.Set();
					eventsReady.Reset();
					eventsReady.WaitOne();
				}
			}
		}

		void ParsingManager()
		{
			var _reader = new LogReader(Stream, true);

			StreamHeader = new LogStreamHeader(_reader);
			var avaibleWorkers = new Queue<Worker>();
			CacheFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(CacheFolder);
			for (ulong i = ulong.MaxValue - (ulong)Environment.ProcessorCount; i < ulong.MaxValue; i++) {
				avaibleWorkers.Enqueue(new Worker(token, this, i, CacheFolder));
			}
			var workingWorkers = new List<Worker>();
			var unreportedEvents = new Dictionary<int, List<LogEvent>>();
			int bufferId = 0;
			int lastReportedId = 0;
			startLength = Stream.Length;
			while (true) {
				while (avaibleWorkers.Count > 0) {
					if (!Wait(48)) {
						avaibleWorkers.Dequeue().Stop();
						continue;
					}
					var _bufferHeader = new LogBufferHeader(StreamHeader, _reader);
					if (!Wait(_bufferHeader.Length)) {
						avaibleWorkers.Dequeue().Stop();
						continue;
					}
					var worker = avaibleWorkers.Dequeue();
					worker.BufferId = bufferId++;
					worker.bufferHeader = _bufferHeader;
					worker.memoryStream.Position = 0;
					worker.memoryStream.SetLength(_bufferHeader.Length);
					if (Stream.Read(worker.memoryStream.GetBuffer(), 0, _bufferHeader.Length) != _bufferHeader.Length)
						throw new InvalidOperationException();
					worker.done = new TaskCompletionSource<bool>();
					worker.list = listsPool.GetObject();
					workingWorkers.Add(worker);
					worker.StartWork();
					if (bufferId == 1)
						worker.done.Task.Wait();//Temporary workaround to make sure 1st event time is set correctly
				}
				if (workingWorkers.Count == 0) {
					fileFinished = true;
					eventsReady.Set();
					return;
				}
				if (queue.Count > QueueSize) {
					needMoreEvents.Reset();
					needMoreEvents.WaitOne();
				}
				foreach (var workingWorker in workingWorkers.ToArray()) {
					if (workingWorker.done.Task.IsCompleted) {
						workingWorkers.Remove(workingWorker);
						unreportedEvents.Add(workingWorker.BufferId, workingWorker.list);
						while (unreportedEvents.ContainsKey(lastReportedId)) {
							queue.Enqueue(unreportedEvents[lastReportedId]);
							eventsReady.Set();
							unreportedEvents.Remove(lastReportedId++);
						}
						avaibleWorkers.Enqueue(workingWorker);
					}
				}
			}
		}


		class Worker
		{
			public void StartWork()
			{
				if (thread == null) {
					thread = new Thread(new ThreadStart(Loop));
					thread.Start();
				} else {
					WaitForWork.Set();
				}
			}

			void Loop()
			{
				while (!token.IsCancellationRequested) {
					using (var reader = new LogReader(memoryStream, true)) {
						while (memoryStream.Position < memoryStream.Length) {
							list.Add(processor.ReadEvent(reader, bufferHeader, idPrefix, fileStream));
						}
						done.SetResult(true);
					}
					WaitForWork.WaitOne();
				}
				fileStream.Close();
			}

			internal void Stop()
			{
				cancellationTokenSource.Cancel();
				WaitForWork.Set();
			}

			internal TaskCompletionSource<bool> done;
			internal Thread thread;
			internal List<LogEvent> list;
			internal MemoryStream memoryStream = new MemoryStream(4096 * 16);
			internal AutoResetEvent WaitForWork = new AutoResetEvent(false);
			internal int BufferId;
			internal LogBufferHeader bufferHeader;
			private CancellationToken token;
			readonly LogProcessor processor;
			CancellationTokenSource cancellationTokenSource;
			readonly ulong idPrefix;
			FileStream fileStream;

			public Worker(CancellationToken token, LogProcessor processor, ulong idPrefix, string folder)
			{
				fileStream = new FileStream(Path.Combine(folder, idPrefix.ToString() + ".krof"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
				this.idPrefix = idPrefix;
				cancellationTokenSource = new CancellationTokenSource();
				token.Register(cancellationTokenSource.Cancel);
				this.processor = processor;
				this.token = cancellationTokenSource.Token;
			}
		}
		long startLength;
		private bool Wait(int requestedBytes)
		{
			while (Stream.Length - Stream.Position < requestedBytes) {
				if (live || Stream.Length != startLength) {
					live = true;
					if (token.IsCancellationRequested)
						return false;
					Thread.Sleep(100);
				} else
					return false;
			}
			return true;
		}

		LogEvent ReadEvent(LogReader _reader, LogBufferHeader _bufferHeader, ulong idPrefix, FileStream fs)
		{
			var type = _reader.ReadByte();
			var basicType = (LogEventType)(type & 0xf);
			var extType = (LogEventType)(type & 0xf0);

			var _time = ReadTime(_reader, _bufferHeader);

			if (minimalTime > _time) {
				minimalTime = _time;
				_time = 0;
			} else {
				_time = _time - minimalTime;
			}
			LogEvent ev = null;

			switch (basicType) {
				case LogEventType.Allocation:
					switch (extType) {
						case LogEventType.AllocationBacktrace:
						case LogEventType.AllocationNoBacktrace:
							ev = new AllocationEvent {
								VTablePointer = StreamHeader.FormatVersion >= 15 ? ReadPointer(_reader, _bufferHeader) : 0,
								ObjectPointer = ReadObject(_reader, _bufferHeader),
								ObjectSize = (long)_reader.ReadULeb128(),
								FilePointer = ((ulong)fs.Position) | idPrefix
							};
							fs.Write(BitConverter.GetBytes(ev.Timestamp), 0, 8);
							if (extType == LogEventType.AllocationBacktrace) {
								ushort length = (ushort)_reader.ReadULeb128();
								fs.Write(BitConverter.GetBytes(length), 0, 2);
								for (var i = 0; i < length; i++)
									fs.Write(BitConverter.GetBytes(ReadMethod(_reader, _bufferHeader)), 0, 8);
							}
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.GC:
					switch (extType) {
						case LogEventType.GCEvent:
							ev = new GCEvent {
								Type = (LogGCEvent)_reader.ReadByte(),
								Generation = _reader.ReadByte(),
							};
							break;
						case LogEventType.GCResize:
							ev = new GCResizeEvent {
								NewSize = (long)_reader.ReadULeb128(),
							};
							break;
						case LogEventType.GCMove: {
								var list = new long[(int)_reader.ReadULeb128()];

								for (var i = 0; i < list.Length; i++)
									list[i] = ReadObject(_reader, _bufferHeader);

								ev = new GCMoveEvent {
									OldObjectPointers = list.Where((_, i) => i % 2 == 0).ToArray(),
									NewObjectPointers = list.Where((_, i) => i % 2 != 0).ToArray(),
								};
								break;
							}
						case LogEventType.GCHandleCreationNoBacktrace:
						case LogEventType.GCHandleCreationBacktrace:
							ev = new GCHandleCreationEvent {
								Type = (LogGCHandleType)_reader.ReadULeb128(),
								Handle = (long)_reader.ReadULeb128(),
								ObjectPointer = ReadObject(_reader, _bufferHeader),
								Backtrace = ReadBacktrace(extType == LogEventType.GCHandleCreationBacktrace, _reader, _bufferHeader),
							};
							break;
						case LogEventType.GCHandleDeletionNoBacktrace:
						case LogEventType.GCHandleDeletionBacktrace:
							ev = new GCHandleDeletionEvent {
								Type = (LogGCHandleType)_reader.ReadULeb128(),
								Handle = (long)_reader.ReadULeb128(),
								Backtrace = ReadBacktrace(extType == LogEventType.GCHandleDeletionBacktrace, _reader, _bufferHeader),
							};
							break;
						case LogEventType.GCFinalizeBegin:
							ev = new GCFinalizeBeginEvent();
							break;
						case LogEventType.GCFinalizeEnd:
							ev = new GCFinalizeEndEvent();
							break;
						case LogEventType.GCFinalizeObjectBegin:
							ev = new GCFinalizeObjectBeginEvent {
								ObjectPointer = ReadObject(_reader, _bufferHeader),
							};
							break;
						case LogEventType.GCFinalizeObjectEnd:
							ev = new GCFinalizeObjectEndEvent {
								ObjectPointer = ReadObject(_reader, _bufferHeader),
							};
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Metadata: {
						var load = false;
						var unload = false;

						switch (extType) {
							case LogEventType.MetadataExtra:
								break;
							case LogEventType.MetadataEndLoad:
								load = true;
								break;
							case LogEventType.MetadataEndUnload:
								unload = true;
								break;
							default:
								throw new LogException($"Invalid extended event type ({extType}).");
						}

						var metadataType = (LogMetadataType)_reader.ReadByte();

						switch (metadataType) {
							case LogMetadataType.Class:
								if (load) {
									ev = new ClassLoadEvent {
										ClassPointer = ReadPointer(_reader, _bufferHeader),
										ImagePointer = ReadPointer(_reader, _bufferHeader),
										Name = _reader.ReadCString(),
									};
								} else
									throw new LogException("Invalid class metadata event.");
								break;
							case LogMetadataType.Image:
								if (load) {
									ev = new ImageLoadEvent {
										ImagePointer = ReadPointer(_reader, _bufferHeader),
										Name = _reader.ReadCString(),
									};
								} else if (unload) {
									ev = new ImageUnloadEvent {
										ImagePointer = ReadPointer(_reader, _bufferHeader),
										Name = _reader.ReadCString(),
									};
								} else
									throw new LogException("Invalid image metadata event.");
								break;
							case LogMetadataType.Assembly:
								if (load) {
									ev = new AssemblyLoadEvent {
										AssemblyPointer = ReadPointer(_reader, _bufferHeader),
										ImagePointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(_reader, _bufferHeader) : 0,
										Name = _reader.ReadCString(),
									};
								} else if (unload) {
									ev = new AssemblyUnloadEvent {
										AssemblyPointer = ReadPointer(_reader, _bufferHeader),
										ImagePointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(_reader, _bufferHeader) : 0,
										Name = _reader.ReadCString(),
									};
								} else
									throw new LogException("Invalid assembly metadata event.");
								break;
							case LogMetadataType.AppDomain:
								if (load) {
									ev = new AppDomainLoadEvent {
										AppDomainId = ReadPointer(_reader, _bufferHeader),
									};
								} else if (unload) {
									ev = new AppDomainUnloadEvent {
										AppDomainId = ReadPointer(_reader, _bufferHeader),
									};
								} else {
									ev = new AppDomainNameEvent {
										AppDomainId = ReadPointer(_reader, _bufferHeader),
										Name = _reader.ReadCString(),
									};
								}
								break;
							case LogMetadataType.Thread:
								if (load) {
									ev = new ThreadStartEvent {
										ThreadId = ReadPointer(_reader, _bufferHeader),
									};
								} else if (unload) {
									ev = new ThreadEndEvent {
										ThreadId = ReadPointer(_reader, _bufferHeader),
									};
								} else {
									ev = new ThreadNameEvent {
										ThreadId = ReadPointer(_reader, _bufferHeader),
										Name = _reader.ReadCString(),
									};
								}
								break;
							case LogMetadataType.Context:
								if (load) {
									ev = new ContextLoadEvent {
										ContextId = ReadPointer(_reader, _bufferHeader),
										AppDomainId = ReadPointer(_reader, _bufferHeader),
									};
								} else if (unload) {
									ev = new ContextUnloadEvent {
										ContextId = ReadPointer(_reader, _bufferHeader),
										AppDomainId = ReadPointer(_reader, _bufferHeader),
									};
								} else
									throw new LogException("Invalid context metadata event.");
								break;
							case LogMetadataType.VTable:
								if (load) {
									ev = new VTableLoadEvent {
										VTablePointer = ReadPointer(_reader, _bufferHeader),
										AppDomainId = ReadPointer(_reader, _bufferHeader),
										ClassPointer = ReadPointer(_reader, _bufferHeader),
									};
								} else
									throw new LogException("Invalid VTable metadata event.");
								break;
							default:
								throw new LogException($"Invalid metadata type ({metadataType}).");
						}
						break;
					}
				case LogEventType.Method:
					switch (extType) {
						case LogEventType.MethodLeave:
							ev = new LeaveEvent {
								MethodPointer = ReadMethod(_reader, _bufferHeader),
							};
							break;
						case LogEventType.MethodEnter:
							ev = new EnterEvent {
								MethodPointer = ReadMethod(_reader, _bufferHeader),
							};
							break;
						case LogEventType.MethodLeaveExceptional:
							ev = new ExceptionalLeaveEvent {
								MethodPointer = ReadMethod(_reader, _bufferHeader),
							};
							break;
						case LogEventType.MethodJit:
							ev = new JitEvent {
								MethodPointer = ReadMethod(_reader, _bufferHeader),
								CodePointer = ReadPointer(_reader, _bufferHeader),
								CodeSize = (long)_reader.ReadULeb128(),
								Name = _reader.ReadCString(),
							};
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Exception:
					switch (extType) {
						case LogEventType.ExceptionThrowNoBacktrace:
						case LogEventType.ExceptionThrowBacktrace:
							ev = new ThrowEvent {
								ObjectPointer = ReadObject(_reader, _bufferHeader),
								Backtrace = ReadBacktrace(extType == LogEventType.ExceptionThrowBacktrace, _reader, _bufferHeader),
							};
							break;
						case LogEventType.ExceptionClause:
							ev = new ExceptionClauseEvent {
								Type = (LogExceptionClause)_reader.ReadByte(),
								Index = (long)_reader.ReadULeb128(),
								MethodPointer = ReadMethod(_reader, _bufferHeader),
								ObjectPointer = StreamHeader.FormatVersion >= 14 ? ReadObject(_reader, _bufferHeader) : 0,
							};
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Monitor:
					if (StreamHeader.FormatVersion < 14) {
						if (extType.HasFlag(LogEventType.MonitorBacktrace)) {
							extType = LogEventType.MonitorBacktrace;
						} else {
							extType = LogEventType.MonitorNoBacktrace;
						}
					}
					switch (extType) {
						case LogEventType.MonitorNoBacktrace:
						case LogEventType.MonitorBacktrace:
							ev = new MonitorEvent {
								Event = StreamHeader.FormatVersion >= 14 ?
										(LogMonitorEvent)_reader.ReadByte() :
										(LogMonitorEvent)((((byte)type & 0xf0) >> 4) & 0x3),
								ObjectPointer = ReadObject(_reader, _bufferHeader),
								Backtrace = ReadBacktrace(extType == LogEventType.MonitorBacktrace, _reader, _bufferHeader),
							};
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Heap:
					switch (extType) {
						case LogEventType.HeapBegin:
							ev = new HeapBeginEvent();
							break;
						case LogEventType.HeapEnd:
							ev = new HeapEndEvent();
							break;
						case LogEventType.HeapObject: {
								HeapObjectEvent hoe = new HeapObjectEvent {
									ObjectPointer = ReadObject(_reader, _bufferHeader),
									VTablePointer = StreamHeader.FormatVersion >= 15 ? ReadPointer(_reader, _bufferHeader) : 0,
									ObjectSize = (long)_reader.ReadULeb128(),
								};

								var listTo = new long[(int)_reader.ReadULeb128()];
								var listAt = new ushort[listTo.Length];

								for (var i = 0; i < listTo.Length; i++) {
									listAt[i] = (ushort)_reader.ReadULeb128();
									listTo[i] = ReadObject(_reader, _bufferHeader);
								}

								hoe.ReferencesAt = listAt;
								hoe.ReferencesTo = listTo;
								ev = hoe;

								break;
							}

						case LogEventType.HeapRoots: {
								var hre = new HeapRootsEvent();
								var list = new HeapRootsEvent.HeapRoot[(int)_reader.ReadULeb128()];

								for (var i = 0; i < list.Length; i++) {
									list[i] = new HeapRootsEvent.HeapRoot {
										AddressPointer = StreamHeader.FormatVersion >= 15 ? ReadPointer(_reader, _bufferHeader) : 0,
										ObjectPointer = ReadObject(_reader, _bufferHeader)
									};
								}

								hre.Roots = list;
								ev = hre;

								break;
							}
						case LogEventType.HeapRootRegister:
							ev = new HeapRootRegisterEvent {
								RootPointer = ReadPointer(_reader, _bufferHeader),
								RootSize = (long)_reader.ReadULeb128(),
								Source = (LogHeapRootSource)_reader.ReadByte(),
								Key = ReadPointer(_reader, _bufferHeader),
								Name = _reader.ReadCString(),
							};
							break;
						case LogEventType.HeapRootUnregister:
							ev = new HeapRootUnregisterEvent {
								RootPointer = ReadPointer(_reader, _bufferHeader),
							};
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Sample:
					switch (extType) {
						case LogEventType.SampleHit:
							if (StreamHeader.FormatVersion < 14) {
								// Read SampleType (always set to .Cycles) for versions < 14
								_reader.ReadByte();
							}
							ev = new SampleHitEvent {
								ThreadId = ReadPointer(_reader, _bufferHeader),
								UnmanagedBacktrace = ReadBacktrace(true, _reader, _bufferHeader, false),
								ManagedBacktrace = ReadBacktrace(true, _reader, _bufferHeader).Reverse().ToArray(),
							};
							break;
						case LogEventType.SampleUnmanagedSymbol:
							ev = new UnmanagedSymbolEvent {
								CodePointer = ReadPointer(_reader, _bufferHeader),
								CodeSize = (long)_reader.ReadULeb128(),
								Name = _reader.ReadCString(),
							};
							break;
						case LogEventType.SampleUnmanagedBinary:
							ev = new UnmanagedBinaryEvent {
								SegmentPointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(_reader, _bufferHeader) : _reader.ReadSLeb128(),
								SegmentOffset = (long)_reader.ReadULeb128(),
								SegmentSize = (long)_reader.ReadULeb128(),
								FileName = _reader.ReadCString(),
							};
							break;
						case LogEventType.SampleCounterDescriptions: {
								var cde = new CounterDescriptionsEvent();
								var list = new CounterDescriptionsEvent.CounterDescription[(int)_reader.ReadULeb128()];

								for (var i = 0; i < list.Length; i++) {
									var section = (LogCounterSection)_reader.ReadULeb128();

									list[i] = new CounterDescriptionsEvent.CounterDescription {
										Section = section,
										SectionName = section == LogCounterSection.User ? _reader.ReadCString() : null,
										CounterName = _reader.ReadCString(),
										Type = StreamHeader.FormatVersion < 15 ? (LogCounterType)_reader.ReadByte() : (LogCounterType)_reader.ReadULeb128(),
										Unit = StreamHeader.FormatVersion < 15 ? (LogCounterUnit)_reader.ReadByte() : (LogCounterUnit)_reader.ReadULeb128(),
										Variance = StreamHeader.FormatVersion < 15 ? (LogCounterVariance)_reader.ReadByte() : (LogCounterVariance)_reader.ReadULeb128(),
										Index = (long)_reader.ReadULeb128(),
									};
								}

								cde.Descriptions = list;
								ev = cde;

								break;
							}
						case LogEventType.SampleCounters: {
								var cse = new CounterSamplesEvent();
								var list = new List<CounterSamplesEvent.CounterSample>();

								while (true) {
									var index = (long)_reader.ReadULeb128();

									if (index == 0)
										break;

									var counterType = StreamHeader.FormatVersion < 15 ? (LogCounterType)_reader.ReadByte() : (LogCounterType)_reader.ReadULeb128();

									object value = null;

									switch (counterType) {
										case LogCounterType.String:
											value = _reader.ReadByte() == 1 ? _reader.ReadCString() : null;
											break;
										case LogCounterType.Int32:
										case LogCounterType.Word:
										case LogCounterType.Int64:
										case LogCounterType.Interval:
											value = _reader.ReadSLeb128();
											break;
										case LogCounterType.UInt32:
										case LogCounterType.UInt64:
											value = _reader.ReadULeb128();
											break;
										case LogCounterType.Double:
											value = _reader.ReadDouble();
											break;
										default:
											throw new LogException($"Invalid counter type ({counterType}).");
									}

									list.Add(new CounterSamplesEvent.CounterSample {
										Index = index,
										Type = counterType,
										Value = value,
									});
								}

								cse.Samples = list;
								ev = cse;

								break;
							}
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Runtime:
					switch (extType) {
						case LogEventType.RuntimeJitHelper: {
								var helperType = (LogJitHelper)_reader.ReadByte();

								ev = new JitHelperEvent {
									Type = helperType,
									BufferPointer = ReadPointer(_reader, _bufferHeader),
									BufferSize = (long)_reader.ReadULeb128(),
									Name = helperType == LogJitHelper.SpecificTrampoline ? _reader.ReadCString() : null,
								};
								break;
							}
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Meta:
					switch (extType) {
						case LogEventType.MetaSynchronizationPoint:
							ev = new SynchronizationPointEvent {
								Type = (LogSynchronizationPoint)_reader.ReadByte(),
							};
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				default:
					throw new LogException($"Invalid basic event type ({basicType}).");
			}

			ev.Timestamp = _time;

			return ev;
		}

		long ReadPointer(LogReader _reader, LogBufferHeader _bufferHeader)
		{
			var ptr = _reader.ReadSLeb128() + _bufferHeader.PointerBase;

			return StreamHeader.PointerSize == sizeof(long) ? ptr : ptr & 0xffffffffL;
		}

		long ReadObject(LogReader _reader, LogBufferHeader _bufferHeader)
		{
			return _reader.ReadSLeb128() + _bufferHeader.ObjectBase << 3;
		}

		long ReadMethod(LogReader _reader, LogBufferHeader _bufferHeader)
		{
			return _bufferHeader.CurrentMethod += _reader.ReadSLeb128();
		}

		ulong ReadTime(LogReader _reader, LogBufferHeader _bufferHeader)
		{
			return _bufferHeader.CurrentTime += _reader.ReadULeb128();
		}

		IReadOnlyList<long> ReadBacktrace(bool actuallyRead, LogReader _reader, LogBufferHeader _bufferHeader, bool managed = true)
		{
			if (!actuallyRead)
				return Array.Empty<long>();

			var list = new long[(int)_reader.ReadULeb128()];

			for (var i = 0; i < list.Length; i++)
				list[i] = managed ? ReadMethod(_reader, _bufferHeader) : ReadPointer(_reader, _bufferHeader);

			return list;
		}
	}
}
