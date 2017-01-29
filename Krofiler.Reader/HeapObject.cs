using System.IO;
using System.Collections.Generic;

namespace Krofiler.Reader
{
	public class HeapObject
	{
		public readonly long Address;
		public readonly ushort ClassId;
		public readonly ushort Size;
		public long[] Refs;
		public List<long> RefsIn = new List<long>();
		public ushort[] Offsets;
		public readonly string StringValue;
		public string[] allocStack;
		public long AllocAddress;

		internal HeapObject(MyBinaryReader reader)
		{
			Address = reader.ReadPointer();
			ClassId = reader.ReadUInt16();
			Size = reader.ReadUInt16();
			Refs = new long[reader.ReadByte()];
			Offsets = new ushort[Refs.Length];
			for (int i = 0; i < Refs.Length; i++)
			{
				Refs[i] = reader.ReadPointer();
				Offsets[i] = reader.ReadUInt16();
			}
			if (ClassId == 2)
				StringValue = reader.ReadString();
		}

		public HeapObject(long addr, string[] allocStack)
		{
			this.Address = addr;
			this.allocStack = allocStack;
		}
	}
}