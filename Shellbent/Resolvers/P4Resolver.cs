using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Shellbent.Resolvers
{
	class P4Resolver : Resolver
	{
		public P4Resolver(Models.SolutionModel solutionModel)
			: base(new [] { "p4", "p4-client", "p4-view" })
		{
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			var solutionDir = new FileInfo(solutionFilepath).Directory;

			// we need to parse the output of "p4 clients" to find a matching directory
			try
			{
				var clients = ResolverUtils.ExecuteProcess("p4.exe", "clients");
				if (clients == null)
					return;

				// get mapping of directories -> client-names
				var dirMapping = clients
					.Split(new[] { '\n' })
					.Select(x => Regex.Match(x, @"Client (.+?) .+ root (.+?) '"))
					.Where(r => r.Success)
					.Select(r => Tuple.Create(r.Groups[1].Value, PathAddBackslash(r.Groups[2].Value)))
					.ToDictionary(x => x.Item2, x => x.Item1);

				// find if our solution-directory is inside the root directory of one of these clients
				if (!(solutionDir.FindAncestor(
					x => x.Parent,
					x => dirMapping.Keys.Contains(PathAddBackslash(x.FullName))) is DirectoryInfo ancestor))
					return;

				p4Path = PathAddBackslash(ancestor.FullName);

				// resolve client-name
				if (!dirMapping.TryGetValue(p4Path, out p4Client))
					return;

				// get Views of client-spec
				var clientInfo = ResolverUtils.ExecuteProcess("p4.exe", string.Format("client -o {0}", p4Client));
				p4Views = clientInfo
					.Split(new[] { '\n' })
					.SkipWhile(x => !x.StartsWith("View:"))
					.Skip(1)
					.TakeWhile(x => x.StartsWith("\t"))
					.Select(ExtractFirstView)
					.ToList();
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
			}
		}

		private void OnAfterSolutionClosed()
		{
			p4Path = "";
			p4Client = "";
			p4Views = new List<string>();
		}

		private string ExtractFirstView(string str)
		{
			string result = "";
			str = str.Trim();

			// include +/-
			if (str.StartsWith("-") || str.StartsWith("+"))
			{
				result += str.First();
				str = str.Substring(1);
			}

			// quote is easy, just take everything inside quotes
			if (str.StartsWith("\""))
			{
				result += str.Skip(1).TakeWhile(x => x != '\"').ToString();
			}
			else
			{
				result += new string(str.TakeWhile(x => !string.IsNullOrWhiteSpace(x.ToString())).ToArray());
			}

			return result;
		}

		// mad theft from StackOverflow
		private string PathAddBackslash(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));

			path = path.TrimEnd();

			if (PathEndsWithDirectorySeparator())
				return path;

			return path + GetDirectorySeparatorUsedInPath();

			bool PathEndsWithDirectorySeparator()
			{
				if (path.Length == 0)
					return false;

				char lastChar = path[path.Length - 1];
				return lastChar == Path.DirectorySeparatorChar
					|| lastChar == Path.AltDirectorySeparatorChar;
			}

			char GetDirectorySeparatorUsedInPath()
			{
				if (path.Contains(Path.AltDirectorySeparatorChar))
					return Path.AltDirectorySeparatorChar;

				return Path.DirectorySeparatorChar;
			}
		}


		public override bool Available => !string.IsNullOrEmpty(p4Path);

		public override ChangedDelegate Changed { get; set; }

		public override string Resolve(VsState state, string tag)
		{
			if (tag.StartsWith("p4-view"))
			{
				var view = SplitFunction.ApplyFunction(tag, "p4-view", (bits) =>
				{
					int from = 0;
					int count = 1;
					string regex = "";

					try
					{
						if (bits.Count > 0)
							from = int.Parse(bits[0]);
						if (bits.Count > 1)
							count = int.Parse(bits[1]);

						var filteredViews = string.IsNullOrEmpty(regex)
							? p4Views
							: p4Views.Where(x => Regex.IsMatch(x, regex));

						// split view into pieces
						var joined = filteredViews
							.Select(x => x.TrimPrefix("+").TrimPrefix("-").TrimPrefix("//").Split('/'))
							.Where(x => x.Length >= (from + count))
							.Select(x => x.Skip(from).Take(count))
							.Select(x => string.Join("/", x))
							.FirstOrDefault();

						return joined;
					}
					catch (Exception)
					{
					}

					return default;

				});

				if (string.IsNullOrEmpty(view))
					return null;
				else
					return view;
			}
			else if (tag == "p4-client")
			{
				return p4Client;
			}
			else
			{
				return default;
			}
		}

		public override bool ResolveBoolean(VsState state, string tag)
		{
			return tag == "p4";
		}

		public override bool SatisfiesDependency(Tuple<string, string> d)
		{
			if (!Available)
				return false;

			switch (d.Item1)
			{
				case "p4": return Available;
				case "p4-client": return GlobMatch(d.Item2, p4Client);
				case "p4-view": return p4Views.Any(v => GlobMatch(d.Item2, v));

				default: return false;
			}
		}

		private string p4Path;
		private string p4Client;
		private List<string> p4Views;
	}
}
