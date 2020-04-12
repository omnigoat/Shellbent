using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using SettingsPageGrid = Shellbent.Settings.SettingsPageGrid;
using System.Collections.Generic;
using Shellbent.Resolvers;
using System.Windows;
using Shellbent.Utilities;
using Microsoft.VisualStudio;
using System.Windows.Media;

namespace Shellbent
{
	public enum VsEditingMode
	{
		Nothing,
		Document,
		Solution
	}

	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	[Guid(PackageGuidString)]
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
	[ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	//[ProvideOptionPage(typeof(SettingsPageGrid), "Title Bar None", "Settings", 101, 1000, true)]
	public class ShellbentPackage : AsyncPackage
	{
		public const string PackageGuidString = "16599b2d-db6e-49cd-a76e-2b6da7343bcc";

		public const string SolutionSettingsOverrideExtension = ".rn.xml";
		public const string PathTag = "Path";
		public const string SolutionNameTag = "solution-name";
		public const string SolutionPatternTag = "solution-pattern";

		public ShellbentPackage()
		{
		}

		public DTE DTE
		{
			get;
			private set;
		}

		internal Models.TitleBarData TitleBarData =>
			SettingsTriplets
				.Where(TripletDependenciesAreSatisfied)
				//.Select(TitleBarFormatRightNow)
				//.Concat(new[] { new Settings.TitleBarFormat("") { BackgroundBrush = SystemColors.ActiveBorderBrush, ForegroundBrush = SystemColors.ActiveCaptionTextBrush } })
				.Aggregate(new Models.TitleBarData(), (Models.TitleBarData acc, Settings.SettingsTriplet x) =>
				{
					acc.TitleBarForegroundBrush = acc.TitleBarForegroundBrush ?? x.TitleBarForegroundBrush;
					acc.Vs2017TitleBarBackgroundBrush = acc.Vs2017TitleBarBackgroundBrush ?? x.Vs2017TitleBarBackgroundBrush;
					acc.Vs2019TitleBarBackgroundBrush = acc.Vs2019TitleBarBackgroundBrush ?? x.Vs2019TitleBarBackgroundBrush;

					acc.Infos = acc.Infos ?? x.Blocks?.Select(ti =>
					{
						return new Models.TitleBarInfoBlockData()
						{
							Text = ti.Text,
							TextBrush = ti.Foreground.NullOr(c => new SolidColorBrush(c)),
							BackgroundBrush = ti.Background.NullOr(c => new SolidColorBrush(c))
						};
					}).ToList();

					return acc;
				});

#if false
		private Settings.TitleBarFormat TitleBarFormatRightNow(Settings.SettingsTriplet st)
		{
			if (DTE.Solution.IsOpen)
				return st.FormatIfSolutionOpened;
			else if (DTE.Documents.Count > 0)
				return st.FormatIfDocumentOpened;
			else
				return st.FormatIfNothingOpened;
		}
#endif

		private IEnumerable<Resolver> Resolvers => m_Resolvers.AsEnumerable().Reverse();

		private IEnumerable<Settings.SettingsTriplet> SettingsTriplets =>
			m_UserDirFileChangeProvider.Triplets
				.Concat(m_SolutionsFileChangeProvider?.Triplets ?? new List<Settings.SettingsTriplet>())
				//.Concat(m_VsOptionsChangeProvider?.Triplets ?? new List<Settings.SettingsTriplet>())
				.Concat(m_DefaultsChangeProvider.Triplets);

		private bool TripletDependenciesAreSatisfied(Settings.SettingsTriplet triplet)
		{
			return triplet.Predicates.All(d => Resolvers.Any(r => r.SatisfiesDependency(d)));
		}

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			// load ResourceDictionary
			try
			{
				Application.Current.Resources.MergedDictionaries.Add(
					new ResourceDictionary
					{
						Source = new Uri("/Shellbent;component/Resources/Brushes.xaml", UriKind.Relative)
					});
			}
			catch (Exception e)
			{
				WriteOutput(e.Message);
			}


			// switch to UI thread
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);


			// initialize the DTE and bind events
			DTE = await GetServiceAsync(typeof(DTE)) as DTE;

			// create models of IDE/Solution
			ideModel = new Models.IDEModel(DTE);
			ideModel.WindowShown += (EnvDTE.Window w) => UpdateModelsAsync();
			ideModel.IdeModeChanged += (dbgDebugMode mode) => m_Mode = mode;
			ideModel.StartupComplete += UpdateModelsAsync;

			solutionModel = new Models.SolutionModel(DTE);
			solutionModel.SolutionOpened += OnSolutionOpened;
			solutionModel.SolutionClosed += OnSolutionClosed;

			// create resolvers
			m_Resolvers = new List<Resolver>
			{
				IDEResolver.Create(ideModel),
				SolutionResolver.Create(solutionModel),
				GitResolver.Create(solutionModel),
				VsrResolver.Create(solutionModel),
				SvnResolver.Create(solutionModel)
			};

			foreach (var resolver in m_Resolvers)
			{
				resolver.Changed += (Resolver r) => UpdateModelsAsync();
			}

			// create settings readers for user-dir
			m_UserDirFileChangeProvider = new Settings.UserDirFileChangeProvider();
			m_UserDirFileChangeProvider.Changed += UpdateModelsAsync;


			// get UI settings hooks
			//UISettings = GetDialogPage(typeof(SettingsPageGrid)) as SettingsPageGrid;
			//m_VsOptionsChangeProvider = new Settings.VsOptionsChangeProvider(UISettings);
			//m_VsOptionsChangeProvider.Changed += UpdateModelsAsync;


			// async initialize window state in case this plugin loaded after the
			// IDE was brought up, because this plugin loads async to the UI
			var d = TitleBarData;

			// we are the UI thread
			//var (_, discovered) = WindowsLostAndDiscovered;
			//foreach (var w in discovered)
			//	w.UpdateTitleBar(d);
		}

		private void OnSolutionOpened(Solution solution)
		{
			WriteOutput("OnSolutionOpened");

			// reset the solution-file settings file
			m_SolutionsFileChangeProvider = new Settings.SolutionFileChangeProvider(solution.FileName);

			UpdateModelsAsync();
		}

		private void OnSolutionClosed()
		{
			if (m_SolutionsFileChangeProvider != null)
				m_SolutionsFileChangeProvider.Dispose();

			UpdateModelsAsync();
		}

		public static void WriteOutput(string str, params object[] args)
		{
			try
			{
				Application.Current.Dispatcher?.Invoke(() =>
				{
					var generalPaneGuid = VSConstants.OutputWindowPaneGuid.DebugPane_guid;
					if (GetGlobalService(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow)
					{
						outWindow.GetPane(ref generalPaneGuid, out IVsOutputWindowPane generalPane);
						generalPane.OutputString("Shellbent: " + string.Format(str, args) + "\r\n");
						generalPane.Activate();
					}
				});
			}
			catch
			{
			}
		}

		private void UpdateModelsAsync()
		{
			//var d = TitleBarData;

			Application.Current.Dispatcher.Invoke(() =>
			{
				var (lost, discovered) = WindowsLostAndDiscovered;

				// reset old models back to how they were
				foreach (var x in lost)
				{
					x.Reset();
				}

				// update all models
				foreach (var x in knownWindowModels)
				{
					x.UpdateTitleBar(TitleBarData);
					x.ResetBackgroundToThemedDefault();
				}

				//ChangeWindowTitle(d.TitleBarText);
			});
		}

		private List<Models.TitleBarModel> knownWindowModels = new List<Models.TitleBarModel>();

		private Tuple<List<Models.TitleBarModel>, List<Models.TitleBarModel>> WindowsLostAndDiscovered
		{
			get
			{
				var seenWindows = Application.Current.Windows.Cast<System.Windows.Window>();

				var lost = knownWindowModels
					.Where(x => !seenWindows.Contains(x.Window))
					.ToList();

				var discovered = seenWindows
					.Except(knownWindowModels.Select(x => x.Window))
					.Select(x => Models.TitleBarModel.Make(DTE.Version, x))
					.Where(x => x != null)
					.ToList();

				var lostWindows = lost.Select(x => x.Window);

				knownWindowModels = knownWindowModels
					.Where(x => !lostWindows.Contains(x.Window))
					.Concat(discovered)
					.ToList();

				return Tuple.Create(lost, discovered);
			}
		}

		private void ChangeWindowTitle(string title)
		{
			if (title == null)
			{
				Debug.Print("ChangeWindowTitle - exiting early because title == null");
				return;
			}

			if (Application.Current.MainWindow == null)
			{
				Debug.Print("ChangeWindowTitle - exiting early because Application.Current.MainWindow == null");
				return;
			}

			try
			{
				Application.Current.MainWindow.Title = DTE.MainWindow.Caption;
				if (Application.Current.MainWindow.Title != title)
					Application.Current.MainWindow.Title = title;
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
			}
		}

		// UI
		internal SettingsPageGrid UISettings { get; private set; }

		// models
		private Models.SolutionModel solutionModel;
		private Models.IDEModel ideModel;

		// apparently these could get garbage collected otherwise
		private dbgDebugMode m_Mode = dbgDebugMode.dbgDesignMode;

		private List<Resolver> m_Resolvers;

		private Settings.SolutionFileChangeProvider m_SolutionsFileChangeProvider;
		private Settings.UserDirFileChangeProvider m_UserDirFileChangeProvider;
		//private Settings.VsOptionsChangeProvider m_VsOptionsChangeProvider;
		private Settings.DefaultsChangeProvider m_DefaultsChangeProvider = new Settings.DefaultsChangeProvider();
	}
}
