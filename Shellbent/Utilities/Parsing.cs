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
				if (ParseImpl(state, pattern, null, out string transformed, out int advance))
					return transformed;
			}
			catch (Exception e)
			{
				System.Console.WriteLine("Exception: " + e.Message);
			}

			return string.Empty;
		}

		private static bool ParseImpl(VsState state, string pattern, string singleDollar, out string transformed, out int advance)
		{
			transformed = "";
			advance = 0;

			bool inBraceScopeForText = false;

			// begin pattern parsing
			while (advance < pattern.Length)
			{
				// escape sequences
				if (pattern[advance] == '\\')
				{
					++advance;
					if (advance == pattern.Length)
						break;
					transformed += pattern[advance];
					++advance;
				}
				// predicates
				else if (pattern[advance] == '?')
				{
					if (ParseQuestion(state, pattern.Substring(advance), singleDollar, out string r, out int predicateAdvance))
						transformed += r;
					advance += predicateAdvance;
				}
				// dollars
				else if (pattern[advance] == '$' && ParseDollar(out string r2, state, pattern, ref advance, singleDollar))
				{
					transformed += r2;
				}
#if false
				else if (pattern[i] == '{')
				{
					inBraceScopeForText = true;
					
					if (++i == pattern.Length)
						break;
					
					if (pattern[i] == '{')
					{
						transformed += '{';
						inBraceScopeForText = false;
					}
					
				}
#endif
				else if (inBraceScopeForText)
				{
					transformed += pattern[advance];
					++advance;
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
					FindMatchingBracesScope(pattern.Substring(outAdvance), out string _, out int advance);
					outAdvance += advance;

					result = null;
					return false;
				}
				else
				{
					bool validInnerExpr = FindMatchingBracesScope(pattern.Substring(outAdvance), out string innerExpr, out int advance);
					outAdvance += advance;

					if (!validInnerExpr || string.IsNullOrEmpty(transformed_tag))
					{
						result = null;
						return false;
					}
					else
					{
						ParseImpl(state, innerExpr, transformed_tag, out result, out int _);
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

		private static bool FindMatchingBracesScope(string pattern, out string innerExpr, out int advance)
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
					// only increase the scope when it's not "{{", which is the escaped brace
					if (advance + 1 == pattern.Length)
						continue;
					else if (pattern[advance + 1] != '{')
						++scopes;
					else
						++advance;
				}
				else if (pattern[advance] == '}')
				{
					if (advance + 1 == pattern.Length)
					{
						continue;
					}
					else if (pattern[advance + 1] != '}')
					{
						if (--scopes == 0)
							break;
					}
					else
					{
						++advance;
					}
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
				if (FindMatchingBracesScope(pattern.Substring(i), out string innerExpr, out int advance))
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
