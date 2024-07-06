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

		private struct ParsingState
		{
			public ParsingState(VsState state, string enclosingTag, bool consuming)
			{
				this.vsState = state;
				this.enclosingTag = enclosingTag;
				this.consuming = consuming;
			}

			public VsState vsState;
			public string enclosingTag;
			public bool consuming;
		}

		public static string ParseFormatString(VsState state, string pattern)
		{
			if (pattern == null)
				return null;

			try
			{
				ParsingState parsingState;
				parsingState.vsState = state;
				parsingState.consuming = true;
				parsingState.enclosingTag = null;

				if (pattern.StartsWith("return"))
				{
					pattern = pattern.Substring("return".Length);
					parsingState.consuming = false;
				}
				
				pattern = pattern.Trim();

				if (ParseImpl(parsingState, pattern, out string transformed, out int advance))
					return transformed;
			}
			catch (Exception e)
			{
				System.Console.WriteLine("Exception: " + e.Message);
			}

			return null;
		}

		private static bool ParseImpl(ParsingState state, string pattern, out string transformed, out int advance)
		{
			transformed = "";
			advance = 0;

			bool inBraceScopeForText = state.consuming;
			int braceScopes = 0;

			// if we start with an open-brace, then let's only parse until we close
			bool endAtScopeClose = pattern.First() == '{';


			// begin pattern parsing
			while (advance < pattern.Length)
			{
				if (pattern[advance] == '"' && !inBraceScopeForText)
				{
					inBraceScopeForText = true;
					++advance;
				}
				else if (pattern[advance] == '"' && inBraceScopeForText)
				{
					inBraceScopeForText = false;
					++advance;
				}
				// escape sequence
				else if (inBraceScopeForText && pattern[advance] == '\\')
				{
					++advance;

					if (advance == pattern.Length)
					{
						break;
					}
					else if (pattern[advance] == '"')
					{
						transformed += "\"";
						++advance;
					}
					else if (pattern[advance] == 'n')
					{
						transformed += "\n";
						++advance;
					}
					else
					{
						transformed += pattern[advance];
						++advance;
					}
				}
				else if (pattern[advance] == '{' && !inBraceScopeForText)
				{
					++advance;
					++braceScopes;
				}
				else if (pattern[advance] == '}' && !inBraceScopeForText)
				{
					++advance;
					if (--braceScopes == 0)
						break;
				}
				// in string - we can expand
				else if (inBraceScopeForText)
				{
					if (ParseIdentifier(state, pattern.Substring(advance), out string transformedExpr, out int innerAdvance))
					{
						advance += innerAdvance;
						transformed += transformedExpr;
					}
					else
					{
						transformed += pattern[advance];
						++advance;
					}
				}
				else if (ParseIfStatement(state, pattern.Substring(advance), out string resultText, out int textAdvance))
				{
					advance += textAdvance;
					transformed += resultText;
				}
#if false
				// escape sequences
				if (pattern[advance] == '\\')
				{
					++advance;
					if (advance == pattern.Length)
						break;
					transformed += pattern[advance];
					++advance;
				}
				// keywords ('if')
				else if (pattern[advance] == 'i' && ParseIfStatement(state, pattern.Substring(advance), out string resultExpression, out int resultAdvance))
				{
					transformed += resultExpression;
					advance += resultAdvance;
				}
				// identifiers
				else if (pattern[advance] == '$' && ParseIdentifier(state, pattern.Substring(advance), out string r2, out int innerAdvance))
				{
					transformed += r2;
					advance += innerAdvance;
				}
				else if (pattern[advance] == '{')
				{
					// peek for brace escape
					if (++advance == pattern.Length)
					{
						break;
					}
					else if (pattern[advance] == '{')
					{
						if (inBraceScopeForText)
						{
							transformed += '{';
							++advance;
						}
					}
					else
					{
						inBraceScopeForText = true;
					}
				}
				else if (pattern[advance] == '}')
				{
					// peek for brace escape
					if (++advance == pattern.Length)
					{
						break;
					}
					else if (pattern[advance] == '}')
					{
						if (inBraceScopeForText)
						{
							transformed += '}';
							++advance;
						}
					}
					else
					{
						inBraceScopeForText = false;
					}
				}
				else if (inBraceScopeForText)
				{
					transformed += pattern[advance];
					++advance;
				}
#endif
				else
				{
					++advance;
				}
			}

			return true;
		}




		private static void ConsumeWhitespace(ref string expr, ref int advance)
		{
			while (expr.First() == ' ' || expr.First() == '\t' || expr.First() == '\f' || expr.First() == '\n' || expr.First() == '\r')
			{
				++advance;
				expr = expr.Substring(1);
			}
		}

		private static bool ParseIfStatement(ParsingState state, string expr, out string result, out int resultAdvance)
		{
			result = "";
			resultAdvance = 0;

			if (!expr.StartsWith("if"))
				return false;
			else
			{
				expr = expr.Substring(2);
				resultAdvance += 2;
			}

			// skip whitespace between 'if' and opening bracket
			ConsumeWhitespace(ref expr, ref resultAdvance);

			// opening brace
			if (expr.First() != '(')
			{
				return false;
			}
			else
			{
				++resultAdvance;
				expr = expr.Substring(1);
			}

			ConsumeWhitespace(ref expr, ref resultAdvance);

			// predicate is currently only allowed to be an identifier
			if (!ParseIdentifier(state, expr, out string identifier, out int identifierAdvance))
				return false;
			else
				expr = expr.Substring(identifierAdvance);

			resultAdvance += identifierAdvance;

			ConsumeWhitespace(ref expr, ref resultAdvance);

			if (expr.First() != ')')
			{
				return false;
			}
			else
			{
				++resultAdvance;
				expr = expr.Substring(1);
			}

			// if we failed our predicate, we need to skip past the body
			if (string.IsNullOrEmpty(identifier))
			{
				FindMatchingBracesScope(expr, out int ifBodyAdvance);
				resultAdvance += ifBodyAdvance;

				// if there's an else-clause, parse it
				expr = expr.Substring(ifBodyAdvance);
				ConsumeWhitespace(ref expr, ref resultAdvance);

				if (expr.StartsWith("else"))
				{
					resultAdvance += 4;
					expr = expr.Substring(4);

					ConsumeWhitespace(ref expr, ref resultAdvance);

					ParsingState substate = new ParsingState(state.vsState, state.enclosingTag, false);
					if (!ParseImpl(substate, expr, out string transformed, out int bodyAdvance))
						return false;

					result += transformed;
					resultAdvance += bodyAdvance;
				}

				return true;
			}
			else
			{
				ConsumeWhitespace(ref expr, ref resultAdvance);

				// parse the true body
				{
					ParsingState substate = new ParsingState(state.vsState, state.enclosingTag, false);
					if (!ParseImpl(substate, expr, out string transformed, out int bodyAdvance))
						return false;

					result += transformed;
					resultAdvance += bodyAdvance;
					expr = expr.Substring(bodyAdvance);
				}

				ConsumeWhitespace(ref expr, ref resultAdvance);

				// skip any else clause
				if (expr.StartsWith("else"))
				{
					resultAdvance += 4;
					expr = expr.Substring(4);

					ConsumeWhitespace(ref expr, ref resultAdvance);

					FindMatchingBracesScope(expr, out int ifBodyAdvance);
					resultAdvance += ifBodyAdvance;
				}
			}

			return true;
		}

		


		private static bool FindMatchingBracesScope(string pattern, out int advance)
		{
			advance = 0;
			int scopes = 0;
			bool inString = false;

			for (; advance != pattern.Length; ++advance)
			{
				if (advance == pattern.Length)
				{
					return false;
				}
				else if (pattern[advance] == '\\' && inString)
				{
					if (advance + 1 == pattern.Length)
						return false;
					else if (pattern[advance + 1] == '"')
						++advance;
				}
				else if (pattern[advance] == '"')
				{
					inString = !inString;
				}
				else if (pattern[advance] == '{' && !inString)
				{
					++scopes;
				}
				else if (pattern[advance] == '}' && !inString)
				{
					if (--scopes == 0)
					{
						++advance;
						break;
					}
				}
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
		private static bool ParseIdentifier(ParsingState state, string pattern, out string result, out int advance)
		{
			result = "";

			if (pattern.First() != '$')
			{
				advance = 0;
				return false;
			}
			else
			{
				advance = 1;
			}

			if (advance == pattern.Length)
			{
				result = state.enclosingTag ?? "";
			}
			else if (char.IsWhiteSpace(pattern[advance]) || char.IsNumber(pattern[advance]))
			{
				result = state.enclosingTag ?? "";
			}
			// automatic expansion for everything inside braces
			else if (pattern[advance] == '{')
			{
#if false
				if (FindMatchingBracesScope(pattern.Substring(advance), out string innerExpr, out int innerAdvance))
				{
					advance += innerAdvance;
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
							var resolver = state.vsState.Resolvers.FirstOrDefault(r => r.Applicable(x));
							if (resolver != null)
							{
								var resolveResult = resolver.Resolve(state.vsState, x);
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
#endif
			}
			// find identifier
			else if (ExtensionMethods.RegexMatches(pattern.Substring(advance), @"\G([a-z][a-z0-9-]*)([!?])?", out Match m))
			{
				var idenExpr = m.Groups[0].Value;

				advance += idenExpr.Length;

				var identifier = m.Groups[1].Value;

				var transformedTag = state.vsState.Resolvers
					.FirstOrDefault(x => x.Applicable(identifier))
					?.Resolve(state.vsState, identifier);

				// default case: the transformed-tag, or the unresolved version
				result = transformedTag;

				if (advance == pattern.Length)
				{
					// nop
				}
				// predicate case
				else if (m.Groups[2].Success)
				{
					bool trueComparison = m.Groups[2].Value == "?";

					if (!FindMatchingBracesScope(pattern.Substring(advance), out int innerAdvance))
						return false;

					// predicate failed, skip braces
					if (string.IsNullOrEmpty(transformedTag) == trueComparison)
					{
						advance += innerAdvance;
						result = "";
					}
					// predicate succeeded, parse inner scope as normal (with updated '$' identifier)
					else if (innerAdvance >= 2)
					{
						// make sure we only parse inbetween (and _not_ including) the braces
						ParsingState substate = new ParsingState(state.vsState, transformedTag, state.consuming);
						ParseImpl(substate, pattern.Substring(advance + 1, innerAdvance - 2), out result, out int _);
						advance += innerAdvance;
					}
					else
					{
						return false;
					}
				}
				// function-call
				else if (pattern[advance] == '(')
				{
					var argExpr = new string(pattern
						.Substring(advance)
						.TakeWhile(x => x != ')')
						.ToArray());

					advance += argExpr.Length;
					if (advance != pattern.Length && pattern[advance] == ')')
					{
						++advance;
						argExpr += ')';
					}

					idenExpr += argExpr;

					// update result with full resolution w/ function call
					result = state.vsState.Resolvers
						.FirstOrDefault(x => x.Applicable(idenExpr))
						?.Resolve(state.vsState, idenExpr);
				}
			}
			else
			{
				result = state.enclosingTag;
			}

			return true;
		}
	}
}
