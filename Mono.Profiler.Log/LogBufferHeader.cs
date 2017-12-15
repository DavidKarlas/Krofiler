// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Mono.Profiler.Log {

	public unsafe sealed class LogBufferHeader {

		const int Id = 0x4d504c01;

		public LogStreamHeader StreamHeader { get; }

		public int Length { get; }

		public ulong TimeBase { get; }

		public long PointerBase { get; }

		public long ObjectBase { get; }

		public long ThreadId { get; }

		public long MethodBase { get; }

		internal ulong CurrentTime;

		internal long CurrentMethod { get; set; }

		readonly byte* bufferStart;
		readonly ulong streamPosition;

		unsafe internal LogBufferHeader (LogStreamHeader streamHeader, LogReader reader, ulong position, byte* bufferStart)
		{
			this.streamPosition = position;
			this.bufferStart = bufferStart;
			StreamHeader = streamHeader;

			var id = reader.ReadInt32 ();

			if (id != Id)
				throw new LogException ($"Invalid buffer header ID (0x{id:X}).");

			Length = reader.ReadInt32 ();
			TimeBase = CurrentTime = reader.ReadUInt64 ();
			PointerBase = reader.ReadInt64 ();
			ObjectBase = reader.ReadInt64 ();
			ThreadId = reader.ReadInt64 ();
			MethodBase = CurrentMethod = reader.ReadInt64 ();
		}

		internal unsafe ulong GetFileOffset(byte* pos)
		{
			return streamPosition + (ulong)(pos - bufferStart);
		}
	}
}
