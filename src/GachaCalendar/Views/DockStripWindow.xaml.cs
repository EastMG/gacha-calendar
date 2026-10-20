using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GachaCalendar.Services;

namespace GachaCalendar.Views;

public partial class DockStripWindow : Window
{
	public DockStripWindow()
	{
		InitializeComponent();
		SourceInitialized += (_, _) =>
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			Taskbar.MakeToolWindow(hwnd, noActivate: true);
		};
		Loaded += (_, _) => App.Instance?.RepositionDock();
		Closing += (_, e) =>
		{
			if (App.Instance is { IsShuttingDown: false }) e.Cancel = true;
		};
	}

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
