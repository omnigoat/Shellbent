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
				lock (syncLock) return GlobMatch(d.Item2, gitBranch);
			}
			else if (d.Item1 == "git-sha")
			{
				lock (syncLock) return GlobMatch(d.Item2, gitSha);
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
				lock (syncLock) return gitBranch;
			else if (tag == "git-sha")
				lock (syncLock) return gitSha;
			else if (tag == "git-commit-time-relative")
				lock (syncLock) return gitCommitTimeRelative;
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
		}

		private void ReadInfo()
		{
			using (var p = new System.Diagnostics.Process()
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
			})
			using (var p2 = new System.Diagnostics.Process()
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = "git.exe",
					Arguments = "show -s --format=\"%h%n%cr\" HEAD",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					WorkingDirectory = gitPath
				}
			})
			{
				p.OutputDataReceived += GitBranchReceived;
				p.Start();
				p.BeginOutputReadLine();

				p2.OutputDataReceived += GitShaReceived;
				p2.Start();
				p2.BeginOutputReadLine();

				// don't wait on the processes, let them dispose and synchronously
				// update our data whenever it happens
			}
		}

		private void GitBranchReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				lock (syncLock)
				{
					gitBranch = e.Data;
				}

				Changed?.Invoke(this);
			}
		}

		private void GitShaReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				var data = e.Data.Split("\n".ToCharArray()).ToList();

				lock (syncLock)
				{
					gitSha = data[0];
					gitCommitTimeRelative = data[1];
				}

				Changed?.Invoke(this);
			}
		}

		private static IEnumerable<DirectoryInfo> GetAllParentDirectories(DirectoryInfo directoryToScan)
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


		private FileSystemWatcher fileWatcher;

		private readonly object syncLock = new object();
		private string gitPath = string.Empty;
		private string gitBranch = "";
		private string gitSha = "";
		private string gitCommitTimeRelative = "";
	}
}
