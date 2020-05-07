using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SettingsPageGrid = Shellbent.Settings.SettingsPageGrid;
using System.Collections.Generic;
using Shellbent.Resolvers;
using System.Windows;
using Shellbent.Utilities;
using System.Windows.Media;
using VisualStudioEvents = Microsoft.VisualStudio.Shell.Events;

using Task = System.Threading.Tasks.Task;

namespace Shellbent
{
	public enum VsEditingMode
	{
		Nothing,
		Document,
		Solution
	}

	[Guid(PackageGuidString)]
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("Shellbent", "Colourizes the shell per-project/SCM system", "1.0")]
	[ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	public sealed class ShellbentPackage : AsyncPackage
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

		internal VsState CurrentVsState =>
			new VsState() { Resolvers = Resolvers, Mode = DTE.Debugger.CurrentMode, Solution = DTE.Solution };

		internal Models.TitleBarData TitleBarData =>
			SettingsTriplets
				.Where(TripletDependenciesAreSatisfied)
				.Aggregate(new Models.TitleBarData(), (Models.TitleBarData acc, Settings.SettingsTriplet x) =>
				{
					// a selection of resolvers that satisfy the triplet
					var state = new VsState()
					{
						Resolvers = Resolvers,
						Mode = DTE.Debugger.CurrentMode,
						Solution = DTE.Solution
					};

					acc.TitleBarText = acc.TitleBarText ?? Parsing.ParseFormatString(state, x.TitleBarCaption);
					acc.TitleBarForegroundBrush = acc.TitleBarForegroundBrush ?? x.TitleBarForegroundBrush;
					acc.TitleBarBackgroundBrush = acc.TitleBarBackgroundBrush ?? x.TitleBarBackgroundBrush;

					var applicableBlocks = x.Blocks
						?.Where(b => b.Predicates.All(PredicateIsSatisfied));

					if (applicableBlocks != null)
					{
						acc.Infos = acc.Infos ?? applicableBlocks
							.Select(ti =>
							{
								return new Models.TitleBarInfoBlockData()
								{
									Text = Parsing.ParseFormatString(state, ti.Text),
									TextBrush = ti.Foreground.NullOr(c => new SolidColorBrush(c)),
									BackgroundBrush = ti.Background.NullOr(c => new SolidColorBrush(c))
								};
							}).ToList();
					}

					return acc;
				});


		private IEnumerable<Resolver> Resolvers => m_Resolvers.AsEnumerable().Reverse();

		private IEnumerable<Settings.SettingsTriplet> SettingsTriplets =>
			m_UserDirFileChangeProvider.Triplets
				.Concat(m_SolutionsFileChangeProvider?.Triplets ?? new List<Settings.SettingsTriplet>())
				//.Concat(m_VsOptionsChangeProvider?.Triplets ?? new List<Settings.SettingsTriplet>())
				.Concat(m_DefaultsChangeProvider.Triplets);

		private bool PredicateIsSatisfied(Tuple<string, string> predicate)
		{
			return Resolvers.Any(r => r.SatisfiesDependency(predicate));
		}

		private bool TripletDependenciesAreSatisfied(Settings.SettingsTriplet triplet)
		{
			return triplet.Predicates.All(PredicateIsSatisfied);
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


			// switch to Main thread
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			// initialize the DTE and bind events
			DTE = await GetServiceAsync(typeof(DTE)) as DTE;

			// create models of IDE & Solution
			//
			// do this specifically after switching to the main thread so that we can
			// set things up and know that the solution is being opened up underneath us
			ideModel = new Models.IDEModel(DTE);
			ideModel.WindowShown += (EnvDTE.Window w) => UpdateModelsAsync();
			ideModel.IdeModeChanged += (dbgDebugMode mode) => m_Mode = mode;
			ideModel.StartupComplete += UpdateModelsAsync;

			solutionModel = new Models.SolutionModel();
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;

			//((DTE as DTE2).Events.SolutionEvents as IVsSolutionLoadEvents).OnAfterBackgroundSolutionLoadComplete

			//var vss = (IVsSolution)await GetServiceAsync(typeof(SVsSolution));
			////vss.AdviseSolutionEvents(this, out uint cookie);
			///
			//VisualStudioEvents.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += HandleOpenSolution;
			//VisualStudioEvents.SolutionEvents.

			// create resolvers
			m_Resolvers = new List<Resolver>
			{
				IDEResolver.Create(ideModel),
				SolutionResolver.Create(solutionModel),
				GitResolver.Create(solutionModel),
				VsrResolver.Create(solutionModel),
				SvnResolver.Create(solutionModel),
				new P4Resolver(solutionModel)
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
			var (_, discovered) = WindowsLostAndDiscovered;
			foreach (var w in discovered)
				w.UpdateTitleBar(d);
		}

		private void HandleOpenSolution(object sender = null, EventArgs e = null)
		{
			// Handle the open solution and try to do as much work
			// on a background thread as possible
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			// reset the solution-file settings file
			m_SolutionsFileChangeProvider?.Dispose();
			m_SolutionsFileChangeProvider = new Settings.SolutionFileChangeProvider(solutionFilepath);

			UpdateModelsAsync();
		}

		private void OnAfterSolutionClosed()
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
			Application.Current.Dispatcher.Invoke(() =>
			{
				var (lost, discovered) = WindowsLostAndDiscovered;

				// update all models
				foreach (var x in knownWindowModels)
				{
					x.UpdateTitleBar(TitleBarData);
				}
			});
		}

		private List<Models.WindowWrapper> knownWindowModels = new List<Models.WindowWrapper>();

		private Tuple<List<Models.WindowWrapper>, List<Models.WindowWrapper>> WindowsLostAndDiscovered
		{
			get
			{
				var seenWindows = Application.Current.Windows.Cast<System.Windows.Window>();

				var lost = knownWindowModels
					.Where(x => !seenWindows.Contains(x.Window))
					.ToList();

				var discovered = seenWindows
					.Except(knownWindowModels.Select(x => x.Window))
					.Select(x => Models.WindowWrapper.Make(DTE.Version, x))
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
