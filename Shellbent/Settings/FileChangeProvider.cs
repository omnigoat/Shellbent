using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using Shellbent.Utilities;

namespace Shellbent.Settings
{
	class FileChangeProvider : ChangeProvider
	{
		public FileChangeProvider(string filepath)
		{
			FilePath = filepath;
			if (!File.Exists(FilePath) || Path.GetFileName(FilePath) != Defaults.ConfgFileName)
				return;

			// file system watcher
			WatchingDirectory = Path.GetDirectoryName(FilePath);
			m_Watcher = new FileSystemWatcher(WatchingDirectory, Defaults.ConfgFileName);
			m_Watcher.Changed += Watcher_Changed;

			Watcher_Changed(null, new FileSystemEventArgs(WatcherChangeTypes.Created, WatchingDirectory, filepath));

			m_Watcher.EnableRaisingEvents = true;
		}

		public override event ChangedEvent Changed;
		public override List<SettingsTriplet> Triplets => m_Triplets;

		public string FilePath { get; internal set; }

		protected virtual void Watcher_Changed(object sender, FileSystemEventArgs e)
		{
			List<string> lines = new List<string>();

			for (int i = 0; i != 3; ++i)
			{
				try
				{
					Thread.Sleep(100);
					var file = new FileInfo(FilePath);
					if (!file.Exists || file.Name != Defaults.ConfgFileName)
						return;

					Parsing.ParseYaml(File.ReadAllText(FilePath));
					lines = File.ReadAllLines(FilePath).ToList();

					break;
				}
				catch (IOException)
				{
				}
			}

			if (lines.Count == 0)
			{
				return;
			}

			var triplets = Parsing.ParseLines(lines);

			if (!triplets.Equals(m_Triplets))
			{
				m_Triplets = triplets;
				Changed?.Invoke();
			}
		}

		// IDisposable implementation
		protected override void DisposeImpl()
		{
			m_Watcher?.Dispose();
		}



		private readonly string WatchingDirectory;
		private FileSystemWatcher m_Watcher;
		private readonly Regex m_NothingTitleRegex = new Regex("nothing-title: (.+)$");
		private readonly Regex m_DocumentTitleRegex = new Regex("document-title: (.+)$");
		private readonly Regex m_SolutionTitleRegex = new Regex("solution-title: (.+)$");

		private Regex m_NothingColorRegex = new Regex("nothing-color: (.+)$");
		private Regex m_DocumentColorRegex = new Regex("document-color: (.+)$");
		private Regex m_SolutionColorRegex = new Regex("solution-color: (.+)$");

		private List<SettingsTriplet> m_Triplets = new List<SettingsTriplet>();

	}
}
