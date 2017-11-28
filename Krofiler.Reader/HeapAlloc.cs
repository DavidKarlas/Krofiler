using System;
namespace Krofiler.Reader
{
	public class HeapAlloc
	{
		public long Address;
		public string[] AllocStack;
		public long[] AllocStackAddresses;
	}
}
