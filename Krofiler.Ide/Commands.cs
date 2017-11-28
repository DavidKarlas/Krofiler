using System;
using System.Runtime.CompilerServices;
using Krofiler.Capturer;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;

namespace Krofiler.Ide
{
	public class TakeHeapshot : CommandHandler
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Start()
		{
			Heapshot.Start(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Desktop", BrandingService.ApplicationName + "_Heapshot_" + DateTime.Now.ToString("yyyy-MM-dd__HH-mm-ss") + ".krof"),
						   CaptureFlags.Allocs | CaptureFlags.Moves);
		}
		static bool started = false;
		protected override void Run()
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			//Place Start into it's own method to avoid having path string on heap(held by stack)
			if (!started) {
				Start();
				started = true;
			}
			Heapshot.TakeHeapshot();
			//Heapshot.Stop();
		}

	}
}
