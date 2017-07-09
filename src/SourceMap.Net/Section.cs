namespace SourceMap.Net
{
	public class Section
	{
		public string Url { get; set; }
		public Offset Offset { get; set; }
		public SourceMapDescription Map { get; set; }
	}
}