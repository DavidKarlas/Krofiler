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
		void PrintTypeReport(KeyValuePair<long, List<long>> p)
		{
			Console.WriteLine(GetTypeName(p.Key) + " " + p.Value.Count + ": " + string.Join(",", p.Value.Select(i => i.ToString())));
		}

		public RetentionPathNode CreateTreeToRoots(Heapshot hs, long objId)
		{
			var objInfo = hs.ObjectsInfoMap[objId];
			var refs = objInfo.GetReferencesFrom(hs);
			var listOfRefFromPaths = new List<RetentionPathNode>();
			foreach (var refFrom in refs) {
				listOfRefFromPaths.Add(CreateTreeToRoots(hs, refFrom));
			}
			return new RetentionPathNode() {
				ObjId = objId,
				RefsFrom = listOfRefFromPaths.ToArray()
			};
		}

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

		Dictionary<FieldKey, string> fieldNamesCached = new Dictionary<FieldKey, string>();
		List<Mono.Cecil.ModuleDefinition> cecils = new List<Mono.Cecil.ModuleDefinition>();

		public string GetFieldName(long typeId, ushort fieldOffset)
		{
			var key = new FieldKey(typeId, fieldOffset);
			if (fieldNamesCached.ContainsKey(key))
				return fieldNamesCached[key];
			var typeName = GetTypeName(typeId);
			if (typeName.EndsWith("[]", StringComparison.Ordinal)) {
				//Array...
				//TODO: Check what that -16 represents and check if we are on 64bit(/8 instead of /4)
				return fieldNamesCached[key] = $"[{(fieldOffset - 16) / 4}]";
			}
			if (typeName.EndsWith(">", StringComparison.Ordinal)) {
				//TODO: Handle more then just `1...(check for commas but skip nested <>)
				typeName = typeName.Remove(typeName.IndexOf('<'));
				typeName += "`1";
			}
			if (cecils.Count < allImagesPaths.Count) {
				for (int i = cecils.Count; i < allImagesPaths.Count; i++) {
					if (File.Exists(allImagesPaths[i])) {
						try {
							cecils.Add(Mono.Cecil.ModuleDefinition.ReadModule(allImagesPaths[i]));
						} catch { cecils.Add(null); }
					}
				}
			}
			Mono.Cecil.TypeDefinition type = null;
			foreach (var cecil in cecils) {
				type = cecil?.GetType(typeName);
				if (type != null)
					break;
			}
			ushort currentOffset = 16;//Looks like it always starts with offset 8
			if (type != null) {
				foreach (var f in type.Fields) {
					if (f.FieldType.IsPrimitive || f.FieldType.IsValueType)
						continue;
					if (currentOffset == fieldOffset)
						return fieldNamesCached[key] = f.Name;
					currentOffset += 8;
				}
				return fieldNamesCached[key] = "<field not found>";
			} else {
				return fieldNamesCached[key] = "<type not found>";
			}
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

