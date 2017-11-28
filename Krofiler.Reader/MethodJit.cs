using System;
namespace Krofiler.Reader
{
	public class MethodJit
	{
		public string Name { get; set; }
		public long Start { get; set; }
		public int Size { get; set; }

		internal MethodJit(MyBinaryReader reader)
		{
			Name = reader.ReadString();
			Start = reader.ReadPointer();
			Size = reader.ReadInt32();
		}
	}
}
