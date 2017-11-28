using System;
using System.Linq;
using Krofiler;

namespace Prototype
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Documents/3SnapshotsWithOpeningAndCLosingProject.mlpd");
			session.NewHeapshot += (s, e) => {
				var hs = e;
				hs.GetShortestPathToRoot(hs.ObjectsInfoMap.Keys.First());
				foreach (var obj in hs.ObjectsInfoMap.Values) {
					if(obj.ReferencesFrom.Count==0 && !hs.Roots.ContainsKey(obj.ObjAddr)){
						Console.WriteLine($"{obj.ObjAddr} type:{s.GetTypeName(obj.TypeId)}");
					}
				}
			};
			session.StartParsing().Wait();
		}
	}
}
