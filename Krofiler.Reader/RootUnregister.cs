using System;
namespace Krofiler.Reader
{
	public class RootUnregister
	{
		internal RootUnregister(MyBinaryReader reader)
		{
			Start = reader.ReadPointer();
		}

		public long Start {
			get;
			set;
		}
	}
}
