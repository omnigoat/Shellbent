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
using System.Windows.Shapes;
using stdole;
using System.ComponentModel;
using System.Reflection;

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
		public string AltText;
		public SolidColorBrush TextBrush;
		public SolidColorBrush BackgroundBrush;
	}
	
	internal struct TitleBarData
	{
		public string TitleBarText;
		public SolidColorBrush TitleBarForegroundBrush;
		public SolidColorBrush TitleBarBackgroundBrush;
		public bool? QuickSearchVisible;
		public bool? ColorizeWindowGlow;

		public List<TitleBarInfoBlockData> Infos;
	}





	internal abstract class WindowWrapper
	{
		public WindowWrapper(Window window)
		{
			Window = window;

			OriginalActiveGlowColor = Window.GetType().GetProperty("ActiveGlowColor")?.GetValue(Window) as Color?;
		}

		public static WindowWrapper Make(string vsVersion, Window w)
		{
			try
			{
				if (IsMainWindow(w))
				{
					if (IsMsvc2022(vsVersion))
						return new Vs2022MainWindowWrapper(w);
					else if (IsMsvc2019(vsVersion))
						return new Vs2019MainWindowWrapper(w);
				}
				else
				{
					return new ToolWindowWrapper(w);
				}
			}
			catch
			{
			}

			return null;
		}

		public Window Window { get; private set; }

		protected Color? OriginalActiveGlowColor { get; private set; }

		public abstract void UpdateStyling(TitleBarData data);

		protected static bool IsMainWindow(Window w) => w?.GetElement<UIElement>("MainWindowTitleBar") != null;
		protected static bool IsMsvc2017(string str) => str.StartsWith("15");
		protected static bool IsMsvc2019(string str) => str.StartsWith("16");
		protected static bool IsMsvc2022(string str) => str.StartsWith("17");
	}



	internal class ToolWindowWrapper : WindowWrapper
	{
		public ToolWindowWrapper(Window w)
			: base(w)
		{
			Window.Activated += Window_ActivationChanged;
			Window.Deactivated += Window_ActivationChanged;
		}

		private void Window_ActivationChanged(object sender, EventArgs e)
		{
			UpdateToolWindowColors(titleColor);
		}

		public override void UpdateStyling(TitleBarData data)
		{
			titleColor = data.TitleBarBackgroundBrush?.Color;
			colorizeWindowGlow = data.ColorizeWindowGlow ?? false;

			UpdateToolWindowColors(data.TitleBarBackgroundBrush?.Color);
		}

		private void UpdateToolWindowColors(Color? maybeColor)
		{
			if (maybeColor is Color color)
			{
				var brush = new SolidColorBrush(color);
				var brightBrush = WindowUtils.CalculateHighlightBrush(color, 0.5f);

				// background
				ToolWindowBorder?.SetValue(Border.BackgroundProperty, brush);
				ToolWindowBorder?.SetValue(Border.BorderBrushProperty, brush);

				// the drag-handle is conditionally present, but seems to derive its
				// colour from somewhere else, so we must conditionally paint it
				if (Window.IsActive)
					DragHandle?.SetValue(Shape.FillProperty, GenerateFillBrush(brightBrush));
				else
					DragHandle?.ClearValue(Shape.FillProperty);

				if (colorizeWindowGlow)
				{
					Window.GetType().GetProperty("ActiveGlowColor")
						?.SetValue(Window, brightBrush.Color);
				}
				else
				{
					Window.GetType().GetProperty("ActiveGlowColor")
						?.SetValue(Window, OriginalActiveGlowColor);
				}
			}
			else
			{
				ToolWindowBorder?.ClearValue(Border.BackgroundProperty);
				ToolWindowBorder?.ClearValue(Border.BorderBrushProperty);

				DragHandle?.ClearValue(Shape.FillProperty);

				Window.GetType().GetProperty("ActiveGlowColor")
					?.SetValue(Window, OriginalActiveGlowColor);
			}
		}

		private Brush GenerateFillBrush(Brush brush)
		{
			var vsgeom = DragHandleBrush.Drawing as GeometryDrawing;
			var geom = new GeometryDrawing(brush, vsgeom.Pen, vsgeom.Geometry);
			var fillBrush = DragHandleBrush.Clone();
			fillBrush.Drawing = geom;
			return fillBrush;
		}


		private UIElement cachedTitleBar;
		private UIElement TitleBar =>
			cachedTitleBar ??
			(cachedTitleBar = Window
				.GetElement<UIElement>("TitleBar"));

		private UIElement cachedTitleBarTextBlock;
		private UIElement TitleBarTextBlock =>
			cachedTitleBarTextBlock ??
			(cachedTitleBarTextBlock = TitleBar
				?.GetElement<TextBlock>("WindowTitle"));

		private Border cachedToolWindowBorder;
		private Border ToolWindowBorder => cachedToolWindowBorder ??
			(cachedToolWindowBorder = Window
				?.GetElement<Border>("ContentBorder")
				?.GetElement<Border>("Bd"));

		private Rectangle cachedDragHandle;
		private Rectangle DragHandle => cachedDragHandle ??
			(cachedDragHandle = ToolWindowBorder
				?.GetElement<Rectangle>("DragHandleTexture"));

		// drag-handle brush (the ::::::::: thing to the side of the tool-window title)
		private DrawingBrush cachedDragHandleBrush;
		private DrawingBrush DragHandleBrush => cachedDragHandleBrush ??
			(cachedDragHandleBrush = (DragHandle?.Fill as DrawingBrush));

		private Color? titleColor;
		private bool colorizeWindowGlow = false;
	}

	

	internal class Vs2019MainWindowWrapper : WindowWrapper
	{
		public Vs2019MainWindowWrapper(Window window)
			: base(window)
		{
			Window.Deactivated += Window_ActivationChanged;
			Window.Activated += Window_ActivationChanged;
		}

		private void Window_ActivationChanged(object sender, EventArgs e)
		{
			Func<InfoBlock, Brush> makeBlockForegroundBrush = (InfoBlock block) => Window.IsActive
				? new SolidColorBrush(block.TextColor.Value)
				: WindowUtils.CalculateRelativeColorBrush(block.TextColor.Value, 0.75f);

			foreach (var block in synthesizedInfoBlocks)
			{
				var textblock = block.Element?.GetElement<TextBlock>();
				if (textblock == null)
					continue;

				if (!block.TextColor.HasValue)
				{
					textblock.ClearValue(TextBlock.ForegroundProperty);
				}
				else
				{
					textblock.SetValue(TextBlock.ForegroundProperty, makeBlockForegroundBrush(block));
				}
			}
		}

		private class TitleInfoBlock
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

		public override void UpdateStyling(TitleBarData data)
		{
			// update text, which no longer gets shown in 2019+ on the GUI,
			// but is viewable when you hover over the window on the Taskbar
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

			// update the background to title-bar
			if (data.TitleBarBackgroundBrush == null)
			{
				TitleBar?.ClearValue(Border.BackgroundProperty);
				TitleBarVsMenu?.ClearValue(Panel.BackgroundProperty);
			}
			else
			{
				TitleBar?.SetValue(Border.BackgroundProperty, data.TitleBarBackgroundBrush);
				TitleBarVsMenu?.SetValue(Panel.BackgroundProperty, data.TitleBarBackgroundBrush);
			}

			// if we have an override background-colour, then calculate a nice default
			// background-colour for the blocks, and apply it to the prime block
			if (PrimeTitleInfoBlock != null)
			{
				if (data.TitleBarBackgroundBrush != null)
				{
					// just 25% less bright
					PrimeTitleInfoBlock.Border.Background = new SolidColorBrush(data.TitleBarBackgroundBrush.Color * 0.75f);
				}
				else
				{
					PrimeTitleInfoBlock?.Border.ClearValue(Border.BackgroundProperty);
				}
			}

			if (PrimeTitleInfoBlock != null)
			{
				// remove all previously-synthesized info-blocks
				foreach (var block in synthesizedInfoBlocks.Select(x => x.Element))
					TitleBarInfoGrid.Children.Remove(block);

				// reset column-definitions from before
				if (TitleBarInfoGrid.ColumnDefinitions.Count > 2)
					TitleBarInfoGrid.ColumnDefinitions.RemoveRange(2, TitleBarInfoGrid.ColumnDefinitions.Count - 2);

				if (data.Infos != null)
				{
					if (cachedTitleBarInfoGrid != null)
					{
						cachedTitleBarInfoGrid.ColumnDefinitions[1].Width = GridLength.Auto;
					}
				
					// recalculate title-bar-infos
					synthesizedInfoBlocks = data.Infos
						.Where(x => !string.IsNullOrEmpty(x.Text))
						.Select((x, idx) => new InfoBlock { Element = MakeInfoBlock(x, idx), TextColor = x.TextBrush?.Color })
						.ToList();

					// add new column-definitions to match
					foreach (var i in synthesizedInfoBlocks)
						TitleBarInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

					// reset all colours to what we are at the time
					Window_ActivationChanged(null, null);

					// add all user-defined blocks
					foreach (var block in synthesizedInfoBlocks.Select(x => x.Element))
						TitleBarInfoGrid.Children.Add(block);
				}
				else if (cachedTitleBarInfoGrid != null)
				{
					// reflow the measuring of the prime info-block back to default values so that if we 
					// open a new solution it will correctly update to the bounds of the new solution's name
					cachedTitleBarInfoGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
				}

				if (cachedTitleBarInfoGrid != null)
				{
					//cachedTitleBarInfoGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
					cachedTitleBarInfoGrid.UpdateLayout();
				}
			}

			// set the glow value for the window
			if ((data.ColorizeWindowGlow ?? false) && WindowUtils.CalculateHighlightBrush(data.TitleBarBackgroundBrush?.Color, 0.5f) is SolidColorBrush glow)
			{
				Window.GetType().GetProperty("ActiveGlowColor")
					?.SetValue(Window, glow.Color);
			}
			else
			{
				// thought there'd be an easier way to do this
				Window.GetType().GetProperty("ActiveGlowColor")
					?.SetValue(Window, OriginalActiveGlowColor);
			}
		}

		protected struct InfoBlock
		{
			public Border Element;
			public Color? TextColor;
		}

		protected List<InfoBlock> synthesizedInfoBlocks = new List<InfoBlock>();

		private UIElement cachedTitleBar;
		protected UIElement TitleBar =>
			cachedTitleBar ??
			(cachedTitleBar = Window
				.GetElement<UIElement>("MainWindowTitleBar"));

		private UIElement cachedVsMenu;
		protected UIElement TitleBarVsMenu => cachedVsMenu ??
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
				?.GetElement<Grid>());

		private TitleInfoBlock cachedPrimeTitleInfoBlock;
		private TitleInfoBlock PrimeTitleInfoBlock =>
			cachedPrimeTitleInfoBlock ??
			(cachedPrimeTitleInfoBlock = TitleInfoBlock.Make(TitleBarInfoGrid
				?.GetElement<Border>("TextBorder")));

		private Border MakeInfoBlock(TitleBarInfoBlockData data, int idx)
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
					ToolTip = data.AltText,

					// just a little more separation than 1px
					Margin = new Thickness(2, 0, 0, 0),

					Child = new Border
					{
						Margin = new Thickness(0, 4.5, 0, 4.5),
						Child = new TextBlock
						{
							Text = data.Text,
							Foreground = data.TextBrush,
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

				if ((r.Child as Border).Child is TextBlock ntb)
				{
					// always bind the foreground colour of our blocks to the prime block, as this
					// will work if the user changes the theme. specified colours will be set as local
					// values. those local values will be cleared if the user chooses no foreground
					// colour, leaving this binding intact and non-overridden
					ntb.SetBinding(TextBlock.ForegroundProperty, new Binding() { Source = text, Path = new PropertyPath("Foreground") });

					// match the prime-info-block for COHESION
					ntb.SetBinding(TextBlock.FontWeightProperty, new Binding() { Source = text, Path = new PropertyPath("FontWeight") });
					ntb.SetBinding(TextBlock.FontSizeProperty, new Binding() { Source = text, Path = new PropertyPath("FontSize") });
					ntb.SetBinding(TextBlock.FontStyleProperty, new Binding() { Source = text, Path = new PropertyPath("FontStyle") });
					ntb.SetBinding(TextBlock.FontStretchProperty, new Binding() { Source = text, Path = new PropertyPath("FontStretch") });
				}

				r.SetValue(Grid.ColumnProperty, idx + 2);
				return r;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			return null;
		}

		
	}

	internal class Vs2022MainWindowWrapper : Vs2019MainWindowWrapper
	{
		public Vs2022MainWindowWrapper(Window window) : base(window)
		{
			TitleBar.LayoutUpdated += TitleBar_LayoutUpdated;
		}

		private void TitleBar_LayoutUpdated(object sender, EventArgs e)
		{
			// this property is only useful when this extension loads before the
			// quicksearch extension does. so we'll listen to the TitleBar for
			// layout events, identify when the quicksearch extension is loaded
			// and its UI inserted, and immediately update its visiblity

			if (SearchBoxLoaded)
			{
				UpdateQuickSearch(quickSearchVisibilityRequired.GetValueOrDefault(true));

				// once we've triggered once, we can _assume_ that the quick-search
				// box won't be removed/added again
				TitleBar.LayoutUpdated -= TitleBar_LayoutUpdated;
			}
		}

		private void UpdateQuickSearch(bool visibilityDesired)
		{
			if (!SearchBoxLoaded)
				return;

			if (visibilityDesired && !quickSearchVisible)
			{
				FrameControlContainer.MinWidth = cachedFrameControlContainerMinWidth;
				FrameControlContainer.MaxWidth = cachedFrameControlContainerMaxWidth;
			}
			else if (!visibilityDesired && quickSearchVisible)
			{
				cachedFrameControlContainerMinWidth = FrameControlContainer.MinWidth;
				cachedFrameControlContainerMaxWidth = FrameControlContainer.MaxWidth;

				FrameControlContainer.MinWidth = 0;
				FrameControlContainer.MaxWidth = 0;
			}
		}

		public override void UpdateStyling(TitleBarData data)
		{
			base.UpdateStyling(data);

			quickSearchVisibilityRequired = data.QuickSearchVisible;
			bool visibilityDesired = quickSearchVisibilityRequired.GetValueOrDefault(true);
			UpdateQuickSearch(visibilityDesired);
		}

		// current assumed status of quick-search visibility
		private bool quickSearchVisible =>
			(FrameControlContainer?.MinWidth ?? 0.0) > 0.0;

		// we must cache the value from the .shellbent file, so that if we were
		// the first extension loaded, when the quick-search extension is loaded
		// next and adds its control, we can immediately apply the cached value.
		private bool? quickSearchVisibilityRequired;

		// cached dimensinos for restoring searchbox, as for some reason
		// whoever wrote it just set the values locally on the control
		private double cachedFrameControlContainerMinWidth = 0;
		private double cachedFrameControlContainerMaxWidth = 0;

		protected bool SearchBoxLoaded =>
			FrameControlContainer?.GetElement<UIElement>("PART_SearchButton") != null;

		private FrameworkElement cachedFrameControlContainer;
		protected FrameworkElement FrameControlContainer =>
			cachedFrameControlContainer ??
			(cachedFrameControlContainer = TitleBar
				?.GetElement<FrameworkElement>("PART_TitleBarLeftFrameControlContainer"));
	}



	internal static class WindowUtils
	{
		public static DependencyProperty FindDependencyProperty(this DependencyObject target, string propName)
		{
			FieldInfo fInfo = target.GetType().GetField(propName, BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public);

			if (fInfo == null) return null;

			return (DependencyProperty)fInfo.GetValue(null);
		}

		public static SolidColorBrush CalculateForegroundBrush(Color? color)
		{
			if (!color.HasValue)
				return null;

			var c = color.Value;

			float luminance = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

			if (luminance > 128.0f) // very bright, use black
				return new SolidColorBrush(Color.FromRgb(0, 0, 0));
			else if (luminance > 48.0f) // medium, use white
				return new SolidColorBrush(Color.FromRgb(255, 255, 255));
			else // very dark, use vanilla msvc grey
				return null;
		}

		public static SolidColorBrush CalculateRelativeColorBrush(Color? color, float mulitplier)
		{
			if (!color.HasValue)
				return null;

			// perceived luminance
			float luminance = (0.299f * color.Value.R + 0.587f * color.Value.G + 0.114f * color.Value.B) / 255.0f;

			return new SolidColorBrush(color.Value * (1.0f + (mulitplier - 1.0f) * (float)Math.Sqrt(luminance)));
		}

		public static SolidColorBrush CalculateHighlightBrush(Color? color, float delta)
		{
			if (!color.HasValue)
				return null;

			float sum = color.Value.R + color.Value.G + color.Value.B;
			byte dR = (byte)((color.Value.R / sum) * (255.0f * delta));
			byte dG = (byte)((color.Value.G / sum) * (255.0f * delta));
			byte dB = (byte)((color.Value.B / sum) * (255.0f * delta));

			var c = Color.FromRgb(
				(byte)Math.Min(color.Value.R + dR, 255),
				(byte)Math.Min(color.Value.G + dG, 255),
				(byte)Math.Min(color.Value.B + dB, 255));

			return new SolidColorBrush(c);
		}
	}
}
