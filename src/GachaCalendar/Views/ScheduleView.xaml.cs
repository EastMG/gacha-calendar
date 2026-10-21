using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GachaCalendar.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace GachaCalendar.Views;

public partial class ScheduleView : UserControl
{
	public ScheduleView()
	{
		InitializeComponent();
	}

	private void OnUrgentEnter(object sender, MouseEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: ScheduleItemVm item } && DataContext is SchedulePanelVm vm)
			vm.SetFocus(item);
	}

	private void OnExpand(object sender, RoutedEventArgs e)
	{
		if (DataContext is SchedulePanelVm vm) vm.ToggleExpand();
	}

	private async void OnRefresh(object sender, RoutedEventArgs e)
	{
		if (App.Instance == null) return;
		await App.Instance.RefreshAsync();
	}
}
