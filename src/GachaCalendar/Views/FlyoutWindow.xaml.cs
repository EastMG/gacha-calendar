using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GachaCalendar.Services;
using GachaCalendar.ViewModels;

namespace GachaCalendar.Views;

public partial class FlyoutWindow : Window
{
	private MainViewModel? _vm;
	private bool _syncingMode;

	public FlyoutWindow()
	{
		_syncingMode = true;
		InitializeComponent();
		_syncingMode = false;
		DataContextChanged += (_, _) => HookViewModel();
		SourceInitialized += (_, _) =>
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			Taskbar.MakeToolWindow(hwnd, noActivate: false);
		};
		Deactivated += (_, _) =>
		{
			if (App.Instance is { IsShuttingDown: false, IsCaptureMode: false } && IsVisible) Hide();
		};
		PreviewKeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape)
			{
				Hide();
				e.Handled = true;
			}
		};
	}

	private void HookViewModel()
	{
		if (_vm != null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
		_vm = DataContext as MainViewModel;
		if (_vm != null) _vm.PropertyChanged += OnViewModelPropertyChanged;
		UpdateRefreshSpin();
		UpdatePanelMode();
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MainViewModel.IsRefreshing)) UpdateRefreshSpin();
		if (e.PropertyName == nameof(MainViewModel.IsNewPanel)) UpdatePanelMode();
	}

	private void UpdatePanelMode()
	{
		if (TableScroll == null || SchedulePanel == null) return;
		var newPanel = _vm?.IsNewPanel == true;
		_syncingMode = true;
		if (OverviewTab != null) OverviewTab.IsChecked = !newPanel;
		if (ScheduleTab != null) ScheduleTab.IsChecked = newPanel;
		_syncingMode = false;
		TableScroll.Visibility = newPanel ? Visibility.Collapsed : Visibility.Visible;
		SchedulePanel.Visibility = newPanel ? Visibility.Visible : Visibility.Collapsed;
		_vm?.Schedule.SetVisible(newPanel);
	}

	private void OnViewModeChanged(object sender, RoutedEventArgs e)
	{
		if (_syncingMode || sender is not System.Windows.Controls.RadioButton { IsChecked: true } radio) return;
		App.Instance?.SetNewPanelMode(ReferenceEquals(radio, ScheduleTab));
	}

	private void UpdateRefreshSpin()
	{
		if (RefreshIcon.RenderTransform is not RotateTransform transform) return;
		if (_vm?.IsRefreshing == true)
		{
			var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
			{
				RepeatBehavior = RepeatBehavior.Forever,
			};
			transform.BeginAnimation(RotateTransform.AngleProperty, spin);
		}
		else
		{
			transform.BeginAnimation(RotateTransform.AngleProperty, null);
			transform.Angle = 0;
		}
	}

	public void ShowNearDock(Window dock)
	{
		Height = Math.Min(660, Math.Max(360, SystemParameters.WorkArea.Height * 0.72));
		Opacity = 0;
		Show();
		Activate();
		BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
		{
			EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
		});
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
		{
			UpdateLayout();
			DockPositioner.PositionFlyout(this, dock);
		}));
	}

	private async void OnRefresh(object sender, RoutedEventArgs e)
	{
		if (App.Instance == null) return;
		await App.Instance.RefreshAsync();
	}

	private void OnSettings(object sender, RoutedEventArgs e) => App.Instance?.ShowSettings();
}
