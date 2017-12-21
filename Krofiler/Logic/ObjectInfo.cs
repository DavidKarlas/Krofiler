using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Profiler.Log;

namespace Krofiler
{
	public struct ObjectInfo
	{
		public long ObjAddr;
		public long TypeId;
		public ulong Allocation;

		public long[] Backtrace(KrofilerSession session)
		{
			using (var stream = session.GetFileStream(Allocation)) {
				var bytes = new byte[8];
				stream.Position += 8;
				var depth = (byte)stream.ReadByte();
				if (depth == 0)
					return Array.Empty<long>();
				var buffer = new byte[8];
				var result = new long[depth];
				for (int i = 0; i < depth; i++) {
					stream.Read(buffer, 0, 8);
					result[i] = BitConverter.ToInt64(buffer, 0);
				}
				return result;
			}
		}

		public ulong AllocationTimestamp(KrofilerSession session)
		{
			using (var stream = session.GetFileStream(Allocation)) {
				var bytes = new byte[8];
				stream.Read(bytes, 0, 8);
				return BitConverter.ToUInt64(bytes, 0);
			}
		}
	}

	public class Allocation
	{
		public long Time;
		public long[] Backtrace;
	}
}

