using System;
using System.Text.RegularExpressions;
using System.Web;

namespace SourceMap.Net
{
	public static class Util
	{
		public static string Relative(string root, string path)
		{
			var rootUri = new Uri(root, UriKind.RelativeOrAbsolute);
			var pathUri = new Uri(path, UriKind.RelativeOrAbsolute);

			return rootUri.MakeRelativeUri(pathUri).ToString();
		}

		public static bool IsAbsolute(string sourceRoot)
		{
			return Uri.IsWellFormedUriString(sourceRoot, UriKind.Absolute);
		}

		public static string Normalize(string source)
		{
			var url = UrlParse(source);
			
			var regex=  new Regex(@"/+");
			return regex.Replace(url.ToString(), "/");
		}

		public static string Join(string root, string path)
		{
			string baseUrl = VirtualPathUtility.AppendTrailingSlash(root);
			string combinedUrl = VirtualPathUtility.Combine(baseUrl, path);

			return combinedUrl;

			var rootUri = new Uri(root, UriKind.RelativeOrAbsolute);
			var pathUri = new Uri(path, UriKind.RelativeOrAbsolute);

			var result = new Uri(rootUri, pathUri);

			return result.ToString();
		}

		public static int CompareByGeneratedPositionsDeflated(MappingItemIndexed x, MappingItemIndexed y)
		{
			return CompareByGeneratedPositionsDeflated(x, y, true);
		}

		public static int CompareByGeneratedPositionsDeflated(MappingItemIndexed x, MappingItemIndexed y, bool onlyCompareGenerated)
		{
			var cmp = x.GeneratedLine - y.GeneratedLine;
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = x.GeneratedColumn - y.GeneratedColumn;
			if (cmp != 0 || onlyCompareGenerated)
			{
				return cmp;
			}

			cmp = x.Source ?? 0 - y.Source ?? 0;
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = x.OriginalLine ?? 0 - y.OriginalLine ?? 0;
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = x.OriginalColumn ?? 0 - y.OriginalColumn ?? 0;
			if (cmp != 0)
			{
				return cmp;
			}

			return x.Name ?? 0 - y.Name ?? 0;
		}

		public static int CompareByOriginalPositions(MappingItemIndexed x, MappingItemIndexed y)
		{
			return CompareByOriginalPositions(x, y, true);
		}

		public static int CompareByOriginalPositions(MappingItemIndexed x, MappingItemIndexed y, bool onlyCompareOriginal)
		{
			var cmp = x.Source ?? 0 - y.Source ?? 0;
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = x.OriginalLine ?? 0 - y.OriginalLine ?? 0;
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = x.OriginalColumn ?? 0 - y.OriginalColumn ?? 0;
			if (cmp != 0 || onlyCompareOriginal)
			{
				return cmp;
			}

			cmp = x.GeneratedColumn - y.GeneratedColumn;
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = x.GeneratedLine - y.GeneratedLine;
			if (cmp != 0)
			{
				return cmp;
			}

			return x.Name ?? 0 - y.Name ?? 0;
		}

		public static Uri UrlParse(string src)
		{
			return new Uri(src, UriKind.RelativeOrAbsolute);
		}
	}
}