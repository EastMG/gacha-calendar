using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GachaCalendar.Models;
using GachaCalendar.Services;
using GachaCalendar.ViewModels;
using GachaCalendar.Views;

namespace GachaCalendar;

public partial class App : System.Windows.Application
{
	public static App? Instance { get; private set; }

	public bool IsShuttingDown { get; private set; }
	public AppSettings Settings { get; private set; } = new();
	public MainViewModel Vm { get; } = new();
	public string? CurrentDataPath { get; private set; }
	public EngineHostClient? Engine => _engine;

	private readonly SettingsStore _settingsStore = new();
	private SnapshotStore? _snapshots;
	private TrayIcon? _tray;
	private DockStripWindow? _dock;
	private FlyoutWindow? _flyout;
	private SettingsWindow? _settingsWindow;
	private DispatcherTimer? _tick;
	private EngineHostClient? _engine;
	private bool _autoRefreshing;
	private DateTime _lastAutoCheck = DateTime.MinValue;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		Instance = this;
		Log.Info("启动 GachaCalendar");

		try
		{
			StartupCore(e);
		}
		catch (Exception ex)
		{
			Log.Error("启动失败", ex);
			System.Windows.MessageBox.Show("二游日历启动失败：\n" + ex.Message, "二游日历", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown(1);
		}
	}

	private void StartupCore(StartupEventArgs e)
	{
		DispatcherUnhandledException += (_, args) =>
		{
			Log.Error("UI 线程未处理异常", args.Exception);
			args.Handled = true;
		};
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
			Log.Error("未处理异常", args.ExceptionObject as Exception);

		if (!SingleInstance.Acquire())
		{
			SingleInstance.NotifyExisting();
			Shutdown();
			return;
		}

		ThemeManager.Apply();
		Settings = _settingsStore.Load();
		AppPaths.EnsureLocalDataDir();

		_snapshots = new SnapshotStore();
		_snapshots.Changed += load => Dispatcher.BeginInvoke(new Action(() => ApplySnapshot(load)));

		_dock = new DockStripWindow { DataContext = Vm };
		_dock.Show();
		_flyout = new FlyoutWindow { DataContext = Vm };
		HookMessages();

		_tray = new TrayIcon(ShowFlyout, ToggleDock, ShowSettings, ReloadData, ExitApp);
		_tray.SetDockVisible(Settings.DockEnabled);

		LoadSnapshot();
		_ = InitEngineAsync();

		_tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_tick.Tick += (_, _) => OnTick();
		_tick.Start();
		RepositionDock();

		// 手动启动时展开面板；--autostart / --silent 只留常驻条；--show-flyout 给截图/演示用
		if (e.Args.Contains("--show-flyout")) ScheduleShow(900);
		else if (!e.Args.Contains("--autostart") && !e.Args.Contains("--silent")) ScheduleShow(500);
		if (e.Args.Contains("--show-settings")) ScheduleSettings(2500);
	}

	private void ScheduleSettings(int delayMs)
	{
		var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			ShowSettings();
		};
		timer.Start();
	}

	private void ScheduleShow(int delayMs)
	{
		var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			ShowFlyout();
		};
		timer.Start();
	}

	private void OnTick()
	{
		Vm.Tick(DateTime.Now);
		RepositionDock();

		// 自动刷新：Host 内部已经做了 due/backoff 判断，这里只需低频触发。
		if (_engine?.IsAvailable == true && !_autoRefreshing &&
			(DateTime.Now - _lastAutoCheck).TotalSeconds >= 60)
		{
			_lastAutoCheck = DateTime.Now;
			_ = AutoRefreshAsync();
		}
	}

	private async Task AutoRefreshAsync()
	{
		if (_autoRefreshing || _engine?.IsAvailable != true) return;
		_autoRefreshing = true;
		try
		{
			var decision = await _engine.RefreshIfDueAsync();
			if (decision.Refreshed) LoadSnapshot();
			else if (!string.IsNullOrWhiteSpace(decision.Error)) Vm.SetRefreshError(decision.Error!);
		}
		catch (Exception ex)
		{
			Log.Warn("自动刷新失败：" + ex.Message);
			Vm.SetRefreshError("自动刷新失败：" + ex.Message);
		}
		finally
		{
			_autoRefreshing = false;
		}
	}

	private async Task InitEngineAsync()
	{
		try
		{
			_engine = new EngineHostClient();
			var ok = await _engine.StartAsync(AppPaths.LocalDataDir);
			Vm.SetEngineState(ok, _engine.LastError);
			if (!ok)
			{
				Log.Warn("应用内引擎不可用：" + _engine.LastError);
				return;
			}

			// 受限环境里 .NET TLS 可能不可用；图标改由 Node Host 取，再重载一次让失败图标重试
			IconLoader.SetFallback(_engine.FetchBinaryAsync);
			LoadSnapshot();

			Vm.SetRefreshing(true);
			try
			{
				var decision = await _engine.RefreshIfDueAsync();
				if (decision.Refreshed) LoadSnapshot();
				else if (!string.IsNullOrWhiteSpace(decision.Error)) Vm.SetRefreshError(decision.Error!);
			}
			finally
			{
				Vm.SetRefreshing(false);
			}
		}
		catch (Exception ex)
		{
			Log.Error("初始化应用内引擎失败", ex);
			Vm.SetEngineState(false, ex.Message);
		}
	}

	/// <summary>面板刷新按钮：强制刷新一轮（无视调度/退避）。</summary>
	public async Task RefreshAsync()
	{
		if (_engine is not { IsAvailable: true })
		{
			Vm.SetRefreshError("应用内刷新不可用：" + (_engine?.LastError ?? "请检查 Node 与 Host 脚本"));
			return;
		}
		Vm.SetRefreshing(true);
		try
		{
			await _engine.RefreshAsync();
			LoadSnapshot();
		}
		catch (Exception ex)
		{
			Log.Warn("手动刷新失败：" + ex.Message);
			Vm.SetRefreshError("刷新失败：" + ex.Message);
		}
		finally
		{
			Vm.SetRefreshing(false);
		}
	}

	public void RepositionDock()
	{
		if (_dock == null) return;
		var shouldShow = Settings.DockEnabled &&
			!(Settings.HideWhenFullscreen && Taskbar.IsForegroundFullscreen());
		DockPositioner.Apply(_dock, shouldShow);
	}

	private void LoadSnapshot()
	{
		if (_snapshots == null) return;
		var load = _snapshots.Reload(Settings);
		ApplySnapshot(load);
	}

	private void ApplySnapshot(SnapshotLoad load)
	{
		if (load.Ok)
		{
			CurrentDataPath = load.Path;
			Vm.Apply(load.Snapshot!, Settings, load.IsSample, load.Path);
			Log.Info($"快照已载入：{load.Path}（示例={load.IsSample}）");
		}
		else
		{
			Vm.ShowError(load.Error ?? "未知错误");
			Log.Warn("快照载入失败：" + load.Error);
		}
	}

	public void LoadSettingsIntoViewModel() => Vm.UpdateSettings(Settings);

	public void ShowFlyout()
	{
		if (_flyout == null || _dock == null) return;
		if (_flyout.IsVisible)
		{
			_flyout.Activate();
			return;
		}
		_flyout.ShowNearDock(_dock);
		_ = AutoRefreshAsync();
	}

	public void ToggleFlyout()
	{
		if (_flyout == null) return;
		if (_flyout.IsVisible) _flyout.Hide();
		else ShowFlyout();
	}

	public void ToggleDock()
	{
		Settings.DockEnabled = !Settings.DockEnabled;
		_settingsStore.Save(Settings);
		_tray?.SetDockVisible(Settings.DockEnabled);
		RepositionDock();
	}

	public void ShowSettings()
	{
		if (_settingsWindow == null)
		{
			_settingsWindow = new SettingsWindow();
			_settingsWindow.Closed += (_, _) => _settingsWindow = null;
		}
		_settingsWindow.LoadFromSettings();
		_settingsWindow.Show();
		_settingsWindow.Activate();
		DockPositioner.CenterOnWorkArea(_settingsWindow);
	}

	public void ApplySettings(AppSettings settings)
	{
		Settings = settings;
		_settingsStore.Save(settings);
		Vm.UpdateSettings(settings);
		Autostart.Set(settings.StartWithWindows);
		_tray?.SetDockVisible(settings.DockEnabled);
		RepositionDock();
		if (_settingsWindow != null) _settingsWindow.LoadFromSettings();
	}

	public void ReloadData() => LoadSnapshot();

	/// <summary>设置页改完显隐/排序后调用：Host 重建快照 + 面板立即重载。</summary>
	public void ReloadSnapshot() => LoadSnapshot();

	public void ExitApp()
	{
		IsShuttingDown = true;
		_tick?.Stop();
		_engine?.Kill();
		_tray?.Dispose();
		_tray = null;
		_settingsWindow?.Close();
		_flyout?.Close();
		_dock?.Close();
		Shutdown();
	}

	private void HookMessages()
	{
		if (_dock == null) return;
		var source = HwndSource.FromHwnd(new WindowInteropHelper(_dock).Handle);
		source?.AddHook(WndProc);
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if ((uint)msg == Taskbar.ShowPanelMessage)
		{
			ShowFlyout();
			handled = true;
		}
		return IntPtr.Zero;
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_engine?.Kill();
		_snapshots?.Dispose();
		_tray?.Dispose();
		base.OnExit(e);
	}
}
