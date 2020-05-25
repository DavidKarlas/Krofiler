using System;
using System.Collections.Generic;
using System.Linq;
using SQLitePCL;

namespace Krofiler
{
	public class DiffLazyObjectsList : LazyObjectsList
	{
		readonly sqlite3 mainDb;
		readonly sqlite3 attachedDb;
		readonly long typeId;

		public DiffLazyObjectsList(Heapshot hs1, Heapshot hs2, int count, long size, long typeId)
			: base(count, size)
		{
			this.typeId = typeId;
			this.mainDb = hs1.GetObjsDb();
			this.attachedDb = hs2.GetObjsDb();
		}
		public override IEnumerable<ObjectInfo> CreateList(string orderByColum = "Size", bool descending = true, int limit = 100)
		{
			var fileName = raw.sqlite3_db_filename(attachedDb, null);
			DbUtils.check_ok(mainDb, raw.sqlite3_exec(mainDb, $"attach '{fileName.utf8_to_string()}' as attachedDb;"));
			var result = new List<ObjectInfo>(limit);
			sqlite3_stmt query;
			if(string.IsNullOrEmpty(orderByColum))
				DbUtils.check_ok(mainDb, raw.sqlite3_prepare_v2(mainDb, $"SELECT Address, Allocation, Size FROM Objs WHERE TypeId=? AND Allocation NOT IN (SELECT Allocation FROM attachedDb.Objs) LIMIT {limit}", out query));
			else
				DbUtils.check_ok(mainDb, raw.sqlite3_prepare_v2(mainDb, $"SELECT Address, Allocation, Size FROM Objs WHERE TypeId=? AND Allocation NOT IN (SELECT Allocation FROM attachedDb.Objs) ORDER BY {orderByColum} {(descending ? "DESC" : "ASC")}  LIMIT {limit}", out query));
			DbUtils.check_ok(mainDb, raw.sqlite3_bind_int64(query, 1, typeId));
			int res;
			while ((res = raw.sqlite3_step(query)) == raw.SQLITE_ROW) {
				yield return new ObjectInfo(raw.sqlite3_column_int64(query, 0),
										  raw.sqlite3_column_int64(query, 1),
										  typeId,
										  raw.sqlite3_column_int64(query, 2)
										   );
			}
			if (res != raw.SQLITE_DONE)
				DbUtils.check_ok(mainDb, res);
			DbUtils.check_ok(mainDb, raw.sqlite3_finalize(query));
			DbUtils.check_ok(mainDb, raw.sqlite3_exec(mainDb, $"detach attachedDb;"));
		}
	}

	public class DiffHeap
	{
		public Heapshot OldHeapshot { get; internal set; }
		public Heapshot NewHeapshot { get; internal set; }
		public Dictionary<long, LazyObjectsList> NewObjects { get; private set; } = new Dictionary<long, LazyObjectsList>();
		public Dictionary<long, LazyObjectsList> DeletedObjects { get; private set; } = new Dictionary<long, LazyObjectsList>();


		public DiffHeap(Heapshot oldHs, Heapshot newHs)
		{
			OldHeapshot = oldHs;
			NewHeapshot = newHs;

			var oldDb = oldHs.GetObjsDb();
			var fileName = raw.sqlite3_db_filename(newHs.GetObjsDb(), null);
			DbUtils.check_ok(oldDb, raw.sqlite3_exec(oldDb, $"attach '{fileName.utf8_to_string()}' as newDb;"));
			DbUtils.check_ok(oldDb, raw.sqlite3_prepare_v2(oldDb, "SELECT TypeId, Count(*), Sum(Size) FROM Objs WHERE Allocation NOT IN (SELECT Allocation FROM newDb.Objs) GROUP BY TypeId", out var deadStmt));
			int res;
			while ((res = raw.sqlite3_step(deadStmt)) == raw.SQLITE_ROW) {
				long typeId = raw.sqlite3_column_int64(deadStmt, 0);
				DeletedObjects.Add(typeId, new DiffLazyObjectsList(oldHs, newHs, raw.sqlite3_column_int(deadStmt, 1), raw.sqlite3_column_int64(deadStmt, 2), typeId));
			}
			if (res != raw.SQLITE_DONE)
				DbUtils.check_ok(oldDb, res);
			DbUtils.check_ok(oldDb, raw.sqlite3_finalize(deadStmt));
			DbUtils.check_ok(oldDb, raw.sqlite3_prepare_v2(oldDb, "SELECT TypeId, Count(*), Sum(Size) FROM newDb.Objs WHERE Allocation NOT IN (SELECT Allocation FROM Objs) GROUP BY TypeId", out var newStmt));
			while ((res = raw.sqlite3_step(newStmt)) == raw.SQLITE_ROW) {
				long typeId = raw.sqlite3_column_int64(newStmt, 0);
				NewObjects.Add(typeId, new DiffLazyObjectsList(newHs, oldHs, raw.sqlite3_column_int(newStmt, 1), raw.sqlite3_column_int64(newStmt, 2), typeId));
			}
			if (res != raw.SQLITE_DONE)
				DbUtils.check_ok(oldDb, res);
			DbUtils.check_ok(oldDb, raw.sqlite3_finalize(newStmt));
			DbUtils.check_ok(oldDb, raw.sqlite3_exec(oldDb, $"detach newDb;"));
		}
	}
}
