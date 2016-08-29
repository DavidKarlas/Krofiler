using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Profiler;

namespace Krofiler
{

	public class Heapshot
	{
		ulong totalSize = 0;
		long totalObjects = 0;
		public ulong startTime;
		public ulong endTime;
		public int MovesPosition;
		readonly KrofilerSession session;

		public Heapshot(KrofilerSession session, string name)
		{
			this.session = session;
			this.Name = name;
			MovesPosition = session.allMoves.Count;
		}


		public Dictionary<long, List<long>> TypesToObjectsListMap = new Dictionary<long, List<long>>();
		public Dictionary<long, ObjectInfo> ObjectsInfoMap = new Dictionary<long, ObjectInfo>();

		public string Name { get; set; }

		public void AddObject(HeapEvent ev)
		{
			var typeId = ev.Class;
			var objAddr = ev.Object;
			Console.WriteLine(Name + " " + objAddr);
			//if (ObjectsInfoMap.ContainsKey(objAddr)) {
			//	if (ObjectsInfoMap[objAddr].TypeId != typeId)
			//		throw new Exception("Type of duplicate object in heap mismatch.");
			//	return;
			//}
			totalSize += ev.Size;
			totalObjects++;
			if (!TypesToObjectsListMap.ContainsKey(typeId))
				TypesToObjectsListMap[typeId] = new List<long>();
			TypesToObjectsListMap[typeId].Add(objAddr);
			ObjectsInfoMap[objAddr] = new ObjectInfo() {
				ObjAddr = objAddr,
				TypeId = typeId,
				ReferencesAt = ev.RelOffset,
				ReferencesTo = ev.ObjectRefs,
				StackFrame = session.allocs.ContainsKey(objAddr) ? session.allocs[objAddr].Item2 : null
			};
		}

		internal static Dictionary<long, List<long>> NewObjects(Heapshot oldHeapShot, Heapshot newHeapShot)
		{
			var result = new Dictionary<long, List<long>>();
			var oldHeapShotWithMoves = (Heapshot)oldHeapShot.MemberwiseClone();
			for (int i = oldHeapShotWithMoves.MovesPosition; i < newHeapShot.MovesPosition; i++) {
				oldHeapShotWithMoves.ChangeAddress(newHeapShot.session.allMoves[i].From, newHeapShot.session.allMoves[i].To);
			}
			foreach (var t in newHeapShot.TypesToObjectsListMap) {
				if (oldHeapShotWithMoves.TypesToObjectsListMap.ContainsKey(t.Key)) {
					var list = t.Value.Except(oldHeapShotWithMoves.TypesToObjectsListMap[t.Key]).ToList();
					if (list.Count == 0) {
						continue;
					} else {
						result.Add(t.Key, list);
					}
				} else {
					result.Add(t.Key, t.Value);
				}
			}
			return result;
		}

		internal static Dictionary<long, List<long>> DeletedObjects(Heapshot oldHeapShot, Heapshot newHeapShot)
		{
			return NewObjects(newHeapShot, oldHeapShot);
		}

		internal void ChangeAddress(long oldAddress, long newAddress)
		{
			if (!ObjectsInfoMap.ContainsKey(oldAddress))
				return;//I guess this allocation happened before our HS
			var ttt = ObjectsInfoMap[oldAddress];
			var typeMap = TypesToObjectsListMap[ttt.TypeId];
			typeMap.Remove(oldAddress);
			typeMap.Add(newAddress);
			ObjectsInfoMap.Remove(oldAddress);
			ObjectsInfoMap.Add(newAddress, ttt);
		}

		internal void AddRootRef(long v1, HeapEvent.RootType rootType, ulong v2)
		{

		}
	}

}

