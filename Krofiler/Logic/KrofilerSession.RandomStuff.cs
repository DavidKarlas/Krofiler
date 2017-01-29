using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace Krofiler
{
	//In this file are rendom helper methods that are kinda hacks...
	//eventually content of this file should move elsewhere...
	public partial class KrofilerSession
	{
		//void PrintTypeReport(KeyValuePair<ushort, List<long>> p)
		//{
		//	Console.WriteLine(GetTypeName(p.Key) + " " + p.Value.Count + ": " + string.Join(",", p.Value.Select(i => i.ToString())));
		//}

		Tuple<int, StackFrame> MX(StackFrame value)
		{
			var max = 0; Tuple<int, StackFrame> tuple = new Tuple<int, StackFrame>(0, value);
			foreach (var v in value.Children) {
				var t = MX(v.Value);
				if (t.Item1 > max) {
					max = t.Item1;
					tuple = t;
				}
			}

			return new Tuple<int, StackFrame>(max + 1, tuple.Item2);
		}

		struct FieldKey
		{
			readonly long a;
			readonly ushort b;

			public FieldKey(long typeId, ushort fieldOffset)
			{
				this.a = typeId;
				this.b = fieldOffset;
			}

			public override int GetHashCode()
			{
				return a.GetHashCode() ^ (((int)b) << 16);
			}
		}

		public string GetFieldName(long typeId, ushort fieldOffset)
		{
			throw new NotImplementedException();
		}

		void DoSomeCoolStuff()
		{

			//foreach (var v in rootStackFrame.Children.OrderBy(f => f.Value.MethodName)) {
			//	var tuple123123123 = MX(v.Value);
			//	Console.WriteLine(v.Key + " " + v.Value.MethodName + " MaxDepth:" + tuple123123123.Item1);
			//	var cur123 = tuple123123123.Item2;
			//	while (cur123.Parent != null) {
			//		Console.WriteLine(cur123.MethodName);
			//		cur123 = cur123.Parent;
			//	}
			//}

			//foreach (var hs in heapshots) {
			//	Console.WriteLine("New HS:");
			//	foreach (var p in hs.TypesToObjectsListMap)
			//		PrintTypeReport(p);
			//}

			//var typeId = classIdToName.First(p => p.Value == "leaker.Ha").Key;
			//for (int i = 0; i < heapshots.Count; i++) {
			//	Console.WriteLine(i + " " + string.Join(",", heapshots[i].TypesToObjectsListMap[typeId].Select(id => id.ToString())));
			//	foreach (var obj in heapshots[i].TypesToObjectsListMap[typeId].Select(id => new { Id = id, Info = heapshots[i].ObjectsInfoMap[id] })) {
			//		Console.WriteLine(obj.Id + " " + string.Join(",", obj.Info.ReferencesAt.Select(r => r.ToString())) + " " + string.Join(",", obj.Info.ReferencesTo.Select(r => r.ToString())));
			//	}
			//}

			//for (int i = 0; i < heapshots.Count - 1; i++) {
			//	Console.WriteLine("".PadLeft(100, '-'));
			//	Console.WriteLine("New objects:");
			//	foreach (var typeReport in Heapshot.NewObjects(heapshots[i], heapshots[i + 1], allMoves)) {
			//		PrintTypeReport(typeReport);
			//	}
			//	Console.WriteLine("".PadLeft(100, '-'));
			//	Console.WriteLine("Deleted objects:");
			//	foreach (var typeReport in Heapshot.DeletedObjects(heapshots[i], heapshots[i + 1], allMoves)) {
			//		PrintTypeReport(typeReport);
			//	}
			//	Console.WriteLine("".PadLeft(100, '-'));
			//}

			//var heap = heapshots[2];
			//var objId = 913482;
			//var pathNode = CreateTreeToRoots(heap, objId);
		}
	}


	public class RetentionPathNode
	{
		public long ObjId;
		public RetentionPathNode[] RefsFrom;
	}
}

