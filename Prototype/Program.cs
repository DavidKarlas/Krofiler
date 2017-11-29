using System;
using System.Linq;
using Krofiler;

namespace Prototype
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Desktop/fd1r0lbc.fsu.mlpd");
			session.NewHeapshot += (s, e) => {
				var hs = e;
				hs.BuildReferencesFrom();
				Console.WriteLine (new string ('=', 30));
				Console.WriteLine ("Hs:" + e.Name);
				foreach (var obj in hs.ObjectsInfoMap.Values) {
					if(obj.ReferencesFrom.Count==0 && !hs.Roots.ContainsKey(obj.ObjAddr)){
						Console.WriteLine($"{obj.ObjAddr} type:{s.GetTypeName(obj.TypeId)}");
					}
				}
				Console.WriteLine (new string ('=', 30));
			};
			session.StartParsing().Wait();
		}
	}
}
