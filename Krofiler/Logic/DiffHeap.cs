using System;
using System.Collections.Generic;
using System.Linq;

namespace Krofiler
{
	public class DiffHeap
	{
		public Heapshot OldHeapshot { get; internal set; }
		public Heapshot NewHeapshot { get; internal set; }
		public List<long> NewObjects { get; private set; } = new List<long>();
		public List<long> DeletedObjects { get; private set; } = new List<long>();


		public DiffHeap(Heapshot oldHs, Heapshot newHs)
		{
			OldHeapshot = oldHs;
			NewHeapshot = newHs;

			var newAllocs = newHs.ObjectsInfoMap.ToDictionary(p => p.Value.Allocation, p => p.Value.ObjAddr);

			foreach (var a in oldHs.ObjectsInfoMap.Values) {
				if (!newAllocs.Remove(a.Allocation)) {
					DeletedObjects.Add(a.ObjAddr);
				}
			}
			// What is left in list is new
			NewObjects.AddRange(newAllocs.Values);
		}
	}
}
