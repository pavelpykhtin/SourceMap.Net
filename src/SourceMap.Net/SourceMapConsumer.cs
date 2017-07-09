//var util = require('./util');
//var binarySearch = require('./binary-search');
//var ArraySet = require('./array-set').ArraySet;
//var base64VLQ = require('./base64-vlq');
//var quickSort = require('./quick-sort').quickSort;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SourceMap.Net
{
	public abstract class SourceMapConsumer: ISourceMapConsumer
	{
		// `__generatedMappings` and `__originalMappings` are arrays that hold the
		// parsed mapping coordinates from the source map's "mappings" attribute. They
		// are lazily instantiated, accessed via the `_generatedMappings` and
		// `_originalMappings` getters respectively, and we only parse the mappings
		// and create these arrays once queried for a source location. We jump through
		// these hoops because there can be many thousands of mappings, and parsing
		// them is expensive, so we only want to do it if we must.
		//
		// `_generatedMappings` is ordered by the generated positions.
		//
		// `_originalMappings` is ordered by the original positions.
		protected List<MappingItemIndexed> _generatedMappings = null;
		protected List<MappingItemIndexed> _originalMappings = null;
		protected ArraySet _sources;
		protected ArraySet _names;
		protected string _mappings;

		/**
		 * The version of the source mapping spec that we are consuming.
		 */
		public int Version { get { return 3; } }
		public ArraySet SourcesSet { get { return _sources; } }
		public ArraySet NamesSet { get {return _names;} }
		public string SourceRoot { get; set; }
		public List<MappingItemIndexed> GeneratedMappings
		{
			get
			{
				if (_generatedMappings != null)
					return _generatedMappings;

				ParseMappings(_mappings, SourceRoot);

				return _generatedMappings;
			}
		}
		public List<MappingItemIndexed> OriginalMappings
		{
			get
			{
				if (_originalMappings != null)
					return _originalMappings;

				ParseMappings(_mappings, SourceRoot);

				return _originalMappings;
			}
		}
		
		public static ISourceMapConsumer GetConsumer(string sourceMap)
		{
			var regex = new Regex(@"\A\)\]\}");
			var cookedSourceMap = regex.Replace(sourceMap, "");
			var serializer = new JsonSerializer();

			var textReader = new StringReader(cookedSourceMap);
			var reader = new JsonTextReader(textReader);
			var sourceMapDescription = serializer.Deserialize<SourceMapDescription>(reader);

			return GetConsumer((SourceMapDescription) sourceMapDescription);
		}

		public static ISourceMapConsumer GetConsumer(SourceMapDescription sourceMapDescription)
		{
			return sourceMapDescription.Sections != null
				? (ISourceMapConsumer) new IndexedSourceMapConsumer(sourceMapDescription)
				: new BasicSourceMapConsumer(sourceMapDescription);
		}

		public static ISourceMapConsumer FromSourceMap(string sourceMap)
		{
			return BasicSourceMapConsumer.FromSourceMap(sourceMap);
		}

		protected bool CharIsMappingSeparator(string aStr, int index)
		{
			var c = aStr[index];
			return c == ';' || c == ',';
		}
		
		/**
		* Iterate over each mapping between an original source/line/column and a
		* generated line/column in this source map.
		*
		* @param Function aCallback
		*        The function that is called with each mapping.
		* @param Object aContext
		*        Optional. If specified, this object will be the value of `this` every
		*        time that `aCallback` is called.
		* @param aOrder
		*        Either `SourceMapConsumer.GENERATED_ORDER` or
		*        `SourceMapConsumer.ORIGINAL_ORDER`. Specifies whether you want to
		*        iterate over the mappings sorted by the generated file's line/column
		*        order or the original's source/line/column order, respectively. Defaults to
		*        `SourceMapConsumer.GENERATED_ORDER`.
		*/
		private void EachMapping(Action<MappingItem, object> aCallback, object aContext = null,
			MappingOrder? aOrder = MappingOrder.GENERATED_ORDER)
		{
			var context = aContext;
			var order = aOrder ?? MappingOrder.GENERATED_ORDER;

			List<MappingItemIndexed> mappings;
			switch (order)
			{
				case MappingOrder.GENERATED_ORDER:
					mappings = GeneratedMappings;
					break;
				case MappingOrder.ORIGINAL_ORDER:
					mappings = OriginalMappings;
					break;
				default:
					throw new ArgumentException(nameof(aOrder), "Unknown order of iteration.");
			}

			var sourceRoot = SourceRoot;
			var cooked = mappings
				.Select(m =>
				{
					var source = m.Source == null ? null : _sources.At((int) m.Source);
					if (source != null && sourceRoot != null)
					{
						source = Util.Join(sourceRoot, source);
					}

					return new MappingItem
					{
						Source = source,
						GeneratedLine = m.GeneratedLine,
						GeneratedColumn = m.GeneratedColumn,
						OriginalLine = m.OriginalLine,
						OriginalColumn = m.OriginalColumn,
						Name = m.Name == null ? null : _names.At((int) m.Name)
					};
				});

			foreach (var m in cooked)
				aCallback(m, aContext);
		}

		public abstract string[] Sources { get; }
		
		/**
		 * Parse the mappings in a string in to a data structure which we can easily
		 * query (the ordered arrays in the `this.__generatedMappings` and
		 * `this.__originalMappings` properties).
		 */
		protected abstract void ParseMappings(string aStr, string aSourceRoot);
		public abstract OriginalPosition OriginalPositionFor(int line, int column, EBias? bias = null);
		public abstract bool HasContentsOfAllSources();
		public abstract string SourceContentFor(string aSource, bool nullOnMissing);
		public abstract Position GeneratedPositionFor(string source, int line, int column, EBias? bias = null);
	}
}
