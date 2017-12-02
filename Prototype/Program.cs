using System;
using System.Diagnostics;
using System.Linq;
using Krofiler;

namespace Prototype
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			var sw = Stopwatch.StartNew();
			var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Desktop/MonoDevelop.exe_2017-12-02__21-58-52.mlpd");
			session.NewHeapshot += (s, e) => {
				Console.WriteLine("GAGA:" + sw.Elapsed);
				var hs = e;

				hs.BuildReferencesFrom();
				//Console.WriteLine (new string ('=', 30));
				Console.WriteLine ("Hs:" + e.Name);
				Console.WriteLine("Objects Count:" + e.ObjectsInfoMap.Count);
				int count = 0;
				foreach (var obj in hs.ObjectsInfoMap.Values) {
					if(obj.ReferencesFrom.Count==0 && !hs.Roots.ContainsKey(obj.ObjAddr)){
						 count++;
						//Console.WriteLine($"{obj.ObjAddr} type:{s.GetTypeName(obj.TypeId)}");
						//foreach (var b in obj.Allocation.Backtrace) {
						//	Console.WriteLine("   " + session.GetMethodName(b));
						//}
					}
				}
				Console.WriteLine("Orphans count:" + count);
				//Console.WriteLine(new string('=', 30));
				//Console.WriteLine($"HEAPSHOT {e.Name}");
				//Console.WriteLine(new string('=', 30));
			};
			//session.NewCounters += (s, e) => {

			//};
			session.StartParsing().Wait();
		}
	}
}
