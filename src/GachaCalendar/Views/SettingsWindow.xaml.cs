using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GachaCalendar.Models;
using GachaCalendar.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Image = System.Windows.Controls.Image;
using TextBox = System.Windows.Controls.TextBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace GachaCalendar.Views;

public partial class SettingsWindow : Window
{
	private static readonly (int Hours, string Label)[] DockRanges =
	{
		(12, "12 小时"),
		(24, "24 小时"),
		(48, "48 小时"),
		(72, "72 小时"),
	};

	private sealed record DockRangeOption(int Hours, string Label);

	private static readonly RefreshOption[] StandardRefreshOptions =
	{
		new() { Label = "1 天", Minutes = 1 * 24 * 60 },
		new() { Label = "5 天", Minutes = 5 * 24 * 60 },
		new() { Label = "7 天", Minutes = 7 * 24 * 60 },
		new() { Label = "15 天", Minutes = 15 * 24 * 60 },
		new() { Label = "21 天", Minutes = 21 * 24 * 60 },
		new() { Label = "30 天", Minutes = 30 * 24 * 60 },
		new() { Label = "42 天", Minutes = 42 * 24 * 60 },
	};

	private List<EntryDto> _entries = new();
	private JsonObject? _config;
	private JsonObject _customUrls = new();
	private JsonObject _customEventUrls = new();
	private bool _building;
	private bool _addingCustom;
	private string _customName = "";
	private string _customIcon = "";
	private string _customBannerUrl = "";
	private string _customEventUrl = "";

	private EngineHostClient? Engine => App.Instance?.Engine;

	public SettingsWindow()
	{
		InitializeComponent();
		SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
	}

	public void LoadFromSettings() => _ = ReloadAsync();

	private async Task ReloadAsync()
	{
		var engine = Engine;
		if (engine is not { IsAvailable: true })
		{
			ShowError("应用内引擎不可用（需要 Node Host），暂时无法编辑设置。");
			return;
		}

		_building = true;
		LoadingText.Visibility = Visibility.Visible;
		try
		{
			var cfgNode = await engine.GetConfigAsync();
			_config = cfgNode as JsonObject ?? new JsonObject();
			_customUrls = _config["customUrls"] as JsonObject ?? new JsonObject();
			_customEventUrls = _config["customEventUrls"] as JsonObject ?? new JsonObject();

			var entriesNode = await engine.ListEntriesAsync();
			_entries = JsonSerializer.Deserialize<List<EntryDto>>(entriesNode?.ToJsonString() ?? "[]", Json.Read) ?? new List<EntryDto>();

			var order = new List<string>();
			if (_config["order"] is JsonArray orderArr)
			{
				foreach (var item in orderArr)
				{
					var id = item?.GetValue<string>();
					if (!string.IsNullOrWhiteSpace(id)) order.Add(id);
				}
			}
			_entries = ApplyOrder(_entries, order);

			AutoRefreshCheck.IsChecked = _config["autoRefresh"]?.GetValue<bool>() ?? true;
			NewPanelCheck.IsChecked = _config["newPanel"]?.GetValue<bool>() ?? false;
			LoadDockPreferences();
			BuildRefreshOptions();
			BuildEntries();
			ShowError(null);
		}
		catch (Exception ex)
		{
			Log.Error("设置页读取失败", ex);
			ShowError("读取设置失败：" + ex.Message);
		}
		finally
		{
			_building = false;
			LoadingText.Visibility = Visibility.Collapsed;
		}
	}

	private static List<EntryDto> ApplyOrder(List<EntryDto> entries, List<string> order)
	{
		if (order.Count == 0) return entries;
		var byId = entries.ToDictionary(e => e.Id, e => e);
		var result = new List<EntryDto>();
		foreach (var id in order)
			if (byId.TryGetValue(id, out var entry))
				result.Add(entry);
		foreach (var entry in entries)
			if (!result.Contains(entry))
				result.Add(entry);
		return result;
	}

	private void BuildRefreshOptions()
	{
		var current = _config?["refreshMinutes"]?.GetValue<int?>() ?? 1440;
		var options = StandardRefreshOptions.ToList();
		if (options.All(o => o.Minutes != current))
			options.Add(new RefreshOption { Label = $"自定义（{current} 分钟）", Minutes = current });

		RefreshIntervalCombo.ItemsSource = options;
		RefreshIntervalCombo.DisplayMemberPath = nameof(RefreshOption.Label);
		RefreshIntervalCombo.SelectedValuePath = nameof(RefreshOption.Minutes);
		RefreshIntervalCombo.SelectedValue = current;
	}

	private void LoadDockPreferences()
	{
		var settings = App.Instance?.Settings ?? new AppSettings();
		DockEnabledCheck.IsChecked = settings.DockEnabled;
		DockTickerCheck.IsChecked = settings.DockTickerEnabled;
		DockRangeCombo.ItemsSource = DockRanges.Select(r => new DockRangeOption(r.Hours, r.Label)).ToArray();
		DockRangeCombo.DisplayMemberPath = nameof(DockRangeOption.Label);
		DockRangeCombo.SelectedValuePath = nameof(DockRangeOption.Hours);
		DockRangeCombo.SelectedValue = settings.DockTickerHours;
	}

	private void OnDockPreferencesChanged(object sender, RoutedEventArgs e)
	{
		if (_building) return;
		var app = App.Instance;
		if (app == null) return;
		var hours = DockRangeCombo.SelectedValue is int h ? h : app.Settings.DockTickerHours;
		app.UpdateDockPreferences(DockEnabledCheck.IsChecked == true, DockTickerCheck.IsChecked == true, hours);
	}

	private void OnDockRangeChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_building) return;
		var app = App.Instance;
		if (app == null) return;
		var hours = DockRangeCombo.SelectedValue is int h ? h : app.Settings.DockTickerHours;
		app.UpdateDockPreferences(DockEnabledCheck.IsChecked == true, DockTickerCheck.IsChecked == true, hours);
	}

	private void BuildEntries()
	{
		EntriesPanel.Children.Clear();
		for (var index = 0; index < _entries.Count; index++)
		{
			var entry = _entries[index];
			var card = new Border
			{
				BorderBrush = (Brush)FindResource("Divider"),
				BorderThickness = new Thickness(0, 0, 0, 1),
				Padding = new Thickness(0, 7, 0, 7),
			};
			var rowStyle = new Style(typeof(Border));
			var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
			hover.Setters.Add(new Setter(Border.BackgroundProperty, (Brush)FindResource("HoverBg")));
			rowStyle.Triggers.Add(hover);
			card.Style = rowStyle;
			var stack = new StackPanel();

			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });

			// 游戏图标 + 名称
			var gamePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
			var iconGrid = new Grid { Width = 22, Height = 22, Margin = new Thickness(0, 0, 6, 0) };
			var fallback = new Border
			{
				CornerRadius = new CornerRadius(5),
				Background = Brushes.SlateGray,
			};
			fallback.Child = new TextBlock
			{
				Text = FirstGlyph(entry.Name),
				FontSize = 11,
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			var image = new Image { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
			image.Clip = new RectangleGeometry(new Rect(0, 0, 22, 22), 5, 5);
			iconGrid.Children.Add(fallback);
			iconGrid.Children.Add(image);
			gamePanel.Children.Add(iconGrid);
			gamePanel.Children.Add(new TextBlock
			{
				Text = entry.Name,
				FontSize = 12,
				VerticalAlignment = VerticalAlignment.Center,
				TextTrimming = TextTrimming.CharacterEllipsis,
				ToolTip = entry.Name,
			});
			Grid.SetColumn(gamePanel, 0);
			grid.Children.Add(gamePanel);
			_ = LoadIconAsync(entry.Icon, image, fallback);

			var color = MakeColorButton(entry);
			Grid.SetColumn(color, 1);
			grid.Children.Add(color);

			var check = new CheckBox
			{
				IsChecked = entry.Visible,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				ToolTip = "展示/隐藏这一条",
			};
			check.Checked += async (_, _) => await OnEntryVisibilityChangedAsync(entry, check);
			check.Unchecked += async (_, _) => await OnEntryVisibilityChangedAsync(entry, check);
			Grid.SetColumn(check, 2);
			grid.Children.Add(check);

			var gachaCombo = BuildSourceCombo(entry, false);
			Grid.SetColumn(gachaCombo, 3);
			grid.Children.Add(gachaCombo);

			var eventCombo = BuildSourceCombo(entry, true);
			Grid.SetColumn(eventCombo, 4);
			grid.Children.Add(eventCombo);

			var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
			var up = SmallIconButton("M2,8 L7,3 L12,8", "上移", (Brush)FindResource("Fg"));
			up.IsEnabled = index > 0;
			up.Click += async (_, _) => await MoveAsync(entry.Id, -1);
			var down = SmallIconButton("M2,4 L7,9 L12,4", "下移", (Brush)FindResource("Fg"));
			down.IsEnabled = index < _entries.Count - 1;
			down.Click += async (_, _) => await MoveAsync(entry.Id, 1);
			var delete = SmallIconButton(
				"M4,5 H16 M7,5 V3 H13 V5 M6,5 L7,18 H13 L14,5 M9,8 V15 M12,8 V15",
				"删除", (Brush)FindResource("FgDim"));
			delete.Click += async (_, _) =>
			{
				await RunWriteAsync(async () =>
				{
					await Engine!.RemoveEntryAsync(entry.Id);
					await Engine!.RebuildSnapshotAsync();
					App.Instance?.ReloadSnapshot();
					await ReloadAsync();
				});
			};
			actions.Children.Add(up);
			actions.Children.Add(down);
			actions.Children.Add(delete);
			Grid.SetColumn(actions, 5);
			grid.Children.Add(actions);

			stack.Children.Add(grid);

			// 自定义 URL 行（选择「自定义」后展开）
			var gachaValue = _customUrls[entry.Id]?.GetValue<string>();
			if (IsCustomMode(gachaValue))
			{
				var text = new TextBox
				{
					Text = StripCustom(gachaValue),
					FontSize = 12,
					Margin = new Thickness(0, 6, 0, 0),
					ToolTip = "卡池自定义来源地址",
				};
				text.LostFocus += async (_, _) => await CommitCustomUrlAsync(entry.Id, false, text.Text);
				stack.Children.Add(new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Children =
					{
						new TextBlock { Text = "卡池来源地址", FontSize = 11, Foreground = (Brush)FindResource("FgFaint"), Width = 90, VerticalAlignment = VerticalAlignment.Center },
						text,
					},
				});
			}
			var eventValue = _customEventUrls[entry.Id]?.GetValue<string>();
			if (IsCustomMode(eventValue))
			{
				var text = new TextBox
				{
					Text = StripCustom(eventValue),
					FontSize = 12,
					Margin = new Thickness(0, 6, 0, 0),
					ToolTip = "活动自定义来源地址",
				};
				text.LostFocus += async (_, _) => await CommitCustomUrlAsync(entry.Id, true, text.Text);
				stack.Children.Add(new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Children =
					{
						new TextBlock { Text = "活动来源地址", FontSize = 11, Foreground = (Brush)FindResource("FgFaint"), Width = 90, VerticalAlignment = VerticalAlignment.Center },
						text,
					},
				});
			}

			card.Child = stack;
			EntriesPanel.Children.Add(card);
		}
		BuildCustomEntryArea();
	}

	private void BuildCustomEntryArea()
	{
		CustomEntryArea.Children.Clear();
		if (!_addingCustom)
		{
			var add = new Button
			{
				Content = "+ 添加自定义条目",
				Style = (Style)FindResource("SmallButton"),
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0, 4, 0, 0),
			};
			add.Click += (_, _) =>
			{
				_addingCustom = true;
				BuildCustomEntryArea();
			};
			CustomEntryArea.Children.Add(add);
			return;
		}

		var card = new Border
		{
			BorderBrush = (Brush)FindResource("PanelBorder"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(10),
			Margin = new Thickness(0, 4, 0, 0),
		};
		var panel = new StackPanel();
		panel.Children.Add(new TextBlock { Text = "添加自定义条目", FontWeight = FontWeights.SemiBold, FontSize = 12 });
		panel.Children.Add(new TextBlock
		{
			Text = "名称与图标手动填写；卡池与活动由来源链接解析产出。",
			FontSize = 11,
			Foreground = (Brush)FindResource("FgFaint"),
			Margin = new Thickness(0, 4, 0, 8),
		});

		var name = MakeInput("名称（必填）", _customName);
		name.TextChanged += (_, _) => _customName = name.Text;
		var icon = MakeInput("图标 URL（可选）", _customIcon);
		icon.TextChanged += (_, _) => _customIcon = icon.Text;
		var banner = MakeInput("卡池来源链接（必填）", _customBannerUrl);
		banner.TextChanged += (_, _) => _customBannerUrl = banner.Text;
		var ev = MakeInput("活动来源链接（可选）", _customEventUrl);
		ev.TextChanged += (_, _) => _customEventUrl = ev.Text;
		panel.Children.Add(name);
		panel.Children.Add(icon);
		panel.Children.Add(banner);
		panel.Children.Add(ev);

		var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
		var addButton = new Button
		{
			Content = "添加",
			Style = (Style)FindResource("SmallButton"),
			IsEnabled = !string.IsNullOrWhiteSpace(_customName) && !string.IsNullOrWhiteSpace(_customBannerUrl),
		};
		addButton.Click += async (_, _) => await AddCustomEntryAsync();
		name.TextChanged += (_, _) => addButton.IsEnabled = CanAddCustom();
		banner.TextChanged += (_, _) => addButton.IsEnabled = CanAddCustom();
		var cancel = new Button { Content = "取消", Style = (Style)FindResource("SmallButton"), Margin = new Thickness(8, 0, 0, 0) };
		cancel.Click += (_, _) =>
		{
			_addingCustom = false;
			ResetCustomForm();
			BuildCustomEntryArea();
		};
		actions.Children.Add(addButton);
		actions.Children.Add(cancel);
		panel.Children.Add(actions);
		card.Child = panel;
		CustomEntryArea.Children.Add(card);
	}

	private TextBox MakeInput(string placeholder, string value)
	{
		return new TextBox
		{
			Text = value,
			FontSize = 12,
			Margin = new Thickness(0, 3, 0, 0),
			ToolTip = placeholder,
			Tag = placeholder,
		};
	}

	private async Task AddCustomEntryAsync()
	{		if (string.IsNullOrWhiteSpace(_customName) || string.IsNullOrWhiteSpace(_customBannerUrl)) return;
		await RunWriteAsync(async () =>
		{
			var entries = _config?["customEntries"] as JsonArray;
			var next = new JsonArray();
			if (entries != null)
				foreach (var item in entries)
					next.Add(item?.DeepClone());
			var entry = new JsonObject
			{
				["id"] = "custom-" + DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString("x"),
				["name"] = _customName.Trim(),
				["icon"] = _customIcon.Trim(),
				["url"] = _customBannerUrl.Trim(),
				["eventUrl"] = _customEventUrl.Trim(),
			};
			next.Add(entry);
			await Engine!.UpdateConfigAsync(new JsonObject { ["customEntries"] = next.DeepClone() });
			_addingCustom = false;
			ResetCustomForm();
			await Engine.RebuildSnapshotAsync();
			App.Instance?.ReloadSnapshot();
			await ReloadAsync();
		});
	}

	private void ResetCustomForm()
	{
		_customName = "";
		_customIcon = "";
		_customBannerUrl = "";
		_customEventUrl = "";
	}

	private bool CanAddCustom() =>
		!string.IsNullOrWhiteSpace(_customName) && !string.IsNullOrWhiteSpace(_customBannerUrl);

	private async Task OnEntryVisibilityChangedAsync(EntryDto entry, CheckBox check)
	{
		if (_building) return;
		await RunWriteAsync(async () =>
		{
			await Engine!.SetEntryVisibilityAsync(entry.Id, check.IsChecked == true);
			await Engine!.RebuildSnapshotAsync();
			App.Instance?.ReloadSnapshot();
			await ReloadAsync();
		});
	}

	private ComboBox BuildSourceCombo(EntryDto entry, bool isEvent)	{
		var options = new List<SourceOption>
		{
			new() { Label = string.IsNullOrWhiteSpace(isEvent ? entry.EventSource : entry.Source) ? "未配置" : (isEvent ? entry.EventSource : entry.Source), Value = "default" },
		};
		var alts = isEvent ? entry.EventAltSources : entry.AltSources;
		foreach (var alt in alts ?? new List<SourceOption>())
			if (!string.IsNullOrWhiteSpace(alt.Value))
				options.Add(new SourceOption { Label = alt.Label, Value = alt.Value });
		options.Add(new SourceOption { Label = "自定义", Value = "custom" });

		var combo = new ComboBox
		{
			ItemsSource = options,
			DisplayMemberPath = nameof(SourceOption.Label),
			SelectedValuePath = nameof(SourceOption.Value),
			FontSize = 12,
			Margin = new Thickness(4, 0, 4, 0),
			ToolTip = isEvent ? "活动来源" : "卡池来源",
		};

		var map = isEvent ? _customEventUrls : _customUrls;
		var value = map[entry.Id]?.GetValue<string>();
		combo.SelectedValue = IsCustomMode(value) ? "custom" : (string.IsNullOrWhiteSpace(value) ? "default" : value);

		combo.SelectionChanged += async (_, _) =>
		{
			if (_building || combo.SelectedValue is not string mode) return;
			if (mode == "custom")
			{
				var current = StripCustom(value);
				await CommitCustomUrlAsync(entry.Id, isEvent, current, forceReload: true, allowEmpty: true);
				return;
			}
			await RunWriteAsync(async () =>
			{
				var next = new JsonObject();
				foreach (var pair in map) next[pair.Key] = pair.Value?.DeepClone();
				if (mode == "default") next.Remove(entry.Id);
				else next[entry.Id] = mode;
				if (isEvent) _customEventUrls = next; else _customUrls = next;
				var patch = new JsonObject { [isEvent ? "customEventUrls" : "customUrls"] = next.DeepClone() };
				await Engine!.UpdateConfigAsync(patch);
				await ReloadAsync();
			});
		};
		return combo;
	}

	private async Task CommitCustomUrlAsync(string id, bool isEvent, string url, bool forceReload = true, bool allowEmpty = false)
	{
		await RunWriteAsync(async () =>
		{
			var map = isEvent ? _customEventUrls : _customUrls;
			var next = new JsonObject();
			foreach (var pair in map) next[pair.Key] = pair.Value?.DeepClone();
			if (string.IsNullOrWhiteSpace(url) && !allowEmpty) next.Remove(id);
			else next[id] = "custom:" + url;
			if (isEvent) _customEventUrls = next; else _customUrls = next;
			var patch = new JsonObject { [isEvent ? "customEventUrls" : "customUrls"] = next.DeepClone() };
			await Engine!.UpdateConfigAsync(patch);
			if (forceReload) await ReloadAsync();
		});
	}

	private async Task MoveAsync(string id, int delta)
	{
		var ids = _entries.Select(e => e.Id).ToList();
		var index = ids.IndexOf(id);
		var target = index + delta;
		if (index < 0 || target < 0 || target >= ids.Count) return;
		var moved = ids[index];
		ids.RemoveAt(index);
		ids.Insert(target, moved);
		await RunWriteAsync(async () =>
		{
			await Engine!.SetOrderAsync(ids);
			await ReloadAsync();
		});
	}

	private async Task RunWriteAsync(Func<Task> action)
	{
		try
		{
			ShowError(null);
			await action();
		}
		catch (Exception ex)
		{
			Log.Warn("设置写入失败：" + ex.Message);
			ShowError("保存失败：" + ex.Message);
		}
	}

	private void ShowError(string? message)
	{
		SaveErrorText.Text = message ?? "";
		SaveErrorText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
	}

	private async void OnAutoRefreshChanged(object sender, RoutedEventArgs e)
	{
		if (_building || Engine is not { IsAvailable: true }) return;
		await RunWriteAsync(async () =>
		{
			var patch = new JsonObject { ["autoRefresh"] = AutoRefreshCheck.IsChecked == true };
			await Engine.UpdateConfigAsync(patch);
		});
	}

	private async void OnRefreshIntervalChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_building || Engine is not { IsAvailable: true }) return;
		if (RefreshIntervalCombo.SelectedValue is not int minutes) return;
		await RunWriteAsync(async () =>
		{
			var patch = new JsonObject { ["refreshMinutes"] = minutes };
			await Engine.UpdateConfigAsync(patch);
		});
	}

	private async void OnNewPanelChanged(object sender, RoutedEventArgs e)
	{
		if (_building || Engine is not { IsAvailable: true }) return;
		await RunWriteAsync(async () =>
		{
			var patch = new JsonObject { ["newPanel"] = NewPanelCheck.IsChecked == true };
			await Engine.UpdateConfigAsync(patch);
			await Engine.RebuildSnapshotAsync();
			App.Instance?.ReloadSnapshot();
		});
	}

	private async void OnResetOrder(object sender, RoutedEventArgs e)
	{
		if (Engine is not { IsAvailable: true }) return;
		await RunWriteAsync(async () =>
		{
			await Engine.SetOrderAsync(Array.Empty<string>());
			await ReloadAsync();
		});
	}

	private async void OnRestoreEntries(object sender, RoutedEventArgs e)
	{
		if (Engine is not { IsAvailable: true }) return;
		await RunWriteAsync(async () =>
		{
			var patch = new JsonObject { ["removed"] = new JsonArray(), ["customEntries"] = new JsonArray() };
			await Engine.UpdateConfigAsync(patch);
			await Engine.RebuildSnapshotAsync();
			App.Instance?.ReloadSnapshot();
			await ReloadAsync();
		});
	}

	private async void OnSelfCheck(object sender, RoutedEventArgs e)
	{
		if (Engine is not { IsAvailable: true }) return;
		SelfCheckButton.IsEnabled = false;
		SelfCheckButton.Content = "自检中…";
		SelfCheckPanel.Children.Clear();
		try
		{
			var report = await Engine.SelfCheckAsync();
			BuildSelfCheckReport(report);
		}
		catch (Exception ex)
		{
			SelfCheckPanel.Children.Add(new TextBlock
			{
				Text = "自检失败：" + ex.Message,
				FontSize = 12,
				Foreground = (Brush)FindResource("Danger"),
				TextWrapping = TextWrapping.Wrap,
			});
		}
		finally
		{
			SelfCheckButton.IsEnabled = true;
			SelfCheckButton.Content = "重新自检";
		}
	}

	private void BuildSelfCheckReport(JsonNode? report)
	{
		if (report == null)
		{
			SelfCheckPanel.Children.Add(new TextBlock { Text = "自检没有返回结果", FontSize = 12 });
			return;
		}
		var total = report["total"]?.GetValue<int?>() ?? 0;
		var summary = report["summary"] as JsonObject;
		var ok = summary?["ok"]?.GetValue<int?>() ?? 0;
		var nomatch = summary?["nomatch"]?.GetValue<int?>() ?? 0;
		var unconfigured = summary?["unconfigured"]?.GetValue<int?>() ?? 0;
		var down = summary?["down"]?.GetValue<int?>() ?? 0;

		SelfCheckPanel.Children.Add(new TextBlock
		{
			Text = $"{total} 款游戏 · 成功 {ok} / 无匹配 {nomatch} / 未配置 {unconfigured} / 失败 {down}",
			FontSize = 12,
			TextWrapping = TextWrapping.Wrap,
		});
		var problems = report["problems"] as JsonArray;
		if (problems == null || problems.Count == 0)
		{
			SelfCheckPanel.Children.Add(new TextBlock
			{
				Text = "✓ 所有来源都正常",
				FontSize = 12,
				Foreground = (Brush)FindResource("Accent"),
				Margin = new Thickness(0, 4, 0, 0),
			});
			return;
		}
		foreach (var problem in problems)
		{
			SelfCheckPanel.Children.Add(new TextBlock
			{
				Text = "· " + (problem?.GetValue<string>() ?? ""),
				FontSize = 12,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 2, 0, 0),
			});
		}
	}

	private async Task LoadIconAsync(string? url, Image image, Border fallback)
	{
		try
		{
			var icon = await IconLoader.GetAsync(url);
			if (icon == null) return;
			image.Source = icon;
			image.Visibility = Visibility.Visible;
			fallback.Visibility = Visibility.Collapsed;
		}
		catch { /* 图标失败保留首字 */ }
	}

	private Border MakeColorButton(EntryDto entry)
	{
		var swatch = new Border
		{
			Width = 20,
			Height = 18,
			CornerRadius = new CornerRadius(5),
			Background = ColorFor(entry),
			BorderBrush = (Brush)FindResource("PanelBorder"),
			BorderThickness = new Thickness(1),
			Cursor = System.Windows.Input.Cursors.Hand,
			ToolTip = "主题色（用于日程视图）",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		swatch.MouseLeftButtonUp += async (_, _) => await PickColorAsync(entry);
		return swatch;
	}

	private async Task PickColorAsync(EntryDto entry)
	{
		var current = ColorFor(entry);
		var color = current is SolidColorBrush solid ? solid.Color : Colors.SlateGray;
		using var dialog = new System.Windows.Forms.ColorDialog
		{
			FullOpen = true,
			Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B),
		};
		var owner = new NativeWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
		if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return;
		var c = dialog.Color;
		var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
		await RunWriteAsync(async () =>
		{
			var map = new JsonObject();
			if (_config?["gameColors"] is JsonObject existing)
				foreach (var pair in existing) map[pair.Key] = pair.Value?.DeepClone();
			map[entry.Id] = hex;
			if (_config != null) _config["gameColors"] = map.DeepClone();
			await Engine!.UpdateConfigAsync(new JsonObject { ["gameColors"] = map.DeepClone() });
			await Engine!.RebuildSnapshotAsync();
			App.Instance?.ReloadSnapshot();
			await ReloadAsync();
		});
	}

	private Brush ColorFor(EntryDto entry)
	{
		if (_config?["gameColors"] is JsonObject map &&
			map[entry.Id]?.GetValue<string>() is { Length: 7 } hex && hex[0] == '#' &&
			hex.Skip(1).All(Uri.IsHexDigit))
		{
			try { return (Brush)new BrushConverter().ConvertFromString(hex)!; }
			catch { /* 回落 */ }
		}
		var hash = 0;
		foreach (var ch in entry.Name) hash = (hash * 31 + ch) & 0x7fffffff;
		var palette = new[]
		{
			"#7C5CFC", "#3B82F6", "#10B981", "#F59E0B", "#EC4899",
			"#8B5CF6", "#06B6D4", "#EF4444", "#84CC16", "#6366F1",
		};
		return (Brush)new BrushConverter().ConvertFromString(palette[hash % palette.Length])!;
	}

	private Button SmallButton(object content, string tooltip)	{
		var button = new Button
		{
			Content = content,
			Style = (Style)FindResource("SmallButton"),
			ToolTip = tooltip,
			Margin = new Thickness(2, 0, 2, 0),
			MinWidth = 24,
			Padding = new Thickness(4, 0, 4, 0),
		};
		System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
		return button;
	}

	private Button SmallIconButton(string data, string tooltip, Brush stroke)
	{
		var icon = new System.Windows.Shapes.Path
		{
			Data = System.Windows.Media.Geometry.Parse(data),
			Stroke = stroke,
			StrokeThickness = 1.5,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Width = 14,
			Height = 14,
			Stretch = Stretch.Uniform,
		};
		return SmallButton(icon, tooltip);
	}

	private static bool IsCustomMode(string? value) =>
		value != null && value.StartsWith("custom:", StringComparison.Ordinal);

	private static string StripCustom(string? value) =>
		value != null && value.StartsWith("custom:", StringComparison.Ordinal) ? value["custom:".Length..] : "";

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

	private sealed class NativeWindow : System.Windows.Forms.IWin32Window
	{
		public NativeWindow(IntPtr handle) => Handle = handle;
		public IntPtr Handle { get; }
	}
}
