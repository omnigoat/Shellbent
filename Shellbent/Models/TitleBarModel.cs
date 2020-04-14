using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Markup;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows.Data;
using Shellbent.Utilities;

namespace Shellbent.Models
{
	static class UIElementExtensions
	{
		public static T GetElement<T>(this UIElement root, string name = null, int max_depth = int.MaxValue) where T : class
		{
			DependencyObject find(DependencyObject r, int depth)
			{
				if (depth == 0) return null;
				var c = VisualTreeHelper.GetChildrenCount(r);
				for (int i = 0; i < c; ++i)
				{
					var e = VisualTreeHelper.GetChild(r, i);
					if (e is T)
					{
						if (name == null || (e as FrameworkElement)?.Name == name)
							return e;
					}
					e = find(e, depth - 1);
					if (e != null) return e;
				}
				return null;
			}

			return find(root, max_depth) as T;
		}

		public static List<T> GetChildren<T>(this UIElement r)
			where T : UIElement
		{
			List<T> children = new List<T>();
			var c = VisualTreeHelper.GetChildrenCount(r);
			for (int i = 0; i < c; ++i)
			{
				var dp = VisualTreeHelper.GetChild(r, i);
				if (dp is T dpt)
					children.Add(dpt);
			}

			return children;
		}

		public static T WithNotNull<T>(this T o, Action<T> f)
			where T : class
		{
			if (o != null)
				f(o);

			return o;
		}
	}



	internal struct TitleBarInfoBlockData
	{
		public string Text;
		public Brush TextBrush;
		public Brush BackgroundBrush;
	}
	
	internal struct TitleBarData
	{
		public string TitleBarText;
		public SolidColorBrush TitleBarForegroundBrush;
		public SolidColorBrush TitleBarBackgroundBrush;

		public List<TitleBarInfoBlockData> Infos;
	}






	internal abstract class TitleBarModel
	{
		public TitleBarModel(Window window)
		{
			Window = window;
		}

		public static TitleBarModel Make(string vsVersion, Window x)
		{
			try
			{
				if (IsMsvc2017(vsVersion))
					return new TitleBarModel2017(x);
				else if (IsMsvc2019(vsVersion))
					return new TitleBarModel2019(x);
			}
			catch
			{
			}

			return null;
		}


		public Window Window { get; private set; }

		public abstract void UpdateTitleBar(TitleBarData data);

		public SolidColorBrush CalculateForegroundBrush(Color? color)
		{
			if (!color.HasValue)
				return null;

			var c = color.Value;

			float luminance = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

			if (luminance > 128.0f) // very bright, use black
				return new SolidColorBrush(Color.FromRgb(0, 0, 0));
			else if (luminance > 64.0f) // medium, use white
				return new SolidColorBrush(Color.FromRgb(255, 255, 255));
			else // very dark, use vanilla msvc grey
				return null; 
		}

		private static bool IsMsvc2017(string str) => str.StartsWith("15");
		private static bool IsMsvc2019(string str) => str.StartsWith("16");
	}

	internal class TitleBarModel2017 : TitleBarModel
	{
		public bool IsMainWindow => Window != null && Window == Application.Current.MainWindow;

		public TitleBarModel2017(Window window) : base(window)
		{
		}


		private UIElement cachedTitleBar;
		protected UIElement TitleBar => cachedTitleBar ??
			(cachedTitleBar = IsMainWindow
				? Window.GetElement<UIElement>("MainWindowTitleBar")
				: (UIElement)Window
					.GetElement<UIElement>("TitleBar")
					?.GetElement<Grid>());

		protected TextBlock cachedTitleBarTextBlock;
		protected virtual TextBlock TitleBarTextBlock => cachedTitleBarTextBlock ??
			(cachedTitleBarTextBlock = IsMainWindow
				? TitleBar
					?.GetElement<DockPanel>()
					?.GetElement<TextBlock>(null, 1)
				: TitleBar
					?.GetElement<TextBlock>());

		protected System.Reflection.PropertyInfo TitleBarBackgroundProperty => TitleBar.NullOr(x => x.GetType().GetProperty("Background"));
		protected System.Reflection.PropertyInfo TitleBarForegroundProperty => TitleBarTextBlock.NullOr(x => x.GetType().GetProperty("Foreground"));


		public override void UpdateTitleBar(TitleBarData data)
		{
			// main-window title
			if (IsMainWindow)
			{
				if (string.IsNullOrEmpty(data.TitleBarText))
				{
					// the main-window's title property is by default bound to
					// a style, so just clear the local value to get vanilla MSVC
					Window.ClearValue(Window.TitleProperty);
				}
				else if (Window.Title != data.TitleBarText)
				{
					Window.Title = data.TitleBarText;
				}
			}

			// title-bar colors
			if (TitleBar != null)
			{
				// setting to null resets to vanilla msvc
				TitleBarBackgroundProperty?.SetValue(TitleBar, data.TitleBarBackgroundBrush);

				var foregroundBrush = data.TitleBarForegroundBrush ?? CalculateForegroundBrush(data.TitleBarBackgroundBrush?.Color);
				if (foregroundBrush != null)
					TitleBarForegroundProperty?.SetValue(TitleBarTextBlock, foregroundBrush);
				else
					TitleBarTextBlock?.ClearValue(TextBlock.ForegroundProperty);
			}
		}
	}

	internal class TitleInfoBlock
	{
		public static TitleInfoBlock Make(Border border)
		{
			if (border == null)
				return null;
			else
				return new TitleInfoBlock(border);
		}

		public TitleInfoBlock(Border border)
		{
			Border = border;
			TextBox = border.GetElement<TextBlock>();
		}

		public readonly Border Border;
		public readonly TextBlock TextBox;
	}






	internal class TitleBarModel2019 : TitleBarModel2017
	{
		public TitleBarModel2019(Window window)
			: base(window)
		{ }

		public override void UpdateTitleBar(TitleBarData data)
		{
			base.UpdateTitleBar(data);

			// extend background colour to the menubar
			TitleBarVsMenu?.SetValue(Panel.BackgroundProperty, data.TitleBarBackgroundBrush);

			// info-blocks
			if (TitleBarInfoGrid != null)
			{
				// if we have an override background-colour, then calculate a nice default
				// background-colour for the blocks, and apply it to the prime block
				if (data.TitleBarBackgroundBrush != null)
				{
					// just 25% less bright
					PrimeTitleInfoBlock.Border.Background = new SolidColorBrush(data.TitleBarBackgroundBrush.Color * 0.75f);
				}
				else
				{
					PrimeTitleInfoBlock.Border.ClearValue(Border.BackgroundProperty);
				}

				// remove all previously-synthesized info-blocks
				synthesizedInfoBlocks.ForEach(TitleBarInfoGrid.Children.Remove);

				// reset column-definitions from before
				if (TitleBarInfoGrid.ColumnDefinitions.Count > 2)
					TitleBarInfoGrid.ColumnDefinitions.RemoveRange(2, TitleBarInfoGrid.ColumnDefinitions.Count - 2);

				if (data.Infos != null)
				{
					// add new column-definitions to match
					foreach (var i in data.Infos)
						TitleBarInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

					// recalculate title-bar-infos
					synthesizedInfoBlocks = data.Infos
						.Select(MakeInfoBlock)
						.ToList();

					// add all user-defined blocks
					foreach (var c in synthesizedInfoBlocks)
						TitleBarInfoGrid.Children.Add(c);
				}
			}
		}

		protected List<Border> synthesizedInfoBlocks = new List<Border>();

		protected override TextBlock TitleBarTextBlock => cachedTitleBarTextBlock ??
			(cachedTitleBarTextBlock = IsMainWindow
				? TitleBar?.GetElement<TextBlock>("TextBlock_1")
				: TitleBar?.GetElement<TextBlock>("WindowTitle"));

		private UIElement cachedVsMenu;
		private UIElement TitleBarVsMenu => cachedVsMenu ??
			(cachedVsMenu = TitleBar
				?.GetElement<ContentControl>("PART_MinimalMainMenuBar")
				?.GetElement<ContentPresenter>()
				?.GetElement<ContentPresenter>()
				?.GetElement<UIElement>());

		private Grid cachedTitleBarInfoGrid;
		protected Grid TitleBarInfoGrid =>
			cachedTitleBarInfoGrid ??
			(cachedTitleBarInfoGrid = TitleBar
				?.GetElement<ContentControl>("PART_SolutionInfoControlHost")
				?.GetElement<Grid>()
				.WithNotNull(x =>
				{
					x.ColumnDefinitions[1].Width = GridLength.Auto;
				}));

		private TitleInfoBlock cachedPrimeTitleInfoBlock;
		protected TitleInfoBlock PrimeTitleInfoBlock =>
			cachedPrimeTitleInfoBlock ??
			(cachedPrimeTitleInfoBlock = TitleInfoBlock.Make(TitleBarInfoGrid
				?.GetElement<Border>("TextBorder")));

		protected Border MakeInfoBlock(TitleBarInfoBlockData data, int idx)
		{
			var border = PrimeTitleInfoBlock.Border;
			var text = PrimeTitleInfoBlock.TextBox;

			try
			{
				var r = new Border
				{
					Background = data.BackgroundBrush,
					BorderBrush = border.BorderBrush,
					BorderThickness = border.BorderThickness,
					Padding = new Thickness(border.Padding.Left, border.Padding.Top, border.Padding.Right, border.Padding.Bottom),
					DataContext = border.DataContext,
					HorizontalAlignment = border.HorizontalAlignment,
					
					// just a little more separation than 1px
					Margin = new Thickness(2, 0, 0, 0),

					Child = new Border
					{
						Margin = new Thickness(0, 4.5, 0, 4.5),
						Child = new TextBlock
						{
							Text = data.Text,
							Foreground = data.TextBrush,

							FontFamily = text.FontFamily,
							FontWeight = text.FontWeight,
							FontSize = text.FontSize,
							FontStyle = text.FontStyle,
							FontStretch = text.FontStretch,
							TextAlignment = text.TextAlignment,
							TextEffects = text.TextEffects,
							Padding = text.Padding,
							BaselineOffset = text.BaselineOffset,
							Width = Math.Floor(MeasureString(text, data.Text).Width)
						}
					}
				};

				// if no colour specified, set background to match the main block
				if (data.BackgroundBrush == null)
				{
					// TEST - it works
					// PrimeTitleInfoBlock.Border.ClearValue(Border.BackgroundProperty);

					r.SetBinding(Border.BackgroundProperty, new Binding()
					{
						Source = border,
						Path = new PropertyPath("Background")
					});
				}
				else
				{
					// TEST - it works
					// PrimeTitleInfoBlock.Border.SetValue(Border.BackgroundProperty, data.BackgroundBrush);
				}

				r.SetValue(Grid.ColumnProperty, idx + 2);
				return r;
			}
			catch (Exception e)
			{
				System.Console.WriteLine(e.Message);
			}

			return null;
		}

		private Size MeasureString(TextBlock textBlock, string candidate)
		{
			var formattedText = new FormattedText(
				candidate,
				System.Globalization.CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight,
				new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
				textBlock.FontSize,
				Brushes.Black,
				new NumberSubstitution(),
				1);

			return new Size(formattedText.Width, formattedText.Height);
		}
	}
}
