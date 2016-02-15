using System;
using System.Collections.Generic;

namespace Krofiler
{
	public class LargeList<T>
	{
		const int MaxSizeOfArray = 1000000;

		int currentSize;
		readonly List<T[]> arrays = new List<T[]>();

		public int Add(T item)
		{
			int arrayToUse = currentSize / MaxSizeOfArray;
			if (arrays.Count < arrayToUse + 1) {
				arrays.Add(new T[MaxSizeOfArray]);
			}

			int indexInArray = currentSize % MaxSizeOfArray;
			arrays[arrayToUse][indexInArray] = item;
			return currentSize++;
		}

		public int Count {
			get {
				return currentSize;
			}
		}

		public T this[int index] {
			get {
				int arrayToUse = index / MaxSizeOfArray;
				int indexInArray = index % MaxSizeOfArray;
				return arrays[arrayToUse][indexInArray];
			}
		}
	}
}

