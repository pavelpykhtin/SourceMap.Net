namespace SourceMap.Net
{
	public class MappingItemIndexed
	{
		public int GeneratedLine;
		public int GeneratedColumn;
		public int? Source;
		public int? OriginalLine;
		public int? OriginalColumn;
		public int? Name;
		public int LastGeneratedColumn { get; set; }
	}
}