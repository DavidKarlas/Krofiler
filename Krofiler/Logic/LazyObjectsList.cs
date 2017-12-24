using System;
using System.Collections.Generic;

namespace Krofiler
{
	public abstract class LazyObjectsList
	{
		public int Count { get; }
		public long Size { get; }

		protected LazyObjectsList(int count, long size)
		{
			Count = count;
			Size = size;
		}

		public abstract IEnumerable<ObjectInfo> CreateList(string orderByColum = "Size", bool descending = true, int limit = 100);
	}

	public class SingleLazyObjectList : LazyObjectsList
	{
		readonly ObjectInfo obj;

		public SingleLazyObjectList(ObjectInfo obj)
			: base(1, obj.Size)
		{
			this.obj = obj;
		}

		public override IEnumerable<ObjectInfo> CreateList(string orderByColum = "Size", bool descending = true, int limit = 100)
		{
			return new List<ObjectInfo>() { obj };
		}
	}

	public class EmptyObjectsList : LazyObjectsList
	{
		public static EmptyObjectsList Instance = new EmptyObjectsList();
		private EmptyObjectsList() : base(0, 0) { }
		public override IEnumerable<ObjectInfo> CreateList(string orderByColum = "Size", bool descending = true, int limit = 100)
		{
			return new List<ObjectInfo>();
		}
	}
}
