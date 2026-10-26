using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace GachaCalendar.Services;

/// <summary>
/// Windows 11 DWM 系统材质（Mica/Acrylic）。失败时返回 false，窗口继续使用不透明表面。
/// 需要无边框窗口、关闭 AllowsTransparency，并让 WPF 组合目标透明。
/// </summary>
internal static class WindowBackdrop
{
	private const int DwmwaUseImmersiveDarkMode = 20;
	private const int DwmwaWindowCornerPreference = 33;
	private const int DwmwaSystemBackdropType = 38;
	private const int DwmsbtTransientWindow = 3;

	[DllImport("dwmapi.dll")]
	private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	[StructLayout(LayoutKind.Sequential)]
	private struct Margins
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	public static bool TryEnable(Window window, bool dark)
	{
		try
		{
			var hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd == IntPtr.Zero) return false;

			var margins = new Margins { Left = -1, Top = -1, Right = -1, Bottom = -1 };
			if (DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0) return false;

			var backdrop = DwmsbtTransientWindow;
			if (DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) != 0)
				return false;

			var darkMode = dark ? 1 : 0;
			DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
			var corner = 2;
			DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

			if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
				target.BackgroundColor = Colors.Transparent;

			return true;
		}
		catch
		{
			return false;
		}
	}
}
