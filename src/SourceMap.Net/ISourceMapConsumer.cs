using System.Collections.Generic;

namespace SourceMap.Net
{
	public interface ISourceMapConsumer
	{
		string[] Sources { get; }
		List<MappingItemIndexed> GeneratedMappings { get; }
		List<MappingItemIndexed> OriginalMappings { get; }
		ArraySet SourcesSet { get; }
		ArraySet NamesSet { get; }
		string SourceRoot { get; set; }

		OriginalPosition OriginalPositionFor(int line, int column, EBias? bias = null);
		bool HasContentsOfAllSources();
		string SourceContentFor(string aSource, bool nullOnMissing);
		Position GeneratedPositionFor(string source, int line, int column, EBias? bias = null);
	}
}