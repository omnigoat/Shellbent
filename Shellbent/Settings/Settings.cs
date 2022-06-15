using Shellbent.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using YamlDotNet.Serialization;

namespace Shellbent.Settings
{
	[Flags]
	public enum Dependencies
	{
		None = 0,
		SolutionGlob = 1,
		Git = 2,
		Versionr = 4,
		SVN = 8,
		P4 = 16
	}

	public class TitleBarSetting
	{
		public class BlockSettings
		{
			[YamlMember(Alias = "predicates")]
			public List<string> PredicateString = new List<string>();

			private List<Tuple<string, string>> predicates;
			public List<Tuple<string, string>> Predicates => predicates ??
				(predicates = PredicateString
					.Select(x => x.Trim())
					.Where(x => !string.IsNullOrEmpty(x))
					.Select(Parsing.ParsePredicate)
					.ToList());

			[YamlMember(Alias = "text")]
			public string Text;

			[YamlMember(Alias = "alt-text")]
			public string AltText;

			[YamlMember(Alias = "foreground")]
			public Color? Foreground;

			[YamlMember(Alias = "background")]
			public Color? Background;
		}


		// when no predicate is specified, we assume the user wants
		// these settings to apply to the "empty state" of the IDE
		[YamlMember(Alias = "predicates")]
		public List<string> PredicateString = new List<string>();

		private List<Tuple<string, string>> predicates;
		public List<Tuple<string, string>> Predicates => predicates ??
			(predicates = PredicateString
				.Select(x => x.Trim())
				.Where(x => !string.IsNullOrEmpty(x))
				.Select(Parsing.ParsePredicate)
				.ToList());


		[YamlMember(Alias = "title-bar-caption")]
		public string TitleBarCaption;

		[YamlMember(Alias = "title-bar-foreground")]
		public Color? TitleBarForeground;
		public SolidColorBrush TitleBarForegroundBrush => TitleBarForeground.NullOr(c => new SolidColorBrush(c));

		[YamlMember(Alias = "title-bar-background")]
		public Color? TitleBarBackground;
		public SolidColorBrush TitleBarBackgroundBrush => TitleBarBackground.NullOr(c => new SolidColorBrush(c));

		[YamlMember(Alias = "search-box")]
		public bool? SearchBox;

		// vs2019+
		[YamlMember(Alias = "blocks")]
		public List<BlockSettings> Blocks;
	}

}
