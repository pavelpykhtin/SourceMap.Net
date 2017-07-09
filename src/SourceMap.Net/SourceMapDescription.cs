namespace SourceMap.Net
{
	public class SourceMapDescription
	{
		public Section[] Sections { get; set; }
		public int Version { get; set; }
		public string[] Sources { get; set; }
		public string[] Names { get; set; }
		public string SourceRoot { get; set; }
		public string[] SourcesContent { get; set; }
		public string Mappings { get; set; }
		public string File { get; set; }
	}
}