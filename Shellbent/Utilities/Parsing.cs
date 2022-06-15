using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shellbent.Settings;
using Shellbent.Resolvers;

namespace Shellbent.Utilities
{
	static class Parsing
	{
		public static List<TitleBarSetting> ParseYaml(string text)
		{
			List<TitleBarSetting> root;
			try
			{
				root = new YamlDotNet.Serialization.Deserializer().Deserialize<List<TitleBarSetting>>(text);
			}
			catch
			{
				root = new List<TitleBarSetting>();
			}

			return root;
		}


		public static Tuple<string, string> ParsePredicate(string x)
		{
			var m = Regex.Match(x, @"([a-z0-9-]+)(\s*=~\s*(.+))?");

			if (m.Groups[3].Success)
				return Tuple.Create(m.Groups[1].Value, m.Groups[3].Value);
			else if (m.Success)
				return Tuple.Create(m.Groups[1].Value, "");
			else
				throw new InvalidOperationException(string.Format($"bad predicate: {x}"));
		}

		public static string ParseFormatString(VsState state, string pattern)
		{
			if (pattern == null)
				return string.Empty;

			try
			{
				int i = 0;
				if (ParseImpl(out string transformed, state, pattern, ref i, null))
					return transformed;
			}
			catch (Exception e)
			{
				System.Console.WriteLine("Exception: " + e.Message);
			}

			return string.Empty;
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
				else if (pattern[i] == '?')
				{
					if (ParseQuestion(state, pattern.Substring(i), singleDollar, out string r, out int advance))
						transformed += r;
					i += advance;
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



		// takes an expression like ?git-ahead{...}
		private static bool ParseQuestion(VsState state, string pattern, string singleDollar, out string result, out int outAdvance)
		{
			if (!ResolverUtils.ExtractTag(pattern, out string tag))
			{
				outAdvance = 0;
				result = null;
				return false;
			}

			outAdvance = 1 + tag.Length;

			var transformed_tag = state.Resolvers
				.FirstOrDefault(x => x.Resolvable(state, tag))
				?.Resolve(state, tag);

			// look for braced group {....}, and skip if question was bad
			if (outAdvance != pattern.Length && pattern[outAdvance] == '{')
			{
				if (string.IsNullOrEmpty(transformed_tag))
				{
					ParseScopedBracesAdvance(pattern.Substring(outAdvance), out string _, out int advance);
					outAdvance += advance;

					result = null;
					return false;
				}
				else
				{
					bool validInnerExpr = ParseScopedBracesAdvance(pattern.Substring(outAdvance), out string innerExpr, out int advance);
					outAdvance += advance;

					if (!validInnerExpr || string.IsNullOrEmpty(transformed_tag))
					{
						result = null;
						return false;
					}
					else
					{
						int j = 0;
						ParseImpl(out result, state, innerExpr, ref j, transformed_tag);
					}
				}
			}
			// no braced group, this predicate is wholly about substittion at the point of the tag
			else
			{
				result = transformed_tag;
				return false;
			}

			return true;
		}

		private static bool ParseScopedBracesAdvance(string pattern, out string innerExpr, out int advance)
		{
			advance = 0;

			int scopes = 0;
			for (; advance != pattern.Length; ++advance)
			{
				if (advance == pattern.Length)
				{
					innerExpr = "";
					return false;
				}

				if (pattern[advance] == '{')
				{
					++scopes;
				}
				else if (pattern[advance] == '}' && --scopes == 0)
				{
					++advance;
					break;
				}
			}

			innerExpr = pattern.Substring(1, advance - 2);

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
			if (i == pattern.Length)
			{
				result = singleDollar ?? "";
			}
			if (char.IsWhiteSpace(pattern[i]) || char.IsNumber(pattern[i]))
			{
				result = singleDollar ?? "";
				return true;
			}
			// find brace
			else if (pattern[i] == '{')
			{
				if (ParseScopedBracesAdvance(pattern.Substring(i), out string innerExpr, out int advance))
				{
					i += advance;
				}
				else
				{
					result = "";
					return false;
				}

				// maybe:
				//  - split by whitespace
				//  - attempt to resolve all
				//  - join together
				try
				{
					result = innerExpr.Split(' ')
						.Select(x =>
						{
							var resolver = state.Resolvers.FirstOrDefault(r => r.Applicable(x));
							if (resolver != null)
							{
								var resolveResult = resolver.Resolve(state, x);
								if (!string.IsNullOrEmpty(resolveResult))
									return resolveResult;
								else
									throw new InvalidOperationException("not a real exception lmao.");
							}

							return x;
						})
						.Aggregate((a, b) => a + " " + b);
				}
				catch (Exception e)
				{
					result = "";
				}
			}

			// find identifier
			else if (ExtensionMethods.RegexMatches(pattern.Substring(i), @"[a-z][a-z0-9-]*", out Match m))
			{
				var idenExpr = m.Groups[0].Value;

				i += idenExpr.Length;

				if (i != pattern.Length && pattern[i] == '(')
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
