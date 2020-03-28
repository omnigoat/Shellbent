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

	internal struct TitleBarData
	{
		public string TitleBarText;
		public Brush TitleBarForegroundColor;
		public Brush Vs2017TitleBarBackgroundColor;
		public Brush Vs2019TitleBarBackgroundColor;

		public List<TitleBarData> Infos;
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
			// title-bar background
			if (MainTitleBar != null)
			{
				{
					System.Reflection.PropertyInfo property = MainTitleBar.GetType().GetProperty("Background");
					property.SetValue(MainTitleBar, data.Vs2019TitleBarBackgroundColor ?? defaultMainTitleBarBackground, null);
				}

				// title-bar foreground
				{
					//System.Reflection.PropertyInfo property = MainTitleBar.GetType().GetProperty("Foreground");
					//property.SetValue(MainTitleBar, data.TitleBarForegroundColor ?? defaultMainTitleBarForeground, null);
				}
			}

			// title-bar text
			{
				var title = data.TitleBarText ?? defaultMainTitleBarText;
				if (Window.Title != title)
					Window.Title = title;
			}

			var k = PrimeTitleInfoBlock;

			// title-bar-infos for visual studio 2019
			{
				// something something
			}
		}

		public bool IsMainWindow => Window != null && Window == Application.Current.MainWindow;

		private UIElement cachedMainTitleBar;
		private Brush defaultMainTitleBarBackground;
		private Brush defaultMainTitleBarForeground;
		private string defaultMainTitleBarText;
		protected UIElement MainTitleBar
		{
			get
			{
				if (cachedMainTitleBar != null)
					return cachedMainTitleBar;

				cachedMainTitleBar = IsMainWindow
					? Window.GetElement<Border>("MainWindowTitleBar")
					: Window.GetElement<Border>("MainWindowTitleBar");

				if (cachedMainTitleBar != null)
				{
					System.Reflection.PropertyInfo backgroundProp = cachedMainTitleBar.GetType().GetProperty("Background");
					defaultMainTitleBarBackground = backgroundProp.GetValue(cachedMainTitleBar) as Brush;

					//System.Reflection.PropertyInfo foregroundProp = cachedMainTitleBar.GetType().GetProperty("Foreground");
					//defaultMainTitleBarBackground = foregroundProp.GetValue(cachedMainTitleBar) as Brush;

					defaultMainTitleBarText = Window.Title;
				}

				return cachedMainTitleBar;
			}
		}

		private TitleInfoBlock cachedPrimeTitleInfoBlock;
		private Brush defaultTitleInfoBlockBackground;
		private Brush defaultTitleInfoBlockForeground;
		protected TitleInfoBlock PrimeTitleInfoBlock =>
			cachedPrimeTitleInfoBlock ??
			(cachedPrimeTitleInfoBlock = (TitleInfoBlock)ModifyTitleBarInfoGrid() ?? TitleInfoBlock.Make(MainTitleBar
				?.GetElement<ContentControl>("PART_SolutionInfoControlHost")
				?.GetElement<Border>("TextBorder"))
				?.WithNotNull(x =>
				{
					defaultTitleInfoBlockBackground = x.Border.GetType().GetProperty("Background").GetValue(x.Border) as Brush;

					// this is almost certainly wrong, as if the main window isn't focused when
					// it loads, we might get a completely different colour
					//var s = Application.Current.FindResource(typeof(TextBlock)) as Style;
					//defaultTitleInfoBlockForeground = x.TextBox.GetType().GetProperty("Foreground").GetValue(x.TextBox) as Brush;
				}));

		private List<TitleInfoBlock> cachedAdditionalTitleBarInfoBlocks = null;
		protected List<TitleInfoBlock> AdditionalTitleBarInfoBlocks =>
			cachedAdditionalTitleBarInfoBlocks ??
			(cachedAdditionalTitleBarInfoBlocks = (List<TitleInfoBlock>)ModifyTitleBarInfoGrid() ??
				new List<TitleInfoBlock>());

		private bool cachedTitleBarInfoGridModified = false;
		protected object ModifyTitleBarInfoGrid()
		{
			if (cachedTitleBarInfoGridModified)
				return null;

			MainTitleBar
				?.GetElement<ContentControl>("PART_SolutionInfoControlHost")
				?.GetElement<Grid>()
				?.WithNotNull(x =>
				{
					x.ColumnDefinitions[1].Width = GridLength.Auto;
					x.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

					cachedTitleBarInfoGridModified = true;
				});

			return null;
		}

		//protected void LayoutTitleBarInfos(IEnumerable)
		protected UIElement DeconstructTitleBar()
		{
#if false
			// see if we have info - if we don't we probably loaded an empty Visual Studio
			partInfoControlHost = titleBar.GetElement<ContentControl>("PART_SolutionInfoControlHost");
			if (!partInfoControlHost.HasContent)
				return null;

			// get 
			var iegrid = partInfoControlHost.GetElement<Grid>();

			var labels = iegrid
				.GetChildren<Border>()
				.Where(x => x.GetElement<TextBlock>() != null)
				.Select(x => new TitleInfoBlock(x));

			this.titleBarBorder = partInfoControlHost?.GetElement<Border>("TextBorder");
			this.titleBarTextBox = this.titleBarBorder?.GetElement<TextBlock>();

			var g = new Border
			{
				Background = titleBarBorder.Background,
				BorderBrush = titleBarBorder.BorderBrush,
				BorderThickness = titleBarBorder.BorderThickness,
				Padding = new Thickness(titleBarBorder.Padding.Left, titleBarBorder.Padding.Top, titleBarBorder.Padding.Right, titleBarBorder.Padding.Bottom),
				Child = new Border
				{
					Margin = new Thickness(0, 4.5, 0, 4.5),
					Child = new TextBlock
					{
						Text = "atma",

						FontFamily = titleBarTextBox.FontFamily,
						FontWeight = titleBarTextBox.FontWeight,
						FontSize = titleBarTextBox.FontSize,
						FontStyle = titleBarTextBox.FontStyle,
						FontStretch = titleBarTextBox.FontStretch,
						Foreground = titleBarTextBox.Foreground,
						TextAlignment = titleBarTextBox.TextAlignment,
						TextEffects = titleBarTextBox.TextEffects,
						Padding = titleBarTextBox.Padding,
						BaselineOffset = titleBarTextBox.BaselineOffset,
						Width = Math.Floor(MeasureString(titleBarTextBox, "atma").Width)
					}
				}
			};

			iegrid.ColumnDefinitions[1].Width = GridLength.Auto;
			iegrid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});

			g.SetValue(Grid.ColumnProperty, 2);
			//g.HorizontalAlignment = HorizontalAlignment.;
			//iegrid.SetValue(Grid.WidthProperty, Size)
			iegrid.Children.Add(g);
#endif
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

		protected ContentControl partInfoControlHost = null;
	}
}
