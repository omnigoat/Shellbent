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

					var triplets = Parsing.ParseYaml(File.ReadAllText(FilePath));
					if (!triplets.Equals(m_Triplets))
					{
						m_Triplets = triplets;
						Changed?.Invoke();
					}

					break;
				}
				catch (IOException)
				{
				}
			}
		}

		// IDisposable implementation
		protected override void DisposeImpl()
		{
			m_Watcher?.Dispose();
		}



		private readonly string WatchingDirectory;
		private readonly FileSystemWatcher m_Watcher;
		
		private List<SettingsTriplet> m_Triplets = new List<SettingsTriplet>();

	}
}
