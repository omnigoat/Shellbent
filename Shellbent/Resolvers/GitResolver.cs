using Shellbent.Settings;
using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using Shellbent.Utilities;

namespace Shellbent.Resolvers
{
	public class GitResolver : Resolver
	{
		public GitResolver(Models.SolutionModel solutionModel)
			: base(new[] { "git", "git-branch", "git-sha", "git-commit-time-relative", "git-author",
					"git-subject",
					"git-remote-fetch", "git-fetch-ahead", "git-fetch-behind",
					"git-remote-push", "git-push-ahead", "git-push-behind",
					"git-ahead-behind" })
		{
			gitExePath = GetGitExePath();

			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;

			// won't be started unless valid git context found
			dispatcher.Interval = new TimeSpan(0, 1, 0);
			dispatcher.Tick += OnTimerTick;
		}

		public override bool Available => gitExePath != null && gitPath != null;

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "git-branch": return GlobMatch(value, gitBranch);
				case "git-sha": return GlobMatch(value, gitSha);
				case "git-author": return GlobMatch(value, gitAuthor);
				case "git-subject": return GlobMatch(value, gitSubject);
				case "git-remote-fetch": return GlobMatch(value, gitRemoteFetch);
				case "git-remote-push": return GlobMatch(value, gitRemotePush);
				case "git-fetch-ahead": return GlobMatch(value,  gitFetchAhead);
				case "git-fetch-behind": return GlobMatch(value, gitFetchBehind);
				case "git-push-ahead": return GlobMatch(value, gitPushAhead);
				case "git-push-behind": return GlobMatch(value, gitPushBehind);
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
				case "git-author": return gitAuthor;
				case "git-subject": return gitSubject;
				case "git-remote-fetch": return gitRemoteFetch;
				case "git-remote-push": return gitRemotePush;
				case "git-fetch-ahead": return gitFetchAhead;
				case "git-fetch-behind": return gitFetchBehind;
				case "git-fetch-ahead-behind":
					{
						if (string.IsNullOrEmpty(gitFetchAhead) && string.IsNullOrEmpty(gitFetchBehind))
							return string.Empty;
						else if (string.IsNullOrEmpty(gitFetchBehind))
							return $"ahead {gitFetchAhead}";
						else if (string.IsNullOrEmpty(gitFetchAhead))
							return $"behind {gitFetchBehind}";
						else
							return $"ahead {gitFetchAhead}, behind {gitFetchBehind}";
					}

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

				dispatcher.Start();
			}
		}

		private void OnTimerTick(object sender, EventArgs e)
		{
			ReadInfo();
			Changed?.Invoke(this);
		}

		private void OnAfterSolutionClosed()
		{
			dispatcher.Stop();

			if (fileWatcher != null)
			{
				fileWatcher.EnableRaisingEvents = false;
				fileWatcher.Dispose();
			}

			gitPath = null;
		}

		private void OnGitFolderChanged(object sender, FileSystemEventArgs e)
		{
			ReadInfo();
			Changed?.Invoke(this);
		}

		private void ParseAheadBehind(string str, ref string ahead, ref string behind)
		{
			if (ExtensionMethods.RegexMatches(str, @"\[(ahead ([0-9]+))?(behind ([0-9]+))?(, behind ([0-9]+))?\]", out Match m))
			{
				if (m.Groups[1].Success)
				{
					ahead = m.Groups[2].Value;
				}

				if (m.Groups[3].Success)
				{
					behind = m.Groups[4].Value;
				}
				else if (m.Groups[5].Success)
				{
					behind = m.Groups[6].Value;
				}
			}
		}

		private void ReadInfo()
		{
			try
			{
				gitBranch = ResolverUtils.ExecuteProcess(gitPath, gitExePath, "rev-parse --abbrev-ref HEAD").Trim();

				var info = ResolverUtils.ExecuteProcess(gitPath, gitExePath, "show -s --format=\"%h|%cr|%an|%s\" HEAD")
					.Split('|')
					.Select(x => x.Trim())
					.ToList();

				gitSha = info[0];
				gitCommitTimeRelative = info[1];
				gitAuthor = info[2];
				gitSubject = info[3];

				var info2 = ResolverUtils.ExecuteProcess(gitPath, gitExePath, $"for-each-ref --format=\"%(upstream:short)|%(upstream:track)|%(push:short)|%(push:track)\" refs/heads/{gitBranch}")
					.Split('|')
					.Select(x => x.Trim())
					.ToList();

				gitRemoteFetch = info2[0];
				ParseAheadBehind(info2[1], ref gitFetchAhead, ref gitFetchBehind);
				gitRemotePush = info2[2];
				ParseAheadBehind(info2[3], ref gitPushAhead, ref gitPushBehind);

			}
			catch
			{
				gitSha = string.Empty;
				gitCommitTimeRelative = string.Empty;
				gitAuthor = string.Empty;
				gitSubject = string.Empty;
				gitRemoteFetch = string.Empty;
				gitRemotePush = string.Empty;
				gitFetchAhead = string.Empty;
				gitFetchBehind = string.Empty;
				gitPushAhead = string.Empty;
				gitPushBehind = string.Empty;
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
		private DispatcherTimer dispatcher = new DispatcherTimer(DispatcherPriority.Background, System.Windows.Application.Current.Dispatcher);

		private string gitPath;
		private string gitBranch;

		private string gitSha;
		private string gitCommitTimeRelative;
		private string gitAuthor;
		private string gitSubject;
		private string gitRemoteFetch, gitRemotePush;
		private string gitFetchAhead, gitFetchBehind;
		private string gitPushAhead, gitPushBehind;
	}
}
