using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GachaCalendar.Controls;

/// <summary>
/// 复刻 DSH 面板的 hover 跑马灯：文字超出单元格时才左右滚动，未超宽保持省略号。
/// 用法：外层 Border/Grid 要 ClipToBounds=True，TextBlock 上加 controls:Marquee.Enabled="True"。
/// </summary>
public static class Marquee
{
	private sealed class State
	{
		public TextTrimming Trimming = TextTrimming.CharacterEllipsis;
		public TextWrapping Wrapping = TextWrapping.NoWrap;
		public TranslateTransform? Transform;
	}

	private static readonly DependencyProperty StateProperty =
		DependencyProperty.RegisterAttached("State", typeof(State), typeof(Marquee), new PropertyMetadata(null));

	public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
		"Enabled", typeof(bool), typeof(Marquee), new PropertyMetadata(false, OnEnabledChanged));

	public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
	public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

	private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBlock text) return;
		if ((bool)e.NewValue)
		{
			text.Loaded += OnLoaded;
			text.Unloaded += OnUnloaded;
			text.MouseEnter += OnMouseEnter;
			text.MouseLeave += OnMouseLeave;
		}
		else
		{
			text.Loaded -= OnLoaded;
			text.Unloaded -= OnUnloaded;
			text.MouseEnter -= OnMouseEnter;
			text.MouseLeave -= OnMouseLeave;
			Stop(text);
		}
	}

	private static void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is TextBlock text) text.SetValue(StateProperty, new State());
	}

	private static void OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (sender is TextBlock text) Stop(text);
	}

	private static void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (sender is not TextBlock text) return;
		if (text.TextTrimming == TextTrimming.None) return;
		var state = text.GetValue(StateProperty) as State ?? new State();
		text.SetValue(StateProperty, state);

		var width = MeasureTextWidth(text);
		var overflow = width - text.ActualWidth;
		if (overflow <= 1) return;

		state.Trimming = text.TextTrimming;
		state.Wrapping = text.TextWrapping;
		text.TextTrimming = TextTrimming.None;
		text.TextWrapping = TextWrapping.NoWrap;

		var transform = new TranslateTransform();
		state.Transform = transform;
		text.RenderTransform = transform;

		var animation = new DoubleAnimationUsingKeyFrames
		{
			Duration = TimeSpan.FromSeconds(3.5),
			RepeatBehavior = RepeatBehavior.Forever,
		};
		animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
		animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.10)));
		animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromPercent(0.45)));
		animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromPercent(0.75)));
		animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));
		transform.BeginAnimation(TranslateTransform.XProperty, animation);
	}

	private static void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (sender is TextBlock text) Stop(text);
	}

	private static void Stop(TextBlock text)
	{
		if (text.GetValue(StateProperty) is not State state) return;
		state.Transform?.BeginAnimation(TranslateTransform.XProperty, null);
		if (state.Transform != null) state.Transform.X = 0;
		text.RenderTransform = null;
		text.TextTrimming = state.Trimming;
		text.TextWrapping = state.Wrapping;
		state.Transform = null;
	}

	private static double MeasureTextWidth(TextBlock text)
	{
		var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
		var dpi = VisualTreeHelper.GetDpi(text);
		var formatted = new FormattedText(
			text.Text ?? "",
			CultureInfo.CurrentUICulture,
			text.FlowDirection,
			typeface,
			text.FontSize,
			text.Foreground,
			dpi.PixelsPerDip);
		return formatted.WidthIncludingTrailingWhitespace;
	}
}
