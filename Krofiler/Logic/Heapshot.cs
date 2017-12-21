using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mono.Profiler.Log;
using QuickGraph;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Algorithms.Search;
using SQLitePCL;

namespace Krofiler
{
	public class ReferenceEdge : IEdge<long>
	{
		public long Source { get; set; }
		public long Target { get; set; }
		public ReferenceEdge(long s, long t)
		{
			Source = s;
			Target = t;
		}
	}

	public class Heapshot : IVertexListGraph<long, ReferenceEdge>
	{
		public Dictionary<long, List<long>> TypesToObjectsListMap = new Dictionary<long, List<long>>();
		public Dictionary<long, ObjectInfo> ObjectsInfoMap = new Dictionary<long, ObjectInfo>();
		public Dictionary<long, SuperEvent> Roots = new Dictionary<long, SuperEvent>();
		sqlite3 db;
		sqlite3_stmt refsFromStmt;

		private static void check_ok(sqlite3 db, int rc)
		{
			if (raw.SQLITE_OK != rc)
				throw new Exception(raw.sqlite3_errstr(rc) + ": " + raw.sqlite3_errmsg(db));
		}

		void GenerateRefsFromStmt()
		{
			if (refsFromStmt != null)
				return;
			GenerateDb();
			check_ok(db, raw.sqlite3_prepare_v2(db, "SELECT AddressFrom FROM Refs WHERE AddressTo=?", out refsFromStmt));
		}

		private void GenerateDb()
		{
			if (db != null)
				return;
			var rc = raw.sqlite3_open_v2($"file:{Path.Combine(Session.processor.cacheFolder, $"Heapshot_{Id}.db")}", out db, raw.SQLITE_OPEN_URI | raw.SQLITE_OPEN_READONLY, null);
			if (rc != raw.SQLITE_OK)
				throw new Exception(raw.sqlite3_errstr(rc));

		}

		Dictionary<long, List<long>> ReferencedFromCache = new Dictionary<long, List<long>>();
		public List<long> GetReferencedFrom(long objAddr)
		{
			if (ReferencedFromCache.TryGetValue(objAddr, out var list))
				return list;
			GenerateRefsFromStmt();
			check_ok(db, raw.sqlite3_bind_int64(refsFromStmt, 1, objAddr));
			list = new List<long>();
			while (raw.sqlite3_step(refsFromStmt) == raw.SQLITE_ROW) {
				list.Add(raw.sqlite3_column_int64(refsFromStmt, 0));
			}
			check_ok(db, raw.sqlite3_reset(refsFromStmt));
			ReferencedFromCache[objAddr] = list;
			return list;
		}

		public sqlite3_stmt GetStmt()
		{
			return refsFromStmt;
		}

		public sqlite3 GetDb()
		{
			return db;
		}

		public List<long> GetReferencedTo(long objAddr)
		{
			GenerateDb();
			check_ok(db, raw.sqlite3_prepare_v2(db, "SELECT AddressTo FROM Refs WHERE AddressFrom=?", out var stmt));
			check_ok(db, raw.sqlite3_bind_int64(stmt, 1, objAddr));
			var list = new List<long>();
			while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW) {
				list.Add(raw.sqlite3_column_int64(stmt, 0));
			}
			check_ok(db, raw.sqlite3_finalize(stmt));
			return list;
		}

		public string Name {
			get {
				return Id.ToString();
			}
		}

		public KrofilerSession Session { get; }
		public int Id { get; }

		public Heapshot(KrofilerSession session, int id)
		{
			Id = id;
			Session = session;
		}

		List<IEnumerable<ReferenceEdge>> cachedResult;
		long cachedAddr;
		public List<IEnumerable<ReferenceEdge>> GetTop5PathsToRoots (long addr)
		{
			if (cachedAddr == addr && cachedResult != null)
				return cachedResult;
			cachedResult = new List<IEnumerable<ReferenceEdge>> ();
			cachedAddr = addr;
			if (Roots.ContainsKey (addr))
			{
				return cachedResult;
			}
			var result = new List<List<ReferenceEdge>> ();
			var bfsa = new BreadthFirstSearchAlgorithm<long, ReferenceEdge> (this);
			var vis = new VertexPredecessorRecorderObserver<long, ReferenceEdge> ();
			vis.Attach (bfsa);
			var visitedRoots = new HashSet<long> ();
			bfsa.ExamineVertex += (vertex) =>
			{
				if (Roots.ContainsKey (vertex))
				{
					visitedRoots.Add (vertex);
					if (visitedRoots.Count == 5)
					{
						bfsa.Services.CancelManager.Cancel ();
					}
				}
			};
			bfsa.Compute (addr);
			foreach (var root in visitedRoots)
			{
				if (vis.TryGetPath (root, out var path))
				{
					cachedResult.Add (path);
				}
			}
			return cachedResult;
		}

		#region QuickGraph interface
		public bool AllowParallelEdges {
			get {
				return false;
			}
		}

		public bool IsDirected {
			get {
				return true;
			}
		}

		public bool IsVerticesEmpty {
			get {
				return ObjectsInfoMap.Count == 0;
			}
		}

		public int VertexCount {
			get {
				return ObjectsInfoMap.Count;
			}
		}

		public IEnumerable<long> Vertices {
			get {
				return ObjectsInfoMap.Keys;
			}
		}

		public bool ContainsEdge(long source, long target)
		{
			return GetReferencedFrom(source).Contains(target);
		}

		public bool ContainsVertex(long vertex)
		{
			return ObjectsInfoMap.ContainsKey(vertex);
		}

		public bool IsOutEdgesEmpty(long v)
		{
			return GetReferencedFrom(v).Count == 0;
		}

		public int OutDegree(long v)
		{
			return GetReferencedFrom(v).Count;
		}

		public ReferenceEdge OutEdge(long v, int index)
		{
			return new ReferenceEdge(v, GetReferencedFrom(v)[index]);
		}

		public IEnumerable<ReferenceEdge> OutEdges(long v)
		{
			foreach (var r in GetReferencedFrom(v)) {
				yield return new ReferenceEdge(v, r);
			}
		}

		public bool TryGetEdge(long source, long target, out ReferenceEdge edge)
		{
			if (GetReferencedFrom(source).Contains(target)) {
				edge = default(ReferenceEdge);
				return false;
			}
			edge = new ReferenceEdge(source, target);
			return true;
		}

		public bool TryGetEdges(long source, long target, out IEnumerable<ReferenceEdge> edges)
		{
			throw new Exception("This graph doesn't allow Parallel Edges");
		}

		public bool TryGetOutEdges(long v, out IEnumerable<ReferenceEdge> edges)
		{
			throw new NotImplementedException();
		}
		#endregion
	}
}
