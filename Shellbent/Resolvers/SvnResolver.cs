using Shellbent.Settings;
using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Shellbent.Resolvers
{
	class SvnResolver : Resolver
	{
		public SvnResolver(Models.SolutionModel solutionModel)
			: base(new[] { "svn", "svn-url" })
		{
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available => svnPath != null;

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "svn": return true;
				case "svn-url": return GlobMatch(value, svnUrl);
				default: return false;
			}
		}

		public override string Resolve(VsState state, string tag)
		{
			switch (tag)
			{
				case "svn-url": return svnUrl;
				default: return string.Empty;
			}
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			var solutionDir = new FileInfo(solutionFilepath).Directory;

			svnPath = ResolverUtils.GetAllParentDirectories(solutionDir)
					.SelectMany(x => x.GetDirectories())
					.FirstOrDefault(x => x.Name == ".svn")?.FullName;

			if (svnPath != null)
			{
				fileWatcher = new FileSystemWatcher(svnPath);
				fileWatcher.Changed += SvnFolderChanged;
				fileWatcher.IncludeSubdirectories = true;
				fileWatcher.EnableRaisingEvents = true;

				ReadInfo();
			}
		}

		private void OnAfterSolutionClosed()
		{
			svnPath = null;
			if (fileWatcher != null)
			{
				fileWatcher.EnableRaisingEvents = false;
				fileWatcher.Dispose();
			}
		}

		private void SvnFolderChanged(object sender, FileSystemEventArgs e)
		{
			ReadInfo();
			Changed?.Invoke(this);
		}

		private void ReadInfo()
		{
			string svnInfo = ResolverUtils.ExecuteProcess(Path.GetDirectoryName(svnPath), "svn.exe", "info");

			var newUrl =
				svnInfo.SplitIntoLines()
				.Where(x => x.StartsWith("URL: "))
				.Select(x => x.Substring(5))
				.FirstOrDefault();

			if (svnUrl != newUrl)
			{
				svnUrl = newUrl;
				Changed?.Invoke(this);
			}
		}

		private string svnPath;
		private FileSystemWatcher fileWatcher;
		private string svnUrl;
	}
}

