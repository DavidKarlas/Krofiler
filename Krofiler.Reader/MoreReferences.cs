namespace Krofiler.Reader
{
	public class MoreReferences
	{
		public readonly long Address;
		public readonly long[] Refs;
		public readonly ushort[] Offsets;
		internal MoreReferences(MyBinaryReader reader)
		{
			Address = reader.ReadPointer();
			Refs = new long[reader.ReadByte()];
			Offsets = new ushort[Refs.Length];
			for (int i = 0; i < Refs.Length; i++)
			{
				Refs[i] = reader.ReadPointer();
				Offsets[i] = reader.ReadUInt16();
			}
		}
	}
}