using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shellbent.Settings
{
	static class Defaults
	{
		public const string ConfgFileName = ".title-bar-none";

		public const string PatternIfNothingOpen = "$ide-name";
		public const string PatternIfDocumentOpen = "$document-name - $ide-name";
		public const string PatternIfSolutionOpen = "$solution-name?ide-mode{ $} - $ide-name";

		public const string GitPatternIfOpen = "?git{[$git-branch] }$item-name?ide-mode{ $} - $ide-name";
		public const string VsrPatternIfOpen = "?vsr{[$vsr-branch] }$item-name?ide-mode{ $} - $ide-name";
	}
}
