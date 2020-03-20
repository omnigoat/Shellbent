using Shellbent.Settings;
using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Shellbent.Resolvers
{
	public class GitResolver : Resolver
	{
		public static GitResolver Create(Models.SolutionModel solutionModel)
		{
			return new GitResolver(solutionModel);
		}

		public GitResolver(Models.SolutionModel solutionModel)
			: base(new[] { "git", "git-branch", "git-sha" })
		{
			OnSolutionOpened(solutionModel.StartupSolution);

			solutionModel.SolutionOpened += OnSolutionOpened;
			solutionModel.SolutionClosed += OnSolutionClosed;
		}

		public override bool Available => gitPath != null;

		public override ChangedDelegate Changed { get; set; }

		public override bool SatisfiesDependency(Tuple<string, string> d)
		{
			if (!Available)
				return false;

			if (d.Item1 == "git-branch")
			{
				return GlobMatch(d.Item2, gitBranch);
			}
			else if (d.Item1 == "git-sha")
			{
				return GlobMatch(d.Item2, gitSha);
			}
			else if (d.Item1 == "git")
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public override bool ResolveBoolean(VsState state, string tag)
		{
			return tag == "git";
		}

		public override string Resolve(VsState state, string tag)
		{
			if (tag == "git-branch")
				return gitBranch;
			else if (tag == "git-sha")
				return gitSha;
			else
				return "";
		}

		private void OnSolutionOpened(Solution solution)
		{
			if (string.IsNullOrEmpty(solution?.FileName))
				return;

			var solutionDir = new FileInfo(solution.FileName).Directory;

			gitPath = GetAllParentDirectories(solutionDir)
				.SelectMany(x => x.GetDirectories())
				.FirstOrDefault(x => x.Name == ".git")?.FullName;

			if (gitPath != null)
			{
				fileWatcher = new FileSystemWatcher(gitPath);
				fileWatcher.Changed += GitFolderChanged;
				fileWatcher.EnableRaisingEvents = true;

				ReadInfo();
			}
		}

		private void OnSolutionClosed()
		{
			gitPath = null;
			if (fileWatcher != null)
			{
				fileWatcher.EnableRaisingEvents = false;
				fileWatcher.Dispose();
			}
		}

		private void GitFolderChanged(object sender, FileSystemEventArgs e)
		{
			ReadInfo();
		}

		private void ReadInfo()
		{
			var p = new System.Diagnostics.Process()
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = "git.exe",
					Arguments = "symbolic-ref -q --short HEAD",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					WorkingDirectory = gitPath
				}
			};

			p.OutputDataReceived += GitBranchReceived;
			p.Start();
			p.BeginOutputReadLine();

			var p2 = new System.Diagnostics.Process()
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = "git.exe",
					Arguments = "rev-parse --short HEAD",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					WorkingDirectory = gitPath
				}
			};

			p2.OutputDataReceived += GitShaReceived;
			p2.Start();
			p2.BeginOutputReadLine();

			p.WaitForExit();
			p2.WaitForExit();
		}

		private void GitBranchReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data != null && gitBranch != e.Data)
			{
				gitBranch = e.Data;
				Changed?.Invoke(this);
			}
		}

		private void GitShaReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data != null && gitSha != e.Data)
			{
				gitSha = e.Data;
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

		private string gitPath = string.Empty;
		private FileSystemWatcher fileWatcher;
		private string gitBranch = "";
		private string gitSha = "";
	}
}
