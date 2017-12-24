using System;
namespace Krofiler
{
	public static class PrettyPrint
	{
		public static string PrintBytes(long size)
		{
			if (size < 1024)
				return size + "B";
			else if (size < (1024 * 1024))
				return (size / 1024.0).ToString("0.0KB");
			else if (size < (1024 * 1024 * 1024))
				return (size / (1024.0 * 1024)).ToString("0.0MB");
			return (size / (1024.0 * 1024 * 1024)).ToString("0.0GB");
		
	}
}
}
