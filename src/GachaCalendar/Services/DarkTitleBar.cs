using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GachaCalendar.Services;

/// <summary>把带系统标题栏的窗口（设置页）切到深色标题栏，避免深色主题下顶部一条白。</summary>
public static class DarkTitleBar
{
	private const int DwmwaUseImmersiveDarkMode = 20;
	private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

	public static void Apply(Window window)
	{
		if (ThemeManager.IsLight) return;
		try
		{
			var hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd == IntPtr.Zero) return;
			var dark = 1;
			if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
				DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
		}
		catch
		{
			// 旧系统不支持就保持系统默认标题栏
		}
	}
}
