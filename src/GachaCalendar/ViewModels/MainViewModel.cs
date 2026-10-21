using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GachaCalendar.Models;
using GachaCalendar.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace GachaCalendar.ViewModels;

public sealed class RowVm : INotifyPropertyChanged
{
	private readonly GameMeta _game;
	private readonly GameRecord? _rec;

	private string _badgeText = "";
	private string _badgeKind = "";
	private bool _hasBadge;
	private string _gachaText = "—";
	private string _gachaTip = "";
	private string _gachaDateText = "";
	private string _gachaDateTip = "";
	private string _eventText = "—";
	private string _eventTip = "";
	private string _eventDateText = "";
	private string _eventDateTip = "";
	private ImageSource? _iconSource;
	private bool _hasIcon;
	private string _iconGlyph = "?";
	private Brush _iconBrush = Brushes.SlateGray;

	public RowVm(GameMeta game, GameRecord? rec)
	{
		_game = game;
		_rec = rec;
		Name = game.Name;
		IconGlyph = FirstGlyph(game.Name);
		IconBrush = FallbackBrush(game.Name);
		Refresh(DateTime.Now);
		_ = LoadIconAsync(game.Icon);
	}

	public string Name { get; }
	public string GameId => _game.Id;
	public GameRecord? Record => _rec;

	public string BadgeText { get => _badgeText; private set => Set(ref _badgeText, value); }
	public string BadgeKind { get => _badgeKind; private set => Set(ref _badgeKind, value); }
	public bool HasBadge { get => _hasBadge; private set => Set(ref _hasBadge, value); }
	public string GachaText { get => _gachaText; private set => Set(ref _gachaText, value); }
	public string GachaTip { get => _gachaTip; private set => Set(ref _gachaTip, value); }
	public string GachaDateText { get => _gachaDateText; private set => Set(ref _gachaDateText, value); }
	public string GachaDateTip { get => _gachaDateTip; private set => Set(ref _gachaDateTip, value); }
	public string EventText { get => _eventText; private set => Set(ref _eventText, value); }
	public string EventTip { get => _eventTip; private set => Set(ref _eventTip, value); }
	public string EventDateText { get => _eventDateText; private set => Set(ref _eventDateText, value); }
	public string EventDateTip { get => _eventDateTip; private set => Set(ref _eventDateTip, value); }

	public ImageSource? IconSource { get => _iconSource; private set => Set(ref _iconSource, value); }
	public bool HasIcon { get => _hasIcon; private set => Set(ref _hasIcon, value); }
	public string IconGlyph { get => _iconGlyph; private set => Set(ref _iconGlyph, value); }
	public Brush IconBrush { get => _iconBrush; private set => Set(ref _iconBrush, value); }

	private async Task LoadIconAsync(string? url)
	{
		if (string.IsNullOrWhiteSpace(url)) return;
		try
		{
			var icon = await IconLoader.GetAsync(url);
			if (icon == null) return;
			IconSource = icon;
			HasIcon = true;
		}
		catch { /* 图标失败不影响行 */ }
	}

	private static readonly Brush[] IconPalette =
	{
		new SolidColorBrush(Color.FromRgb(0x9A, 0x6B, 0xD6)),
		new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xD9)),
		new SolidColorBrush(Color.FromRgb(0x4F, 0xA8, 0x7A)),
		new SolidColorBrush(Color.FromRgb(0xC9, 0x8A, 0x4B)),
		new SolidColorBrush(Color.FromRgb(0xB8, 0x6A, 0x8C)),
	};

	private static Brush FallbackBrush(string name)
	{
		var hash = 0;
		foreach (var ch in name) hash = (hash * 31 + ch) & 0x7fffffff;
		return IconPalette[hash % IconPalette.Length];
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

	public void Refresh(DateTime now)
	{
		var d = RowBuilder.Build(_game, _rec, now);
		BadgeText = d.BadgeText;
		BadgeKind = d.BadgeKind;
		HasBadge = d.HasBadge;
		GachaText = d.GachaText;
		GachaTip = d.GachaTip;
		GachaDateText = d.GachaDateText;
		GachaDateTip = d.GachaDateTip;
		EventText = d.EventText;
		EventTip = d.EventTip;
		EventDateText = d.EventDateText;
		EventDateTip = d.EventDateTip;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}

public sealed class ChipVm
{
	public string GameName { get; init; } = "";
	public string Remaining { get; init; } = "";
	public string Tip { get; init; } = "";
	public Brush Accent { get; init; } = Brushes.DodgerBlue;
}

public sealed class MainViewModel : INotifyPropertyChanged
{
	private static readonly Brush[] DarkPalette =
	{
		Hex("#7AB0FF"), Hex("#8BD17C"), Hex("#F2B36B"), Hex("#E58BE5"), Hex("#6FD3D3"), Hex("#E58B8B"),
	};

	private static readonly Brush[] LightPalette =
	{
		Hex("#2563EB"), Hex("#2E9E4F"), Hex("#B45309"), Hex("#A21CAF"), Hex("#0E7490"), Hex("#B91C1C"),
	};

	private Snapshot? _snapshot;
	private AppSettings _settings = new();
	private int _lastMinute = -1;

	private string _statusText = "载入中…";
	private string _statusTip = "";
	private string _updatedText = "—";
	private string _headerCount = "";
	private string _dataStatusText = "数据：—";
	private string _scrapeText = "";
	private string _dataPath = "";
	private bool _isSample;
	private bool _isRefreshing;
	private bool _canRefresh;
	private bool _engineAvailable;
	private string _refreshTip = "应用内刷新不可用";

	public ObservableCollection<RowVm> Rows { get; } = new();
	public ObservableCollection<ChipVm> Chips { get; } = new();

	public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
	public string StatusTip { get => _statusTip; private set => Set(ref _statusTip, value); }
	public string UpdatedText { get => _updatedText; private set => Set(ref _updatedText, value); }
	public string HeaderCount { get => _headerCount; private set => Set(ref _headerCount, value); }
	public string DataStatusText { get => _dataStatusText; private set => Set(ref _dataStatusText, value); }
	public string ScrapeText { get => _scrapeText; private set => Set(ref _scrapeText, value); }
	public string DataPath { get => _dataPath; private set => Set(ref _dataPath, value); }
	public bool IsSample { get => _isSample; private set => Set(ref _isSample, value); }
	public bool IsRefreshing { get => _isRefreshing; private set => Set(ref _isRefreshing, value); }
	public bool CanRefresh { get => _canRefresh; private set => Set(ref _canRefresh, value); }
	public string RefreshTip { get => _refreshTip; private set => Set(ref _refreshTip, value); }
	public bool HasChips => Chips.Count > 0;
	public bool HasNoChips => Chips.Count == 0;
	public bool HasError { get; private set; }
	public string ErrorText { get; private set; } = "";

	public event PropertyChangedEventHandler? PropertyChanged;

	public void Apply(Snapshot snapshot, AppSettings settings, bool isSample, string? path)
	{
		_snapshot = snapshot;
		_settings = settings;
		IsSample = isSample;
		DataPath = path ?? "";

		Rows.Clear();
		foreach (var game in snapshot.Games.Where(g => !g.Hidden))
		{
			snapshot.Result.Games.TryGetValue(game.Id, out var rec);
			Rows.Add(new RowVm(game, rec));
		}

		HeaderCount = $"{Rows.Count} 款";
		RefreshStatus();
		Tick(DateTime.Now, force: true);
	}

	/// <summary>引擎 Host 可用性变化（启动失败/退出时清空 CanRefresh）。</summary>
	public void SetEngineState(bool available, string? error)
	{
		_engineAvailable = available;
		CanRefresh = available && !IsRefreshing;
		RefreshTip = available ? "刷新" : "应用内刷新不可用：" + (error ?? "未知原因");
		if (!available && !string.IsNullOrWhiteSpace(error)) StatusTip = error;
	}

	public void SetRefreshing(bool value)
	{
		IsRefreshing = value;
		CanRefresh = _engineAvailable && !value;
		RefreshTip = value ? "刷新中…" : (_engineAvailable ? "刷新" : "应用内刷新不可用");
	}

	public void SetRefreshError(string message) => StatusTip = message;

	public void ShowError(string message)
	{
		_snapshot = null;
		Rows.Clear();
		Chips.Clear();
		HasError = true;
		ErrorText = message;
		StatusText = "载入失败";
		DataStatusText = "数据：载入失败";
		ScrapeText = message;
		StatusTip = message;
		UpdatedText = "刷新：—";
		HeaderCount = "";
		Raise(nameof(HasError));
		Raise(nameof(ErrorText));
		Raise(nameof(HasChips));
		Raise(nameof(HasNoChips));
	}

	public void UpdateSettings(AppSettings settings)
	{
		_settings = settings;
		Tick(DateTime.Now, force: true);
	}

	public void Tick(DateTime now, bool force = false)
	{
		if (_snapshot == null) return;
		if (!force && now.Minute == _lastMinute) return;
		_lastMinute = now.Minute;
		foreach (var row in Rows) row.Refresh(now);
		RebuildChips(now);
	}

	private void RebuildChips(DateTime now)
	{
		Chips.Clear();
		if (_snapshot == null) return;

		var candidates = new List<(DateTime End, GameMeta Game, GameRecord Rec, string Kind)>();
		foreach (var game in _snapshot.Games.Where(g => !g.Hidden))
		{
			if (!_snapshot.Result.Games.TryGetValue(game.Id, out var rec)) continue;
			var bw = RowBuilder.ParseRange(rec.BannerDates, now);
			if (bw is { } b && now >= b.Start && now <= b.End) candidates.Add((b.End, game, rec, "卡池"));
			var ew = RowBuilder.ParseRange(rec.EventDates, now);
			if (ew is { } e && now >= e.Start && now <= e.End) candidates.Add((e.End, game, rec, "活动"));
		}

		// 每款游戏最多一个 chip（取它最快结束的那一项），再按结束时间取前 N 个
		var picked = candidates
			.GroupBy(c => c.Game.Id)
			.Select(g => g.OrderBy(c => c.End).First())
			.OrderBy(c => c.End)
			.Take(Math.Clamp(_settings.DockItems, 1, 6))
			.ToList();

		var palette = (ThemeManager.IsLight ? LightPalette : DarkPalette);
		for (var i = 0; i < picked.Count; i++)
		{
			var (end, game, rec, kind) = picked[i];
			var title = kind == "卡池" ? (string.IsNullOrWhiteSpace(rec.Banner) ? "—" : rec.Banner) : (string.IsNullOrWhiteSpace(rec.Event) ? "—" : rec.Event);
			var dates = kind == "卡池" ? rec.BannerDates : rec.EventDates;
			var full = RowBuilder.FormatRemaining(end, now);
			Chips.Add(new ChipVm
			{
				GameName = ShortName(game.Name),
				Remaining = RowBuilder.FormatShortRemaining(end - now),
				Accent = palette[i % palette.Length],
				Tip = $"{game.Name}\n{kind}：{title}\n{dates}\n还剩 {full}",
			});
		}

		Raise(nameof(HasChips));
		Raise(nameof(HasNoChips));
	}

	private void RefreshStatus()
	{
		if (_snapshot == null) return;
		var visible = _snapshot.Games.Where(g => !g.Hidden).ToList();
		var ok = 0;
		var gachaDown = new List<string>();
		var eventDown = new List<string>();
		var gachaNomatch = new List<string>();
		var eventNomatch = new List<string>();

		foreach (var game in visible)
		{
			if (!_snapshot.Result.Games.TryGetValue(game.Id, out var rec)) continue;
			if (rec.Skipped) continue;
			var gDown = rec.GachaFail?.Kind == "down";
			var eDown = rec.EventFail?.Kind == "down";
			if (!gDown && !eDown) ok++;
			if (gDown) gachaDown.Add(game.Name);
			if (eDown) eventDown.Add(game.Name);
			if (rec.GachaFail?.Kind == "nomatch") gachaNomatch.Add(game.Name);
			if (rec.EventFail?.Kind == "nomatch") eventNomatch.Add(game.Name);
		}

		var prefix = IsSample ? "示例数据" : (ok > 0 ? "联网数据" : "尚未联网");
		StatusText = $"{prefix} · 成功 {ok}/{visible.Count}";
		DataStatusText = "数据：" + prefix;

		var lines = new List<string>();
		void Add(string label, List<string> names)
		{
			if (names.Count > 0) lines.Add($"{label}：{string.Join("、", names.Take(8))}" + (names.Count > 8 ? $" 等 {names.Count} 个" : ""));
		}
		Add("卡池失败", gachaDown);
		Add("活动失败", eventDown);
		Add("新卡池未公布", gachaNomatch);
		Add("新活动未公布", eventNomatch);
		StatusTip = lines.Count == 0 ? "所有可见条目都抓到了当期内容" : string.Join("\n", lines);
		ScrapeText = $"成功 {ok}/{visible.Count}";
		if (lines.Count > 0) ScrapeText += "  " + string.Join("  ", lines);

		var ts = _snapshot.Result.RefreshedAt > 0 ? _snapshot.Result.RefreshedAt : _snapshot.GeneratedAt;
		UpdatedText = ts > 0
			? "刷新：" + DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime().ToString("yyyy/M/d HH:mm:ss")
			: "刷新：—";
	}

	private static string ShortName(string name)
	{
		if (name.Length <= 9) return name;
		return name[..8] + "…";
	}

	private static Brush Hex(string hex) => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		Raise(name);
	}

	private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
