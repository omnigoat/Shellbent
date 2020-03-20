using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using System.Linq;

//http://stackoverflow.com/questions/24291249/dialogpage-string-array-not-persisted
//http://www.codeproject.com/Articles/351172/CodeStash-a-journey-into-the-dark-side-of-Visual-S

namespace Shellbent.Settings
{
	public class SettingsPageGrid : DialogPage
	{
		[Category("Default Patterns")]
		[DisplayName("Nothing Opened")]
		[DefaultValue(Defaults.PatternIfNothingOpen)]
		[Editor(typeof(Editors.PatternEditor), typeof(System.Drawing.Design.UITypeEditor))]
		[TypeConverter(typeof(TitleBarFormatConverter))]
		public TitleBarFormat PatternIfNothingOpen { get; set; } = new TitleBarFormat(Defaults.PatternIfNothingOpen);

		[Category("Default Patterns")]
		[DisplayName("Document Open")]
		[DefaultValue(Defaults.PatternIfDocumentOpen)]
		[Editor(typeof(Editors.PatternEditor), typeof(System.Drawing.Design.UITypeEditor))]
		[Editors.PreviewRequires(Editors.PreviewRequiresAttribute.Requirement.Document)]
		[TypeConverter(typeof(TitleBarFormatConverter))]
		public TitleBarFormat PatternIfDocumentOpen { get; set; } = new TitleBarFormat(Defaults.PatternIfDocumentOpen);

		[Category("Default Patterns")]
		[DisplayName("Solution Opened")]
		[DefaultValue(Defaults.PatternIfSolutionOpen)]
		[Editor(typeof(Editors.PatternEditor), typeof(System.Drawing.Design.UITypeEditor))]
		[Editors.PreviewRequires(Editors.PreviewRequiresAttribute.Requirement.Solution)]
		[TypeConverter(typeof(TitleBarFormatConverter))]
		public TitleBarFormat PatternIfSolutionOpen { get; set; } = new TitleBarFormat(Defaults.PatternIfSolutionOpen);

		[Category("Source Control")]
		[DisplayName("Git - Item Open")]
		[Description("What pattern to use when a document or solution is loaded in a valid .git repository")]
		[DefaultValue(Defaults.GitPatternIfOpen)]
		[TypeConverter(typeof(TitleBarFormatConverter))]
		public TitleBarFormat GitPatternIfOpen { get; set; } = new TitleBarFormat(Defaults.GitPatternIfOpen);

#if false
		[Category("Source Control")]
		[DisplayName("SVN - Item Open")]
		[Description("What pattern to use when a document or solution is loaded in a valid .git repository")]
		[DefaultValue(Defaults.GitPatternIfOpen]
		[TypeConverter(typeof(TitleBarFormatConverter))]
		public TitleBarFormat SvnPatternIfOpen { get; set; } = new TitleBarFormat(Defaults.GitPatternIfOpen);
#endif

#if false
		[Category("Source Control")]
		[DisplayName("Hg binaries directory")]
		[Description("Default: Empty. Search windows PATH for hg if empty.")]
		[Editor(typeof(FilePickerEditor), typeof(System.Drawing.Design.UITypeEditor))]
		[FilePicker(true, HgBranchNameResolver.HgExecFn, "Hg executable(hg.exe)|hg.exe|All files(*.*)|*.*", 1)]
		[DefaultValue("")]
		public string HgDirectory { get; set; } = "";

		[Category("Source Control")]
		[DisplayName("SVN binaries directory")]
		[Description("Default: Empty. Search windows PATH for svn if empty.")]
		[Editor(typeof(FilePickerEditor), typeof(System.Drawing.Design.UITypeEditor))]
		[FilePicker(true, SvnResolver.SvnExecFn, "SVN executable(svn.exe)|svn.exe|All files(*.*)|*.*", 1)]
		[DefaultValue("")]
		public string SvnDirectory { get; set; } = "";

		[Category("Source Control")]
		[DisplayName("SVN directory separator")]
		[Description("Default: '/'. Specify the character used to separate the SVN directories.")]
		[DefaultValue("/")]
		public string SvnDirectorySeparator { get; set; } = "/";

		[Category("Source Control")]
		[DisplayName("Versionr binaries directory")]
		[Description("Default: Empty. Search windows PATH for svn if empty.")]
		[Editor(typeof(FilePickerEditor), typeof(System.Drawing.Design.UITypeEditor))]
		[FilePicker(true, VsrResolver.VsrExecFn, "Versionr executable(vsr.exe)|vsr.exe|All files(*.*)|*.*", 1)]
		[DefaultValue("")]
		public string VsrDirectory { get; set; } = "";

		[Category("Source Control")]
		[DisplayName("Versionr directory separator")]
		[Description("Default: '/'. Specify the character used to separate the Versionr directories.")]
		[DefaultValue("/")]
		public string VsrDirectorySeparator { get; set; } = "/";
#endif

		[Category("Debug")]
		[DisplayName("Enable debug mode")]
		[Description("Default: false. Set to true to activate debug output to Output window.")]
		[DefaultValue(false)]
		public bool EnableDebugMode { get; set; } = false;

		public event EventHandler SettingsChanged;

		protected override void OnApply(PageApplyEventArgs e)
		{
			base.OnApply(e);
			if (e.ApplyBehavior != ApplyKind.Apply)
				return;
			this.SettingsChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}