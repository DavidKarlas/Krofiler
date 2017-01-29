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
		public readonly long[] Addresses;
		internal Root(MyBinaryReader reader)
		{
			Addresses = new long[reader.ReadByte()];
			for (int i = 0; i < Addresses.Length; i++)
				Addresses[i] = reader.ReadPointer();
		}
	}
}