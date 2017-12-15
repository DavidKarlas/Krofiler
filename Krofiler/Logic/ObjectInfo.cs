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
		internal ulong Allocation;

		public long[] Backtrace {
			get {
				throw new NotImplementedException();
			}
		}

		public long AllocationTimestamp {
			get {
				throw new NotImplementedException();
			}
		}
	}

	public class Allocation
	{
		public long Time;
		public long[] Backtrace;
	}
}

