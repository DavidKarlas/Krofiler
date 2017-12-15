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
			Stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			Visitor = visitor;
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

		bool live;
		bool fileFinished;
		CancellationToken token;
		public string CacheFolder;
		const int QueueSize = 100;
		public unsafe void Process(CancellationToken token, bool live = false)
		{
			if (_used)
				throw new InvalidOperationException("This log processor cannot be reused.");
			_used = true;
			this.live = live;
			this.token = token;

			var _reader = new LogReader(Stream, true);

			StreamHeader = new LogStreamHeader(_reader);
			startLength = Stream.Length;
			const int BufferSize = 4096 * 16 + 16;//+16 in case of optimisation that read ahead don't fall out
			var Buffer = new byte[BufferSize];
			var cacheStream = new WriteFile();
			cacheStream.pointer = Mono.Unix.Native.Stdlib.fopen("cache.krof", "wb");
			try {
				fixed (byte* startPointer = &Buffer[0]) {
					while (!this.token.IsCancellationRequested) {
						if (!Wait(48))
							return;
						var _bufferHeader = new LogBufferHeader(StreamHeader, _reader, (ulong)Stream.Position, startPointer);
						if (!Wait(_bufferHeader.Length))
							return;
						Stream.Read(Buffer, 0, _bufferHeader.Length);
						var pointer = startPointer;
						while ((pointer - startPointer) < _bufferHeader.Length) {
							ReadEvent(ref pointer, _bufferHeader, cacheStream);
						}
					}
				}
			} finally {
				Mono.Unix.Native.Stdlib.fclose(cacheStream.pointer);
			}
		}

		static unsafe ulong ReadCString(ref byte* pointer, LogBufferHeader _bufferHeader)
		{
			var pos = pointer;
			while (*pointer++ != 0) { }
			//Console.WriteLine(Encoding.UTF8.GetString(pos, (int)(pointer - pos - 1)));
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
			// Sign extend negative numbers.
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


		unsafe long ReadPointer(ref byte* span, LogBufferHeader _bufferHeader)
		{
			var ptr = ReadSLeb128(ref span) + _bufferHeader.PointerBase;

			return StreamHeader.PointerSize == sizeof(long) ? ptr : ptr & 0xffffffffL;
		}

		unsafe long ReadObject(ref byte* span, LogBufferHeader _bufferHeader)
		{
			return ReadSLeb128(ref span) + _bufferHeader.ObjectBase << 3;
		}

		unsafe static long ReadMethod(ref byte* _reader, LogBufferHeader _bufferHeader)
		{
			return _bufferHeader.CurrentMethod += ReadSLeb128(ref _reader);
		}
		struct WriteFile
		{
			public ulong position;
			public IntPtr pointer;
		}
		unsafe ulong ReadBacktrace(bool actuallyRead, ref byte* span, LogBufferHeader _bufferHeader, WriteFile fs, bool managed = true)
		{
			if (!actuallyRead)
				return ulong.MaxValue;
			var posBefore = fs.position;
			var length = (int)ReadULeb128(ref span);

			for (var i = 0; i < length; i++) {
				var ptr = managed ? ReadMethod(ref span, _bufferHeader) : ReadPointer(ref span, _bufferHeader);
				var written = Mono.Unix.Native.Stdlib.fwrite(&ptr, 8, 1, fs.pointer);
				fs.position += written;
				Debug.Assert(written == 8);
			}
			return posBefore;
		}

		unsafe void ReadEvent(ref byte* span, LogBufferHeader _bufferHeader, WriteFile fs)
		{
			var type = *span++;
			var basicType = (LogEventType)(type & 0xf);
			var extType = (LogEventType)(type & 0xf0);

			_bufferHeader.CurrentTime += ReadULeb128(ref span);

			switch (basicType) {
				case LogEventType.Allocation:
					switch (extType) {
						case LogEventType.AllocationBacktrace:
						case LogEventType.AllocationNoBacktrace:
							Visitor.VisitAllocationEvent(new SuperEvent() {
								AllocationEvent_VTablePointer = ReadPointer(ref span, _bufferHeader),
								AllocationEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								AllocationEvent_ObjectSize = ReadULeb128(ref span),
								AllocationEvent_FilePointer = fs.position
							});
							var time = _bufferHeader.CurrentTime;
							Mono.Unix.Native.Stdlib.fwrite(&time, 8, 1, fs.pointer);
							ReadBacktrace(extType == LogEventType.AllocationBacktrace, ref span, _bufferHeader, fs);
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.GC:
					switch (extType) {
						case LogEventType.GCEvent:
							Visitor.VisitGCEvent(new SuperEvent() {
								GCEvent_Type = (LogGCEvent)(byte)*span++,
								GCEvent_Generation = *span++,
							});
							break;
						case LogEventType.GCResize:
							Visitor.VisitGCResizeEvent(new SuperEvent() {
								GCResizeEvent_NewSize = (long)ReadULeb128(ref span),
							});
							break;
						case LogEventType.GCMove: {
								var length = ReadULeb128(ref span) / 2;

								for (ulong i = 0; i < length; i++)
									Visitor.VisitGCMoveEvent(new SuperEvent() {
										GCMoveEvent_OldObjectPointer = ReadObject(ref span, _bufferHeader),
										GCMoveEvent_NewObjectPointer = ReadObject(ref span, _bufferHeader)
									});
								break;
							}
						case LogEventType.GCHandleCreationNoBacktrace:
						case LogEventType.GCHandleCreationBacktrace:
							Visitor.VisitGCHandleCreationEvent(new SuperEvent() {
								GCHandleCreationEvent_Type = (LogGCHandleType)ReadULeb128(ref span),
								GCHandleCreationEvent_Handle = (long)ReadULeb128(ref span),
								GCHandleCreationEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								GCHandleCreationEvent_Backtrace = ReadBacktrace(extType == LogEventType.GCHandleCreationBacktrace, ref span, _bufferHeader, fs),
							});
							break;
						case LogEventType.GCHandleDeletionNoBacktrace:
						case LogEventType.GCHandleDeletionBacktrace:
							Visitor.VisitGCHandleDeletionEvent(new SuperEvent() {
								GCHandleDeletionEvent_Type = (LogGCHandleType)ReadULeb128(ref span),
								GCHandleDeletionEvent_Handle = (long)ReadULeb128(ref span),
								GCHandleDeletionEvent_Backtrace = ReadBacktrace(extType == LogEventType.GCHandleDeletionBacktrace, ref span, _bufferHeader, fs),
							});
							break;
						case LogEventType.GCFinalizeBegin:
							Visitor.VisitGCFinalizeBeginEvent(new SuperEvent());
							break;
						case LogEventType.GCFinalizeEnd:
							Visitor.VisitGCFinalizeEndEvent(new SuperEvent());
							break;
						case LogEventType.GCFinalizeObjectBegin:
							Visitor.VisitGCFinalizeObjectBeginEvent(new SuperEvent() {
								GCFinalizeObjectBeginEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
							});
							break;
						case LogEventType.GCFinalizeObjectEnd:
							Visitor.VisitGCFinalizeObjectEndEvent(new SuperEvent() {
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
									Visitor.VisitClassLoadEvent(new SuperEvent() {
										ClassLoadEvent_ClassPointer = ReadPointer(ref span, _bufferHeader),
										ClassLoadEvent_ImagePointer = ReadPointer(ref span, _bufferHeader),
										ClassLoadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid class metadata event.");
								break;
							case LogMetadataType.Image:
								if (load) {
									Visitor.VisitImageLoadEvent(new SuperEvent() {
										ImageLoadEvent_ImagePointer = ReadPointer(ref span, _bufferHeader),
										ImageLoadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else if (unload) {
									Visitor.VisitImageUnloadEvent(new SuperEvent() {
										ImageUnloadEvent_ImagePointer = ReadPointer(ref span, _bufferHeader),
										ImageUnloadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid image metadata event.");
								break;
							case LogMetadataType.Assembly:
								if (load) {
									Visitor.VisitAssemblyLoadEvent(new SuperEvent() {
										AssemblyLoadEvent_AssemblyPointer = ReadPointer(ref span, _bufferHeader),
										AssemblyLoadEvent_ImagePointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(ref span, _bufferHeader) : 0,
										AssemblyLoadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else if (unload) {
									Visitor.VisitAssemblyUnloadEvent(new SuperEvent() {
										AssemblyUnloadEvent_AssemblyPointer = ReadPointer(ref span, _bufferHeader),
										AssemblyUnloadEvent_ImagePointer = StreamHeader.FormatVersion >= 14 ? ReadPointer(ref span, _bufferHeader) : 0,
										AssemblyUnloadEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid assembly metadata event.");
								break;
							case LogMetadataType.AppDomain:
								if (load) {
									Visitor.VisitAppDomainLoadEvent(new SuperEvent() {
										AppDomainLoadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else if (unload) {
									Visitor.VisitAppDomainUnloadEvent(new SuperEvent() {
										AppDomainUnloadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else {
									Visitor.VisitAppDomainNameEvent(new SuperEvent() {
										AppDomainNameEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
										AppDomainNameEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								}
								break;
							case LogMetadataType.Thread:
								if (load) {
									Visitor.VisitThreadStartEvent(new SuperEvent() {
										ThreadStartEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
									});
								} else if (unload) {
									Visitor.VisitThreadEndEvent(new SuperEvent() {
										ThreadEndEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
									});
								} else {
									Visitor.VisitThreadNameEvent(new SuperEvent() {
										ThreadNameEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
										ThreadNameEvent_Name = ReadCString(ref span, _bufferHeader),
									});
								}
								break;
							case LogMetadataType.Context:
								if (load) {
									Visitor.VisitContextLoadEvent(new SuperEvent() {
										ContextLoadEvent_ContextId = ReadPointer(ref span, _bufferHeader),
										ContextLoadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else if (unload) {
									Visitor.VisitContextUnloadEvent(new SuperEvent() {
										ContextUnloadEvent_ContextId = ReadPointer(ref span, _bufferHeader),
										ContextUnloadEvent_AppDomainId = ReadPointer(ref span, _bufferHeader),
									});
								} else
									throw new LogException("Invalid context metadata event.");
								break;
							case LogMetadataType.VTable:
								if (load) {
									Visitor.VisitVTableLoadEvent(new SuperEvent() {
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
							Visitor.VisitLeaveEvent(new SuperEvent() {
								LeaveEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
							});
							break;
						case LogEventType.MethodEnter:
							Visitor.VisitEnterEvent(new SuperEvent() {
								EnterEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
							});
							break;
						case LogEventType.MethodLeaveExceptional:
							Visitor.VisitExceptionalLeaveEvent(new SuperEvent() {
								ExceptionalLeaveEvent_MethodPointer = ReadMethod(ref span, _bufferHeader),
							});
							break;
						case LogEventType.MethodJit:
							Visitor.VisitJitEvent(new SuperEvent() {
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
							Visitor.VisitThrowEvent(new SuperEvent() {
								ThrowEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								ThrowEvent_Backtrace = ReadBacktrace(extType == LogEventType.ExceptionThrowBacktrace, ref span, _bufferHeader, fs),
							});
							break;
						case LogEventType.ExceptionClause:
							Visitor.VisitExceptionClauseEvent(new SuperEvent() {
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
							Visitor.VisitMonitorEvent(new SuperEvent() {
								MonitorEvent_Event = StreamHeader.FormatVersion >= 14 ?
										(LogMonitorEvent)(byte)*span++ :
										(LogMonitorEvent)((((byte)type & 0xf0) >> 4) & 0x3),
								MonitorEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
								MonitorEvent_Backtrace = ReadBacktrace(extType == LogEventType.MonitorBacktrace, ref span, _bufferHeader, fs),
							});
							break;
						default:
							throw new LogException($"Invalid extended event type ({extType}).");
					}
					break;
				case LogEventType.Heap:
					switch (extType) {
						case LogEventType.HeapBegin:
							Visitor.VisitHeapBeginEvent(new SuperEvent());
							break;
						case LogEventType.HeapEnd:
							Visitor.VisitHeapEndEvent(new SuperEvent());
							break;
						case LogEventType.HeapObject: {
								var hoe = new SuperEvent() {
									HeapObjectEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
									HeapObjectEvent_VTablePointer = ReadPointer(ref span, _bufferHeader),
									HeapObjectEvent_ObjectSize = (long)ReadULeb128(ref span),
								};

								var listTo = new long[(int)ReadULeb128(ref span)];
								var listAt = new ushort[listTo.Length];

								for (var i = 0; i < listTo.Length; i++) {
									listAt[i] = (ushort)ReadULeb128(ref span);
									listTo[i] = ReadObject(ref span, _bufferHeader);
								}

								//hoe.ReferencesAt = listAt;
								//hoe.ReferencesTo = listTo;
								Visitor.VisitHeapObjectEvent(hoe);
								break;
							}

						case LogEventType.HeapRoots: {
								var length = ReadULeb128(ref span);
								if (length % 2 == 1) {
									Visitor.VisitHeapRootsEvent(new SuperEvent() {
										HeapRootsEvent_AddressPointer = ReadPointer(ref span, _bufferHeader),
										HeapRootsEvent_ObjectPointer = ReadObject(ref span, _bufferHeader)
									});
									length--;
								}
								for (ulong i = 0; i < length; i += 2) {
									Visitor.VisitHeapRootsEvent(new SuperEvent() {
										HeapRootsEvent_AddressPointer = ReadPointer(ref span, _bufferHeader),
										HeapRootsEvent_ObjectPointer = ReadObject(ref span, _bufferHeader),
										HeapRootsEvent_AddressPointer2 = ReadPointer(ref span, _bufferHeader),
										HeapRootsEvent_ObjectPointer2 = ReadObject(ref span, _bufferHeader)
									});
								}
								break;
							}
						case LogEventType.HeapRootRegister:
							Visitor.VisitHeapRootRegisterEvent(new SuperEvent() {
								HeapRootRegisterEvent_RootPointer = ReadPointer(ref span, _bufferHeader),
								HeapRootRegisterEvent_RootSize = (uint)ReadULeb128(ref span),
								HeapRootRegisterEvent_Source = (LogHeapRootSource)(byte)*span++,
								HeapRootRegisterEvent_Key = ReadPointer(ref span, _bufferHeader),
								HeapRootRegisterEvent_Name = ReadCString(ref span, _bufferHeader),
							});
							break;
						case LogEventType.HeapRootUnregister:
							Visitor.VisitHeapRootUnregisterEvent(new SuperEvent() {
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
							Visitor.VisitSampleHitEvent(new SuperEvent() {
								SampleHitEvent_ThreadId = ReadPointer(ref span, _bufferHeader),
								SampleHitEvent_UnmanagedBacktrace = ReadBacktrace(true, ref span, _bufferHeader, fs, false),
								SampleHitEvent_ManagedBacktrace = ReadBacktrace(true, ref span, _bufferHeader, fs),
							});
							break;
						case LogEventType.SampleUnmanagedSymbol:
							Visitor.VisitUnmanagedSymbolEvent(new SuperEvent() {
								UnmanagedSymbolEvent_CodePointer = ReadPointer(ref span, _bufferHeader),
								UnmanagedSymbolEvent_CodeSize = (long)ReadULeb128(ref span),
								UnmanagedSymbolEvent_Name = ReadCString(ref span, _bufferHeader),
							});
							break;
						case LogEventType.SampleUnmanagedBinary:
							Visitor.VisitUnmanagedBinaryEvent(new SuperEvent() {
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
									Visitor.VisitCounterDescriptionsEvent(new SuperEvent() {
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

									object value = null;

									switch (counterType) {
										case LogCounterType.String:
											value = *span++ == 1 ? ReadCString(ref span, _bufferHeader) : 0;
											break;
										case LogCounterType.Int32:
										case LogCounterType.Word:
										case LogCounterType.Int64:
										case LogCounterType.Interval:
											value = ReadSLeb128(ref span);
											break;
										case LogCounterType.UInt32:
										case LogCounterType.UInt64:
											value = ReadULeb128(ref span);
											break;
										case LogCounterType.Double:
											value = ReadDouble(ref span);
											break;
										default:
											throw new LogException($"Invalid counter type ({counterType}).");
									}

									Visitor.VisitCounterSamplesEvent(new SuperEvent() {
										CounterSamplesEvent_Index = index,
										CounterSamplesEvent_Type = counterType,
										CounterSamplesEvent_Value = value,
									});
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
								Visitor.VisitJitHelperEvent(new SuperEvent() {
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
							Visitor.VisitSynchronizationPointEvent(new SuperEvent() {
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
		byte[] bytes = new byte[4096];
		internal string ReadString(ulong position)
		{
			using (var fs = new FileStream(Stream.Name, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				fs.Position = (long)position;
				var read = fs.Read(bytes, 0, bytes.Length);
				while (bytes[position++] != 0) { }
				return Encoding.UTF8.GetString(bytes, 0, (int)position);
			}
		}
	}
}
