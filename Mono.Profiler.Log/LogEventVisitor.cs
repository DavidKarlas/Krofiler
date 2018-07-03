// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Mono.Profiler.Log
{
	public enum LogEventId : byte
	{
		Unset,
		AppDomainLoad,
		AppDomainUnload,
		AppDomainName,
		ContextLoad,
		ContextUnload,
		ThreadStart,
		ThreadEnd,
		ThreadName,
		ImageLoad,
		ImageUnload,
		AssemblyLoad,
		AssemblyUnload,
		ClassLoad,
		VTableLoad,
		Jit,
		JitHelper,
		Allocation,
		HeapBegin,
		HeapEnd,
		HeapObject,
		HeapRoots,
		HeapRootRegister,
		HeapRootUnregister,
		GC,
		GCResize,
		GCMove,
		GCHandleCreation,
		GCHandleDeletion,
		GCFinalizeBegin,
		GCFinalizeEnd,
		GCFinalizeObjectBegin,
		GCFinalizeObjectEnd,
		Throw,
		ExceptionClause,
		Enter,
		Leave,
		ExceptionalLeave,
		Monitor,
		SampleHit,
		CounterSamples,
		CounterDescriptions,
		UnmanagedBinary,
		UnmanagedSymbol,
		SynchronizationPoint,
		MetaAotId,
	}

	public abstract class LogEventVisitor
	{
		public virtual void VisitAppDomainLoadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitAppDomainUnloadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitAppDomainNameEvent(SuperEvent ev)
		{
		}

		public virtual void VisitContextLoadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitContextUnloadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitThreadStartEvent(SuperEvent ev)
		{
		}

		public virtual void VisitThreadEndEvent(SuperEvent ev)
		{
		}

		public virtual void VisitThreadNameEvent(SuperEvent ev)
		{
		}

		public virtual void VisitImageLoadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitImageUnloadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitAssemblyLoadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitAssemblyUnloadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitClassLoadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitVTableLoadEvent(SuperEvent ev)
		{
		}

		public virtual void VisitJitEvent(SuperEvent ev)
		{
		}

		public virtual void VisitJitHelperEvent(SuperEvent ev)
		{
		}

		public virtual void VisitAllocationEvent(SuperEvent ev)
		{
		}

		public virtual void VisitHeapBeginEvent(SuperEvent ev)
		{
		}

		public virtual void VisitHeapEndEvent(SuperEvent ev)
		{
		}

		public virtual void VisitHeapObjectEvent(SuperEvent ev)
		{
		}

		public virtual void VisitHeapRootsEvent(SuperEvent ev)
		{
		}

		public virtual void VisitHeapRootRegisterEvent(SuperEvent ev)
		{
		}

		public virtual void VisitHeapRootUnregisterEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCResizeEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCMoveEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCHandleCreationEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCHandleDeletionEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCFinalizeBeginEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCFinalizeEndEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCFinalizeObjectBeginEvent(SuperEvent ev)
		{
		}

		public virtual void VisitGCFinalizeObjectEndEvent(SuperEvent ev)
		{
		}

		public virtual void VisitThrowEvent(SuperEvent ev)
		{
		}

		public virtual void VisitExceptionClauseEvent(SuperEvent ev)
		{
		}

		public virtual void VisitEnterEvent(SuperEvent ev)
		{
		}

		public virtual void VisitLeaveEvent(SuperEvent ev)
		{
		}

		public virtual void VisitExceptionalLeaveEvent(SuperEvent ev)
		{
		}

		public virtual void VisitMonitorEvent(SuperEvent ev)
		{
		}

		public virtual void VisitSampleHitEvent(SuperEvent ev)
		{
		}

		public virtual void VisitCounterSamplesEvent(SuperEvent ev)
		{
		}

		public virtual void VisitCounterDescriptionsEvent(SuperEvent ev)
		{
		}

		public virtual void VisitUnmanagedBinaryEvent(SuperEvent ev)
		{
		}

		public virtual void VisitUnmanagedSymbolEvent(SuperEvent ev)
		{
		}

		public virtual void VisitSynchronizationPointEvent(SuperEvent ev)
		{
		}

		public virtual void VisitMetaAotId(SuperEvent ev)
		{
		}

		internal void VisitSuper(SuperEvent superEvent)
		{
			switch ((LogEventId)(superEvent.TimestampAndType & 0xff)) {
				case LogEventId.AppDomainLoad:
					VisitAppDomainLoadEvent(superEvent);
					break;
				case LogEventId.AppDomainUnload:
					VisitAppDomainUnloadEvent(superEvent);
					break;
				case LogEventId.AppDomainName:
					VisitAppDomainNameEvent(superEvent);
					break;
				case LogEventId.ContextLoad:
					VisitContextLoadEvent(superEvent);
					break;
				case LogEventId.ContextUnload:
					VisitContextUnloadEvent(superEvent);
					break;
				case LogEventId.ThreadStart:
					VisitThreadStartEvent(superEvent);
					break;
				case LogEventId.ThreadEnd:
					VisitThreadEndEvent(superEvent);
					break;
				case LogEventId.ThreadName:
					VisitThreadNameEvent(superEvent);
					break;
				case LogEventId.ImageLoad:
					VisitImageLoadEvent(superEvent);
					break;
				case LogEventId.ImageUnload:
					VisitImageUnloadEvent(superEvent);
					break;
				case LogEventId.AssemblyLoad:
					VisitAssemblyLoadEvent(superEvent);
					break;
				case LogEventId.AssemblyUnload:
					VisitAssemblyUnloadEvent(superEvent);
					break;
				case LogEventId.ClassLoad:
					VisitClassLoadEvent(superEvent);
					break;
				case LogEventId.VTableLoad:
					VisitVTableLoadEvent(superEvent);
					break;
				case LogEventId.Jit:
					VisitJitEvent(superEvent);
					break;
				case LogEventId.JitHelper:
					VisitJitHelperEvent(superEvent);
					break;
				case LogEventId.Allocation:
					VisitAllocationEvent(superEvent);
					break;
				case LogEventId.HeapBegin:
					VisitHeapBeginEvent(superEvent);
					break;
				case LogEventId.HeapEnd:
					VisitHeapEndEvent(superEvent);
					break;
				case LogEventId.HeapObject:
					VisitHeapObjectEvent(superEvent);
					break;
				case LogEventId.HeapRoots:
					VisitHeapRootsEvent(superEvent);
					break;
				case LogEventId.HeapRootRegister:
					VisitHeapRootRegisterEvent(superEvent);
					break;
				case LogEventId.HeapRootUnregister:
					VisitHeapRootUnregisterEvent(superEvent);
					break;
				case LogEventId.GC:
					VisitGCEvent(superEvent);
					break;
				case LogEventId.GCResize:
					VisitGCResizeEvent(superEvent);
					break;
				case LogEventId.GCMove:
					VisitGCMoveEvent(superEvent);
					break;
				case LogEventId.GCHandleCreation:
					VisitGCHandleCreationEvent(superEvent);
					break;
				case LogEventId.GCHandleDeletion:
					VisitGCHandleDeletionEvent(superEvent);
					break;
				case LogEventId.GCFinalizeBegin:
					VisitGCFinalizeBeginEvent(superEvent);
					break;
				case LogEventId.GCFinalizeEnd:
					VisitGCFinalizeEndEvent(superEvent);
					break;
				case LogEventId.GCFinalizeObjectBegin:
					VisitGCFinalizeObjectBeginEvent(superEvent);
					break;
				case LogEventId.GCFinalizeObjectEnd:
					VisitGCFinalizeObjectEndEvent(superEvent);
					break;
				case LogEventId.Throw:
					VisitThrowEvent(superEvent);
					break;
				case LogEventId.ExceptionClause:
					VisitExceptionClauseEvent(superEvent);
					break;
				case LogEventId.Enter:
					VisitEnterEvent(superEvent);
					break;
				case LogEventId.Leave:
					VisitLeaveEvent(superEvent);
					break;
				case LogEventId.ExceptionalLeave:
					VisitExceptionalLeaveEvent(superEvent);
					break;
				case LogEventId.Monitor:
					VisitMonitorEvent(superEvent);
					break;
				case LogEventId.SampleHit:
					VisitSampleHitEvent(superEvent);
					break;
				case LogEventId.CounterSamples:
					VisitCounterSamplesEvent(superEvent);
					break;
				case LogEventId.CounterDescriptions:
					VisitCounterDescriptionsEvent(superEvent);
					break;
				case LogEventId.UnmanagedBinary:
					VisitUnmanagedBinaryEvent(superEvent);
					break;
				case LogEventId.UnmanagedSymbol:
					VisitUnmanagedSymbolEvent(superEvent);
					break;
				case LogEventId.SynchronizationPoint:
					VisitSynchronizationPointEvent(superEvent);
					break;
				case LogEventId.MetaAotId:
					VisitMetaAotId(superEvent);
					break;
				default:
					throw new NotImplementedException((superEvent.TimestampAndType & 0xff).ToString());
			}
		}
	}
}
