using System.Collections;
using System.Collections.Generic;

namespace SourceMap.Net
{
	public class ArraySet: IEnumerable<string>
	{
		private readonly List<string> _innerList;
		private readonly Dictionary<string, int> _innerSet;

		public ArraySet()
		{
			_innerList = new List<string>();
			_innerSet = new Dictionary<string, int>();
		}

		public ArraySet(IEnumerable<string> src, bool allowDuplicates)
		{
			_innerList = new List<string>();
			_innerSet = new Dictionary<string, int>();

			foreach (var item in src)
				Add(item, allowDuplicates);
		}

		public void Add(string item, bool allowDuplicates)
		{
			var sStr = GetKey(item);
			var isDuplicate = _innerSet.ContainsKey(sStr);
			var idx = _innerList.Count;
			if (!isDuplicate || allowDuplicates)
			{
				_innerList.Add(item);
			}
			if (!isDuplicate)
			{
				_innerSet[sStr] = idx;
			}
		}

		private string GetKey(string aStr)
		{
			return aStr.ToLowerInvariant();
		}

		public bool Has(string item)
		{
			var key = GetKey(item);
			return _innerSet.ContainsKey(key);
		}

		public int IndexOf(string item)
		{
			var key = GetKey(item);
			return _innerSet[key];
		}

		public string At(int index)
		{
			return _innerList[index];
		}

		public IEnumerator<string> GetEnumerator()
		{
			return _innerList.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}