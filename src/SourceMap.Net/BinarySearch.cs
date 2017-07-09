using System;
using System.Collections.Generic;

namespace SourceMap.Net
{
	public static class BinarySearch
	{
		public static int Search<T>(T term, List<T> collection, Func<T, T, int> comparer, EBias bias)
		{
			var index = collection.BinarySearch(term, new Comaprer<T>(comparer));

			return NormalizeIndex(index, collection, bias);
		}

		public static int Search<TTerm, T>(TTerm term, List<T> collection, Func<TTerm, T, int> comparer, EBias bias) where T : new()
		{
			var marker = new T();
			var index = collection.BinarySearch(marker, new Comaprer<TTerm, T>(term, comparer, marker));

			return NormalizeIndex(index, collection, bias);
		}
		
		private static int NormalizeIndex<T>(int index, List<T> collection, EBias bias)
		{
			if (index > 0)
				return index;

			index = ~index;

			if (index == collection.Count)
				return -1;

			return bias == EBias.GREATEST_LOWER_BOUND ? index - 1 : index;
		}

		private class Comaprer<T> : IComparer<T>
		{
			private readonly Func<T, T, int> _comparer;

			public Comaprer(Func<T, T, int> comparer)
			{
				_comparer = comparer;
			}

			public int Compare(T x, T y)
			{
				return _comparer(x, y);
			}
		}

		private class Comaprer<TTerm, T> : IComparer<T>
		{
			private readonly TTerm _term;
			private readonly Func<TTerm, T, int> _comparer;
			private readonly T _marker;

			public Comaprer(TTerm term, Func<TTerm, T, int> comparer, T marker)
			{
				_term = term;
				_comparer = comparer;
				_marker = marker;
			}

			public int Compare(T x, T y)
			{
				var result = _comparer(_term, y);
				return ReferenceEquals(x, _marker) ? result : -result;
			}
		}
	}
}