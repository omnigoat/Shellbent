using Shellbent.Settings;
using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shellbent.Resolvers
{
	class VsrResolver : Resolver
	{
		public static VsrResolver Create(Models.SolutionModel solutionModel)
		{
			return new VsrResolver(solutionModel);
		}

		public VsrResolver(Models.SolutionModel solutionModel)
			: base(new[] { "vsr", "vsr-branch", "vsr-sha" })
		{
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available => vsrPath != null;

		public override ChangedDelegate Changed { get; set; }

		public override bool ResolveBoolean(VsState state, string tag)
		{
			return tag == "vsr";
		}

		public override string Resolve(VsState state, string tag)
		{
			if (tag == "vsr-branch")
				return vsrBranch;
			else if (tag == "vsr-sha")
				return vsrSHA;
			else
				return "";
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			var solutionDir = new FileInfo(solutionFilepath).Directory;

			vsrPath = GetAllParentDirectories(solutionDir)
				.SelectMany(x => x.GetDirectories())
				.FirstOrDefault(x => x.Name == ".versionr")?.FullName;

			if (vsrPath == null)
				return;

			watcher = new FileSystemWatcher(vsrPath)
			{
				IncludeSubdirectories = true
			};

			watcher.Changed += VsrFolderChanged;
			watcher.EnableRaisingEvents = true;

			ReadInfo();
		}

		private void OnAfterSolutionClosed()
		{
			vsrPath = null;
			if (watcher != null)
			{
				watcher.EnableRaisingEvents = false;
				watcher.Dispose();
			}
		}

		public override bool SatisfiesDependency(Tuple<string, string> d)
		{
			if (!Available)
				return false;

			if (d.Item1 == "vsr-branch")
			{
				return GlobMatch(d.Item2, vsrBranch);
			}
			else if (d.Item1 == "vsr-sha")
			{
				return GlobMatch(d.Item2, vsrSHA);
			}
			else if (d.Item1 == "vsr")
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private void VsrFolderChanged(object sender, FileSystemEventArgs e)
		{
			ReadInfo();
		}

		private void ReadInfo()
		{
			string info = ResolverUtils.ExecuteProcess("vsr.exe", "info --nocolours");

			if (string.IsNullOrEmpty(info))
				return;

			var lines = info.Split('\n');
			bool changed = false;

			// parse branch
			{
				var match = Regex.Match(lines[0], "on branch \"([a-zA-Z0-9_-]+)\"");
				if (match.Success && vsrBranch != match.Groups[1].Value)
				{
					vsrBranch = match.Groups[1].Value;
					changed = true;
				}
			}

			// parse SHA
			{
				var match = Regex.Match(lines[0], "Version ([a-fA-F0-9-]+)");
				if (match.Success)
				{
					vsrSHA = match.Groups[1].Value;
					changed = true;
				}
			}

			if (changed)
			{
				Changed?.Invoke(this);
			}
		}

		private static IEnumerable<DirectoryInfo> GetAllParentDirectories(DirectoryInfo directoryToScan)
		{
			Stack<DirectoryInfo> ret = new Stack<DirectoryInfo>();
			GetAllParentDirectories(directoryToScan, ref ret);
			return ret;
		}

		private static void GetAllParentDirectories(DirectoryInfo directoryToScan, ref Stack<DirectoryInfo> directories)
		{
			if (directoryToScan == null || directoryToScan.Name == directoryToScan.Root.Name)
				return;

			directories.Push(directoryToScan);
			GetAllParentDirectories(directoryToScan.Parent, ref directories);
		}

		private string vsrPath;
		private FileSystemWatcher watcher;
		private string vsrBranch;
		private string vsrSHA;
	}
}
