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
			if (Path.GetFileName(FilePath) != Defaults.ConfgFileName)
				return;

			// file system watcher
			WatchingDirectory = Path.GetDirectoryName(FilePath);
			m_Watcher = new FileSystemWatcher(WatchingDirectory, Defaults.ConfgFileName);
			m_Watcher.Created += Watcher_Changed;
			m_Watcher.Changed += Watcher_Changed;
			m_Watcher.Deleted += Watcher_Changed;
			m_Watcher.Renamed += Watcher_Renamed;
			Watcher_Changed(null, new FileSystemEventArgs(WatcherChangeTypes.Created, WatchingDirectory, Defaults.ConfgFileName));

			m_Watcher.EnableRaisingEvents = true;
		}

		private void Watcher_Renamed(object sender, RenamedEventArgs e)
		{
			// if someone renamed the config-file to something else
			if (e.OldFullPath == FilePath)
			{
				settings = new List<TitleBarSetting>();
				Changed?.Invoke();
			}
			// a random file renamed to config-file name
			else
			{
				var file = new FileInfo(e.FullPath);
				Watcher_Changed(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, file.DirectoryName, file.Name));
			}
		}

		public override event ChangedEvent Changed;
		public override List<TitleBarSetting> Settings => settings;

		public string FilePath { get; internal set; }

		protected virtual void Watcher_Changed(object sender, FileSystemEventArgs e)
		{
			if (e.FullPath != FilePath)
				return;

			if (e.ChangeType != WatcherChangeTypes.Deleted)
			{
				for (int i = 0; i != 3; ++i)
				{
					try
					{
						Thread.Sleep(100);
						var file = new FileInfo(FilePath);
						if (!file.Exists)
							continue;

						var yamlSettings = Parsing.ParseYaml(File.ReadAllText(FilePath));
						if (!yamlSettings.Equals(settings))
						{
							settings = yamlSettings;
							Changed?.Invoke();
							return;
						}
					}
					catch (IOException)
					{
					}
				}
			}

			// either deleted or bad read = zero settings
			settings = new List<TitleBarSetting>();
			Changed?.Invoke();
		}

		// IDisposable implementation
		protected override void DisposeImpl()
		{
			m_Watcher?.Dispose();
		}



		private readonly string WatchingDirectory;
		private readonly FileSystemWatcher m_Watcher;
		
		private List<TitleBarSetting> settings = new List<TitleBarSetting>();

	}
}
