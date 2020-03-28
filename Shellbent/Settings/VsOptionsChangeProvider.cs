using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shellbent.Settings
{
	class VsOptionsChangeProvider : ChangeProvider
	{
		public VsOptionsChangeProvider(SettingsPageGrid settingsPage)
		{
			SettingsPage = settingsPage;
			SettingsPage.SettingsChanged += SettingsPage_SettingsChanged;

#if false
			triplet.FormatIfNothingOpened = SettingsPage.PatternIfNothingOpen;
			triplet.FormatIfDocumentOpened = SettingsPage.PatternIfDocumentOpen;
			triplet.FormatIfSolutionOpened = SettingsPage.PatternIfSolutionOpen;

			gitTriplet.PatternDependencies.Add(Tuple.Create("git", ""));
			gitTriplet.FormatIfSolutionOpened = SettingsPage.GitPatternIfOpen;
#endif
		}

		public override event ChangedEvent Changed;
		public override List<SettingsTriplet> Triplets => new List<SettingsTriplet> { gitTriplet, triplet };

		private void SettingsPage_SettingsChanged(object sender, EventArgs e)
		{
#if false
			bool requiresUpdate =
				(triplet.FormatIfNothingOpened != SettingsPage.PatternIfNothingOpen) ||
				(triplet.FormatIfDocumentOpened != SettingsPage.PatternIfDocumentOpen) ||
				(triplet.FormatIfSolutionOpened != SettingsPage.PatternIfSolutionOpen);

			triplet.FormatIfNothingOpened = SettingsPage.PatternIfNothingOpen;
			triplet.FormatIfDocumentOpened = SettingsPage.PatternIfDocumentOpen;
			triplet.FormatIfSolutionOpened = SettingsPage.PatternIfSolutionOpen;

			gitTriplet.FormatIfSolutionOpened = SettingsPage.GitPatternIfOpen;

			if (requiresUpdate)
				Changed?.Invoke();
#endif
		}

		protected override void DisposeImpl()
		{
			// nothing to do
		}

		readonly SettingsPageGrid SettingsPage;
		private SettingsTriplet triplet = new SettingsTriplet();
		private SettingsTriplet gitTriplet = new SettingsTriplet();
	}
}
