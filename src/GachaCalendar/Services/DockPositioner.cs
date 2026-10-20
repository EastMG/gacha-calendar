using System.Windows;
using System.Windows.Interop;
using GachaCalendar.Services;

namespace GachaCalendar.Services;

public static class DockPositioner
{
	/// <summary>
	/// 把常驻条停靠到任务栏左侧：高度贴合任务栏、水平贴左、始终置顶且不抢焦点。
	/// 只做窗口定位，不修改任务栏 —— 任务栏布局变化时下一帧重新调用即可。
	/// </summary>
	public static void Apply(Window dock, bool shouldShow)
	{
		if (!dock.IsLoaded) return;
		var hwnd = new WindowInteropHelper(dock).Handle;
		if (hwnd == IntPtr.Zero) return;

		var info = Taskbar.GetInfo();
		var visible = shouldShow && info.Found && !info.Hidden && dock.Width > 0;
		if (!visible)
		{
			if (dock.Visibility != Visibility.Hidden) dock.Visibility = Visibility.Hidden;
			return;
		}

		if (dock.Visibility != Visibility.Visible) dock.Visibility = Visibility.Visible;

		var scale = Math.Max(0.5, Taskbar.GetDpiScale(hwnd));
		var heightDip = Math.Max(26, (info.Rect.Height - 8) / scale);
		if (Math.Abs(dock.Height - heightDip) > 0.5)
		{
			dock.Height = heightDip;
			dock.UpdateLayout();
		}

		var margin = (int)Math.Round(8 * scale);
		var y = info.Rect.Top + (int)Math.Round((info.Rect.Height - dock.Height * scale) / 2.0);
		Taskbar.SetTopmostPosition(hwnd, info.Rect.Left + margin, y);
	}

	/// <summary>面板贴在常驻条上方，并夹在当前显示器工作区内。</summary>
	public static void PositionFlyout(Window flyout, Window dock)
	{
		if (!flyout.IsLoaded) return;
		var hwnd = new WindowInteropHelper(flyout).Handle;
		var dockHwnd = new WindowInteropHelper(dock).Handle;
		if (hwnd == IntPtr.Zero) return;

		var info = Taskbar.GetInfo();
		var d = Taskbar.GetRect(dockHwnd);
		var f = Taskbar.GetRect(hwnd);
		const int gap = 8;

		var x = d.X;
		var y = d.Y - f.Height - gap;
		var work = info.WorkArea;
		if (y < work.Top + 8) y = work.Top + 8;
		if (x + f.Width > work.Right - 8) x = work.Right - f.Width - 8;
		if (x < work.Left + 8) x = work.Left + 8;

		Taskbar.SetTopmostPosition(hwnd, x, y, noActivate: false);
	}

	/// <summary>设置窗居中（用 WPF 的 DIP 工作区，简单可靠）。</summary>
	public static void CenterOnWorkArea(Window window)
	{
		var wa = SystemParameters.WorkArea;
		window.Left = wa.Left + Math.Max(0, (wa.Width - window.ActualWidth) / 2);
		window.Top = wa.Top + Math.Max(0, (wa.Height - window.ActualHeight) / 2);
	}
}
