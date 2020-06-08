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
	static class StringExtensions
	{
		public static IEnumerable<string> SplitIntoLines(this string s)
		{
			return s.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
		}
	
		/// <summary>
		/// Returns true if <paramref name="path"/> starts with the path <paramref name="baseDirPath"/>.
		/// The comparison is case-insensitive, handles / and \ slashes as folder separators and
		/// only matches if the base dir folder name is matched exactly ("c:\foobar\file.txt" is not a sub path of "c:\foo").
		/// </summary>
		public static bool IsSubPathOf(this string path, string baseDirPath)
		{
			if (baseDirPath.Length == 0)
				return false;

			string normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
				.WithEnding("\\"));

			string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
				.WithEnding("\\"));

			return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Returns <paramref name="str"/> with the minimal concatenation of <paramref name="ending"/> (starting from end) that
		/// results in satisfying .EndsWith(ending).
		/// </summary>
		/// <example>"hel".WithEnding("llo") returns "hello", which is the result of "hel" + "lo".</example>
		public static string WithEnding(this string str, string ending)
		{
			if (str == null)
				return ending;

			string result = str;

			// Right() is 1-indexed, so include these cases
			// * Append no characters
			// * Append up to N characters, where N is ending length
			for (int i = 0; i <= ending.Length; i++)
			{
				string tmp = result + ending.Right(i);
				if (tmp.EndsWith(ending))
					return tmp;
			}

			return result;
		}

		/// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
		/// <param name="value">The string to retrieve the substring from.</param>
		/// <param name="length">The number of characters to retrieve.</param>
		/// <returns>The substring.</returns>
		public static string Right(this string value, int length)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length", length, "Length is less than zero");
			}

			return (length < value.Length) ? value.Substring(value.Length - length) : value;
		}
	}

	class P4Resolver : Resolver
	{
		public P4Resolver(Models.SolutionModel solutionModel)
			: base(new [] { "p4", "p4-client", "p4-view" })
		{
			p4ExePath = GetP4ExePath();

			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}


		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			var solutionDir = new FileInfo(solutionFilepath).Directory;

			// we need to parse the output of "p4 clients" to find a matching directory
			try
			{
				// attempt quick lookup with p4 info within the working directory.
				// this will be useful if someone has set a .p4config in their directory
				if (ReadP4InfoQuick(solutionDir.FullName))
					return;

				// extract host name
				var info = ResolverUtils.ExecuteProcess(solutionDir.FullName, "p4.exe", "info");
				if (string.IsNullOrEmpty(info))
					return;

				var host = info
					.SplitIntoLines()
					.Select(x => Regex.Match(x, @"^Client host: (.+)"))
					.Where(r => r.Success)
					.Select(r => r.Groups[1].Value.Trim())
					.FirstOrDefault();

				if (string.IsNullOrEmpty(host))
					return;

				// extract username
				var user = ResolverUtils.ExecuteProcess(solutionDir.FullName, "p4.exe", "user -o");
				if (user == null)
					return;

				var username = user
					.Split('\n')
					.Select(x => Regex.Match(x, @"^User:\t(.+)"))
					.Where(r => r.Success)
					.Select(r => r.Groups[1].Value.Trim())
					.FirstOrDefault();

				if (string.IsNullOrEmpty(username))
					return;

				var clients = ResolverUtils.ExecuteProcess(solutionDir.FullName, "p4.exe", $"clients -u \"{username}\"");
				if (clients == null)
					return;

				// get mapping of directories -> client-names
				var clientInfos = clients
					.Split('\n')
					.Select(x => Regex.Match(x, @"Client (.+?) .+ root (.+?) '"))
					.Where(r => r.Success)
					.Select(r => ResolverUtils.ExecuteProcess(solutionDir.FullName, "p4.exe", $"client -o {r.Groups[1].Value}"))
					.ToArray();

				var dirMapping = clientInfos
					.Select(s => s.Split('\n'))
					.Where(lines => lines.Any(line => Regex.IsMatch(line, $@"Host:\t{host}")))
					.Select(lines => lines.Where(line => line.StartsWith("Client:") || line.StartsWith("Root:")))
					.ToDictionary(lines => PathAddBackslash(lines.Skip(1).First().TrimPrefix("Root:").Trim().ToLower()), lines => lines.First().TrimPrefix("Client:").Trim());

				// find if our solution-directory is inside the root directory of one of these clients
				if (!(solutionDir.FindAncestor(
					x => x.Parent,
					x => dirMapping.Keys.Contains(PathAddBackslash(x.FullName).ToLower())) is DirectoryInfo ancestor))
					return;

				p4ClientRoot = PathAddBackslash(ancestor.FullName.ToLower());

				// resolve client-name
				if (!dirMapping.TryGetValue(p4ClientRoot, out p4ClientName))
					return;

				// get Views of client-spec
				ReadP4ClientInfo(solutionDir.FullName, p4ClientName);
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
			}
		}


		//
		// quickly reads p4 info. assumes someone has set up a .p4config file
		// inside their repo
		//
		private bool ReadP4InfoQuick(string solutionPath)
		{
			var quickInfo = ResolverUtils.ExecuteProcess(solutionPath, "p4.exe", "-ztag -F \"%clientName%,%clientRoot%\" info");
			if (string.IsNullOrEmpty(quickInfo))
				return false;

			var things = quickInfo.Split(',');
			if (things.Length != 2)
				return false;

			p4ClientName = things.First().Trim();
			p4ClientRoot = things.Skip(1).First().Trim();

			// check that this client-root is an ancestor our our cwd
			if (p4ClientName == "*unknown*" || !StringExtensions.IsSubPathOf(solutionPath, p4ClientRoot))
				return false;

			// this client-name has been proved to match to our cwd
			if (!ReadP4ClientInfo(p4ClientRoot, p4ClientName))
				return false;

			return true;
		}


		private bool ReadP4ClientInfo(string solutionDir, string clientName)
		{
			try
			{
				var clientInfo = ResolverUtils.ExecuteProcess(solutionDir, "p4.exe", $"-ztag client -o {clientName}");
				p4Views = clientInfo
					.SplitIntoLines()
					.Where(x => x.StartsWith("... View"))
					.Select(x => Regex.Match(x, @"... View[0-9]+ (.+)"))
					.Where(r => r.Success)
					.Select(r => ExtractFirstView(r.Groups[1].Value))
					.ToList();
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
				return false;
			}

			return true;
		}

		private void OnAfterSolutionClosed()
		{
			p4ClientRoot = "";
			p4ClientName = "";
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


		public override bool Available => !string.IsNullOrEmpty(p4ClientRoot);

		public override string Resolve(VsState state, string tag)
		{
			ResolverUtils.ExtractTag(tag, out string t);

			switch (t)
			{
				case "p4-view": return ResolveP4View(tag);
				case "p4-client": return p4ClientName;
				case "p4-root": return p4ClientRoot;
				default: return string.Empty;
			}
		}

		private string ResolveP4View(string tag)
		{
			var view = ResolverUtils.ApplyFunction(tag, "p4-view", (bits) =>
			{
				try
				{
					int from = 0;
					int count = 1;

					if (bits.Count > 0)
						from = int.Parse(bits[0]);
					if (bits.Count > 1)
						count = int.Parse(bits[1]);

					// split view into pieces
					var joined = p4Views
						.Select(x => x.TrimPrefix("+").TrimPrefix("-").TrimPrefix("//").Split('/'))
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

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "p4": return true;
				case "p4-client": return GlobMatch(value, p4ClientName);
				case "p4-view": return p4Views.Any(v => GlobMatch(value, v));

				default: return false;
			}
		}

		private string GetP4ExePath()
		{
			// standard locations of p4 we'll append to our split-up PATH,
			// just in case the user *has* p4, but not in the PATH
			var standardLocations = new[] { "C:\\Program Files\\Perforce", "C:\\Program Files (x86)\\Perforce" };

			// search global path for git.exe
			return Environment.GetEnvironmentVariable("PATH")
				.Split(Path.PathSeparator)
				.Concat(standardLocations)
				.Select(x => Path.Combine(x, "p4.exe"))
				.FirstOrDefault(x => File.Exists(x));
		}

		private readonly string p4ExePath;
		private string p4ClientName;
		private string p4ClientRoot;
		private List<string> p4Views;
	}
}
