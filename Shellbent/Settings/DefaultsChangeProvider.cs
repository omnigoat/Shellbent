using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shellbent.Settings
{
	class DefaultsChangeProvider : ChangeProvider
	{
		public override List<SettingsTriplet> Triplets => new List<SettingsTriplet>
		{
			new SettingsTriplet {
				FormatIfNothingOpened = new TitleBarFormat(Defaults.PatternIfNothingOpen),
				FormatIfDocumentOpened = new TitleBarFormat(Defaults.PatternIfDocumentOpen),
				FormatIfSolutionOpened = new TitleBarFormat(Defaults.PatternIfSolutionOpen)
			}
		};

#pragma warning disable 67
		public override event ChangedEvent Changed;
#pragma warning restore 67

		protected override void DisposeImpl()
		{
		}
	}
}
