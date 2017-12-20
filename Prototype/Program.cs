using System;
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
			Console.WriteLine("START: " + DateTime.Now);
			var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Desktop/MonoDevelop.exe_2017-12-18__10-43-59.mlpd");
			session.NewHeapshot += (s, e) => {
				var hs = e;

				Console.WriteLine("Hs:" + e.Name + DateTime.Now);
				Console.WriteLine("Objects Count:" + e.ObjectsInfoMap.Count);
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
			Console.WriteLine("DONE: "+DateTime.Now);
			Thread.Sleep(1000000);
		}
	}
}
