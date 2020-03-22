using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Shellbent.Settings;
using Shellbent.Resolvers;

namespace Shellbent.Utilities
{
	static class Parsing
	{
		public static List<SettingsTriplet> ParseYaml(string text)
		{
			List<SettingsTriplet> root;
			try
			{
				root = new YamlDotNet.Serialization.Deserializer().Deserialize<List<SettingsTriplet>>(text);
			}
			catch
			{
				root = new List<SettingsTriplet>();
			}

			return root;
		}


		public static List<SettingsTriplet> ParseLines(List<string> lines)
		{
			var triplets = new List<SettingsTriplet>();

			// split lines into groups. lines started with a dash are sub-elements
			List<List<string>> groups;
			try
			{
				groups = lines
					.Where(x => !string.IsNullOrEmpty(x))
					.Where(x => !x.StartsWith("#"))
					.Aggregate(new List<List<string>>(), (acc, x) =>
					{
						if (Regex.Match(x, "^[a-z]").Success)
							acc.Add(new List<string>());
						acc.Last().Add(x);
						return acc;
					});
			}
			catch
			{
				return triplets;
			}

			// something something
			foreach (var group in groups)
			{
				try
				{
					ParseGroup(ref triplets, group);
				}
				catch
				{
				}
			}

			triplets.Reverse();

			return triplets;
		}

		public static Tuple<string, string> ParsePredicate(string x)
		{
			var m = System.Text.RegularExpressions.Regex.Match(x, @"([a-z-]+)(\s*=~\s*(.+))?");
			if (m.Groups[3].Success)
				return Tuple.Create(m.Groups[1].Value, m.Groups[3].Value);
			else if (m.Success)
				return Tuple.Create(m.Groups[1].Value, "");
			else
				throw new InvalidOperationException(string.Format($"bad predicate: {x}"));
		}

		static void ParseGroup(ref List<SettingsTriplet> triplets, List<string> lines)
		{
			if (lines.Count == 0)
				return;

			if (lines[0].StartsWith("pattern-group"))
			{
				var triplet = new SettingsTriplet();
				ParsePatternGroup(triplet, lines[0]);

				string item = null, solution = null, document = null;
				Color? maybeColor = null;
				foreach (var line in lines.Skip(1))
				{
					ParsePattern(ref item, ref solution, ref document, ref maybeColor, line);
				}

				// solution/document override item
				solution = solution ?? item ?? "";
				document = document ?? item ?? "";

				var drawColor = maybeColor.NullOr(c => Color.FromRgb(c.R, c.G, c.B));

				triplet.FormatIfNothingOpened = new TitleBarFormat(null, drawColor);
				triplet.FormatIfDocumentOpened = new TitleBarFormat(document, drawColor);
				triplet.FormatIfSolutionOpened = new TitleBarFormat(solution, drawColor);
				triplets.Add(triplet);
			}
		}

		static void ParsePatternGroup(SettingsTriplet triplet, string line)
		{
			var m = Regex.Match(line, "^pattern-group(\\[(.+)\\])?\\s*:");
			if (m.Success)
			{
				if (m.Groups.Count > 1)
				{
					var filter = m.Groups[2].Value;
					ParseFilter(triplet, filter);
				}
			}
		}

		static void ParseSourceControlFilter(SettingsTriplet triplet, string filterString)
		{
			var filters = filterString
				.Split(new char[] { ',' })
				.Select(x => x.Trim())
				.Where(x => !string.IsNullOrEmpty(x))
				.Select(x =>
				{
					var m = Regex.Match(x, @"([a-z-]+)\s*=~\s*(.+)");
					if (m.Groups.Count == 2)
						return Tuple.Create(m.Groups[1].Value, "");
					else if (m.Groups.Count == 3)
						return Tuple.Create(m.Groups[1].Value, m.Groups[2].Value);
					else
						throw new InvalidDataException(string.Format($"bad predicate: {x}"));
				});

			triplet.PatternDependencies.Concat(filters);
		}

		static void ParseItemNameFilter(SettingsTriplet triplet, string filterString)
		{
			triplet.PatternDependencies.Add(Tuple.Create("item-name", filterString));
		}


		static void ParseFilter(SettingsTriplet triplet, string filterString)
		{
			var filters = filterString
				.Split(new char[] { ',' })
				.Select(x => x.Trim());

			foreach (var filter in filters)
			{
				if (RegexMatch(out Match match, filter, @"([a-z-]+)\s*=~\s*(.+)"))
				{
					var tag = match.Groups[1].Value;
					var expr = match.Groups[2].Value;

					triplet.PatternDependencies.Add(Tuple.Create(tag, expr));
				}
				else if (!string.IsNullOrEmpty(filter))
				{
					triplet.PatternDependencies.Add(Tuple.Create(filter, ""));
				}
			}
		}

		static void ParsePattern(ref string item, ref string solution, ref string document, ref Color? color, string line)
		{
			if (RegexMatch(out Match matchItem, line, "\\s*-\\s+item-opened: (.+)"))
				item = matchItem.Groups[1].Value;
			else if (RegexMatch(out Match matchSolution, line, "\\s*-\\s+solution-opened: (.+)"))
				solution = matchSolution.Groups[1].Value;
			else if (RegexMatch(out Match matchDocument, line, "\\s*-\\s+document-opened: (.+)"))
				document = matchDocument.Groups[1].Value;
			else if (RegexMatch(out Match matchC, line, "\\s*-\\s+color: (.+)"))
			{
				try
				{
					color = (Color)ColorConverter.ConvertFromString(matchC.Groups[1].Value);
				}
				catch
				{
					color = null;
				}
			}
		}

		private static bool RegexMatch(out Match match, string input, string pattern)
		{
			match = Regex.Match(input, pattern);
			return match.Success;
		}


		public static bool ParseFormatString(out string transformed, VsState state, string pattern)
		{
			int i = 0;
			return ParseImpl(out transformed, state, pattern, ref i, null);
		}

		private static bool ParseImpl(out string transformed, VsState state, string pattern, ref int i, string singleDollar)
		{
			transformed = "";

			// begin pattern parsing
			while (i < pattern.Length)
			{
				// escape sequences
				if (pattern[i] == '\\')
				{
					++i;
					if (i == pattern.Length)
						break;
					transformed += pattern[i];
					++i;
				}
				// predicates
				else if (pattern[i] == '?' && ParseQuestion(out string r, state, pattern, ref i, singleDollar))
				{
					transformed += r;
				}
				// dollars
				else if (pattern[i] == '$' && ParseDollar(out string r2, state, pattern, ref i, singleDollar))
				{
					transformed += r2;
				}
				else
				{
					transformed += pattern[i];
					++i;
				}
			}

			return true;
		}


		private static bool ParseQuestion(out string result, VsState state, string pattern, ref int i, string singleDollar)
		{
			var tag = new string(pattern
				.Substring(i + 1)
				.TakeWhile(x => x >= 'a' && x <= 'z' || x == '-')
				.ToArray());

			i += 1 + tag.Length;

			bool valid = state.Resolvers
				.FirstOrDefault(x => x.Applicable(tag))
				?.ResolveBoolean(state, tag) ?? false;

			if (i == pattern.Length)
			{
				result = null;
				return valid;
			}

			// look for braced group {....}, and skip if question was bad
			if (pattern[i] == '{')
			{
				if (!valid)
				{
					while (i != pattern.Length)
					{
						++i;
						if (pattern[i] == '}')
						{
							++i;
							break;
						}
					}

					result = null;
					return false;
				}
				else
				{
					var transformed_tag = state.Resolvers
						.FirstOrDefault(x => x.Applicable(tag))
						?.Resolve(state, tag);

					var inner = new string(pattern
						.Substring(i + 1)
						.TakeWhile(x => x != '}')
						.ToArray());

					i += 1 + inner.Length + 1;

					int j = 0;
					ParseImpl(out result, state, inner, ref j, transformed_tag);
				}
			}
			else
			{
				result = null;
				return false;
			}

			return true;
		}

		// we support two common methods of string escaping: parens and identifier
		//
		// any pattern that contains a $ will either be immeidately followed with an identifier,
		// or a braced expression, e.g., $git-branch, or ${git-branch}
		//
		// the identifier may be a function-call, like "$path(0, 2)"
		//
		private static bool ParseDollar(out string result, VsState state, string pattern, ref int i, string singleDollar)
		{
			++i;

			// peek for brace vs non-brace
			//
			// find EOF or whitespace or number
			if (i == pattern.Length || char.IsWhiteSpace(pattern[i]) || char.IsNumber(pattern[i]))
			{
				++i;
				result = singleDollar ?? "";
				return true;
			}
			// find brace
			else if (pattern[i] == '{')
			{
				var braceExpr = new string(pattern
					.Substring(i + 1)
					.TakeWhile(x => x != '}')
					.ToArray());

				i += 1 + braceExpr.Length;
				if (i != pattern.Length && pattern[i] == '}')
					++i;

				// maybe:
				//  - split by whitespace
				//  - attempt to resolve all
				//  - join together
				result = braceExpr.Split(' ')
					.Select(x =>
					{
						return state.Resolvers
						.FirstOrDefault(r => r.Applicable(x))
						?.Resolve(state, x) ?? x;
					})
					.Aggregate((a, b) => a + " " + b);

			}
			// find identifier
			else if (pattern[i] >= 'a' && pattern[i] <= 'z')
			{
				var idenExpr = new string(pattern
					.Substring(i)
					.TakeWhile(x => x >= 'a' && x <= 'z' || x == '-')
					.ToArray());

				i += idenExpr.Length;

				if (i != pattern.Length)
				{
					if (pattern[i] == '(')
					{
						var argExpr = new string(pattern
							.Substring(i)
							.TakeWhile(x => x != ')')
							.ToArray());

						i += argExpr.Length;
						if (i != pattern.Length && pattern[i] == ')')
						{
							++i;
							argExpr += ')';
						}

						idenExpr += argExpr;
					}
				}

				result = state.Resolvers
					.FirstOrDefault(x => x.Applicable(idenExpr))
					?.Resolve(state, idenExpr)
					?? idenExpr;
			}
			else
			{
				result = "";
			}

			return true;
		}

	}
}
