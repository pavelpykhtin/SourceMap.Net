namespace SourceMap.Net
{
	public class MappingItem
	{
		public int GeneratedLine;
		public int GeneratedColumn;
		public string Source;
		public int? OriginalLine;
		public int? OriginalColumn;
		public string Name;
	}
}