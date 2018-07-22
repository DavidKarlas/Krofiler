// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Mono.Profiler.Log
{

	[StructLayout(LayoutKind.Explicit, Size = 40)]
	public struct SuperEvent
	{
		public SuperEvent(ulong timeAndType) : this()
		{
			TimestampAndType = timeAndType;
		}

		[FieldOffset(0)]
		public ulong TimestampAndType;

		[FieldOffset(8)]
		public long AppDomainLoadEvent_AppDomainId;

		[FieldOffset(8)]
		public long AppDomainUnloadEvent_AppDomainId;

		[FieldOffset(8)]
		public long AppDomainNameEvent_AppDomainId;
		[FieldOffset(16)]
		public ulong AppDomainNameEvent_Name;

		[FieldOffset(8)]
		public long ContextLoadEvent_ContextId;
		[FieldOffset(16)]
		public long ContextLoadEvent_AppDomainId;

		[FieldOffset(8)]
		public long ContextUnloadEvent_ContextId;
		[FieldOffset(16)]
		public long ContextUnloadEvent_AppDomainId;

		[FieldOffset(8)]
		public long ThreadStartEvent_ThreadId;

		[FieldOffset(8)]
		public long ThreadEndEvent_ThreadId;

		[FieldOffset(8)]
		public long ThreadNameEvent_ThreadId;
		[FieldOffset(16)]
		public ulong ThreadNameEvent_Name;

		[FieldOffset(8)]
		public long ImageLoadEvent_ImagePointer;
		[FieldOffset(16)]
		public ulong ImageLoadEvent_Name;
		[FieldOffset(24)]
		public ulong ImageLoadEvent_ModuleVersionId_Guid;

		[FieldOffset(8)]
		public long ImageUnloadEvent_ImagePointer;
		[FieldOffset(16)]
		public ulong ImageUnloadEvent_Name;

		[FieldOffset(8)]
		public long AssemblyLoadEvent_AssemblyPointer;
		[FieldOffset(16)]
		public long AssemblyLoadEvent_ImagePointer;
		[FieldOffset(24)]
		public ulong AssemblyLoadEvent_Name;

		[FieldOffset(8)]
		public long AssemblyUnloadEvent_AssemblyPointer;
		[FieldOffset(16)]
		public long AssemblyUnloadEvent_ImagePointer;
		[FieldOffset(24)]
		public ulong AssemblyUnloadEvent_Name;


		[FieldOffset(8)]
		public long ClassLoadEvent_ClassPointer;

		[FieldOffset(16)]
		public long ClassLoadEvent_ImagePointer;
		[FieldOffset(24)]
		public ulong ClassLoadEvent_Name;

		[FieldOffset(8)]
		public long VTableLoadEvent_VTablePointer;
		[FieldOffset(16)]
		public long VTableLoadEvent_AppDomainId;
		[FieldOffset(24)]
		public long VTableLoadEvent_ClassPointer;


		[FieldOffset(8)]
		public long JitEvent_MethodPointer;
		[FieldOffset(16)]
		public long JitEvent_CodePointer;
		[FieldOffset(24)]
		public long JitEvent_CodeSize;
		[FieldOffset(32)]
		public ulong JitEvent_Name;

		[FieldOffset(8)]
		public LogJitHelper JitHelperEvent_Type;
		[FieldOffset(16)]
		public long JitHelperEvent_BufferPointer;
		[FieldOffset(24)]
		public long JitHelperEvent_BufferSize;
		[FieldOffset(32)]
		public ulong JitHelperEvent_Name;

		[FieldOffset(8)]
		public long AllocationEvent_VTablePointer;
		[FieldOffset(16)]
		public long AllocationEvent_ObjectPointer;
		[FieldOffset(24)]
		public ulong AllocationEvent_ObjectSize;
		[FieldOffset(32)]
		public ulong AllocationEvent_FilePointer;

		[FieldOffset(8)]
		public long HeapObjectEvent_ObjectPointer;
		[FieldOffset(16)]
		public long HeapObjectEvent_VTablePointer;
		[FieldOffset(24)]
		public long HeapObjectEvent_ObjectSize;
		[FieldOffset(32)]
		public int HeapObjectEvent_Generation;

		[FieldOffset(8)]
		public long HeapRootsEvent_AddressPointer;
		[FieldOffset(16)]
		public long HeapRootsEvent_ObjectPointer;
		[FieldOffset(24)]
		public long HeapRootsEvent_AddressPointer2;
		[FieldOffset(32)]
		public long HeapRootsEvent_ObjectPointer2;


		[FieldOffset(8)]
		public long HeapRootRegisterEvent_RootPointer;
		[FieldOffset(16)]
		public uint HeapRootRegisterEvent_RootSize;
		[FieldOffset(20)]
		public LogHeapRootSource HeapRootRegisterEvent_Source;
		[FieldOffset(24)]
		public long HeapRootRegisterEvent_Key;
		[FieldOffset(32)]
		public ulong HeapRootRegisterEvent_Name;

		[FieldOffset(8)]
		public long HeapRootUnregisterEvent_RootPointer;

		[FieldOffset(8)]
		public LogGCEvent GCEvent_Type;
		[FieldOffset(16)]
		public byte GCEvent_Generation;

		[FieldOffset(8)]
		public long GCResizeEvent_NewSize;

		[FieldOffset(8)]
		public long GCMoveEvent_OldObjectPointer;
		[FieldOffset(16)]
		public long GCMoveEvent_NewObjectPointer;
		[FieldOffset(24)]
		public long GCMoveEvent_OldObjectPointer2;
		[FieldOffset(32)]
		public long GCMoveEvent_NewObjectPointer2;

		[FieldOffset(8)]
		public LogGCHandleType GCHandleCreationEvent_Type;
		[FieldOffset(16)]
		public long GCHandleCreationEvent_Handle;
		[FieldOffset(24)]
		public long GCHandleCreationEvent_ObjectPointer;
		[FieldOffset(32)]
		public ulong GCHandleCreationEvent_Backtrace;

		[FieldOffset(8)]
		public LogGCHandleType GCHandleDeletionEvent_Type;
		[FieldOffset(16)]
		public long GCHandleDeletionEvent_Handle;
		[FieldOffset(24)]
		public ulong GCHandleDeletionEvent_Backtrace;

		[FieldOffset(8)]
		public long GCFinalizeObjectBeginEvent_ObjectPointer;

		[FieldOffset(8)]
		public long GCFinalizeObjectEndEvent_ObjectPointer;

		[FieldOffset(8)]
		public long ThrowEvent_ObjectPointer;
		[FieldOffset(16)]
		public ulong ThrowEvent_Backtrace;

		[FieldOffset(8)]
		public LogExceptionClause ExceptionClauseEvent_Type;
		[FieldOffset(16)]
		public long ExceptionClauseEvent_Index;
		[FieldOffset(24)]
		public long ExceptionClauseEvent_MethodPointer;
		[FieldOffset(32)]
		public long ExceptionClauseEvent_ObjectPointer;

		[FieldOffset(8)]
		public long EnterEvent_MethodPointer;

		[FieldOffset(8)]
		public long LeaveEvent_MethodPointer;

		[FieldOffset(8)]
		public long ExceptionalLeaveEvent_MethodPointer;

		[FieldOffset(8)]
		public LogMonitorEvent MonitorEvent_Event;
		[FieldOffset(16)]
		public long MonitorEvent_ObjectPointer;
		[FieldOffset(24)]
		public ulong MonitorEvent_Backtrace;

		[FieldOffset(8)]
		public long SampleHitEvent_ThreadId;
		[FieldOffset(16)]
		public ulong SampleHitEvent_UnmanagedBacktrace;
		[FieldOffset(24)]
		public ulong SampleHitEvent_ManagedBacktrace;

		[FieldOffset(8)]
		public long CounterSamplesEvent_Index;
		[FieldOffset(16)]
		public LogCounterType CounterSamplesEvent_Type;
		[FieldOffset(24)]
		public double CounterSamplesEvent_Value_Double;
		[FieldOffset(24)]
		public long CounterSamplesEvent_Value_Long;
		[FieldOffset(24)]
		public ulong CounterSamplesEvent_Value_Ulong;

		[FieldOffset(8)]
		public ulong CounterDescriptionsEvent_SectionTypeUnitVariance;
		[FieldOffset(16)]
		public ulong CounterDescriptionsEvent_SectionName;
		[FieldOffset(24)]
		public ulong CounterDescriptionsEvent_CounterName;
		[FieldOffset(32)]
		public long CounterDescriptionsEvent_Index;

		[FieldOffset(8)]
		public long UnmanagedBinaryEvent_SegmentPointer;
		[FieldOffset(16)]
		public long UnmanagedBinaryEvent_SegmentOffset;
		[FieldOffset(24)]
		public long UnmanagedBinaryEvent_SegmentSize;
		[FieldOffset(32)]
		public ulong UnmanagedBinaryEvent_FileName;

		[FieldOffset(8)]
		public long UnmanagedSymbolEvent_CodePointer;
		[FieldOffset(16)]
		public long UnmanagedSymbolEvent_CodeSize;
		[FieldOffset(24)]
		public ulong UnmanagedSymbolEvent_Name;

		[FieldOffset(8)]
		public LogSynchronizationPoint SynchronizationPointEvent_Type;

		[FieldOffset(8)]
		public ulong MetaAotId_AotId_Guid;

		public TimeSpan Time { get => TimeSpan.FromTicks((long)(TimestampAndType >> 8) / 100); }


		internal string GetName(LogProcessor processor)
		{
			switch ((LogEventId)(TimestampAndType & 0xff)) {
				case LogEventId.HeapRootRegister:
					return processor.ReadString(HeapRootRegisterEvent_Name);
				default:
					throw new NotImplementedException("GetName not implemented for " + (LogEventId)(TimestampAndType & 0xff));
			}
		}

		internal object GetSectionName(LogProcessor processor)
		{
			return processor.ReadString(CounterDescriptionsEvent_SectionName);
		}

		internal string GetCounterName(LogProcessor processor)
		{
			return processor.ReadString(CounterDescriptionsEvent_CounterName);
		}
	}
}
