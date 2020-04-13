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
		SVN = 8
	}

	public class TitleBarFormat
	{
		public TitleBarFormat(string pattern)
		{
			Pattern = pattern;
		}

		public TitleBarFormat(string pattern, Color? color)
		{
			Pattern = pattern;

			if (color != null)
				ForegroundBrush = new SolidColorBrush(color.Value);
		}

		public string Pattern;
		public Brush ForegroundBrush;
		public Brush BackgroundBrush;
	}

	public class TitleBarFormatConverter : TypeConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
			{
				return ((TitleBarFormat)value).Pattern;
			}

			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string)
			{
				return new TitleBarFormat(value as string);
			}

			return base.ConvertFrom(context, culture, value);
		}

	}

	public class SettingsTriplet
	{
		public class BlockSettings
		{
			[YamlMember(Alias = "text")]
			public string Text;

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

		// vs2019
		[YamlMember(Alias = "blocks")]
		public List<BlockSettings> Blocks;
	}

}
