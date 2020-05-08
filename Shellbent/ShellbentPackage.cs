using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using Shellbent.Resolvers;
using System.Windows;
using Shellbent.Utilities;
using System.Windows.Media;

using Task = System.Threading.Tasks.Task;
using Shellbent.Settings;

namespace Shellbent
{
	[Guid(PackageGuidString)]
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("Shellbent", "Colourizes the shell per-project/SCM system", "1.0")]
	[ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	public sealed class ShellbentPackage : AsyncPackage
	{
		public const string PackageGuidString = "16599b2d-db6e-49cd-a76e-2b6da7343bcc";

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await base.InitializeAsync(cancellationToken, progress);

			// initialize the DTE and bind events
			DTE = await GetServiceAsync(typeof(DTE)) as DTE;

			ideModel = new Models.IDEModel(DTE);
			ideModel.WindowShown += (EnvDTE.Window w) => UpdateModelsAsync();
			ideModel.StartupComplete += UpdateModelsAsync;

			solutionModel = new Models.SolutionModel();
			solutionModel.SolutionBeforeOpen += OnBeforeSolutionOpened;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;

			// switch to Main thread
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			// create resolvers
			resolvers = new List<Resolver>
			{
				new IDEResolver(ideModel),
				new SolutionResolver(solutionModel),
				new GitResolver(solutionModel),
				new VsrResolver(solutionModel),
				new SvnResolver(solutionModel),
				new P4Resolver(solutionModel)
			};

			foreach (var resolver in resolvers)
			{
				resolver.Changed += (Resolver r) => UpdateModelsAsync();
			}

			// create settings readers for user-dir
			userDirFileChangeProvider = new Settings.UserDirFileChangeProvider();
			userDirFileChangeProvider.Changed += UpdateModelsAsync;


			// async initialize window state in case this plugin loaded after the
			// IDE was brought up, because this plugin loads async to the UI
			var d = TitleBarData;

			// we are the UI thread
			var (_, discovered) = WindowsLostAndDiscovered;
			foreach (var w in discovered)
				w.UpdateTitleBar(d);
		}


		//=========================================================
		// event-handlers
		//=========================================================
		private void HandleOpenSolution(object sender = null, EventArgs e = null)
		{
			// Handle the open solution and try to do as much work
			// on a background thread as possible
		}

		private void OnBeforeSolutionOpened(string solutionFilepath)
		{
			// reset the solution-file settings file
			solutionsFileChangeProvider?.Dispose();
			solutionsFileChangeProvider = new SolutionFileChangeProvider(solutionFilepath);

			UpdateModelsAsync();
		}

		private void OnAfterSolutionClosed()
		{
			solutionsFileChangeProvider?.Dispose();

			UpdateModelsAsync();
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


		//=========================================================
		// members
		//=========================================================
		private DTE DTE { get; set; }

		//
		// TitleBarData
		//  - complete computed data for this point in time
		//
		internal Models.TitleBarData TitleBarData =>
			Settings
				.Where(t => t.Predicates.All(PredicateIsSatisfied))
				.Aggregate(new Models.TitleBarData(), (Models.TitleBarData acc, TitleBarSetting x) =>
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

					acc.Infos = acc.Infos ?? x.Blocks
						?.Where(b => b.Predicates.All(PredicateIsSatisfied))
						?.Select(ti => MakeTitleBarInfoBlockData(state, ti))
						?.ToList();

					return acc;
				});


		// resolvers
		private List<Resolver> resolvers;
		private IEnumerable<Resolver> Resolvers =>
			resolvers.AsEnumerable();

		private IEnumerable<TitleBarSetting> Settings =>
			userDirFileChangeProvider.Settings
				.Concat(solutionsFileChangeProvider?.Settings ?? new List<Settings.TitleBarSetting>());

		// models
		private Models.SolutionModel solutionModel;
		private Models.IDEModel ideModel;

		// change providers
		private SolutionFileChangeProvider solutionsFileChangeProvider;
		private UserDirFileChangeProvider userDirFileChangeProvider;


		private Models.TitleBarInfoBlockData MakeTitleBarInfoBlockData(VsState state, TitleBarSetting.BlockSettings bs)
			=> new Models.TitleBarInfoBlockData()
			{
				Text = Parsing.ParseFormatString(state, bs.Text),
				TextBrush = bs.Foreground.NullOr(c => new SolidColorBrush(c)),
				BackgroundBrush = bs.Background.NullOr(c => new SolidColorBrush(c))
			};

		private bool PredicateIsSatisfied(Tuple<string, string> predicate)
			=> Resolvers.Any(r => r.SatisfiesDependency(predicate));

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

		
	}
}
