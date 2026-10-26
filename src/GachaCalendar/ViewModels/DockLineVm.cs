using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace GachaCalendar.ViewModels;

/// <summary>任务栏轮播的一行：闲置态「二游日历」或一条临近卡池/活动。</summary>
public sealed class DockLineVm
{
	public bool IsIdle { get; private init; }
	public string ItemName { get; private init; } = "";
	public string Kind { get; private init; } = "";
	public string Status { get; private init; } = "";
	public string Tip { get; private init; } = "";
	public Brush Accent { get; private init; } = Brushes.DodgerBlue;
	public Brush StatusBrush { get; private init; } = Brushes.DodgerBlue;
	public ImageSource? IconSource { get; private init; }
	public bool HasIcon { get; private init; }
	public string IconGlyph { get; private init; } = "?";

	public Visibility IdleVisibility => IsIdle ? Visibility.Visible : Visibility.Collapsed;
	public Visibility ItemVisibility => IsIdle ? Visibility.Collapsed : Visibility.Visible;
	public Visibility KindVisibility => string.IsNullOrWhiteSpace(Kind) ? Visibility.Collapsed : Visibility.Visible;
	public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
	public Visibility GlyphVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;

	public static DockLineVm Idle(int count) => new()
	{
		IsIdle = true,
		ItemName = "二游日历",
		Status = count > 0 ? $"{count} 项临近" : "暂无临近",
		StatusBrush = Brushes.Gray,
		Tip = "二游日历 · 点击展开面板" + (count > 0 ? $"\n{count} 项临近排期" : ""),
	};

	public static DockLineVm FromChip(ChipVm chip) => new()
	{
		IsIdle = false,
		ItemName = chip.ItemName,
		Kind = chip.Kind,
		Status = chip.Status,
		Tip = chip.Tip,
		Accent = chip.Accent,
		StatusBrush = chip.Accent,
		IconSource = chip.IconSource,
		HasIcon = chip.HasIcon,
		IconGlyph = chip.IconGlyph,
	};
}
