using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using GachaCalendar.Services;

namespace GachaCalendar.Views;

public partial class FlyoutWindow : Window
{
	public FlyoutWindow()
	{
		InitializeComponent();
		SourceInitialized += (_, _) =>
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			Taskbar.MakeToolWindow(hwnd, noActivate: false);
		};
		Deactivated += (_, _) =>
		{
			if (App.Instance is { IsShuttingDown: false } && IsVisible) Hide();
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

	public void ShowNearDock(Window dock)
	{
		Height = Math.Min(640, Math.Max(360, SystemParameters.WorkArea.Height - 48));
		Show();
		Activate();
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
		{
			UpdateLayout();
			DockPositioner.PositionFlyout(this, dock);
		}));
	}

	private void OnReload(object sender, RoutedEventArgs e) => App.Instance?.ReloadData();
	private void OnSettings(object sender, RoutedEventArgs e) => App.Instance?.ShowSettings();
	private void OnClose(object sender, RoutedEventArgs e) => Hide();
}
