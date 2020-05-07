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
		public static SvnResolver Create(Models.SolutionModel solutionModel)
		{
			return new SvnResolver(solutionModel);
		}

		public static bool Required(out string outpath, string path)
		{
			outpath = GetAllParentDirectories(new DirectoryInfo(path))
				.SelectMany(x => x.GetDirectories())
				.FirstOrDefault(x => x.Name == ".svn")?.FullName;

			return outpath != null;
		}

		public SvnResolver(Models.SolutionModel solutionModel)
			: base(new[] { "svn", "svn-url" })
		{
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available => svnPath != null;

		public override ChangedDelegate Changed { get; set; }

		public override bool SatisfiesDependency(Tuple<string, string> d)
		{
			if (!Available)
				return false;

			if (d.Item1 == "svn-url")
			{
				return GlobMatch(d.Item2, svnUrl);
			}
			else if (d.Item1 == "svn")
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
			return tag == "svn";
		}

		public override string Resolve(VsState state, string tag)
		{
			if (tag == "svn-url")
				return svnUrl;
			else
				return "";
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			var solutionDir = new FileInfo(solutionFilepath).Directory;

			svnPath = GetAllParentDirectories(solutionDir)
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
		}

		private void ReadInfo()
		{
			var p = new System.Diagnostics.Process()
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = "svn.exe",
					Arguments = "info",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					WorkingDirectory = new DirectoryInfo(svnPath)?.Parent?.FullName ?? svnPath
				}
			};

			p.OutputDataReceived += SvnInfoReceived;
			p.Start();
			p.BeginOutputReadLine();

			p.WaitForExit();
		}

		private void SvnInfoReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
		{
			if (e.Data == null)
				return;
			
			var lines = e.Data.Split(new char[] { '\n' });

			var url_line = lines.Where(x => x.StartsWith("URL: ")).FirstOrDefault();
			if (url_line == null)
				return;
				
			var newUrl = url_line.Substring(5); // remove "URL: "
			if (svnUrl != newUrl)
			{
				svnUrl = newUrl;
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

		private string svnPath = string.Empty;
		private FileSystemWatcher fileWatcher;
		private string svnUrl = "";
	}
}

