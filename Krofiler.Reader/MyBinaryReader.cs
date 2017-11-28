using System;
using System.IO;
using System.Text;

namespace Krofiler.Reader
{
	[Flags]
	public enum CaptureFlags
	{
		None = 0,
		Allocs = 1,
		Moves = 2,
		AllocsStackTrace = 4,
		RootEvents = 8,
		RootEventsStackTrace = 16
	}

	class MyBinaryReader : BinaryReader
	{
		public readonly byte pointerSize;
		public readonly CaptureFlags Flags;
		public MyBinaryReader(Stream input) : base(input, Encoding.UTF8)
		{
			var magicString = ReadString();
			if (magicString != "Krofiler")
				throw new InvalidDataException("Invalid file format");
			var version = ReadUInt16();
			if (version != 1)
				throw new InvalidDataException($"Invalid file version {version}");
			Flags = (CaptureFlags)ReadInt32();
			pointerSize = (byte)input.ReadByte();
			if (pointerSize != 4 && pointerSize != 8)
				throw new InvalidDataException($"Pointer Size:{pointerSize}");
		}

		public long ReadPointer()
		{
			if (pointerSize == 4)
				return ReadInt32();
			else
				return ReadInt64();
		}
	}
}
