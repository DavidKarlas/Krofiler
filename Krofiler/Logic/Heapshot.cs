using System;
using System.Collections.Generic;
using System.Linq;
using Krofiler.Reader;
using QuickGraph;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Algorithms.Search;

namespace Krofiler
{
	public class ReferenceEdge : IEdge<long>
	{
		public long Source { get; set; }
		public string FieldName { get; set; }
		public long Target { get; set; }
		public ReferenceEdge(long s, long t)
		{
			Source = s;
			Target = t;
		}
	}

	public class RootInfo
	{
		public string Reason = string.Empty;
	}

	public class Heapshot : Dictionary<long, HeapObject>, IVertexListGraph<long, ReferenceEdge>
	{
		public readonly int Id;
		public readonly KrofilerSession Session;
		public int AllocsAndMovesStartPosition;
		public readonly int MovesStartPosition;
		public Dictionary<ushort, ClassInfo> Types = new Dictionary<ushort, ClassInfo>(1024);

		public string GetTypeName(ushort id)
		{
			return Types[id].Name;
		}

		public string Name {
			get {
				return Id.ToString();
			}
		}
		public Dictionary<long, RootInfo> Roots = new Dictionary<long, RootInfo>();

		public Heapshot(int id, KrofilerSession session, int allocsAndMovesPos)
		{
			Id = id;
			Session = session;
			AllocsAndMovesStartPosition = allocsAndMovesPos;
		}

		public void Initialize()
		{
			foreach (var o in Values) {
				foreach (var r in o.Refs)
					this[r].RefsIn.Add(o.Address);
			}
			foreach (var type in Types.Values) {
				foreach (var field in type.Fields) {
					if (field.PointingTo == 0)
						continue;
					RootInfo root;
					if (Roots.TryGetValue(field.PointingTo, out root))
						root.Reason += "Static field " + field.Name + " in " + type.Name + Environment.NewLine;
				}
			}
		}

		List<ReferenceEdge> cachedResult;
		long cachedAddr;

		public IEnumerable<ReferenceEdge> GetShortestPathToRoot(long addr)
		{
			if (cachedAddr == addr && cachedResult != null)
				return cachedResult;
			var bfsa = new BreadthFirstSearchAlgorithm<long, ReferenceEdge>(this);
			var vis = new VertexPredecessorRecorderObserver<long, ReferenceEdge>();
			vis.Attach(bfsa);
			//possible optimisation to find only shortest path, worthiness is questionable
			//bfsa.ExamineVertex+= (vertex) => {
			//	if (graph.Roots.Contains(vertex))
			//		bfsa.Services.CancelManager.Cancel();
			//};
			bfsa.Compute(addr);
			List<Tuple<long, List<ReferenceEdge>>> distances = new List<Tuple<long, List<ReferenceEdge>>>();
			foreach (var root in Roots.Keys) {
				IEnumerable<ReferenceEdge> p;
				if (vis.TryGetPath(root, out p))
					distances.Add(new Tuple<long, List<ReferenceEdge>>(p.Count(), p.ToList()));
			}
			//Find shortest path
			cachedResult = distances.OrderBy(t => t.Item1).FirstOrDefault()?.Item2 ?? new List<ReferenceEdge>();
			cachedAddr = addr;
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
				return Count == 0;
			}
		}

		public int VertexCount {
			get {
				return Count;
			}
		}

		public IEnumerable<long> Vertices {
			get {
				return Keys;
			}
		}

		public bool ContainsEdge(long source, long target)
		{
			return this[source].RefsIn.Contains(target);
		}

		public bool ContainsVertex(long vertex)
		{
			return this.ContainsKey(vertex);
		}

		public bool IsOutEdgesEmpty(long v)
		{
			return this[v].RefsIn.Count == 0;
		}

		public int OutDegree(long v)
		{
			return this[v].RefsIn.Count;
		}

		public ReferenceEdge OutEdge(long v, int index)
		{
			return new ReferenceEdge(v, this[v].RefsIn[index]);
		}

		public IEnumerable<ReferenceEdge> OutEdges(long v)
		{
			foreach (var r in this[v].RefsIn) {
				yield return new ReferenceEdge(v, r);
			}
		}

		public bool TryGetEdge(long source, long target, out ReferenceEdge edge)
		{
			if (this[source].RefsIn.Contains(target)) {
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

