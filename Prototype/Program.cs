using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Krofiler;
using SQLitePCL;

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
			long heapMem = 0;
			session.GCResize += (t, s) => {
				heapMem = s;
			};
			long totalMemory = 0;
			long swapMemory = 0;
			session.CountersDescriptionsAdded += (desc) => {
				Console.WriteLine("Desc:" + desc.GetCounterName(session.processor) + " " + desc.CounterDescriptionsEvent_Index);
			};
			session.CounterSamplesAdded+= (sample) => {
				if (sample.CounterSamplesEvent_Index == 7)
					swapMemory += sample.CounterSamplesEvent_Value_Long;
				if (sample.CounterSamplesEvent_Index != 6)
					return;
				totalMemory += sample.CounterSamplesEvent_Value_Long;
				Console.WriteLine($"GCResize:" + sample.Time + " " + PrettyPrint.PrintBytes(heapMem));
				Console.WriteLine("Virtual memory:" + PrettyPrint.PrintBytes(totalMemory));
				Console.WriteLine("Swap memory:" + PrettyPrint.PrintBytes(totalMemory));
			};
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
			var refsDb = hs.GetRefsDb();
			var objsDb = hs.GetObjsDb();
			var marked = new HashSet<long>(roots);
			DbUtils.check_ok(refsDb, raw.sqlite3_exec(refsDb, @"CREATE INDEX RefsAddressFrom ON Refs (AddressFrom);"));
			DbUtils.check_ok(refsDb, raw.sqlite3_prepare_v2(refsDb, "SELECT AddressTo FROM Refs WHERE AddressFrom=?", out var refsToStmt));

			void MarkAllReferences(long objAddr)
			{
				var list = new List<long>();
				DbUtils.check_ok(refsDb, raw.sqlite3_bind_int64(refsToStmt, 1, objAddr));
				while (raw.sqlite3_step(refsToStmt) == raw.SQLITE_ROW) {
					list.Add(raw.sqlite3_column_int64(refsToStmt, 0));
				}
				DbUtils.check_ok(refsDb, raw.sqlite3_reset(refsToStmt));
				foreach (var o in list) {
					if (!marked.Add(o))
						continue;
					MarkAllReferences(o);
				}
			}
			foreach (var root in roots) {
				MarkAllReferences(root);
			}
			DbUtils.check_ok(refsDb, raw.sqlite3_finalize(refsToStmt));

			DbUtils.check_ok(objsDb, raw.sqlite3_prepare_v2(objsDb, "SELECT Address FROM Objs", out var stmt));
			int res;
			List<long> unmarkedAddresses = new List<long>();
			while ((res = raw.sqlite3_step(stmt)) == raw.SQLITE_ROW) {
				long objAddr = raw.sqlite3_column_int64(stmt, 0);
				if (!marked.Contains(objAddr))
					unmarkedAddresses.Add(objAddr);
			}
			if (res != raw.SQLITE_DONE)
				DbUtils.check_ok(objsDb, res);
			DbUtils.check_ok(objsDb, raw.sqlite3_finalize(stmt));
			long totalSize = 0;
			long totalCount = 0;
			foreach (var typesGroup in unmarkedAddresses.Select(ua => hs.GetObjectInfo(ua)).GroupBy(o => o.TypeId).OrderByDescending(o => o.Sum(ob => ob.Size))) {
				Console.WriteLine($"{session.GetTypeName(typesGroup.Key)}: {typesGroup.Count()} {PrettyPrint.PrintBytes(typesGroup.Sum(t => t.Size))}");
				totalSize += typesGroup.Sum(t => t.Size);
				totalCount += typesGroup.Count();
			}
			Console.WriteLine("DONE: " + totalCount + " " + PrettyPrint.PrintBytes(totalSize));
			Console.WriteLine("Gc usage:" + GC.GetTotalMemory(true) / (1024.0 * 1024));
		}
	}
}
