using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GachaCalendar.Models;
using GachaCalendar.Services;

namespace GachaCalendar.Views;

public partial class SettingsWindow : Window
{
	public SettingsWindow()
	{
		InitializeComponent();
		LoadFromSettings();
	}

	public void LoadFromSettings()
	{
		var s = App.Instance?.Settings ?? new AppSettings();
		DockEnabled.IsChecked = s.DockEnabled;
		HideFullscreen.IsChecked = s.HideWhenFullscreen;
		OpenOnClick.IsChecked = s.OpenPanelOnClick;
		ItemsCount.SelectedIndex = Math.Clamp(s.DockItems - 1, 0, 5);
		StartWithWindows.IsChecked = Autostart.IsEnabled();
		DataPathText.Text = App.Instance?.CurrentDataPath ?? "（未载入）";
	}

	private void OnSave(object sender, RoutedEventArgs e)
	{
		var app = App.Instance;
		if (app == null) return;
		var s = new AppSettings
		{
			DockEnabled = DockEnabled.IsChecked == true,
			HideWhenFullscreen = HideFullscreen.IsChecked == true,
			OpenPanelOnClick = OpenOnClick.IsChecked == true,
			DockItems = Math.Clamp(ItemsCount.SelectedIndex + 1, 1, 6),
			StartWithWindows = StartWithWindows.IsChecked == true,
		};
		app.ApplySettings(s);
		Close();
	}

	private void OnReload(object sender, RoutedEventArgs e)
	{
		App.Instance?.ReloadData();
		LoadFromSettings();
	}

	private void OnOpenDataDir(object sender, RoutedEventArgs e)
	{
		try
		{
			Directory.CreateDirectory(AppPaths.LocalDataDir);
			Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.LocalDataDir) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			Log.Error("打开数据目录失败", ex);
		}
	}

	private void OnClose(object sender, RoutedEventArgs e) => Close();
}
