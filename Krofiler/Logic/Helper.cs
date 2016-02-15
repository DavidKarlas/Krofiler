using System;
using MonoDevelop.Profiler;

namespace Krofiler
{
	public static class Helper
	{
		public static ulong Time(Event ev)
		{
			return ev.Time;
		}
	}
}

