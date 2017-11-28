using System;
using System.Collections.Generic;
using Krofiler.Reader;
namespace Krofiler.PrototypingGround
{
	public class HeapTracker
	{
		List<RootRegister> heapState = new List<RootRegister>();
		public void AddArea(RootRegister rr)
		{
			heapState.Add(rr);
		}

		public void RemoveArea(long start)
		{

		}

		public bool IsAlive(long address)
		{
			foreach (var area in heapState)
				if (area.Start <= address && area.Start + area.Size >= address)
					return true;
			return false;
		}

		public RootRegister GetRoot(long address)
		{
			foreach (var rr in heapState)
				if (rr.Start <= address && rr.Start + rr.Size >= address)
					return rr;
			return null;
		}
	}
}
