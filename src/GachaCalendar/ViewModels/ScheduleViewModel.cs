using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using GachaCalendar.Models;
using GachaCalendar.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace GachaCalendar.ViewModels;

/// <summary>实验性 newPanel（DSH ScheduleView）的视图模型。</summary>
public sealed class SchedulePanelVm : INotifyPropertyChanged
{
	private static readonly string[] Weekdays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
	private static readonly Brush[] Palette =
	{
		Hex("#7C5CFC"), Hex("#3B82F6"), Hex("#10B981"), Hex("#F59E0B"), Hex("#EC4899"),
		Hex("#8B5CF6"), Hex("#06B6D4"), Hex("#EF4444"), Hex("#84CC16"), Hex("#6366F1"),
	};
	private const int BarDays = 14;
	private const int TodayIndex = 2;

	private Snapshot? _snapshot;
	private DateTime _now = DateTime.Now;
	private bool _expanded;
	private ScheduleItemVm? _focus;
	private string _scrapeText = "";
	private string _failureText = "";
	private string _failureTip = "";
	private readonly DispatcherTimer _cycleTimer;
	private int _page;
	private bool _hovered;

	public SchedulePanelVm()
	{
		_cycleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
		_cycleTimer.Tick += (_, _) => NextPage();
	}

	public ObservableCollection<ScheduleItemVm> Urgent { get; } = new();
	public ObservableCollection<ScheduleItemVm> FocusOthers { get; } = new();
	public ObservableCollection<ScheduleRowVm> Rows { get; } = new();
	public ObservableCollection<DayCellVm> Days { get; } = new();

	public string GlanceText { get; private set; } = "";
	public string DatePill { get; private set; } = "";
	public string UpdatedAgo { get; private set; } = "上次更新 尚未更新";
	public string ScrapeText { get => _scrapeText; private set => Set(ref _scrapeText, value); }
	public string FailureText { get => _failureText; private set => Set(ref _failureText, value); }
	public string FailureTip { get => _failureTip; private set => Set(ref _failureTip, value); }
	public bool HasFailure => FailureText.Length > 0;
	public ScheduleItemVm? Focus { get => _focus; private set => Set(ref _focus, value); }
	public string FocusGameName => Focus?.GameName ?? "";
	public string FocusTitle => Focus?.Show ?? "";
	public string FocusWhen => Focus?.FocusWhen ?? "";
	public string FocusDates => Focus?.DatesText ?? "";
	public string FocusTip => Focus?.Tip ?? "";
	public string FocusNumber => Focus?.FocusNumber ?? "";
	public string FocusUnit => Focus?.FocusUnit ?? "";
	public bool HasFocus => Focus != null;
	public bool HasOthers => FocusOthers.Count > 0;
	public int UrgentTotal { get; private set; }
	public bool CanExpand => UrgentTotal > 10;
	public bool HasExpand => CanExpand;
	public int PageCount => Math.Max(1, (UrgentTotal + 9) / 10);
	public string ExpandText => _expanded
		? "收起"
		: $"展开全部 {UrgentTotal} 条   {_page + 1} / {PageCount}";

	public event PropertyChangedEventHandler? PropertyChanged;

	public void Build(Snapshot? snapshot, DateTime now, string? scrapeText = null)
	{
		_snapshot = snapshot;
		_now = now;
		if (scrapeText != null) ScrapeText = scrapeText;
		Rebuild();
	}

	public void UpdateNow(DateTime now)
	{
		_now = now;
		if (_snapshot != null) Rebuild();
	}

	public void SetFocus(ScheduleItemVm? item)
	{
		Focus = item ?? Urgent.FirstOrDefault();
		RefreshFocusDetails();
	}

	public void ToggleExpand()
	{
		_expanded = !_expanded;
		_page = 0;
		FillUrgent();
		Raise(nameof(ExpandText));
		UpdateTimer();
	}

	public void SetHovered(bool hovered)
	{
		_hovered = hovered;
	}

	private void NextPage()
	{
		if (_expanded || _hovered || !CanExpand) return;
		_page = (_page + 1) % PageCount;
		FillUrgent();
		Raise(nameof(ExpandText));
	}

	private void Rebuild()
	{
		if (_snapshot == null) return;
		Urgent.Clear();
		Rows.Clear();

		Days.Clear();
		var today = _now.Date;
		for (var i = 0; i < BarDays; i++)
		{
			var day = today.AddDays(i - TodayIndex);
			Days.Add(new DayCellVm { Day = day.Day, Today = i == TodayIndex });
		}

		var visible = _snapshot.Games.Where(g => !g.Hidden).ToList();
		var all = new List<ScheduleItemVm>();
		foreach (var game in visible)
		{
			if (!_snapshot.Result.Games.TryGetValue(game.Id, out var rec)) continue;
			var gacha = BuildItem(game, rec, true);
			var ev = BuildItem(game, rec, false);
			if (gacha != null) all.Add(gacha);
			if (ev != null) all.Add(ev);
			Rows.Add(new ScheduleRowVm(game)
			{
				Gacha = gacha,
				Event = ev,
				GameColor = ColorFor(game, _snapshot.Config.GameColors),
			});
		}

		var nowMs = _now;
		var soon24 = all.Count(x => x.State != "ended" &&
			((x.Start > nowMs && x.Start <= nowMs.AddHours(24)) || (x.End > nowMs && x.End <= nowMs.AddHours(24))));
		var recent = all.Where(IsNear).OrderBy(Rank).ThenBy(x => x.State == "live" ? x.End : x.Start).ThenBy(x => x.GameName).ToList();
		UrgentTotal = recent.Count;
		_page = Math.Min(_page, PageCount - 1);
		GlanceText = $"24 小时内 {soon24} 条 · 7 天内 {recent.Count} 条";
		DatePill = $"{_now.Month} 月 {_now.Day} 日 · {Weekdays[(int)_now.DayOfWeek]}";
		FocusOthers.Clear();

		// 先重建行条的 today 标记
		foreach (var item in all)
		{
			item.BuildBars(_now);
		}
		_urgentCache = recent;
		FillUrgent();
		BuildFailure(all);
		var refreshed = _snapshot.Result.RefreshedAt > 0 ? _snapshot.Result.RefreshedAt : _snapshot.GeneratedAt;
		UpdatedAgo = refreshed > 0
			? "上次更新 " + Ago(refreshed, _now)
			: "上次更新 尚未更新";
		Raise(nameof(GlanceText));
		Raise(nameof(DatePill));
		Raise(nameof(UpdatedAgo));
		Raise(nameof(UrgentTotal));
		Raise(nameof(CanExpand));
		Raise(nameof(HasExpand));
		Raise(nameof(ExpandText));
		UpdateTimer();
	}

	private void UpdateTimer()
	{
		if (CanExpand && !_expanded)
		{
			if (!_cycleTimer.IsEnabled) _cycleTimer.Start();
		}
		else
		{
			_cycleTimer.Stop();
		}
	}

	private void BuildFailure(List<ScheduleItemVm> all)
	{
		var rows = new List<string>();
		foreach (var item in all)
		{
			if (item.Fail?.Kind == "down")
				rows.Add($"{item.GameName} · {(string.IsNullOrWhiteSpace(item.Fail.Reason) ? "抓取异常" : item.Fail.Reason)} —— 该条沿用上次成功数据");
			else if (item.Stale)
				rows.Add($"{item.GameName} · 本次刷新失败，沿用上次成功数据");
		}
		FailureTip = string.Join("\n", rows);
		FailureText = rows.FirstOrDefault() ?? "";
		Raise(nameof(HasFailure));
	}

	private List<ScheduleItemVm> _urgentCache = new();

	private void FillUrgent()
	{
		Urgent.Clear();
		if (_urgentCache.Count == 0) return;
		if (_expanded || _urgentCache.Count <= 10)
		{
			foreach (var item in _urgentCache) Urgent.Add(item);
		}
		else
		{
			for (var i = 0; i < 10; i++)
				Urgent.Add(_urgentCache[(_page * 10 + i) % _urgentCache.Count]);
		}
		SetFocus(Urgent.FirstOrDefault());
		Raise(nameof(ExpandText));
	}

	private ScheduleItemVm? BuildItem(GameMeta game, GameRecord rec, bool isGacha)
	{
		var title = isGacha ? rec.Banner : rec.Event;
		var dates = isGacha ? rec.BannerDates : rec.EventDates;
		if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(dates)) return null;
		var roles = isGacha ? rec.Roles : "";
		var show = isGacha && !string.IsNullOrWhiteSpace(roles)
			? RowBuilder.CleanRoleNames(roles, game.Id)
			: title;
		if (string.IsNullOrWhiteSpace(show)) show = title;
		var window = RowBuilder.ParseRange(dates, _now);
		var state = "long";
		var start = DateTime.MinValue;
		var end = DateTime.MinValue;
		if (window is { } w)
		{
			start = w.Start;
			end = w.End;
			var span = end - start;
			if (span > TimeSpan.FromDays(180)) state = "long";
			else if (_now < start) state = "wait";
			else if (_now > end) state = "ended";
			else state = "live";
		}
		var item = new ScheduleItemVm(game)
		{
			IsGacha = isGacha,
			Title = title ?? "",
			Show = show ?? "—",
			DatesText = dates ?? "",
			RawDates = (isGacha ? rec.BannerDatesRaw : rec.EventDatesRaw) is { Length: > 0 } raw ? raw : dates ?? "",
			Start = start,
			End = end,
			State = state,
			Fail = isGacha ? rec.GachaFail : rec.EventFail,
			Stale = isGacha ? rec.GachaStale : rec.EventStale,
			GameColor = ColorFor(game, _snapshot?.Config.GameColors),
			Tip = BuildTip(game, rec, isGacha),
		};
		item.BuildBars(_now);
		return item;
	}

	private static string BuildTip(GameMeta game, GameRecord rec, bool isGacha)
	{
		var title = isGacha ? rec.Banner : rec.Event;
		var dates = isGacha ? rec.BannerDatesRaw : rec.EventDatesRaw;
		if (string.IsNullOrWhiteSpace(dates)) dates = isGacha ? rec.BannerDates : rec.EventDates;
		var hover = isGacha ? rec.BannerHover : rec.EventHover;
		var parts = new[] { hover, title, dates }.Where(x => !string.IsNullOrWhiteSpace(x));
		return string.Join("\n", parts);
	}

	private static bool IsNear(ScheduleItemVm x)
	{
		if (x.State == "wait") return x.Start != DateTime.MinValue && x.Start - x.Now <= TimeSpan.FromDays(7);
		if (x.State == "live") return x.End - x.Now <= TimeSpan.FromDays(7);
		return false;
	}

	private static int Rank(ScheduleItemVm x) => x.State switch
	{
		"live" when x.Remaining <= TimeSpan.FromDays(1) => 0,
		"live" => 1,
		"wait" => 2,
		"long" => 3,
		_ => 4,
	};

	private static string Ago(long ts, DateTime now)
	{
		var min = (int)Math.Max(0, (now - DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime).TotalMinutes);
		if (min < 1) return "刚刚";
		if (min < 60) return min + " 分钟前";
		var h = min / 60;
		if (h < 24) return h + " 小时前";
		return h / 24 + " 天前";
	}

	private void RefreshFocusDetails()
	{
		FocusOthers.Clear();
		if (Focus != null)
		{
			foreach (var item in _urgentCache.Where(x => !ReferenceEquals(x, Focus)).Take(3))
				FocusOthers.Add(item);
		}
		Raise(nameof(FocusGameName));
		Raise(nameof(FocusTitle));
		Raise(nameof(FocusWhen));
		Raise(nameof(FocusDates));
		Raise(nameof(FocusTip));
		Raise(nameof(FocusNumber));
		Raise(nameof(FocusUnit));
		Raise(nameof(HasFocus));
		Raise(nameof(HasOthers));
	}

	private static Brush ColorFor(GameMeta game, IReadOnlyDictionary<string, string>? colors)
	{
		if (colors != null && colors.TryGetValue(game.Id, out var hex) && IsHex(hex))
			return (Brush)new BrushConverter().ConvertFromString(hex)!;
		var hash = 0;
		foreach (var ch in game.Name) hash = (hash * 31 + ch) & 0x7fffffff;
		return Palette[hash % Palette.Length];
	}

	private static bool IsHex(string value) =>
		value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

	private static Brush Hex(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		Raise(name);
	}

	private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ScheduleItemVm : INotifyPropertyChanged
{
	private ImageSource? _icon;
	private bool _hasIcon;

	public ScheduleItemVm(GameMeta game)
	{
		Game = game;
		_ = LoadIconAsync();
	}

	public GameMeta Game { get; }
	public bool IsGacha { get; init; }
	public string Title { get; init; } = "";
	public string Show { get; init; } = "";
	public string DatesText { get; init; } = "";
	public string RawDates { get; init; } = "";
	public DateTime Start { get; init; }
	public DateTime End { get; init; }
	public string State { get; init; } = "long";
	public SideFail? Fail { get; init; }
	public bool Stale { get; init; }
	public Brush GameColor { get; init; } = Brushes.SlateGray;
	public string Tip { get; init; } = "";
	public ObservableCollection<BarCellVm> Bars { get; } = new();

	public DateTime Now => _now;
	private DateTime _now = DateTime.Now;
	public TimeSpan Remaining => State == "live" ? End - _now : TimeSpan.Zero;
	public string GameName => Game.Name;
	public string IconGlyph => FirstGlyph(Game.Name);
	public ImageSource? Icon { get => _icon; private set { _icon = value; Raise(nameof(Icon)); } }
	public bool HasIcon { get => _hasIcon; private set { _hasIcon = value; Raise(nameof(HasIcon)); } }
	public string WhenText => State switch
	{
		"wait" => _now == DateTime.MinValue ? "等待" : Start.ToString("M-d HH:mm") + " 开始",
		"long" => "长期",
		"ended" => "已结束",
		_ => "还剩 " + Countdown(Remaining),
	};
	public string MetaText
	{
		get
		{
			if (State == "long" && End == DateTime.MinValue) return "无结束时间 · 长期";
			if (State == "wait") return Start.ToString("M-d HH:mm") + " 开始 · 至 " + End.ToString("M-d HH:mm");
			return Start.ToString("M-d HH:mm") + " → " + End.ToString("M-d HH:mm") + " · " + WhenText;
		}
	}
	public string Urgency => State == "live" && Remaining <= TimeSpan.FromDays(1) ? "1"
		: State == "live" && Remaining <= TimeSpan.FromDays(7) ? "2" : "";
	public string FocusNumber
	{
		get
		{
			if (State == "wait") return ((Start - _now).TotalDays < 1 ? 0 : (int)Math.Ceiling((Start - _now).TotalDays)).ToString(CultureInfo.InvariantCulture);
			if (State == "live") return Math.Max(0, (int)Math.Ceiling(Remaining.TotalDays)).ToString(CultureInfo.InvariantCulture);
			return "—";
		}
	}
	public string FocusUnit => State == "wait" ? "天后开始" : "天后结束";
	public string FocusWhen => State == "wait" ? "即将开始" : State == "live" ? "进行中" : State == "long" ? "长期" : "已结束";

	private async Task LoadIconAsync()
	{
		try
		{
			var icon = await IconLoader.GetAsync(Game.Icon);
			if (icon == null) return;
			Icon = icon;
			HasIcon = true;
		}
		catch { /* 降级首字 */ }
	}

	public void BuildBars(DateTime now)
	{
		_now = now;
		Bars.Clear();
		var today = now.Date;
		var days = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 2)).ToList();
		var whole = State == "long" || (State == "ended" && !OverlapsAxis(days));
		foreach (var day in days)
		{
			var filled = State != "ended" && (whole || (End > day && Start < day.AddDays(1)));
			Bars.Add(new BarCellVm
			{
				Filled = filled,
				Today = day == today,
				State = State,
				Color = GameColor,
				Day = day.Day,
			});
		}
		Raise(nameof(WhenText));
		Raise(nameof(MetaText));
		Raise(nameof(FocusNumber));
		Raise(nameof(Urgency));
		Raise(nameof(Remaining));
	}

	private bool OverlapsAxis(List<DateTime> days) =>
		State == "ended" && End > days[0] && Start < days[^1].AddDays(1);

	private static string Countdown(TimeSpan span)
	{
		if (span <= TimeSpan.Zero) return "0分";
		var d = (int)span.TotalDays;
		var h = span.Hours;
		var m = span.Minutes;
		if (d > 0) return $"{d}天{h}时";
		if (h > 0) return $"{h}时{m}分";
		return $"{Math.Max(1, m)}分";
	}

	private static string FirstGlyph(string name)
	{
		foreach (var ch in name)
		{
			if (char.IsWhiteSpace(ch)) continue;
			if ("·：:！!?？、，,（）()【】".Contains(ch)) continue;
			return ch.ToString();
		}
		return "?";
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class BarCellVm
{
	public bool Filled { get; init; }
	public bool Today { get; init; }
	public string State { get; init; } = "";
	public Brush Color { get; init; } = Brushes.SlateGray;
	public int Day { get; init; }
}

public sealed class DayCellVm
{
	public int Day { get; init; }
	public bool Today { get; init; }
}

public sealed class ScheduleRowVm : INotifyPropertyChanged
{
	private ImageSource? _icon;
	private bool _hasIcon;

	public ScheduleRowVm(GameMeta game)
	{
		Game = game;
		_ = LoadIconAsync();
	}

	public GameMeta Game { get; }
	public ScheduleItemVm? Gacha { get; init; }
	public ScheduleItemVm? Event { get; init; }
	public string Name => Game.Name;
	public string IconGlyph => FirstGlyph(Game.Name);
	public Brush GameColor { get; init; } = Brushes.SlateGray;
	public ImageSource? Icon { get => _icon; private set { _icon = value; Raise(nameof(Icon)); } }
	public bool HasIcon { get => _hasIcon; private set { _hasIcon = value; Raise(nameof(HasIcon)); } }

	private async Task LoadIconAsync()
	{
		try
		{
			var icon = await IconLoader.GetAsync(Game.Icon);
			if (icon == null) return;
			Icon = icon;
			HasIcon = true;
		}
		catch { /* 降级首字 */ }
	}

	private static string FirstGlyph(string name)
	{
		foreach (var ch in name)
		{
			if (char.IsWhiteSpace(ch)) continue;
			if ("·：:！!?？、，,（）()【】".Contains(ch)) continue;
			return ch.ToString();
		}
		return "?";
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
