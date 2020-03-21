using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using YamlDotNet.Core;
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
		public DefaultableColor(System.Windows.Media.Color x)
		{
			Value = x;
		}

		System.Windows.Media.Color Value;

		public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
		{
			Value = (System.Windows.Media.Color)nestedObjectDeserializer(typeof(System.Windows.Media.Color));
		}

		public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
		{
			throw new NotImplementedException();
		}
	}
#endif


	public class SettingsTriplet
	{
		public List<Tuple<string, string>> PatternDependencies = new List<Tuple<string, string>>();

		public TitleBarFormat FormatIfNothingOpened;
		public TitleBarFormat FormatIfDocumentOpened;
		public TitleBarFormat FormatIfSolutionOpened;


		public class BlockSettings
		{
			[YamlMember(Alias = "text")]
			public string Text;

			[YamlMember(Alias = "foreground")]
			public System.Windows.Media.Color Foreground;

			[YamlMember(Alias = "background")]
			public System.Windows.Media.Color Background;
		}



		[YamlMember(Alias = "predicates")]
		public string predicateString;

		private List<Tuple<string, string>> predicates;
		public List<Tuple<string, string>> Predicates
		{
			get
			{
				if (predicates == null && !string.IsNullOrEmpty(predicateString))
				{
					predicates = predicateString
						.Split(new char[] { ';' })
						.Select(x => x.Trim())
						.Where(x => !string.IsNullOrEmpty(x))
						.Select(x =>
						{
							var m = System.Text.RegularExpressions.Regex.Match(x, @"([a-z-]+)(\s*=~\s*(.+))?");
							if (m.Groups[3].Success)
								return Tuple.Create(m.Groups[1].Value, m.Groups[3].Value);
							else if (m.Success)
								return Tuple.Create(m.Groups[1].Value, "");
							else
								throw new InvalidOperationException(string.Format($"bad predicate: {x}"));
						})
						.ToList();
				}

				return predicates ?? new List<Tuple<string, string>>();
			}
		}


		[YamlMember(Alias = "title-bar-caption")]
		public string TitleBarCaption;

		[YamlMember(Alias = "title-bar-foreground")]
		public Color TitleBarForeground;

		[YamlMember(Alias = "title-bar-background")]
		public Color TitleBarBackground;

		[YamlMember(Alias = "blocks")]
		public List<BlockSettings> Blocks;

		// vs2019
		public List<TitleBarFormat> TextInfos;
	}

}
