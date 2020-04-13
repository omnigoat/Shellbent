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
						if (name == null || (e as FrameworkElement).Name == name)
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

		//public static R WithNotNull<R, T>(this T o, Func<T, R> f)
		//	where T : class
		//{
		//	if (o != null)
		//		return f(o);
		//	else
		//		return default;
		//}
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
		public Brush TitleBarForegroundBrush;
		public Brush Vs2017TitleBarBackgroundBrush;
		public Brush Vs2019TitleBarBackgroundBrush;

		public List<TitleBarInfoBlockData> Infos;
	}

	internal abstract class TitleBarModel
	{
		public TitleBarModel(Window window)
		{
			this.Window = window;
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
		public abstract void Reset();
		public abstract void ResetBackgroundToThemedDefault();

		public void SetTitleBarColor(System.Drawing.Color? color)
		{
			UpdateTitleBar(new TitleBarData());
#if false
			try
			{
				CalculateColors(color, out Brush backgroundColor, out Brush textColor);
				
				if (titleBar != null)
				{
					System.Reflection.PropertyInfo propertyInfo = titleBar.GetType().GetProperty(ColorPropertyName);
					propertyInfo.SetValue(titleBar, backgroundColor, null);
				}
				else if (titleBarBorder != null)
				{
					System.Reflection.PropertyInfo propertyInfo = this.titleBarBorder.GetType().GetProperty(ColorPropertyName);
					propertyInfo.SetValue(this.titleBarBorder, backgroundColor, null);
				}

				if (titleBarTextBox != null)
				{
					//titleBarTextBox.Foreground = textColor;
				}
			}
			catch
			{
				System.Diagnostics.Debug.Fail("TitleBarModel.SetTitleBarColor - couldn't :(");
			}
#endif
		}

		public void CalculateColors(System.Drawing.Color? color, out Brush backgroundColor, out Brush textColor)
		{
			if (!color.HasValue)
			{
				backgroundColor = defaultBackgroundValue;
				textColor = defaultTextForeground;
			}
			else
			{
				var c = color.Value;

				backgroundColor = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));

				float luminance = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
				if (luminance > 128.0f)
					textColor = new SolidColorBrush(Color.FromRgb(0, 0, 0));
				else
					textColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
			}
		}

		// visual-studio 2017 & 2019
		//protected UIElement titleBar = null;

		// background-color
		//protected Border titleBarBorder = null;
		// textbox
		//protected TextBlock titleBarTextBox = null;

		protected Brush defaultBackgroundValue;
		protected Brush defaultTextForeground;

		protected const string ColorPropertyName = "Background";

		private static bool IsMsvc2017(string str) => str.StartsWith("15");
		private static bool IsMsvc2019(string str) => str.StartsWith("16");
	}

	internal class TitleBarModel2017 : TitleBarModel
	{
		public TitleBarModel2017(Window window) : base(window)
		{
#if false
			try
			{
				// set title bar of main window
				if (window == Application.Current.MainWindow)
				{
					var windowContentPresenter = VisualTreeHelper.GetChild(window, 0);
					var rootGrid = VisualTreeHelper.GetChild(windowContentPresenter, 0);

					titleBar = VisualTreeHelper.GetChild(rootGrid, 0);

					var dockPanel = VisualTreeHelper.GetChild(titleBar, 0);
					titleBarTextBox = VisualTreeHelper.GetChild(dockPanel, 3) as TextBlock;
				}
				// haha, do something else?
				else
				{
					var windowContentPresenter = VisualTreeHelper.GetChild(window, 0);
					var rootGrid = VisualTreeHelper.GetChild(windowContentPresenter, 0);
					if (VisualTreeHelper.GetChildrenCount(rootGrid) == 0)
						return;

					var rootDockPanel = VisualTreeHelper.GetChild(rootGrid, 0);
					var titleBar = VisualTreeHelper.GetChild(rootDockPanel, 0);
					var titleBar = VisualTreeHelper.GetChild(titleBar, 0);
					var border = VisualTreeHelper.GetChild(titleBar, 0);
					var contentPresenter = VisualTreeHelper.GetChild(border, 0);
					var grid = VisualTreeHelper.GetChild(contentPresenter, 0);

					this.titleBar = grid;

					this.titleBarTextBox = VisualTreeHelper.GetChild(grid, 1) as TextBlock;
				}

				if (this.titleBar != null)
				{
					System.Reflection.PropertyInfo propertyInfo = this.titleBar.GetType().GetProperty(ColorPropertyName);
					this.defaultBackgroundValue = propertyInfo.GetValue(this.titleBar) as Brush;
				}

				if (this.titleBarTextBox != null)
				{
					this.defaultTextForeground = this.titleBarTextBox.Foreground;
				}
			}
			catch
			{
			}
#endif
		}

		public override void ResetBackgroundToThemedDefault() { }

		public override void Reset()
		{
			throw new NotImplementedException();
		}

		public override void UpdateTitleBar(TitleBarData data)
		{
			throw new NotImplementedException();
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






	internal class TitleBarModel2019 : TitleBarModel
	{
		public TitleBarModel2019(Window window)
			: base(window)
		{ }

		public override void Reset()
		{

		}

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
				TitleBarForegroundProperty?.SetValue(TitleBar, data.TitleBarForegroundBrush);
				TitleBarBackgroundProperty?.SetValue(TitleBar, data.Vs2019TitleBarBackgroundBrush);
			}


			// info-blocks
			if (TitleBarInfoGrid != null)
			{
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

		public override void ResetBackgroundToThemedDefault()
		{
			
		}

		protected List<Border> synthesizedInfoBlocks = new List<Border>();

		public bool IsMainWindow => Window != null && Window == Application.Current.MainWindow;


		//
		// TitleBar
		//

		private System.Reflection.PropertyInfo TitleBarBackgroundProperty => cachedTitleBar.NullOr(x => x.GetType().GetProperty("Background"));
		private System.Reflection.PropertyInfo TitleBarForegroundProperty => cachedTitleBar.NullOr(x => x.GetType().GetProperty("Foreground"));

		private Border cachedTitleBar;
		protected Border TitleBar => cachedTitleBar ??
			(cachedTitleBar = IsMainWindow
				? Window.GetElement<Border>("MainWindowTitleBar")
				: Window.GetElement<Border>("MainWindowTitleBar"));

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
					r.SetBinding(Border.BackgroundProperty, new Binding()
					{
						Source = border,
						Path = new PropertyPath("Background")
					});
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
