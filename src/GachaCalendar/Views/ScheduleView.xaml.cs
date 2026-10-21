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

	private void OnUrgentAreaEnter(object sender, MouseEventArgs e)
	{
		if (DataContext is SchedulePanelVm vm) vm.SetHovered(true);
	}

	private void OnUrgentAreaLeave(object sender, MouseEventArgs e)
	{
		if (DataContext is SchedulePanelVm vm) vm.SetHovered(false);
	}

	private ScheduleItemVm? _openProbe;

	private void OnProbeEnter(object sender, MouseEventArgs e)
	{
		if (sender is not FrameworkElement { Tag: ScheduleItemVm item } || !item.HasExtras) return;
		if (ReferenceEquals(_openProbe, item) && item.IsProbeOpen) return;
		if (_openProbe != null) _openProbe.IsProbeOpen = false;
		_openProbe = item;
		item.IsProbeOpen = true;
	}

	private void OnProbeClosed(object? sender, EventArgs e)
	{
		if (sender is System.Windows.Controls.Primitives.Popup { DataContext: ScheduleItemVm item })
			item.IsProbeOpen = false;
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
