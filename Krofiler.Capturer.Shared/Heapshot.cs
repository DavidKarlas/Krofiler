using System;
using System.Runtime.InteropServices;

namespace Krofiler.Capturer
{
	[Flags]
	public enum CaptureFlags
	{
		None = 0,
		Allocs = 1,
		Moves = 2,
		AllocsStackTrace = 4,
	}

	public static class Heapshot
	{
		[DllImport("libmono-profiler-krofiler")]
		extern static void krofiler_start(string path, int flags);
		[DllImport("libmono-profiler-krofiler")]
		extern static void krofiler_take_heapshot();
		[DllImport("libmono-profiler-krofiler")]
		extern static void krofiler_stop();

		public static void Start(string path, CaptureFlags flags)
		{
			krofiler_start(path, (int)flags);
		}

		public static void TakeHeapshot()
		{
			krofiler_take_heapshot();
		}

		public static void Stop()
		{
			krofiler_stop();
		}
	}
}
