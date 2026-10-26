using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GachaCalendar.Services;
using GachaCalendar.ViewModels;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace GachaCalendar.Views;

public partial class DockStripWindow : Window
{
	private static readonly TimeSpan SlideInterval = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(320);

	private readonly DispatcherTimer _timer;
	private readonly List<DockLineVm> _sequence = new();
	private DispatcherTimer? _settleTimer;
	private MainViewModel? _vm;
	private DockLineVm? _current;
	private int _index;
	private bool _animating;
	private bool _hover;
	private bool _slideDown = true;
	private bool _tickerEnabled = true;

	public DockStripWindow()
	{
		InitializeComponent();
		_timer = new DispatcherTimer { Interval = SlideInterval };
		_timer.Tick += OnTick;

		SourceInitialized += (_, _) =>
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			Taskbar.MakeToolWindow(hwnd, noActivate: true);
			if (WindowBackdrop.TryEnable(this, !ThemeManager.IsLight))
				ThemeManager.UseBackdropMaterials();
		};
		Loaded += (_, _) =>
		{
			App.Instance?.RepositionDock();
			RebuildSequence();
			// 截图时冻结在第一格，避免正好截到 320ms 翻页中间帧。
			SetTickerEnabled(App.Instance?.Settings.DockTickerEnabled ?? true);
		};
		Closing += (_, e) =>
		{
			if (App.Instance is { IsShuttingDown: false }) e.Cancel = true;
			else StopTicker();
		};
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		DetachVm();
		if (e.NewValue is MainViewModel vm)
		{
			_vm = vm;
			vm.Chips.CollectionChanged += OnChipsChanged;
			foreach (var chip in vm.Chips) chip.PropertyChanged += OnChipPropertyChanged;
			RebuildSequence();
		}
	}

	private void DetachVm()
	{
		if (_vm == null) return;
		_vm.Chips.CollectionChanged -= OnChipsChanged;
		foreach (var chip in _vm.Chips) chip.PropertyChanged -= OnChipPropertyChanged;
		_vm = null;
	}

	private void OnChipsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
			foreach (ChipVm chip in e.OldItems) chip.PropertyChanged -= OnChipPropertyChanged;
		if (e.NewItems != null)
			foreach (ChipVm chip in e.NewItems) chip.PropertyChanged += OnChipPropertyChanged;
		RebuildSequence();
	}

	private void OnChipPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// 图标是异步加载的；行对象建立时可能还没有图，加载完需要刷新当前序列。
		if (e.PropertyName is nameof(ChipVm.HasIcon) or nameof(ChipVm.IconSource) or nameof(ChipVm.IconGlyph))
			RebuildSequence();
	}

	private void RebuildSequence()
	{
		StopAnimation();
		_sequence.Clear();
		if (_vm == null) return;

		var idle = DockLineVm.Idle(_vm.Chips.Count);
		if (_vm.Chips.Count == 0)
		{
			_sequence.Add(idle);
		}
		else
		{
			// 任务栏条没有侧栏按钮的固定品牌行，直接把内容做成轮播；只有无条目时才显示「二游日历」。
			foreach (var chip in _vm.Chips)
				_sequence.Add(DockLineVm.FromChip(chip));
		}

		_index = 0;
		_current = _sequence[0];
		SetImmediate(_current);
		UpdateTooltip();
	}

	private void SetImmediate(DockLineVm line)
	{
		PrevLine.Content = null;
		PrevLine.RenderTransform = new TranslateTransform();
		CurrentLine.Content = line;
		CurrentLine.RenderTransform = new TranslateTransform();
	}

	private void OnTick(object? sender, EventArgs e)
	{
		if (!_tickerEnabled || _hover || _animating || _sequence.Count < 2) return;
		_index = (_index + 1) % _sequence.Count;
		AnimateTo(_sequence[_index]);
	}

	public void SetTickerEnabled(bool enabled)
	{
		_tickerEnabled = enabled;
		if (enabled && App.Instance?.IsCaptureMode != true) _timer.Start();
		else _timer.Stop();
	}

	private void AnimateTo(DockLineVm next)
	{
		if (_current == null || ReferenceEquals(next, _current))
		{
			SetImmediate(next);
			_current = next;
			return;
		}

		StopAnimation();
		_animating = true;
		PrevLine.Content = _current;
		CurrentLine.Content = next;

		var offset = 26.0;
		var direction = _slideDown ? 1 : -1;
		_slideDown = !_slideDown;
		var prevTransform = new TranslateTransform();
		var currentTransform = new TranslateTransform(0, -offset * direction);
		PrevLine.RenderTransform = prevTransform;
		CurrentLine.RenderTransform = currentTransform;

		var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
		prevTransform.BeginAnimation(TranslateTransform.YProperty,
			new DoubleAnimation(0, offset * direction, SlideDuration) { EasingFunction = ease });
		currentTransform.BeginAnimation(TranslateTransform.YProperty,
			new DoubleAnimation(-offset * direction, 0, SlideDuration) { EasingFunction = ease });

		_settleTimer?.Stop();
		var settle = new DispatcherTimer { Interval = SlideDuration + TimeSpan.FromMilliseconds(30) };
		_settleTimer = settle;
		settle.Tick += (_, _) =>
		{
			settle.Stop();
			if (!_animating) return;
			_animating = false;
			_settleTimer = null;
			PrevLine.Content = null;
			PrevLine.RenderTransform = new TranslateTransform();
			CurrentLine.RenderTransform = new TranslateTransform();
			_current = next;
		};
		settle.Start();
	}

	private void StopAnimation()
	{
		_animating = false;
		_settleTimer?.Stop();
		_settleTimer = null;
		if (_current != null) SetImmediate(_current);
	}

	private void UpdateTooltip()
	{
		if (_vm == null) return;
		if (_vm.Chips.Count == 0)
		{
			Shell.ToolTip = "二游日历 · 点击展开面板";
			return;
		}
		var lines = _vm.Chips.Select(c => $"{c.Kind}：{c.ItemName}  {c.Status}");
		Shell.ToolTip = "二游日历 · 点击展开面板\n" + string.Join("\n", lines);
	}

	private void StopTicker()
	{
		_timer.Stop();
		StopAnimation();
		DetachVm();
	}

	private void OnMouseEnter(object sender, MouseEventArgs e) => _hover = true;

	private void OnMouseLeave(object sender, MouseEventArgs e) => _hover = false;

	private void OnClick(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Left) return;
		App.Instance?.ToggleFlyout();
	}

	private void OnRightClick(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Right) return;
		var app = App.Instance;
		if (app == null) return;

		var menu = new System.Windows.Controls.ContextMenu();
		var open = new System.Windows.Controls.MenuItem { Header = "打开面板" };
		open.Click += (_, _) => app.ShowFlyout();
		var settings = new System.Windows.Controls.MenuItem { Header = "设置…" };
		settings.Click += (_, _) => app.ShowSettings();
		var reload = new System.Windows.Controls.MenuItem { Header = "重新读取数据" };
		reload.Click += (_, _) => app.ReloadData();
		var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
		exit.Click += (_, _) => app.ExitApp();
		menu.Items.Add(open);
		menu.Items.Add(settings);
		menu.Items.Add(reload);
		menu.Items.Add(new System.Windows.Controls.Separator());
		menu.Items.Add(exit);
		menu.IsOpen = true;
		e.Handled = true;
	}
}
