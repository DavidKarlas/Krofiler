using System;
using System.Collections.Generic;
using System.Linq;

namespace Krofiler
{
	public class ObjectInfo
	{
		public long ObjAddr;
		public long TypeId;
		public long[] ReferencesTo;
		public ushort[] ReferencesAt;
		long[] referenceFrom;
		public long[] GetReferencesFrom(Heapshot heapshot)
		{
			if (referenceFrom != null)
				return referenceFrom;
			var listOfReferences = new List<long>();
			foreach (var obj in heapshot.ObjectsInfoMap.Values) {
				if (obj.ReferencesTo.Contains(ObjAddr))
					listOfReferences.Add(obj.ObjAddr);
			}
			return referenceFrom = listOfReferences.ToArray();
		}

		StackFrame sf;
		internal List<long> ReferencesFrom = new List<long>();

		public StackFrame StackFrame { get; set; }
		public bool IsRoot { get; internal set; }
		//{
		//if (sf != null)
		//	return sf;
		//for (int i = 0; i < session.allocs.Count; i++) {
		//	if (session.allocs[i].Object == ObjId)
		//		return sf = session.allocs[i].StackFrame;
		//}
		//return null;
		//}
	}
}

