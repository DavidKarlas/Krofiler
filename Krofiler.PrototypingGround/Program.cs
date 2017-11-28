using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using QuickGraph;
using QuickGraph.Algorithms.Search;
using QuickGraph.Algorithms.Observers;
using Krofiler.Reader;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Krofiler.PrototypingGround
{

	class MainClass
	{
		public static string GetReferenceFieldName(Heapshot hs, Dictionary<ushort, ClassInfo> classes, long source, long target)
		{
			var s = hs[source];
			var t = hs[target];
			for (int i = 0; i < s.Refs.Length; i++) {
				if (s.Refs[i] == target) {
					var klass = classes[s.ClassId];
					var field = klass.Fields.Single(f => f.Offset == s.Offsets[i]);
					return field.Name;
				}
			}
			return "<unknown field>";
		}
		class A { }
		static A budala;

		static void GenerateGarbage()
		{
			budala = new A();
			foreach (var a in typeof(object).Assembly.GetTypes()) {
				a.ToString();
			}
			foreach (var a in typeof(object).Assembly.GetTypes()) {
				a.ToString();
			}
			foreach (var a in typeof(object).Assembly.GetTypes()) {
				a.ToString();
			}
		}

		static void CaptureMyself()
		{
			GenerateGarbage();
			//Console.WriteLine("PID:" + Process.GetCurrentProcess().Id);
			//var la = Path.Combine("/Users/davidkarlas/Desktop/", new Guid().ToString("N") + ".krof");
			//Krofiler.Capturer.Heapshot.Start(la, Capturer.CaptureFlags.None);
			Krofiler.Capturer.Heapshot.TakeHeapshot();
			Krofiler.Capturer.Heapshot.Stop();
		}

		public static void Main(string[] args)
		{
			CaptureMyself();
			ProcessKrof();
		}

		static void ProcessKrof()
		{
			var stopwatch = Stopwatch.StartNew();
			//var newestFile = Directory.GetFiles("/Users/davidkarlas/Desktop/", "*.krof").Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTime).First().FullName;
			var newestFile = "lol.krof";
			var session = KrofilerSession.CreateFromFile(newestFile);
			session.NewHeapshot += (sender, hs) => {
				//reader = new Reader("/Users/davidkarlas/GIT/mono-heapdump/heap-graph.dot");
				object obj;
				var types = hs.Types;
				Debug.WriteLine("Reading:" + stopwatch.Elapsed.ToString("G"));
				foreach (var o in hs.Values) {
					foreach (var r in o.Refs)
						hs[r].RefsIn.Add(o.Address);
				}
				//foreach (var type in hs.Types.Values) {
				//	foreach (var field in type.Fields) {
				//		if (field.PointingTo == 0x117E10E30) {
				//			Console.WriteLine(type.Name + " " + field.Name);
				//		}
				//	}
				//}
				Debug.WriteLine("Filling:" + stopwatch.Elapsed.ToString("G"));
				//var obj11 = hs.Values.First(o => types[o.ClassId].Name == "Krofiler.MainClass.A");
				//possible optimisation to find only shortest path, worthiness is questionable
				//bfsa.ExamineVertex+= (vertex) => {
				//	if (graph.Roots.Contains(vertex))
				//		bfsa.Services.CancelManager.Cancel();
				//};
				var heapTracker = new HeapTracker();
				var aliveReg = new Dictionary<long, RootRegister>();
				foreach (var item in sender.AllRootRegAndUnReg) {
					if (item is RootRegister rr) {
						Console.WriteLine($"{rr.Start}-{rr.Size}: {rr.Kind} {rr.Message}");
						foreach (var line in EnhanceStack(sender, rr.Stack, rr.StackAddress)) {
							Console.WriteLine(line);
						}
						//aliveReg.Add(rr.Start, rr);
						heapTracker.AddArea(rr);
					} else if (item is RootUnregister ru) {
						if (!aliveReg.Remove(ru.Start)) {
							Console.WriteLine("ha");
						}
						heapTracker.RemoveArea(ru.Start);
					}
				}
				var allAllocs = new Dictionary<long, string[]>();
				for (int t = 0; t < sender.AllAllocsAndMoves.Count; t++) {
					if (sender.AllAllocsAndMoves[t] is HeapMoves hm) {
						for (int g = 0; g < hm.Moves.Length; g += 2) {
							allAllocs[hm.Moves[g + 1]] = allAllocs[hm.Moves[g]];
							allAllocs.Remove(hm.Moves[g]);
						}
					} else if (sender.AllAllocsAndMoves[t] is HeapAlloc ha) {
						allAllocs[ha.Address] = EnhanceStack(sender, ha.AllocStack, ha.AllocStackAddresses);
					}
				}
				int counter = 0;
				int missesCount = 0;
				var found1 = 0;
				var notfound = 0;
				var dic = new Dictionary<string, int>();
				foreach (var r in hs.Roots) {
					var rr = heapTracker.GetRoot(r.Value.Object);
					if (rr == null)
						continue;
					if (dic.ContainsKey(rr.Message))
						dic[rr.Message]++;
					else
						dic[rr.Message] = 0;
					var ob = allAllocs[r.Key];
					if (ob == null)
						continue;
				}
				foreach (var d in dic) {
					Console.WriteLine(d.Key + " " + d.Value);
				}
				Console.WriteLine();
				//foreach (var obj1 in hs.Values) {
				//	if (hs.Roots.TryGetValue(obj1.Address, out var info)) {
				//		obj1.Root = heapTracker.GetRoot(info.Object);
				//		if (obj1.Root != null) {
				//			var rr = obj1.Root;
				//			Console.WriteLine($"{rr.Start}-{rr.Size}: {rr.Kind} {rr.Message}");
				//			Console.WriteLine(types[obj1.ClassId].Name);
				//			if (rr.Kind == LogHeapRootSource.StaticVariable) {
				//				Console.WriteLine(types.FirstOrDefault(t => t.Value.Address == rr.Key).Value?.Name ?? "");
				//			}
				//			Console.WriteLine(counter++);
				//		} else {
				//			missesCount++;
				//		}
				//	}
				//}
				Console.WriteLine("Misses count:" + missesCount);
				int vc = 0;
				foreach (var val in hs.Values) {
					vc++;
					if (vc % 1000 == 0)
						Debug.WriteLine(vc);
					if (hs.Roots.ContainsKey(val.Address))
						continue;
					var vis = new VertexPredecessorRecorderObserver<long, ReferenceEdge>();
					var bfsa = new BreadthFirstSearchAlgorithm<long, ReferenceEdge>(hs);
					vis.Attach(bfsa);
					bfsa.Compute(val.Address);
					bool found = false;
					foreach (var root in hs.Roots) {
						if (vis.TryGetPath(root.Key, out var p)) {
							found = true;
							break;
						}
					}
					if (!found)
						Console.WriteLine("not found");
				}

				//bfsa.Compute(obj11.Address);
				//Debug.WriteLine("Compute:" + stopwatch.Elapsed.ToString("G"));
				//List<Tuple<long, List<ReferenceEdge>>> distances = new List<Tuple<long, List<ReferenceEdge>>>();
				//foreach (var root in hs.Roots) {
				//	IEnumerable<ReferenceEdge> p;
				//	if (vis.TryGetPath(root.Key, out p))
				//		distances.Add(new Tuple<long, List<ReferenceEdge>>(p.Count(), p.ToList()));
				//}

				//foreach (var item in types) {
				//	foreach (var field in item.Value.Fields) {
				//		if (field.PointingTo == obj11.Address)
				//			Debug.WriteLine("Win");
				//	}
				//}

				//if (hs.Roots.Keys.Contains(obj11.Address)) {
				//	Debug.WriteLine("This thing is root");
				//} else {
				//	Debug.WriteLine("Search:" + stopwatch.Elapsed.ToString("G"));
				//	var shortest = distances.OrderBy(t => t.Item1).First();
				//	Debug.WriteLine($"Distance between {obj11.Address:X} and {shortest.Item2.Last().Target:X} is:" + shortest.Item1);
				//	foreach (var e in shortest.Item2) {
				//		Debug.WriteLine(hs[e.Target].Address + " " + types[hs[e.Target].ClassId].Name + " " + GetReferenceFieldName(hs, types, e.Target, e.Source));
				//	}
				//}
				HeapObject[] list;
#if MARKED
			foreach (var ob in objs.Values)
				foreach (var re in ob.Refs)
					objs[re].Marked = true;
			foreach (var ro in roots)
				objs[ro].Marked = true;

			list = objs.Values.Where(a => !a.Marked).ToArray();
#else
				list = hs.Values.ToArray();
#endif
				var result = new List<HeapObject>();
				Debug.WriteLine("Total size:" + list.Sum(s => s.Size));
				var groupedByClassName = list.GroupBy(o => types[o.ClassId]);
				foreach (var gr in groupedByClassName.OrderBy(gr => gr.Sum(o => o.Size)))
					Debug.WriteLine(gr.Key.Name + " " + gr.Sum(o => o.Size));
				//foreach (var p in groupedByClassName.First(ga => ga.Key == "AppKit.NSMenuItem"))
				//{
				//	Debug.WriteLine(p.ResolvedRefs[0].StringValue);
				//}
			};
			var manualReset = new ManualResetEvent(false);
			session.Finished += (obj) => {
				manualReset.Set();
			};
			session.StartParsing();
			manualReset.WaitOne();
		}

		private static string[] EnhanceStack(KrofilerSession sender, string[] stack, long[] addrs)
		{
			var result = new string[stack.Length];
			for (int i = 0; i < result.Length; i++) {
				if (stack[i].Contains("???") || stack[i] == "")
					result[i] = sender.AllMethods.FirstOrDefault(m => m.Start <= addrs[i] && m.Start + m.Size >= addrs[i])?.Name ?? stack[i];
				else
					result[i] = stack[i];
			}
			return result;
		}
	}
}
