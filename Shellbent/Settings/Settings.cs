using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
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

		public TitleBarFormat(string pattern, System.Windows.Media.Color? color)
		{
			Pattern = pattern;

			if (color != null)
				ForegroundBrush = new System.Windows.Media.SolidColorBrush(color.Value);
		}

		public string Pattern;
		public System.Windows.Media.Brush ForegroundBrush;
		public System.Windows.Media.Brush BackgroundBrush;
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

#if false
	public class DefaultableColor : IYamlConvertible
	{
		public DefaultableColor(Color x)
		{
			Va lue = x;
		}

		Color Value;

		public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
		{
			if (parser.TryConsume<Scalar>(out Scalar s))
			if (parser.TryConsume<ParsingEvent>)
			Value = (Color)nestedObjectDeserializer(typeof(Color));
		}

		public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
		{
			throw new NotImplementedException();
		}
	}
#endif


	public class SettingsTriplet
	{
		public class BlockSettings
		{
			[YamlMember(Alias = "text")]
			public string Text;

			[YamlMember(Alias = "foreground")]
			public Color Foreground;

			[YamlMember(Alias = "background")]
			public Color Background;
		}


		// when no predicate is specified, we assume the user wants
		// these settings to apply to the "empty state" of the IDE
		[YamlMember(Alias = "predicates")]
		public List<string> predicateString = new List<string>();

		private List<Tuple<string, string>> predicates;
		public List<Tuple<string, string>> Predicates => predicates ??
			(predicates = predicateString
				.Select(x => x.Trim())
				.Where(x => !string.IsNullOrEmpty(x))
				.Select(Utilities.Parsing.ParsePredicate)
				.DefaultIfEmpty(Tuple.Create("empty", ""))
				.ToList());


		[YamlMember(Alias = "title-bar-caption")]
		public string TitleBarCaption;

		[YamlMember(Alias = "title-bar-foreground")]
		public Color TitleBarForeground;
		public Brush TitleBarForegroundBrush =>
			(TitleBarForeground == null) ? null : new SolidColorBrush(TitleBarForeground);

		[YamlMember(Alias = "vs2017-title-bar-background")]
		public Color Vs2017TitleBarBackground;

		[YamlMember(Alias = "vs2019-title-bar-background")]
		public Color Vs2019TitleBarBackground;

		[YamlMember(Alias = "blocks")]
		public List<BlockSettings> Blocks;

		// vs2019
		public List<TitleBarFormat> TextInfos;
	}

}
