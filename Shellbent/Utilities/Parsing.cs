﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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


		public static Tuple<string, string> ParsePredicate(string x)
		{
			var m = Regex.Match(x, @"([a-z-]+)(\s*=~\s*(.+))?");
			if (m.Groups[3].Success)
				return Tuple.Create(m.Groups[1].Value, m.Groups[3].Value);
			else if (m.Success)
				return Tuple.Create(m.Groups[1].Value, "");
			else
				throw new InvalidOperationException(string.Format($"bad predicate: {x}"));
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
