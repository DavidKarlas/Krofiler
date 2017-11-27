using System;
using Krofiler;

namespace Prototype
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			var session = KrofilerSession.CreateFromFile("/Users/davidkarlas/Documents/3SnapshotsWithOpeningAndCLosingProject.mlpd");
			session.NewHeapshot += (s, e) => {
				Console.WriteLine(e);
			};
			session.StartParsing().Wait();
		}
	}
}
