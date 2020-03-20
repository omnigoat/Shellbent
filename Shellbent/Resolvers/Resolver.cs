using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shellbent.Resolvers
{
	public struct VsState
	{
		public IEnumerable<Resolver> Resolvers;
		public dbgDebugMode Mode;
		public Solution Solution;
	}

	public static class SplitFunction
	{
		public static string Parse(string functionName, char splitDelim, string tag, string data)
		{
			var m = Regex.Match(tag, $"{functionName}\\(([0-9]+), ([0-9]+)\\)");
			if (m.Success)
			{
				var arg1 = int.Parse(m.Groups[1].Value);
				var arg2 = int.Parse(m.Groups[2].Value);

				return data.Split(splitDelim)
					.Reverse()
					.Skip(arg1)
					.Take(arg2)
					.Reverse()
					.Aggregate((a, b) => a + splitDelim + b);
			}

			return "";
		}
	}

	public abstract class Resolver
	{
		protected Resolver(IEnumerable<string> tags)
		{
			m_Tags = tags.ToList();
		}

		public abstract bool Available { get; }

		public delegate void ChangedDelegate(Resolver resolver);
		public abstract ChangedDelegate Changed { get; set; }

		public bool Applicable(string tag)
		{
			return m_Tags.Contains(new string(tag.TakeWhile(x => char.IsLetter(x) || x == '-').ToArray()));
		}

		public abstract bool ResolveBoolean(VsState state, string tag);
		public abstract string Resolve(VsState state, string tag);

		public virtual bool SatisfiesDependency(Tuple<string, string> d)
		{
			return false;
		}

		protected static bool GlobMatch(string pattern, string match)
		{
			return string.IsNullOrEmpty(pattern) || new Regex(
				"^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
				RegexOptions.IgnoreCase | RegexOptions.Singleline).IsMatch(match);
		}

		private readonly List<string> m_Tags;
	}
}
