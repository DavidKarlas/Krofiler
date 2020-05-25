using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mono.Profiler.Log;
using SQLitePCL;

namespace Krofiler
{
	public class Heapshot
	{
		public Heapshot(KrofilerSession session, int id, TimeSpan time)
		{
			Id = id;
			Time = time;
			Session = session;
			var file = Path.Combine(Session.processor.cacheFolder, $"Heapshot_{Id}.db");
			var rc = raw.sqlite3_open_v2($"file:{file}", out objsDb, raw.SQLITE_OPEN_CREATE | raw.SQLITE_OPEN_URI | raw.SQLITE_OPEN_READWRITE, null);
			if (rc != raw.SQLITE_OK)
				throw new Exception(raw.sqlite3_errstr(rc).utf8_to_string());
			check_ok(objsDb, raw.sqlite3_exec(objsDb, "PRAGMA synchronous=OFF"));
			check_ok(objsDb, raw.sqlite3_exec(objsDb, "PRAGMA count_changes=OFF"));
			check_ok(objsDb, raw.sqlite3_exec(objsDb, "PRAGMA journal_mode=OFF"));
			check_ok(objsDb, raw.sqlite3_exec(objsDb, "PRAGMA temp_store=MEMORY"));
			check_ok(objsDb, raw.sqlite3_exec(objsDb, @"CREATE TABLE Objs
			(
				Address INT NOT NULL,
				TypeId INT NOT NULL,
				Allocation INT NOT NULL,
				Size INT NOT NULL
			)"));
			check_ok(objsDb, raw.sqlite3_exec(objsDb, "BEGIN TRANSACTION;"));
			check_ok(objsDb, raw.sqlite3_prepare_v2(objsDb, "INSERT INTO Objs(Address, TypeId, Allocation, Size) VALUES(?,?,?,?)", out objsInsertStmt));
			check_ok(objsDb, raw.sqlite3_prepare_v2(objsDb, "SELECT Allocation, TypeId, Size FROM Objs WHERE Address=?", out objSelectObjInfo));
		}

		internal void Insert(long addr, long typeId, ulong alloc, long size)
		{
			check_ok(objsDb, raw.sqlite3_bind_int64(objsInsertStmt, 1, addr));
			check_ok(objsDb, raw.sqlite3_bind_int64(objsInsertStmt, 2, typeId));
			check_ok(objsDb, raw.sqlite3_bind_int64(objsInsertStmt, 3, (long)alloc));
			check_ok(objsDb, raw.sqlite3_bind_int64(objsInsertStmt, 4, size));
			var result = raw.sqlite3_step(objsInsertStmt);
			if (raw.SQLITE_DONE != result)
				check_ok(objsDb, result);
			check_ok(objsDb, raw.sqlite3_reset(objsInsertStmt));
		}

		Task indexingObjs;
		Task indexingRefs;

		internal void FinishProcessing()
		{
			check_ok(objsDb, raw.sqlite3_exec(objsDb, "COMMIT TRANSACTION;"));
			var rc = raw.sqlite3_open_v2($"file:{Path.Combine(Session.processor.cacheFolder, $"HeapshotRefs_{Id}.db")}", out refsDb, raw.SQLITE_OPEN_URI | raw.SQLITE_OPEN_READWRITE, null);
			if (rc != raw.SQLITE_OK)
				throw new Exception(raw.sqlite3_errstr(rc).utf8_to_string());
			//raw.sqlite3_progress_handler(refsDb,)
			indexingObjs = Task.Run(() => {
				check_ok(objsDb, raw.sqlite3_exec(objsDb, @"CREATE INDEX ObjsAddress ON Objs (Address)"));
			});
			indexingRefs = Task.Run(() => {
				check_ok(refsDb, raw.sqlite3_exec(refsDb, @"CREATE INDEX RefsAddressTo ON Refs (AddressTo);"));
			});
			check_ok(refsDb, raw.sqlite3_prepare_v2(refsDb, "SELECT AddressFrom FROM Refs WHERE AddressTo=?", out refsFromStmt));
		}

		public ObjectInfo GetObjectInfo(long objAdr)
		{
			indexingObjs.Wait();
			check_ok(objsDb, raw.sqlite3_bind_int64(objSelectObjInfo, 1, objAdr));
			var rc = raw.sqlite3_step(objSelectObjInfo);
			if (rc != raw.SQLITE_ROW) {
				if (rc == raw.SQLITE_DONE) {
					throw new KeyNotFoundException();
				} else {
					check_ok(objsDb, rc);
				}
			}
			var typeId = raw.sqlite3_column_int64(objSelectObjInfo, 1);
			var objInfo = new ObjectInfo(objAdr,
										 raw.sqlite3_column_int64(objSelectObjInfo, 0),
										 typeId,
										 raw.sqlite3_column_int64(objSelectObjInfo, 2));
			check_ok(objsDb, raw.sqlite3_reset(objSelectObjInfo));
			return objInfo;
		}

		public Dictionary<long, SuperEvent> Roots = new Dictionary<long, SuperEvent>();
		sqlite3 objsDb;
		sqlite3 refsDb;
		sqlite3_stmt refsFromStmt;
		sqlite3_stmt objsInsertStmt;
		sqlite3_stmt objSelectObjInfo;

		private static void check_ok(sqlite3 db, int rc)
		{
			if (raw.SQLITE_OK != rc)
				throw new Exception(raw.sqlite3_errstr(rc).utf8_to_string() + ": " + raw.sqlite3_errmsg(db).utf8_to_string());
		}

		Dictionary<long, long[]> refsFromCache = new Dictionary<long, long[]>();
		public long[] GetReferencedFrom(long objAddr)
		{
			if (refsFromCache.TryGetValue(objAddr, out var result))
				return result;
			indexingRefs.Wait();
			check_ok(refsDb, raw.sqlite3_bind_int64(refsFromStmt, 1, objAddr));
			var list = new List<long>();
			while (raw.sqlite3_step(refsFromStmt) == raw.SQLITE_ROW) {
				list.Add(raw.sqlite3_column_int64(refsFromStmt, 0));
			}
			check_ok(refsDb, raw.sqlite3_reset(refsFromStmt));
			result = list.ToArray();
			refsFromCache[objAddr] = result;
			return result;
		}

		public sqlite3 GetObjsDb()
		{
			indexingObjs.Wait();
			return objsDb;
		}

		public sqlite3 GetRefsDb()
		{
			indexingRefs.Wait();
			return refsDb;
		}

		public List<long> GetReferencedTo(long objAddr)
		{
			indexingRefs.Wait();
			check_ok(refsDb, raw.sqlite3_prepare_v2(refsDb, "SELECT AddressTo FROM Refs WHERE AddressFrom=?", out var stmt));
			check_ok(refsDb, raw.sqlite3_bind_int64(stmt, 1, objAddr));
			var list = new List<long>();
			while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW) {
				list.Add(raw.sqlite3_column_int64(stmt, 0));
			}
			check_ok(refsDb, raw.sqlite3_finalize(stmt));
			return list;
		}

		public string Name {
			get {
				return Id.ToString();
			}
		}

		public KrofilerSession Session { get; }
		public int Id { get; }
		public TimeSpan Time { get; }

		List<long[]> cachedResult;
		long cachedAddr;

		public List<long[]> SearchRoot(long objAddr, int count)
		{
			var queue = new Queue<long[]>();
			var visited = new HashSet<long>();
			visited.Add(objAddr);
			var result = new List<long[]>(count);
			var lessImportantRoots = new List<long[]>();
			queue.Enqueue(new long[] { objAddr });
			SuperEvent root;
			while (queue.Any()) {
				var cur = queue.Dequeue();
				var node = cur[cur.Length - 1];
				if (Roots.TryGetValue(node, out root)) {
					if (root.HeapRootRegisterEvent_Source == LogHeapRootSource.Ephemeron ||
						root.HeapRootRegisterEvent_Source == LogHeapRootSource.FinalizerQueue ||
						root.HeapRootRegisterEvent_Source == LogHeapRootSource.GCHandle)
						lessImportantRoots.Add(cur);
					else
						result.Add(cur);

					if (result.Count == count) {
						result.Sort(sortByLength);
						return result;
					}
				}
				foreach (var child in GetReferencedFrom(node)) {
					if (visited.Add(child)) {
						var newPath = new long[cur.Length + 1];
						Array.Copy(cur, 0, newPath, 0, cur.Length);
						newPath[cur.Length] = child;
						queue.Enqueue(newPath);
					}
				}
			}
			result.Sort(sortByLength);
			lessImportantRoots.Sort(sortByLength);
			foreach (var lir in lessImportantRoots) {
				if (result.Count == count)
					break;
				result.Add(lir);
			}
			return result;
		}

		static readonly Comparison<long[]> sortByLength = (x, y) => y.Length.CompareTo(x.Length);

		public List<long[]> GetTop5PathsToRoots(long addr)
		{
			if (cachedAddr == addr && cachedResult != null)
				return cachedResult;
			cachedResult = new List<long[]>();
			cachedAddr = addr;
			//if (Roots.ContainsKey(addr)) {
			//	return cachedResult;
			//}
			cachedResult = SearchRoot(addr, 10);
			return cachedResult;
		}

		class HsTypesList : LazyObjectsList
		{
			private sqlite3 db;
			private long typeId;

			public HsTypesList(sqlite3 db, int count, long size, long typeId)
				: base(count, size)
			{
				this.db = db;
				this.typeId = typeId;
			}

			public override IEnumerable<ObjectInfo> CreateList(string orderByColum = "Size", bool descending = true, int limit = 100)
			{
				var result = new List<ObjectInfo>(limit);
				sqlite3_stmt query;
				if (string.IsNullOrEmpty(orderByColum))
					DbUtils.check_ok(db, raw.sqlite3_prepare_v2(db, $"SELECT Address, Allocation, Size FROM Objs WHERE TypeId=? LIMIT {limit}", out query));
				else
					DbUtils.check_ok(db, raw.sqlite3_prepare_v2(db, $"SELECT Address, Allocation, Size FROM Objs WHERE TypeId=? ORDER BY {orderByColum} {(descending ? "DESC" : "ASC")} LIMIT {limit}", out query));
				DbUtils.check_ok(db, raw.sqlite3_bind_int64(query, 1, typeId));
				int res;
				while ((res = raw.sqlite3_step(query)) == raw.SQLITE_ROW) {
					yield return new ObjectInfo(raw.sqlite3_column_int64(query, 0),
											  raw.sqlite3_column_int64(query, 1),
											  typeId,
											  raw.sqlite3_column_int64(query, 2)
											   );
				}
				if (res != raw.SQLITE_DONE)
					DbUtils.check_ok(db, res);
				DbUtils.check_ok(db, raw.sqlite3_finalize(query));
			}
		}
		Dictionary<long, LazyObjectsList> cachedTypesToObjectsListMap;
		public Dictionary<long, LazyObjectsList> TypesToObjectsListMap {
			get {
				if (cachedTypesToObjectsListMap != null)
					return cachedTypesToObjectsListMap;
				cachedTypesToObjectsListMap = new Dictionary<long, LazyObjectsList>();
				DbUtils.check_ok(objsDb, raw.sqlite3_prepare_v2(objsDb, "SELECT TypeId, Count(Address), Sum(Size) FROM Objs GROUP BY TypeId", out var stmt));
				int res;
				while ((res = raw.sqlite3_step(stmt)) == raw.SQLITE_ROW) {
					long typeId = raw.sqlite3_column_int64(stmt, 0);
					cachedTypesToObjectsListMap.Add(typeId, new HsTypesList(objsDb, raw.sqlite3_column_int(stmt, 1), raw.sqlite3_column_int64(stmt, 2), typeId));
				}
				if (res != raw.SQLITE_DONE)
					DbUtils.check_ok(objsDb, res);
				DbUtils.check_ok(objsDb, raw.sqlite3_finalize(stmt));
				return cachedTypesToObjectsListMap;
			}
		}

		public Dictionary<long, SuperEvent> CountersDescriptions { get; set; }
		public CountersRow Counters { get; set; }
	}

	public class CountersRow
	{
		public TimeSpan time;
		public Dictionary<long, double> Counters = new Dictionary<long, double>();
		internal long GcResize;
	}
}
