using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Profiler.Log;
using QuickGraph;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Algorithms.Search;

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
		public Dictionary<long, List<ObjectInfo>> TypesToObjectsListMap = new Dictionary<long, List<ObjectInfo>>();
		public Dictionary<long, ObjectInfo> ObjectsInfoMap = new Dictionary<long, ObjectInfo>();
		public Dictionary<long, HeapRootRegisterEvent> Roots = new Dictionary<long, HeapRootRegisterEvent>();

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

		void BuildReferencesFrom()
		{
			var listOfReferences = new List<long>();
			foreach (var obj in ObjectsInfoMap.Values) {
				foreach (var r in obj.ReferencesTo)
					ObjectsInfoMap[r].ReferencesFrom.Add(obj.ObjAddr);
			}
		}


		List<IEnumerable<ReferenceEdge>> cachedResult;
		long cachedAddr;
		bool referencesFromBuilt=false;
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
			if (!referencesFromBuilt)
			{
				referencesFromBuilt = true;
				var sw = Stopwatch.StartNew ();
				BuildReferencesFrom ();
				sw.Stop ();
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
			return ObjectsInfoMap[source].ReferencesFrom.Contains(target);
		}

		public bool ContainsVertex(long vertex)
		{
			return ObjectsInfoMap.ContainsKey(vertex);
		}

		public bool IsOutEdgesEmpty(long v)
		{
			return ObjectsInfoMap[v].ReferencesFrom.Count == 0;
		}

		public int OutDegree(long v)
		{
			return ObjectsInfoMap[v].ReferencesFrom.Count;
		}

		public ReferenceEdge OutEdge(long v, int index)
		{
			return new ReferenceEdge(v, ObjectsInfoMap[v].ReferencesFrom[index]);
		}

		public IEnumerable<ReferenceEdge> OutEdges(long v)
		{
			foreach (var r in ObjectsInfoMap[v].ReferencesFrom) {
				yield return new ReferenceEdge(v, r);
			}
		}

		public bool TryGetEdge(long source, long target, out ReferenceEdge edge)
		{
			if (ObjectsInfoMap[source].ReferencesFrom.Contains(target)) {
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
