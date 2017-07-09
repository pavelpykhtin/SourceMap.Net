using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SourceMap.Net
{
	public class BasicSourceMapConsumer : SourceMapConsumer
	{
		public override string[] Sources
		{
			get
			{
				return _sources
					.Select<string, string>(s => SourceRoot != null ? Util.Join(SourceRoot, s) : s)
					.ToArray();
			}
		}
		public string[] SourcesContent { get; set; }

		/**
		* A BasicSourceMapConsumer instance represents a parsed source map which we can
		* query for information about the original file positions by giving it a file
		* position in the generated source.
		*
		* The only parameter is the raw source map (either as a JSON string, or
		* already parsed to an object). According to the spec, source maps have the
		* following attributes:
		*
		*   - version: Which version of the source map spec this map is following.
		*   - sources: An array of URLs to the original source files.
		*   - names: An array of identifiers which can be referrenced by individual mappings.
		*   - sourceRoot: Optional. The URL root from which all sources are relative.
		*   - sourcesContent: Optional. An array of contents of the original source files.
		*   - mappings: A string of base64 VLQs which contain the actual mappings.
		*   - file: Optional. The generated file this source map is associated with.
		*
		* Here is an example source map, taken from the source map spec[0]:
		*
		*     {
		*       version : 3,
		*       file: "out.js",
		*       sourceRoot : "",
		*       sources: ["foo.js", "bar.js"],
		*       names: ["src", "maps", "are", "fun"],
		*       mappings: "AA,AB;;ABCDE;"
		*     }
		*
		* [0]: https://docs.google.com/document/d/1U1RGAehQwRypUTovF1KRlpiOFze0b-_2gc6fAH0KY0k/edit?pli=1#
		*/

		public BasicSourceMapConsumer(SourceMapDescription sourceMapDescription)
		{
			var sourceMapDescription1 = sourceMapDescription;

			int version = sourceMapDescription1.Version;
			string[] sources = sourceMapDescription1.Sources;
			// Sass 3.3 leaves out the 'names' array, so we deviate from the spec (which
			// requires the array) to play nice here.
			var names = sourceMapDescription1.Names ?? new string[0];
			var sourceRoot = sourceMapDescription1.SourceRoot;
			var sourcesContent = sourceMapDescription1.SourcesContent;
			var mappings = sourceMapDescription1.Mappings;

			// Once again, Sass deviates from the spec and supplies the version as a
			// string rather than a number, so we use loose equality checking here.
			if (version != Version)
			{
				throw new Exception($"Unsupported version: {version}");
			}

			sources = Enumerable.Select<string, string>(sources
					// Some source maps produce relative source paths like "./foo.js" instead of
					// "foo.js".  Normalize these first so that future comparisons will succeed.
					// See bugzil.la/1090768.
					.Select(Util.Normalize), source =>
					sourceRoot != null &&
					Util.IsAbsolute(sourceRoot) &&
					Util.IsAbsolute(source)
						? Util.Relative(sourceRoot, source)
						: source)
				.ToArray();

			// Pass `true` below to allow duplicate names and sources. While source maps
			// are intended to be compressed and deduplicated, the TypeScript compiler
			// sometimes generates source maps with duplicates in them. See Github issue
			// #72 and bugzil.la/889492.
			_names = new ArraySet(names, true);
			_sources = new ArraySet(sources, true);

			SourceRoot = sourceRoot;
			SourcesContent = sourcesContent;
			_mappings = mappings;
		}


		//public BasicSourceMapConsumer(SourceMapGenerator sourceMap)
		//{
		//	var names = _names = new ArraySet(sourceMap.Names.ToArray(), true);
		//	var sources = _sources = new ArraySet(sourceMap.Sources.ToArray(), true);
		//	SourceRoot = sourceMap.SourceRoot;
		//	SourcesContent = sourceMap._generateSourcesContent(_sources.ToArray(), SourceRoot);
		//	File = sourceMap.File;

		//	// Because we are modifying the entries (by converting string sources and
		//	// names to indices into the sources and names ArraySets), we have to make
		//	// a copy of the entry or else bad things happen. Shared mutable state
		//	// strikes again! See github issue #191.

		//	var generatedMappings = sourceMap.Mappings.ToArray().slice();
		//	var destGeneratedMappings = _generatedMappings = new List<MappingItemIndexed>();
		//	var destOriginalMappings = _originalMappings = new List<MappingItemIndexed>();

		//	for (int i = 0, length = generatedMappings.Length; i < length; i++)
		//	{
		//		var srcMapping = generatedMappings[i];
		//		var destMapping = new MappingItemIndexed();
		//		destMapping.GeneratedLine = srcMapping.GeneratedLine;
		//		destMapping.GeneratedColumn = srcMapping.GeneratedColumn;

		//		if (srcMapping.Source != null)
		//		{
		//			destMapping.Source = sources.IndexOf(srcMapping.Source);
		//			destMapping.OriginalLine = srcMapping.OriginalLine;
		//			destMapping.OriginalColumn = srcMapping.OriginalColumn;

		//			if (srcMapping.Name != null)
		//				destMapping.Name = names.IndexOf(srcMapping.Name);

		//			destOriginalMappings.Add(destMapping);
		//		}

		//		destGeneratedMappings.Add(destMapping);
		//	}

		//	quickSort(_originalMappings, Util.CompareByOriginalPositions);
		//}

		//public static ISourceMapConsumer FromSourceMap(SourceMapGenerator sourceMap)
		//{
		//	return new BasicSourceMapConsumer(sourceMap);
		//}

		/**
		* Parse the mappings in a string in to a data structure which we can easily
		* query (the ordered arrays in the `this.__generatedMappings` and
		* `this.__originalMappings` properties).
		*/

		protected override void ParseMappings(string aStr, string aSourceRoot)
		{
			var generatedLine = 1;
			var previousGeneratedColumn = 0;
			var previousOriginalLine = 0;
			var previousOriginalColumn = 0;
			var previousSource = 0;
			var previousName = 0;
			var length = aStr.Length;
			var index = 0;
			var cachedSegments = new Dictionary<string, List<int>>();
			var originalMappings = new List<MappingItemIndexed>();
			var generatedMappings = new List<MappingItemIndexed>();

			while (index < length)
			{
				if (aStr[index] == ';')
				{
					generatedLine++;
					index++;
					previousGeneratedColumn = 0;
				}
				else if (aStr[index] == ',')
				{
					index++;
				}
				else
				{
					var mapping = new MappingItemIndexed();
					mapping.GeneratedLine = generatedLine;

					// Because each offset is encoded relative to the previous one,
					// many segments often have the same encoding. We can exploit this
					// fact by caching the parsed variable length fields of each segment,
					// allowing us to avoid a second parse if we encounter the same
					// segment again.
					int end;
					for (end = index; end < length; end++)
					{
						if (CharIsMappingSeparator(aStr, end))
						{
							break;
						}
					}
					var str = aStr.Substring(index, end - index);

					List<int> segment;
					if (cachedSegments.ContainsKey(str))
					{
						segment = cachedSegments[str];
						index += str.Length;
					}
					else
					{
						segment = new List<int>();
						while (index < end)
						{
							int value;
							Base64Vlq.Decode(aStr, ref index, out value);
							segment.Add(value);
						}

						if (segment.Count == 2)
						{
							throw new Exception("Found a source, but no line and column");
						}

						if (segment.Count == 3)
						{
							throw new Exception("Found a source and line, but no column");
						}

						cachedSegments.Add(str, segment);
					}

					// Generated column.
					mapping.GeneratedColumn = previousGeneratedColumn + segment[0];
					previousGeneratedColumn = mapping.GeneratedColumn;

					if (segment.Count > 1)
					{
						// Original source.
						mapping.Source = previousSource + segment[1];
						previousSource += segment[1];

						// Original line.
						mapping.OriginalLine = previousOriginalLine + segment[2];
						previousOriginalLine = (int) mapping.OriginalLine;
						// Lines are stored 0-based
						mapping.OriginalLine += 1;

						// Original column.
						mapping.OriginalColumn = previousOriginalColumn + segment[3];
						previousOriginalColumn = (int) mapping.OriginalColumn;

						if (segment.Count > 4)
						{
							// Original name.
							mapping.Name = previousName + segment[4];
							previousName += segment[4];
						}
					}

					generatedMappings.Add(mapping);
					if (mapping.OriginalLine != null)
					{
						originalMappings.Add(mapping);
					}
				}
			}

			generatedMappings.Sort((Comparison<MappingItemIndexed>) Util.CompareByGeneratedPositionsDeflated);
			_generatedMappings = generatedMappings;

			originalMappings.Sort((Comparison<MappingItemIndexed>) Util.CompareByOriginalPositions);
			_originalMappings = originalMappings;
		}

		/**
		 * Find the mapping that best matches the hypothetical "needle" mapping that
		 * we are searching for in the given "haystack" of mappings.
		 */

		protected int FindMapping(
			MappingItemIndexed needle,
			List<MappingItemIndexed> aMappings,
			Func<MappingItemIndexed, int?> line,
			Func<MappingItemIndexed, int?> column,
			Func<MappingItemIndexed, MappingItemIndexed, int> aComparator,
			EBias aBias)
		{
			// To return the position we are searching for, we must first find the
			// mapping for the given position and then return the opposite position it
			// points to. Because the mappings are sorted, we can use binary search to
			// find the best mapping.
			if (line(needle) <= 0)
				throw new ArgumentException($"Line must be greater than or equal to 1, got {line(needle)}");
			if (column(needle) < 0)
				throw new ArgumentException($"Column must be greater than or equal to 0, got {column(needle)}");

			return BinarySearch.Search<MappingItemIndexed>(needle, aMappings, aComparator, aBias);
		}

		/**
		 * Returns all generated line and column information for the original source,
		 * line, and column provided. If no column is provided, returns all mappings
		 * corresponding to a either the line we are searching for or the next
		 * closest line that has any mappings. Otherwise, returns all mappings
		 * corresponding to the given line and either the column we are searching for
		 * or the next closest column that has any offsets.
		 *
		 * The only argument is an object with the following properties:
		 *
		 *   - source: The filename of the original source.
		 *   - line: The line number in the original source.
		 *   - column: Optional. the column number in the original source.
		 *
		 * and an array of objects is returned, each with the following properties:
		 *
		 *   - line: The line number in the generated source, or null.
		 *   - column: The column number in the generated source, or null.
		 */
		public IEnumerable<Position> AllGeneratedPositionsFor(string source, int line, int? column)
		{
			// When there is no exact match, BasicSourceMapConsumer.prototype._findMapping
			// returns the index of the closest mapping less than the needle. By
			// setting needle.originalColumn to 0, we thus find the last mapping for
			// the given line, provided such a mapping exists.
			var relativeSource = SourceRoot == null ? source : Util.Relative(SourceRoot, source);
			if (!_sources.Has(relativeSource))
			{
				return new Position[0];
			}


			var needle = new MappingItemIndexed
			{
				Source = _sources.IndexOf(relativeSource),
				OriginalLine = line,
				OriginalColumn = column ?? 0
			};

			var mappings = new List<Position>();

			int index = FindMapping(needle,
				OriginalMappings,
				x => x.OriginalLine,
				x => x.OriginalColumn,
				Util.CompareByOriginalPositions,
				EBias.LEAST_UPPER_BOUND);
			if (index < 0)
				return new Position[0];

			{
				var mapping = OriginalMappings[index];

				if (column == null)
				{
					var originalLine = mapping.OriginalLine;

					// Iterate until either we run out of mappings, or we run into
					// a mapping for a different line than the one we found. Since
					// mappings are sorted, this is guaranteed to find all mappings for
					// the line we found.
					while (mapping != null && mapping.OriginalLine == originalLine)
					{
						mappings.Add(new Position
						{
							Line = mapping.GeneratedLine,
							Column = mapping.GeneratedColumn,
							LastColumn = mapping.LastGeneratedColumn
						});

						mapping = OriginalMappings[++index];
					}
				}
				else
				{
					var originalColumn = mapping.OriginalColumn;

					// Iterate until either we run out of mappings, or we run into
					// a mapping for a different line than the one we were searching for.
					// Since mappings are sorted, this is guaranteed to find all mappings for
					// the line we are searching for.
					while (mapping != null &&
						   mapping.OriginalLine == line &&
						   mapping.OriginalColumn == originalColumn)
					{
						mappings.Add(new Position
						{
							Line = mapping.GeneratedLine,
							Column = mapping.GeneratedColumn,
							LastColumn = mapping.LastGeneratedColumn
						});


						mapping = OriginalMappings[++index];
					}
				}
			}

			return mappings;
		}

		/**
		 * Compute the last column for each generated mapping. The last column is
		 * inclusive.
		 */

		private void ComputeColumnSpans()
		{
			for (var index = 0; index < GeneratedMappings.Count; ++index)
			{
				var mapping = GeneratedMappings[index];

				// Mappings do not contain a field for the last generated columnt. We
				// can come up with an optimistic estimate, however, by assuming that
				// mappings are contiguous (i.e. given two consecutive mappings, the
				// first mapping ends where the second one starts).
				if (index + 1 < GeneratedMappings.Count)
				{
					var nextMapping = GeneratedMappings[index + 1];

					if (mapping.GeneratedLine == nextMapping.GeneratedLine)
					{
						mapping.LastGeneratedColumn = nextMapping.GeneratedColumn - 1;
						continue;
					}
				}

				// The last mapping for each line spans the entire line.
				mapping.LastGeneratedColumn = int.MaxValue;
			}
		}

		/**
	 * Returns the original source, line, and column information for the generated
	 * source's line and column positions provided. The only argument is an object
	 * with the following properties:
	 *
	 *   - line: The line number in the generated source.
	 *   - column: The column number in the generated source.
	 *   - bias: Either 'SourceMapConsumer.GREATEST_LOWER_BOUND' or
	 *     'SourceMapConsumer.LEAST_UPPER_BOUND'. Specifies whether to return the
	 *     closest element that is smaller than or greater than the one we are
	 *     searching for, respectively, if the exact element cannot be found.
	 *     Defaults to 'SourceMapConsumer.GREATEST_LOWER_BOUND'.
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

			var index = FindMapping(
				needle,
				GeneratedMappings,
				x => x.GeneratedLine,
				x => x.GeneratedColumn,
				Util.CompareByGeneratedPositionsDeflated,
				bias ?? EBias.GREATEST_LOWER_BOUND);

			if (index >= 0)
			{
				var mapping = GeneratedMappings[index];

				if (mapping.GeneratedLine == needle.GeneratedLine)
				{
					var sourceIndex = mapping.Source;
					string source = null;
					if (sourceIndex != null)
					{
						source = _sources.At((int) sourceIndex);
						if (SourceRoot != null)
						{
							source = Util.Join(SourceRoot, source);
						}
					}
					var nameIndex = mapping.Name;
					string name = null;
					if (nameIndex != null)
					{
						name = _names.At((int) nameIndex);
					}
					return new OriginalPosition
					{
						Source = source,
						Line = mapping.OriginalLine,
						Column = mapping.OriginalColumn,
						Name = name
					};
				}
			}

			return new OriginalPosition
			{
				Source = null,
				Line = null,
				Column = null,
				Name = null
			};
		}

		/**
	   * Return true if we have the source content for every source in the source
	   * map, false otherwise.
	   */

		public override bool HasContentsOfAllSources()
		{
			if (SourcesContent == null)
			{
				return false;
			}
			return SourcesContent.Length >= _sources.Count() &&
			       SourcesContent.All(sc => sc != null);
		}

		/**
 * Returns the original source content. The only argument is the url of the
 * original source file. Returns null if no original source content is
 * available.
 */

		public override string SourceContentFor(string aSource, bool nullOnMissing)
		{
			if (SourcesContent == null)
				return null;

			if (SourceRoot != null)
			{
				aSource = Util.Relative(SourceRoot, aSource);
			}

			if (_sources.Has(aSource))
			{
				return SourcesContent[_sources.IndexOf(aSource)];
			}

			Uri url = null;
			if (SourceRoot != null)
				url = Util.UrlParse(SourceRoot);

            if (url != null)
			{
				// XXX: file:// URIs and absolute paths lead to unexpected behavior for
				// many users. We can help them out when they expect file:// URIs to
				// behave like it would if they were running a local HTTP server. See
				// https://bugzilla.mozilla.org/show_bug.cgi?id=885597.
				var regex = new Regex(@"\Afile://");
				var fileUriAbsPath = regex.Replace(aSource, "");
				if (url.Scheme == "file" && _sources.Has(fileUriAbsPath))
					return SourcesContent[_sources.IndexOf(fileUriAbsPath)];

				if (url.LocalPath == "/" && _sources.Has("/" + aSource))
					return SourcesContent[_sources.IndexOf("/" + aSource)];
			}

			// This function is used recursively from
			// IndexedSourceMapConsumer.prototype.sourceContentFor. In that case, we
			// don't want to throw if we can't find the source - we just want to
			// return null, so we provide a flag to exit gracefully.
			if (nullOnMissing)
				return null;
			else
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
	 *   - bias: Either 'SourceMapConsumer.GREATEST_LOWER_BOUND' or
	 *     'SourceMapConsumer.LEAST_UPPER_BOUND'. Specifies whether to return the
	 *     closest element that is smaller than or greater than the one we are
	 *     searching for, respectively, if the exact element cannot be found.
	 *     Defaults to 'SourceMapConsumer.GREATEST_LOWER_BOUND'.
	 *
	 * and an object is returned with the following properties:
	 *
	 *   - line: The line number in the generated source, or null.
	 *   - column: The column number in the generated source, or null.
	 */

		public override Position GeneratedPositionFor(string source, int line, int column, EBias? bias = null)
		{
			if (SourceRoot != null)
			{
				source = Util.Relative(SourceRoot, source);
			}
			if (!_sources.Has(source))
			{
				return new Position
				{
					Line = null,
					Column = null,
					LastColumn = null
				};
			}
			var sourceIndex = _sources.IndexOf(source);

			var needle = new MappingItemIndexed
			{
				Source = sourceIndex,
				OriginalLine = line,
				OriginalColumn = column
			};

			var index = FindMapping(
				needle,
				OriginalMappings,
				x => x.OriginalLine,
				x => x.OriginalColumn,
				Util.CompareByOriginalPositions,
				bias ?? EBias.GREATEST_LOWER_BOUND);

			if (index >= 0)
			{
				var mapping = _originalMappings[index];

				if (mapping.Source == needle.Source)
				{
					return new Position
					{
						Line = mapping.GeneratedLine,
						Column = mapping.GeneratedColumn,
						LastColumn = mapping.LastGeneratedColumn
					};
				}
			}

			return new Position
			{
				Line = null,
				Column = null,
				LastColumn = null
			};
		}
	}
}