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
		public GitResolver(Models.SolutionModel solutionModel)
			: base(new[] { "git", "git-branch", "git-sha", "git-commit-time-relative" })
		{
			gitExePath = GetGitExePath();

			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available => gitExePath != null && gitPath != null;

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "git-branch": return GlobMatch(value, gitBranch);
				case "git-sha": return GlobMatch(value, gitSha);
				case "git": return Available;

				default: return false;
			}
		}

		public override string Resolve(VsState state, string tag)
		{
			switch (tag)
			{
				case "git-branch": return gitBranch;
				case "git-sha": return gitSha;
				case "git-commit-time-relative": return gitCommitTimeRelative;

				default: return string.Empty;
			}
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			if (string.IsNullOrEmpty(solutionFilepath))
				return;

			var solutionDir = new FileInfo(solutionFilepath).Directory;

			gitPath = ResolverUtils.GetAllParentDirectories(solutionDir)
				.SelectMany(x => x.GetDirectories())
				.FirstOrDefault(x => x.Name == ".git")
				?.FullName;

			if (gitPath != null)
			{
				fileWatcher = new FileSystemWatcher(gitPath);
				fileWatcher.Changed += OnGitFolderChanged;
				fileWatcher.EnableRaisingEvents = true;

				ReadInfo();
			}
		}

		private void OnAfterSolutionClosed()
		{
			gitPath = null;
			if (fileWatcher != null)
			{
				fileWatcher.EnableRaisingEvents = false;
				fileWatcher.Dispose();
			}
		}

		private void OnGitFolderChanged(object sender, FileSystemEventArgs e)
		{
			ReadInfo();
			Changed?.Invoke(this);
		}

		private void ReadInfo()
		{
			try
			{
				gitBranch = ResolverUtils.ExecuteProcess(gitPath, gitExePath, "symbolic-ref -q --short HEAD").Trim();

				var info = ResolverUtils.ExecuteProcess(gitPath, gitExePath, "show -s --format=\"%h|%cr\" HEAD")
					.Split('|')
					.Select(x => x.Trim())
					.ToList();

				gitSha = info[0];
				gitCommitTimeRelative = info[1];
			}
			catch
			{
				gitSha = string.Empty;
				gitCommitTimeRelative = string.Empty;
			}
		}

		private string GetGitExePath()
		{
			// search global path for git.exe
			return Environment.GetEnvironmentVariable("PATH")
				.Split(Path.PathSeparator)
				.Select(x => Path.Combine(x, "git.exe"))
				.FirstOrDefault(x => File.Exists(x));
		}

		private readonly string gitExePath;

		private FileSystemWatcher fileWatcher;

		private string gitPath;
		private string gitBranch;
		private string gitSha;
		private string gitCommitTimeRelative;
	}
}
