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
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available => gitPath != null;

		public override ChangedDelegate Changed { get; set; }

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "git-branch": lock (dataLock) return GlobMatch(value, gitBranch);
				case "git-sha": lock (dataLock) return GlobMatch(value, gitSha);
				case "git": return Available;

				default: return false;
			}
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
				fileWatcher.Changed += (object sender, FileSystemEventArgs e) => ReadInfo();
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

		private void ReadInfo()
		{
			gitBranch = ResolverUtils.ExecuteProcess(gitPath, "git.exe", "symbolic-ref -q --short HEAD").Trim();

			var info = ResolverUtils.ExecuteProcess(gitPath, "git.exe", "show -s --format=\"%h|%cr\" HEAD")
				.Split(new char[] { '|' })
				.Select(x => x.Trim())
				.ToList();

			try
			{
				gitSha = info[0];
				gitCommitTimeRelative = info[1];
			}
			catch
			{
				gitSha = "<error>";
				gitCommitTimeRelative = "<error>";
			}
		}

		private FileSystemWatcher fileWatcher;

		private readonly object dataLock = new object();

		private string gitPath = null;
		private string gitBranch = string.Empty;
		private string gitSha = string.Empty;
		private string gitCommitTimeRelative = string.Empty;
	}
}
