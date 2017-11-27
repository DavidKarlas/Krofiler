using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Profiler.Log;

namespace Krofiler
{
	public class ObjectInfo
	{
		public long ObjAddr;
		public long TypeId;
		public long[] ReferencesTo;
		public ushort[] ReferencesAt;
		public List<long> ReferencesFrom = new List<long>();
		internal AllocationEvent Allocation;
	}
}

