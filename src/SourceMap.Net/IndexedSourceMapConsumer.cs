using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceMap.Net
{
	public class IndexedSourceMapConsumer : SourceMapConsumer
	{
		private readonly List<GeneratedSection> _sections;

		public override string[] Sources
		{
			get
			{
				return _sections
					.SelectMany(s => s.Consumer.Sources)
					.ToArray();
			}
		}
		
		/**
		 * An IndexedSourceMapConsumer instance represents a parsed source map which
		 * we can query for information. It differs from BasicSourceMapConsumer in
		 * that it takes "indexed" source maps (i.e. ones with a "sections" field) as
		 * input.
		 *
		 * The only parameter is a raw source map (either as a JSON string, or already
		 * parsed to an object). According to the spec for indexed source maps, they
		 * have the following attributes:
		 *
		 *   - version: Which version of the source map spec this map is following.
		 *   - file: Optional. The generated file this source map is associated with.
		 *   - sections: A list of section definitions.
		 *
		 * Each value under the "sections" field has two fields:
		 *   - offset: The offset into the original specified at which this section
		 *       begins to apply, defined as an object with a "line" and "column"
		 *       field.
		 *   - map: A source map definition. This source map could also be indexed,
		 *       but doesn't have to be.
		 *
		 * Instead of the "map" field, it's also possible to have a "url" field
		 * specifying a URL to retrieve a source map from, but that's currently
		 * unsupported.
		 *
		 * Here's an example source map, taken from the source map spec[0], but
		 * modified to omit a section which uses the "url" field.
		 *
		 *  {
		 *    version : 3,
		 *    file: "app.js",
		 *    sections: [{
		 *      offset: {line:100, column:10},
		 *      map: {
		 *        version : 3,
		 *        file: "section.js",
		 *        sources: ["foo.js", "bar.js"],
		 *        names: ["src", "maps", "are", "fun"],
		 *        mappings: "AAAA,E;;ABCDE;"
		 *      }
		 *    }],
		 *  }
		 *
		 * [0]: https://docs.google.com/document/d/1U1RGAehQwRypUTovF1KRlpiOFze0b-_2gc6fAH0KY0k/edit#heading=h.535es3xeprgt
		 */
		public IndexedSourceMapConsumer(SourceMapDescription sourceMapDescription)
		{
			var version = sourceMapDescription.Version;
			var sections = sourceMapDescription.Sections;

			if (version != Version)
			{
				throw new Exception($"Unsupported version: {version}");
			}

			_sources = new ArraySet();
			_names = new ArraySet();

			var lastOffset = new Offset
			{
				Line = -1,
				Column = 0
			};
			_sections = sections.Select(s =>
			{
				if (s.Url != null)
				{
					// The url field will require support for asynchronicity.
					// See https://github.com/mozilla/source-map/issues/16
					throw new NotImplementedException("Support for url field in sections not implemented.");
				}
				var offset = s.Offset;
				var offsetLine = s.Offset.Line;
				var offsetColumn = s.Offset.Column;

				if (offsetLine < lastOffset.Line ||
				    (offsetLine == lastOffset.Line && offsetColumn < lastOffset.Column))
				{
					throw new Exception("Section offsets must be ordered and non-overlapping.");
				}
				lastOffset = offset;

				return new GeneratedSection
				{
					GeneratedOffset =
						new GeneratedOffset
						{
							// The offset fields are 0-based, but we use 1-based indices when
							// encoding/decoding from VLQ.
							GeneratedLine = offsetLine + 1,
							GeneratedColumn = offsetColumn + 1
						},
					Consumer = GetConsumer(s.Map)
				};
			}).ToList();
		}

		/**
	   * Returns the original source, line, and column information for the generated
	   * source's line and column positions provided. The only argument is an object
	   * with the following properties:
	   *
	   *   - line: The line number in the generated source.
	   *   - column: The column number in the generated source.
	   *
	   * and an object is returned with the following properties:
	   *
	   *   - source: The original source file, or null.
	   *   - line: The line number in the original source, or null.
	   *   - column: The column number in the original source, or null.
	   *   - name: The original identifier, or null.
	   */
		public override OriginalPosition OriginalPositionFor(int line, int column, EBias? bias = null)

		{
			var needle = new MappingItemIndexed
			{
				GeneratedLine = line,
				GeneratedColumn = column
			};

			// Find the section containing the generated position we're trying to map
			// to an original position.
			var sectionIndex = BinarySearch.Search(
				needle,
				_sections,
				(n, s) =>
				{
					var cmp = n.GeneratedLine - s.GeneratedOffset.GeneratedLine;
					if (cmp == 0)
					{
						return cmp;
					}

					return (n.GeneratedColumn - s.GeneratedOffset.GeneratedColumn);
				},
				EBias.GREATEST_LOWER_BOUND);
			var section = _sections[sectionIndex];

			if (section == null)
			{
				return new OriginalPosition
				{
					Source = null,
					Line = null,
					Column = null,
					Name = null
				};
			}

			return section.Consumer.OriginalPositionFor(
				needle.GeneratedLine - (section.GeneratedOffset.GeneratedLine - 1),
				needle.GeneratedColumn - (section.GeneratedOffset.GeneratedLine == needle.GeneratedLine
					? section.GeneratedOffset.GeneratedColumn - 1
					: 0),
				bias);
		}

		/**
   * Return true if we have the source content for every source in the source
   * map, false otherwise.
   */
		public override bool HasContentsOfAllSources()
		{
			return _sections.All(s => s.Consumer.HasContentsOfAllSources());
		}

		/**
		 * Returns the original source content. The only argument is the url of the
		 * original source file. Returns null if no original source content is
		 * available.
		 */
		public override string SourceContentFor(string aSource, bool nullOnMissing)
		{
			foreach (var section in _sections)
			{
				var content = section.Consumer.SourceContentFor(aSource, true);
				if (content != null)
					return content;
			}

			if (nullOnMissing)
				return null;
			throw new Exception($"'{aSource}' is not in the SourceMap.");
		}

		/**
		 * Returns the generated line and column information for the original source,
		 * line, and column positions provided. The only argument is an object with
		 * the following properties:
		 *
		 *   - source: The filename of the original source.
		 *   - line: The line number in the original source.
		 *   - column: The column number in the original source.
		 *
		 * and an object is returned with the following properties:
		 *
		 *   - line: The line number in the generated source, or null.
		 *   - column: The column number in the generated source, or null.
		 */
		public override Position GeneratedPositionFor(string source, int line, int column, EBias? bias = null)
		{
			foreach (var section in _sections)
			{
				// Only consider this section if the requested source is in the list of
				// sources of the consumer.
				if (Array.IndexOf(section.Consumer.Sources, source) == -1)
					continue;
				var generatedPosition = section.Consumer.GeneratedPositionFor(source, line, column, bias);
				if (generatedPosition != null)
				{
					var ret = new Position
					{
						Line = generatedPosition.Line + (section.GeneratedOffset.GeneratedLine - 1),
						Column = generatedPosition.Column + (section.GeneratedOffset.GeneratedLine == generatedPosition.Line
							? section.GeneratedOffset.GeneratedColumn - 1
							: 0)
					};
					return ret;
				}
			}

			return new Position
			{
				Line = null,
				Column = null
			};
		}

		/**
   * Parse the mappings in a string in to a data structure which we can easily
   * query (the ordered arrays in the `this.__generatedMappings` and
   * `this.__originalMappings` properties).
   */
		protected override void ParseMappings(string aStr, string aSourceRoot)
		{
			_generatedMappings = new List<MappingItemIndexed>();
			_originalMappings = new List<MappingItemIndexed>();
			foreach (var section in _sections)
			{
				var sectionMappings = section.Consumer.GeneratedMappings;
				foreach (var mapping in sectionMappings)
				{
					var source = section.Consumer.SourcesSet.At((int) mapping.Source);
					if (section.Consumer.SourceRoot != null)
					{
						source = Util.Join(section.Consumer.SourceRoot, source);
					}
					_sources.Add(source, false);
					var sourceIndex = _sources.IndexOf(source);

					var name = section.Consumer.NamesSet.At((int) mapping.Name);
					_names.Add(name, false);
					var nameIndex = _names.IndexOf(name);

					// The mappings coming from the consumer for the section have
					// generated positions relative to the start of the section, so we
					// need to offset them to be relative to the start of the concatenated
					// generated file.
					var adjustedMapping = new MappingItemIndexed
					{
						Source = sourceIndex,
						GeneratedLine =
							mapping.GeneratedLine +
							(section.GeneratedOffset.GeneratedLine - 1),
						GeneratedColumn =
							mapping.GeneratedColumn +
							(section.GeneratedOffset.GeneratedLine == mapping.GeneratedLine
								? section.GeneratedOffset.GeneratedColumn - 1
								: 0),
						OriginalLine = mapping.OriginalLine,
						OriginalColumn = mapping.OriginalColumn,
						Name = nameIndex
					};

					_generatedMappings.Add(adjustedMapping);
					if (adjustedMapping.OriginalLine != null)
					{
						_originalMappings.Add(adjustedMapping);
					}
				}
			}

			_generatedMappings.Sort((Comparison<MappingItemIndexed>) Util.CompareByGeneratedPositionsDeflated);
			_originalMappings.Sort((Comparison<MappingItemIndexed>) Util.CompareByOriginalPositions);
		}
	}
}