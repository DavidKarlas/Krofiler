// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Mono.Profiler.Log {

	public abstract class LogEventVisitor {
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
	}
}
