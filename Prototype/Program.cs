using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Krofiler;

namespace Prototype
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			var timer = new Timer(delegate {
				Console.WriteLine("Sql usage:" + SQLitePCL.raw.sqlite3_memory_used() / (1024.0 * 1024));
				Console.WriteLine("Gc usage:" + GC.GetTotalMemory(false) / (1024.0 * 1024));
			}, null, 60000, 60000);
			Console.WriteLine("START: " + DateTime.Now);
			var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Desktop/MonoDevelop.exe_2017-12-21__10-35-14.mlpd");
			//var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Desktop/garbageGenerator.exe_2017-12-22__05-34-25.mlpd");
			session.NewHeapshot += (s, e) => {
				Console.WriteLine("Hs:" + e.Name + DateTime.Now);
				//Console.WriteLine("Objects Count:" + e.ObjectsInfoMap.Count);
				//e.GetTop5PathsToRoots(4600206520);
				//int count = 0;
				//foreach (var obj in hs.ObjectsInfoMap.Values) {
				//	if (hs.GetReferencedFrom(obj.ObjAddr).Count == 0 && !hs.Roots.ContainsKey(obj.ObjAddr)) {
				//		count++;
				//		//Console.WriteLine($"{obj.ObjAddr} type:{s.GetTypeName(obj.TypeId)}");
				//		//foreach (var b in obj.Allocation.Backtrace) {
				//		//	Console.WriteLine("   " + session.GetMethodName(b));
				//		//}
				//	}
				//}
				//Console.WriteLine("Orphans count:" + count + DateTime.Now);
				//Console.WriteLine(new string('=', 30));
				//Console.WriteLine($"HEAPSHOT {e.Name}");
				//Console.WriteLine(new string('=', 30));
			};
			//session.NewCounters += (s, e) => {

			//};
			session.StartParsing().Wait();
			var hs = session.Heapshots[1];
			var roots = new HashSet<long>(hs.Roots.Where(p =>
									   p.Value.HeapRootRegisterEvent_Source != Mono.Profiler.Log.LogHeapRootSource.Ephemeron &&
														 p.Value.HeapRootRegisterEvent_Source != Mono.Profiler.Log.LogHeapRootSource.FinalizerQueue).Select(s => s.Key));
			var db = hs.GetObjDb();

			Console.WriteLine("DONE: " + DateTime.Now);
			Console.WriteLine("Gc usage:" + GC.GetTotalMemory(true) / (1024.0 * 1024));
		}
	}
}
