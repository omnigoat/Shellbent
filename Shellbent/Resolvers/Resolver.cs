using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Shellbent.Utilities;
using Microsoft.VisualStudio.Shell;

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


		public static R ApplyFunction<R>(string input, string functionName, Func<List<string>, R> func)
		{
			try
			{
				if (input.StartsWith(functionName))
					input = input.TrimPrefix(functionName);

				var m = Regex.Match(input, @"\(\s*([a-z0-9-/*.]+)(\s*,\s*([a-z0-9-/*.]+))*\s*\)");
				if (m.Success)
				{
					List<string> r = new List<string>
					{
						m.Groups[1].Value
					};

					r.AddRange(m.Groups[3].Captures
						.OfType<Capture>()
						.Select(x => x.Value));

					return func(r);
				}
			}
			finally
			{
			}

			return default;
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
			return m_Tags.Any(x => ExtensionMethods.RegexMatches(tag, @"[a-z][a-z0-9-]*", out Match m) && m.Groups[0].Value == x);
		}

		public abstract bool ResolveBoolean(VsState state, string tag);
		public abstract string Resolve(VsState state, string tag);

		public virtual bool SatisfiesDependency(Tuple<string, string> d)
		{
			return false;
		}

		protected static bool GlobMatch(string pattern, string match)
		{
			return string.IsNullOrEmpty(pattern) || Regex.IsMatch(match,
				"^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
				RegexOptions.IgnoreCase | RegexOptions.Singleline);
		}

		private readonly List<string> m_Tags;
	}

	internal static class ResolverUtils
	{
		// GetAllParentDirectories
		public static IEnumerable<DirectoryInfo> GetAllParentDirectories(DirectoryInfo directoryToScan)
		{
			Stack<DirectoryInfo> ret = new Stack<DirectoryInfo>();
			GetAllParentDirectories(ref ret, directoryToScan);
			return ret;
		}

		private static void GetAllParentDirectories(ref Stack<DirectoryInfo> directories, DirectoryInfo directoryToScan)
		{
			if (directoryToScan == null || directoryToScan.Name == directoryToScan.Root.Name)
				return;

			directories.Push(directoryToScan);
			GetAllParentDirectories(ref directories, directoryToScan.Parent);
		}

		// thing
		private const int ProcessTimeout = 5000;

		public static string ExecuteProcess(string exeName, string arguments)
		{
			using (var process = new System.Diagnostics.Process
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = exeName,
					Arguments = arguments,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				}
			})
			{
				StringBuilder output = new StringBuilder();
				StringBuilder error = new StringBuilder();

				using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
				{
					process.OutputDataReceived += (sender, e) => {
						if (e.Data == null)
							outputWaitHandle.Set();
						else
							output.AppendLine(e.Data);
					};
					process.ErrorDataReceived += (sender, e) =>
					{
						if (e.Data == null)
							errorWaitHandle.Set();
						else
							error.AppendLine(e.Data);
					};

					process.Start();

					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					if (process.WaitForExit(ProcessTimeout) &&
						outputWaitHandle.WaitOne(ProcessTimeout) &&
						errorWaitHandle.WaitOne(ProcessTimeout))
					{
						return output.ToString();
					}
					else
					{
						return null;
					}
				}
			}
		}
	}
}
