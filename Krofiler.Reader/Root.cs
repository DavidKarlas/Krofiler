using System.IO;

namespace Krofiler.Reader
{
	public class Root
	{
		//public readonly long Address;
		//public readonly int RootType;
		//public readonly long ExtraInfo;
		//internal Root(MyBinaryReader reader)
		//{
		//	Address = reader.ReadPointer();
		//	RootType = reader.ReadInt32();
		//	ExtraInfo = reader.ReadPointer();
		//}
		public readonly long[] Objects;
		public readonly long[] Addresses;
		internal Root(MyBinaryReader reader)
		{
			Objects = new long[reader.ReadInt32()];
			Addresses = new long[Objects.Length];
			for (int i = 0; i < Addresses.Length; i++) {
				Objects[i] = reader.ReadPointer();
				Addresses[i] = reader.ReadPointer();
			}
		}
	}
}