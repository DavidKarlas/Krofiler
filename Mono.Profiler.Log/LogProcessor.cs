// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SQLitePCL;

namespace Mono.Profiler.Log
{
	public sealed class LogProcessor
	{
		public FileStream Stream { get; }

		LogEventVisitor Visitor;

		public LogStreamHeader StreamHeader { get; private set; }

		ulong minimalTime = ulong.MaxValue;

		bool _used;

		public LogProcessor(string fileName, LogEventVisitor visitor)
		{
			Batteries_V2.Init();
			Stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
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
		ConcurrentQueue<SuperEventList> queue = new ConcurrentQueue<SuperEventList>();
		ObjectPool<SuperEventList> listsPool = new ObjectPool<SuperEventList>(() => new SuperEventList());
		public string cacheFolder;
		const int QueueSize = 100;

		public void Process(CancellationToken token, bool live = false)
		{
			if (_used)
				throw new InvalidOperationException("This log processor cannot be reused.");
			_used = true;
			cacheFolder = Stream.Name + "Krofiler.Cache";
			if (Directory.Exists(cacheFolder))
				Directory.Delete(cacheFolder, true);
			Directory.CreateDirectory(cacheFolder);
			var managerThread = new Thread(new ThreadStart(ParsingManager));
			managerThread.Start();
			this.live = live;
			this.token = token;
			var visitor = Visitor;
			while (!this.token.IsCancellationRequested) {
				if (queue.TryDequeue(out var list)) {
					int count = list.Count;
					var arr = list.superEventList;
					for (int i = 0; i < count; i++) {
						visitor.VisitSuper(arr[i]);
					}
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
		[System.Runtime.InteropServices.DllImport("e_sqlite3", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		public static extern int sqlite3_limit(IntPtr db, int id, int newVal);
		void ParsingManager()
		{
			var _reader = new LogReader(Stream, true);

			StreamHeader = new LogStreamHeader(_reader);
			var avaibleWorkers = new Queue<Worker>();
			ulong workersCount = (ulong)Environment.ProcessorCount / 2;
			for (ulong i = 1; i < workersCount + 1; i++) {
				avaibleWorkers.Enqueue(new Worker(token, this, cacheFolder, i << 56));
			}
			var workingWorkers = new List<Worker>();
			var unreportedEvents = new Dictionary<int, SuperEventList>();
			int bufferId = 0;
			int lastReportedId = 0;
			startLength = Stream.Length;
			sqlite3 db = null;
			var dbNames = new List<string>();
			var dbs = new List<sqlite3>();
			int heapshotCounter = 0;
			int maxAttachedDbs = 0;
			while (true) {
				while (avaibleWorkers.Count > 0) {
					if (!Wait(48)) {
						avaibleWorkers.Dequeue().Stop();
						continue;
					}
					var _bufferHeader = new LogBufferHeader(StreamHeader, _reader, (ulong)(Stream.Position + 48));
					if (!Wait(_bufferHeader.Length)) {
						avaibleWorkers.Dequeue().Stop();
						continue;
					}
					var worker = avaibleWorkers.Dequeue();
					worker.BufferId = bufferId++;
					worker.bufferHeader = _bufferHeader;
					worker.bufferLength = _bufferHeader.Length;
					if (Stream.Read(worker.buffer, 0, _bufferHeader.Length) != _bufferHeader.Length)
						throw new InvalidOperationException();
					worker.done = new TaskCompletionSource<bool>();
					worker.list = listsPool.GetObject();
					worker.list.Clear();
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
				bool anyComplete = false;
				for (int i = 0; i < workingWorkers.Count; i++) {
					var workingWorker = workingWorkers[i];
					if (workingWorker.done.Task.IsCompleted) {
						anyComplete = true;
						i--;
						workingWorkers.Remove(workingWorker);
						unreportedEvents.Add(workingWorker.BufferId, workingWorker.list);
						while (unreportedEvents.TryGetValue(lastReportedId, out var list)) {
							if (list.HasHeapBegin) {
								var dbFileName = Path.Combine(cacheFolder, $"HeapshotRefs_{++heapshotCounter}.db");
								if (File.Exists(dbFileName))
									File.Delete(dbFileName);
								CreateDatabase($"file:{dbFileName}", false, out db, out var stmt);
								maxAttachedDbs = sqlite3_limit(db.ptr, 7, -1);
							}
							void MergeDatabase()
							{
								while (dbNames.Count > 0) {
									var howManyToAttach = System.Math.Min(dbNames.Count, maxAttachedDbs);
									foreach (var dbname in dbNames.Take(howManyToAttach)) {
										check_ok(db, raw.sqlite3_exec(db, $"attach 'file:{dbname}?mode=memory&cache=shared' as {dbname};"));
									}
									check_ok(db, raw.sqlite3_exec(db, $"BEGIN;"));
									foreach (var dbname in dbNames.Take(howManyToAttach)) {
										check_ok(db, raw.sqlite3_exec(db, $"insert into Refs select * from {dbname}.Refs;"));
									}
									check_ok(db, raw.sqlite3_exec(db, $"COMMIT;"));
									foreach (var dbname in dbNames.Take(howManyToAttach)) {
										check_ok(db, raw.sqlite3_exec(db, $"detach {dbname};"));
									}
									foreach (var d in dbs.Take(howManyToAttach))
										check_ok(list.db, raw.sqlite3_close(d));
									dbNames.RemoveRange(0, howManyToAttach);
									dbs.RemoveRange(0, howManyToAttach);
								}
							}
							if (list.db != null) {
								dbNames.Add(list.dbName);
								dbs.Add(list.db);
								list.db = null;
								if (maxAttachedDbs == dbNames.Count)
									MergeDatabase();
							}
							if (list.HasHeapEnd) {
								MergeDatabase();
								check_ok(db, raw.sqlite3_close(db));
							}

							queue.Enqueue(list);
							eventsReady.Set();
							unreportedEvents.Remove(lastReportedId++);
						}
						avaibleWorkers.Enqueue(workingWorker);
					}
				}
				if (!anyComplete) {
					Task.WaitAny(workingWorkers.Select(w => w.done.Task).ToArray());
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

			unsafe void Loop()
			{
				while (!token.IsCancellationRequested) {
					fixed (byte* startPointer = &buffer[0]) {
						bufferHeader.bufferStart = startPointer;
						byte* pointer = startPointer;
						while ((pointer - startPointer) < bufferLength) {
							processor.ReadEvent(ref pointer, list, bufferHeader, fileStream, filePrefix);
						}
					}
					fileStream.Flush();
					if (list.db != null) {
						check_ok(list.db, raw.sqlite3_exec(list.db, "COMMIT TRANSACTION;"));
						check_ok(list.db, raw.sqlite3_finalize(list.stmt));
						list.stmt = null;
					}
					done.SetResult(true);
					WaitForWork.WaitOne();
				}
			}

			internal void Stop()
			{
				cancellationTokenSource.Cancel();
				WaitForWork.Set();
			}

			internal TaskCompletionSource<bool> done;
			internal Thread thread;
			internal SuperEventList list;
			internal int bufferLength;
			internal byte[] buffer = new byte[4096 * 16];
			internal FileStream fileStream;
			internal AutoResetEvent WaitForWork = new AutoResetEvent(false);
			internal int BufferId;
			internal LogBufferHeader bufferHeader;
			private CancellationToken token;
			readonly LogProcessor processor;
			CancellationTokenSource cancellationTokenSource;
			readonly ulong filePrefix;

			public Worker(CancellationToken token, LogProcessor processor, string cacheFolder, ulong id)
			{
				this.filePrefix = id;
				cancellationTokenSource = new CancellationTokenSource();
				token.Register(cancellationTokenSource.Cancel);
				this.processor = processor;
				this.token = cancellationTokenSource.Token;
				fileStream = new FileStream(Path.Combine(cacheFolder, id.ToString() + ".krof"), FileMode.Create, FileAccess.Write, FileShare.Read);
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

		static unsafe ulong ReadCString(ref byte* pointer, LogBufferHeader _bufferHeader)
		{
			var pos = pointer;
			while (*pointer++ != 0) { }
			return _bufferHeader.GetFileOffset(pos);
		}

		static unsafe long ReadSLeb128(ref byte* p)
		{
			long Value = 0;
			byte Shift = 0;
			byte Byte;
			do {
				Byte = *p++;
				Value |= ((long)(Byte & 0x7f)) << Shift;
				Shift += 7;
			} while (Byte >= 128);
			if ((Byte & 0x40) > 0)
				Value |= (-1L) << Shift;
			return Value;
		}

		unsafe static ulong ReadULeb128(ref byte* p)
		{
			ulong Value = 0;
			byte Shift = 0;
			do {
				ulong Slice = *p & 0x7fUL;
				Value += (*p & 0x7fUL) << Shift;
				Shift += 7;
			} while (*p++ >= 128);
			return Value;
		}

		static unsafe double ReadDouble(ref byte* span)
		{
			uint lo = (uint)(span[0] | span[1] << 8 |
							 span[2] << 16 | span[3] << 24);
			uint hi = (uint)(span[4] | span[5] << 8 |
							 span[6] << 16 | span[7] << 24);
			span += 8;
			ulong tmpBuffer = ((ulong)hi) << 32 | lo;
			return *((double*)&tmpBuffer);
		}


		unsafe static long ReadPointer(ref byte* span, LogBufferHeader _bufferHeader)
		{
			var ptr = ReadSLeb128(ref span) + _bufferHeader.PointerBase;

			return _bufferHeader.StreamHeader.PointerSize == sizeof(long) ? ptr : ptr & 0xffffffffL;
		}

		unsafe static long ReadObject(ref byte* span, LogBufferHeader _bufferHeader)
		{
			return ReadSLeb128(ref span) + _bufferHeader.ObjectBase << 3;
		}

		unsafe static long ReadMethod(ref byte* _reader, LogBufferHeader _bufferHeader)
		{
			return _bufferHeader.CurrentMethod += ReadSLeb128(ref _reader);
		}

		unsafe static ulong ReadBacktrace(bool actuallyRead, ref byte* span, LogBufferHeader _bufferHeader, FileStream fs, ulong fileId, bool managed = true)
		{
			if (!actuallyRead) {
				fs.WriteByte(0);
				return (ulong)(fs.Position - 1) | fileId;
			}
			var posBefore = fs.Position;
			var length = (byte)ReadULeb128(ref span);
			fs.WriteByte(length);
			for (var i = 0; i < length; i++) {
				var ptr = managed ? ReadMethod(ref span, _bufferHeader) : ReadPointer(ref span, _bufferHeader);
				fs.Write(BitConverter.GetBytes(ptr), 0, 8);
			}
			return (ulong)posBefore | fileId;
		}

		unsafe void ReadEvent(ref byte* span, SuperEventList list, LogBufferHeader _bufferHeader, FileStream fs, ulong filePrefix)
		{
			var type = *span++;
			var basicType = (LogEventType)(type & 0xf);
			var extType = (LogEventType)(type & 0xf0);

			var _time = _bufferHeader.CurrentTime += ReadULeb128(ref span);
			if (minimalTime > _time) {
				minimalTime = _time;
				_time = 0;
			} else {
				_time = _time - minimalTime;
			}
			ulong shiftedTime = _time << 8;
			switch (basicType) {
				case LogEventType.Allocation:
					switch (extType) {
						case LogEventType.AllocationBacktrace:
						case LogEventType.AllocationNoBacktrace:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.Allocation) {
								AllocationEvent_VTablePointer = ReadPointer(ref span, _bufferHeader),
								AllocationEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								AllocationEvent_ObjectSize = ReadULeb128(ref span),
								AllocationEvent_FilePointer = (ulong)fs.Position | filePrefix
							});
							var time = _bufferHeader.CurrentTime;
							fs.Write(BitConverter.GetBytes(time), 0, 8);
							ReadBacktrace(extType == LogEventType.AllocationBacktrace, ref span, _bufferHeader, fs, filePrefix);
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.GC:
					switch (extType) {
						case LogEventType.GCEvent:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GC) {
								GCEvent_Type = (LogGCEvent)(byte)*span++,
								GCEvent_Generation = *span++,
							});
							break;
						case LogEventType.GCResize:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCResize) {
								GCResizeEvent_NewSize = (long)ReadULeb128(ref span),
							});
							break;
						case LogEventType.GCMove: {
								var length = ReadULeb128(ref span) / 2;
								if (length % 2 == 1) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCMove) {
										GCMoveEvent_OldObjectPointer = ReadObject(ref span, _bufferHeader),
										GCMoveEvent_NewObjectPointer = ReadObject(ref span, _bufferHeader)
									});
									length--;
								}
								for (ulong i = 0; i < length; i += 2)
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCMove) {
										GCMoveEvent_OldObjectPointer = ReadObject(ref span, _bufferHeader),
										GCMoveEvent_NewObjectPointer = ReadObject(ref span, _bufferHeader),
										GCMoveEvent_OldObjectPointer2 = ReadObject(ref span, _bufferHeader),
										GCMoveEvent_NewObjectPointer2 = ReadObject(ref span, _bufferHeader),
									});
								break;
							}
						case LogEventType.GCHandleCreationNoBacktrace:
						case LogEventType.GCHandleCreationBacktrace:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCHandleCreation) {
								GCHandleCreationEvent_Type = (LogGCHandleType)ReadULeb128(ref span),
								GCHandleCreationEvent_Handle = (long)ReadULeb128(ref span),
								GCHandleCreationEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								GCHandleCreationEvent_Backtrace = ReadBacktrace(extType == LogEventType.GCHandleCreationBacktrace, ref span, _bufferHeader, fs, filePrefix),
							});
							break;
						case LogEventType.GCHandleDeletionNoBacktrace:
						case LogEventType.GCHandleDeletionBacktrace:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCHandleDeletion) {
								GCHandleDeletionEvent_Type = (LogGCHandleType)ReadULeb128(ref span),
								GCHandleDeletionEvent_Handle = (long)ReadULeb128(ref span),
								GCHandleDeletionEvent_Backtrace = ReadBacktrace(extType == LogEventType.GCHandleDeletionBacktrace, ref span, _bufferHeader, fs, filePrefix),
							});
							break;
						case LogEventType.GCFinalizeBegin:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCFinalizeBegin));
							break;
						case LogEventType.GCFinalizeEnd:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCFinalizeEnd));
							break;
						case LogEventType.GCFinalizeObjectBegin:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCFinalizeObjectBegin) {
								GCFinalizeObjectBeginEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
							});
							break;
						case LogEventType.GCFinalizeObjectEnd:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.GCFinalizeObjectEnd) {
								GCFinalizeObjectEndEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
							});
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

						var metadataType = (LogMetadataType)(byte)*span++;

						switch (metadataType) {
							case LogMetadataType.Class:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ClassLoad) {
										ClassLoadEvent_ClassPointer = ReadPointer(ref span, _bufferHeader),
										ClassLoadEvent_ImagePointer = ReadPointer(ref span, _bufferHeader),
										ClassLoadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid class metadata event.");
								break;
							case LogMetadataType.Image:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ImageLoad) {
										ImageLoadEvent_ImagePointer = ReadPointer(ref span, _bufferHeader),
										ImageLoadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else if (unload) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ImageUnload) {
										ImageUnloadEvent_ImagePointer = ReadPointer(ref span, _bufferHeader),
										ImageUnloadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid image metadata event.");
								break;
							case LogMetadataType.Assembly:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.AssemblyLoad) {
										AssemblyLoadEvent_AssemblyPointer = ReadPointer(ref span, _bufferHeader),
										AssemblyLoadEvent_ImagePointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(ref span, _bufferHeader) : 0,
										AssemblyLoadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else if (unload) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.AssemblyUnload) {
										AssemblyUnloadEvent_AssemblyPointer = ReadPointer(ref span, _bufferHeader),
										AssemblyUnloadEvent_ImagePointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(ref span, _bufferHeader) : 0,
										AssemblyUnloadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid assembly metadata event.");
								break;
							case LogMetadataType.AppDomain:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.AppDomainLoad) {
										AppDomainLoadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else if (unload) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.AppDomainUnload) {
										AppDomainUnloadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.AppDomainName) {
										AppDomainNameEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
										AppDomainNameEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								}
								break;
							case LogMetadataType.Thread:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ThreadStart) {
										ThreadStartEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
									});
								} else if (unload) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ThreadEnd) {
										ThreadEndEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
									});
								} else {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ThreadName) {
										ThreadNameEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
										ThreadNameEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								}
								break;
							case LogMetadataType.Context:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ContextLoad) {
										ContextLoadEvent_ContextId = ReadPointer(ref span, _bufferHeader),
										ContextLoadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else if (unload) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ContextUnload) {
										ContextUnloadEvent_ContextId = ReadPointer(ref span, _bufferHeader),
										ContextUnloadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid context metadata event.");
								break;
							case LogMetadataType.VTable:
								if (load) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.VTableLoad) {
										VTableLoadEvent_VTablePointer = ReadPointer(ref span, _bufferHeader),
										VTableLoadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
										VTableLoadEvent_ClassPointer = ReadPointer(ref span, _bufferHeader),
									});
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
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.Leave) {
								LeaveEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
							});
							break;
						case LogEventType.MethodEnter:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.Enter) {
								EnterEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
							});
							break;
						case LogEventType.MethodLeaveExceptional:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ExceptionalLeave) {
								ExceptionalLeaveEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
							});
							break;
						case LogEventType.MethodJit:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.Jit) {
								JitEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
								JitEvent_CodePointer = ReadPointer(ref span, _bufferHeader),
								JitEvent_CodeSize = (long)ReadULeb128(ref span),
								JitEvent_Name = ReadCString(ref span, _bufferHeader),
							});
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Exception:
					switch (extType) {
						case LogEventType.ExceptionThrowNoBacktrace:
						case LogEventType.ExceptionThrowBacktrace:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.Throw) {
								ThrowEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								ThrowEvent_Backtrace = ReadBacktrace(extType == LogEventType.ExceptionThrowBacktrace, ref span, _bufferHeader, fs, filePrefix),
							});
							break;
						case LogEventType.ExceptionClause:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.ExceptionClause) {
								ExceptionClauseEvent_Type = (LogExceptionClause)(byte)*span++,
								ExceptionClauseEvent_Index = (long)ReadULeb128(ref span),
								ExceptionClauseEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
								ExceptionClauseEvent_ObjectPointer = StreamHeader.FormatVersion >= 14 ? ReadObject(ref span, _bufferHeader) : 0,
							});
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Monitor:
					switch (extType) {
						case LogEventType.MonitorNoBacktrace:
						case LogEventType.MonitorBacktrace:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.Monitor) {
								MonitorEvent_Event = (LogMonitorEvent)(byte)*span++,
								MonitorEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								MonitorEvent_Backtrace = ReadBacktrace(extType == LogEventType.MonitorBacktrace, ref span, _bufferHeader, fs, filePrefix),
							});
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Heap:
					switch (extType) {
						case LogEventType.HeapBegin:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.HeapBegin));
							list.HasHeapBegin = true;
							break;
						case LogEventType.HeapEnd:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.HeapEnd));
							list.HasHeapEnd = true;
							break;
						case LogEventType.HeapObject: {
								var hoe = new SuperEvent(shiftedTime | (byte)LogEventId.HeapObject) {
									HeapObjectEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
									HeapObjectEvent_VTablePointer = ReadPointer(ref span, _bufferHeader),
									HeapObjectEvent_ObjectSize = (long)ReadULeb128(ref span)
								};

								var len = (int)ReadULeb128(ref span);
								if (list.db == null) {
									list.dbName = $"refs{_bufferHeader.streamPosition}";
									CreateDatabase($"file:{list.dbName}?mode=memory&cache=shared", true, out list.db, out list.stmt);
								}
								for (var i = 0; i < len; i++) {
									var at = (long)ReadULeb128(ref span);
									var to = ReadObject(ref span, _bufferHeader);
									check_ok(list.db, raw.sqlite3_bind_int64(list.stmt, 1, hoe.HeapObjectEvent_ObjectPointer));
									check_ok(list.db, raw.sqlite3_bind_int64(list.stmt, 2, at));
									check_ok(list.db, raw.sqlite3_bind_int64(list.stmt, 3, to));
									var result = raw.sqlite3_step(list.stmt);
									if (raw.SQLITE_DONE != result)
										check_ok(list.db, result);
									check_ok(list.db, raw.sqlite3_reset(list.stmt));
								}
								if (hoe.HeapObjectEvent_ObjectSize > 0)
									list.Add(hoe);
								break;
							}
						case LogEventType.HeapRoots: {
								var length = ReadULeb128(ref span);
								if (length % 2 == 1) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.HeapRoots) {
										HeapRootsEvent_AddressPointer = ReadPointer(ref span, _bufferHeader),
										HeapRootsEvent_ObjectPointer = ReadObject(ref span, _bufferHeader)
									});
									length--;
								}
								for (ulong i = 0; i < length; i += 2) {
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.HeapRoots) {
										HeapRootsEvent_AddressPointer = ReadPointer(ref span, _bufferHeader),
										HeapRootsEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
										HeapRootsEvent_AddressPointer2 = ReadPointer(ref span, _bufferHeader),
										HeapRootsEvent_ObjectPointer2 = ReadObject(ref span, _bufferHeader)
									});
								}
								break;
							}
						case LogEventType.HeapRootRegister:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.HeapRootRegister) {
								HeapRootRegisterEvent_RootPointer = ReadPointer(ref span, _bufferHeader),
								HeapRootRegisterEvent_RootSize = (uint)ReadULeb128(ref span),
								HeapRootRegisterEvent_Source = (LogHeapRootSource)(byte)*span++,
								HeapRootRegisterEvent_Key = ReadPointer(ref span, _bufferHeader),
								HeapRootRegisterEvent_Name = ReadCString(ref span, _bufferHeader),
							});
							break;
						case LogEventType.HeapRootUnregister:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.HeapRootUnregister) {
								HeapRootUnregisterEvent_RootPointer = ReadPointer(ref span, _bufferHeader),
							});
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Sample:
					switch (extType) {
						case LogEventType.SampleHit:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.SampleHit) {
								SampleHitEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
								SampleHitEvent_UnmanagedBacktrace = ReadBacktrace(true, ref span, _bufferHeader, fs, filePrefix, false),
								SampleHitEvent_ManagedBacktrace = ReadBacktrace(true, ref span, _bufferHeader, fs, filePrefix),
							});
							break;
						case LogEventType.SampleUnmanagedSymbol:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.UnmanagedSymbol) {
								UnmanagedSymbolEvent_CodePointer = ReadPointer(ref span, _bufferHeader),
								UnmanagedSymbolEvent_CodeSize = (long)ReadULeb128(ref span),
								UnmanagedSymbolEvent_Name = ReadCString(ref span, _bufferHeader),
							});
							break;
						case LogEventType.SampleUnmanagedBinary:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.UnmanagedBinary) {
								UnmanagedBinaryEvent_SegmentPointer = ReadPointer(ref span, _bufferHeader),
								UnmanagedBinaryEvent_SegmentOffset = (long)ReadULeb128(ref span),
								UnmanagedBinaryEvent_SegmentSize = (long)ReadULeb128(ref span),
								UnmanagedBinaryEvent_FileName = ReadCString(ref span, _bufferHeader),
							});
							break;
						case LogEventType.SampleCounterDescriptions: {
								var length = (int)ReadULeb128(ref span);
								for (var i = 0; i < length; i++) {
									var section = ReadULeb128(ref span);
									list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.CounterDescriptions) {
										CounterDescriptionsEvent_SectionName = (LogCounterSection)section == LogCounterSection.User ? ReadCString(ref span, _bufferHeader) : 0,
										CounterDescriptionsEvent_CounterName = ReadCString(ref span, _bufferHeader),
										CounterDescriptionsEvent_SectionTypeUnitVariance = ReadULeb128(ref span) | section | ReadULeb128(ref span) | ReadULeb128(ref span),
										CounterDescriptionsEvent_Index = (long)ReadULeb128(ref span),
									});
								}
								break;
							}
						case LogEventType.SampleCounters: {
								while (true) {
									var index = (long)ReadULeb128(ref span);

									if (index == 0)
										break;

									var counterType = (LogCounterType)ReadULeb128(ref span);

									var cse = new SuperEvent(shiftedTime | (byte)LogEventId.CounterSamples) {
										CounterSamplesEvent_Index = index,
										CounterSamplesEvent_Type = counterType,
									};
									switch (counterType) {
										case LogCounterType.String:
											cse.CounterSamplesEvent_Value_Ulong = *span++ == 1 ? ReadCString(ref span, _bufferHeader) : 0;
											break;
										case LogCounterType.Int32:
										case LogCounterType.Word:
										case LogCounterType.Int64:
										case LogCounterType.Interval:
											cse.CounterSamplesEvent_Value_Long = ReadSLeb128(ref span);
											break;
										case LogCounterType.UInt32:
										case LogCounterType.UInt64:
											cse.CounterSamplesEvent_Value_Ulong = ReadULeb128(ref span);
											break;
										case LogCounterType.Double:
											cse.CounterSamplesEvent_Value_Double = ReadDouble(ref span);
											break;
										default:
											throw new LogException($"Invalid counter type ({counterType}).");
									}
									list.Add(cse);
								}
								break;
							}
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Runtime:
					switch (extType) {
						case LogEventType.RuntimeJitHelper: {
								var helperType = (LogJitHelper)(byte)*span++;
								list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.JitHelper) {
									JitHelperEvent_Type = helperType,
									JitHelperEvent_BufferPointer = ReadPointer(ref span, _bufferHeader),
									JitHelperEvent_BufferSize = (long)ReadULeb128(ref span),
									JitHelperEvent_Name = helperType == LogJitHelper.SpecificTrampoline ? ReadCString(ref span, _bufferHeader) : 0,
								});
								break;
							}
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Meta:
					switch (extType) {
						case LogEventType.MetaSynchronizationPoint:
							list.Add(new SuperEvent(shiftedTime | (byte)LogEventId.SynchronizationPoint) {
								SynchronizationPointEvent_Type = (LogSynchronizationPoint)(byte)*span++,
							});
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				default:
					throw new LogException($"Invalid basic event type ({basicType}).");
			}
		}

		private static void check_ok(sqlite3 db, int rc)
		{
			if (raw.SQLITE_OK != rc)
				throw new Exception(raw.sqlite3_errstr(rc) + ": " + raw.sqlite3_errmsg(db));
		}

		private void CreateDatabase(string name, bool createStatement, out sqlite3 db, out sqlite3_stmt stmt)
		{
			var rc = raw.sqlite3_open_v2(name, out db, raw.SQLITE_OPEN_URI | raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, null);
			check_ok(db, rc);
			check_ok(db, raw.sqlite3_exec(db, @"CREATE TABLE Refs
			(
				AddressFrom INT NOT NULL,
				FieldOffset INT NOT NULL,
				AddressTo INT NOT NULL
			)"));
			check_ok(db, raw.sqlite3_exec(db, "PRAGMA synchronous=OFF"));
			check_ok(db, raw.sqlite3_exec(db, "PRAGMA count_changes=OFF"));
			check_ok(db, raw.sqlite3_exec(db, "PRAGMA journal_mode=OFF"));
			check_ok(db, raw.sqlite3_exec(db, "PRAGMA temp_store=MEMORY"));
			if (createStatement) {
				check_ok(db, raw.sqlite3_exec(db, "BEGIN TRANSACTION;"));
				check_ok(db, raw.sqlite3_prepare_v2(db, "INSERT INTO Refs(AddressFrom, FieldOffset, AddressTo) VALUES(?,?,?)", out stmt));
			} else
				stmt = null;
		}

		byte[] bytes = new byte[4096];
		internal string ReadString(ulong position)
		{
			using (var fs = new FileStream(Stream.Name, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				fs.Position = (long)position;
				var read = fs.Read(bytes, 0, bytes.Length);
				var zeroPosition = 0;
				while (zeroPosition < bytes.Length && bytes[zeroPosition++] != 0) { }
				return Encoding.UTF8.GetString(bytes, 0, zeroPosition - 1);
			}
		}

		private class SuperEventList
		{
			public bool HasHeapBegin;
			public bool HasHeapEnd;
			public sqlite3 db;
			public sqlite3_stmt stmt;
			private int count = 0;
			public SuperEvent[] superEventList = new SuperEvent[10000];
			public string dbName;

			public int Count { get => count; }

			internal void Add(SuperEvent superEvent)
			{
				superEventList[count++] = superEvent;
			}

			internal void Clear()
			{
				count = 0;
				HasHeapBegin = false;
				HasHeapEnd = false;
			}
		}
	}
}
