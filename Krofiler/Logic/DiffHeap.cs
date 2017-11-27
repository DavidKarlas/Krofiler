using System;
using System.Collections.Generic;
using System.Linq;

namespace Krofiler
{
	public class DiffHeap
	{
		public Heapshot OldHeapshot { get; internal set; }
		public Heapshot NewHeapshot { get; internal set; }
		public List<ObjectInfo> NewObjects { get; private set; } = new List<ObjectInfo>();
		public List<ObjectInfo> DeletedObjects { get; private set; } = new List<ObjectInfo>();


		public DiffHeap(Heapshot oldHs, Heapshot newHs)
		{
			OldHeapshot = oldHs;
			NewHeapshot = newHs;

			var oldAllocs = oldHs.ObjectsInfoMap.ToDictionary(p => p.Value.Allocation, p => p.Value);
			var newAllocs = newHs.ObjectsInfoMap.ToDictionary(p => p.Value.Allocation, p => p.Value);

			foreach (var a in oldAllocs) {
				if (!newAllocs.Remove(a.Key)) {
					DeletedObjects.Add(a.Value);
				}
			}
			// What is left in list is new
			NewObjects.AddRange(newAllocs.Values);
		}
	}
}
