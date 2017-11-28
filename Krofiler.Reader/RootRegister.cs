using System;
using System.Collections.Generic;

namespace Krofiler.Reader
{
	public enum LogHeapRootSource : byte
	{
		External = 0,
		Stack = 1,
		FinalizerQueue = 2,
		StaticVariable = 3,
		ThreadLocalVariable = 4,
		ContextLocalVariable = 5,
		GCHandle = 6,
		JIT = 7,
		Threading = 8,
		AppDomain = 9,
		Reflection = 10,
		Marshal = 11,
		ThreadPool = 12,
		Debugger = 13,
		RuntimeHandle = 14,
	}

	public class RootRegister
	{
		public long Start { get; set; }
		public int Size { get; set; }
		public LogHeapRootSource Kind { get; set; }
		public long Key { get; set; }
		public string Message { get; set; }
		public string[] Stack;
		public long[] StackAddress;

		internal RootRegister(MyBinaryReader reader)
		{
			Start = reader.ReadPointer();
			Size = reader.ReadInt32();
			Kind = (LogHeapRootSource)reader.ReadByte();
			Key = reader.ReadPointer();
			Message = reader.ReadString();
			if ((reader.Flags & CaptureFlags.RootEventsStackTrace) == CaptureFlags.RootEventsStackTrace) {
				int size = reader.ReadByte();
				Stack = new string[size];
				StackAddress = new long[size];
				for (int i = 0; i < size; i++) {
					StackAddress[i] = reader.ReadPointer();
					Stack[i] = reader.ReadString();
				}
			}
		}
	}
}
