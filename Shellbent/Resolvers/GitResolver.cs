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
			: base(new[] { "git", "git-branch", "git-sha", "git-commit-time-relative" })
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

			switch (d.Item1)
			{
				case "git-branch": lock (dataLock) return GlobMatch(d.Item2, gitBranch);
				case "git-sha": lock (dataLock) return GlobMatch(d.Item2, gitSha);
				case "git": return Available;

				default: return false;
			}
		}

		public override bool ResolveBoolean(VsState state, string tag)
		{
			return tag == "git";
		}

		public override string Resolve(VsState state, string tag)
		{
			switch (tag)
			{
				case "git-branch": lock (dataLock) return gitBranch;
				case "git-sha": lock (dataLock) return gitSha;
				case "git-commit-time-relative": lock (dataLock) return gitCommitTimeRelative;

				default: return string.Empty;
			}
		}

		private void OnSolutionOpened(Solution solution)
		{
			if (string.IsNullOrEmpty(solution?.FileName))
				return;

			var solutionDir = new FileInfo(solution.FileName).Directory;

			gitPath = ResolverUtils.GetAllParentDirectories(solutionDir)
				.SelectMany(x => x.GetDirectories())
				.FirstOrDefault(x => x.Name == ".git")
				?.FullName;

			if (gitPath != null)
			{
				fileWatcher = new FileSystemWatcher(gitPath);
				fileWatcher.Changed += (object sender, FileSystemEventArgs e) => ReadInfo();
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

			Changed?.Invoke(this);
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

			var p2 = new System.Diagnostics.Process()
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = "git.exe",
					Arguments = "show -s --format=\"%h|%cr\" HEAD",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					WorkingDirectory = gitPath
				}
			};

			p.OutputDataReceived += GitBranchReceived;
			p.Start();
			p.BeginOutputReadLine();

			p2.OutputDataReceived += GitCommitReadbackReceived;
			p2.Start();
			p2.BeginOutputReadLine();
		}

		private void GitBranchReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				lock (dataLock)
				{
					gitBranch = e.Data;
				}

				Changed?.Invoke(this);
			}
		}

		private void GitCommitReadbackReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data == null)
				return;

			var data = e.Data
				.Split(new char[] { '|' })
				.ToList();

			lock (dataLock)
			{
				try
				{
					gitSha = data[0];
					gitCommitTimeRelative = data[1];
				}
				catch
				{
					gitSha = "<error>";
					gitCommitTimeRelative = "<error>";
				}
			}

			Changed?.Invoke(this);
		}

		private FileSystemWatcher fileWatcher;

		private readonly object dataLock = new object();

		private string gitPath = null;
		private string gitBranch = string.Empty;
		private string gitSha = string.Empty;
		private string gitCommitTimeRelative = string.Empty;
	}
}
