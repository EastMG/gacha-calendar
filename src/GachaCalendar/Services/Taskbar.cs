using System.Runtime.InteropServices;

namespace GachaCalendar.Services;

/// <summary>
/// 任务栏几何信息与窗口样式。**只读 explorer 的公开窗口**（Shell_TrayWnd），
/// 不注入、不修改任务栏本身 —— 这是 v0.1 刻意选择的稳定方案，见 docs/02。
/// </summary>
public static class Taskbar
{
	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left, Top, Right, Bottom;
		public int Width => Right - Left;
		public int Height => Bottom - Top;
	}

	public readonly record struct Info(
		bool Found,
		RECT Rect,
		RECT Monitor,
		RECT WorkArea,
		bool AutoHide,
		bool Hidden,
		double DpiScale);

	public static uint ShowPanelMessage { get; } = NativeMethods.RegisterWindowMessageW("GachaCalendar.ShowPanel");

	public static void BroadcastShowPanel() =>
		NativeMethods.PostMessageW(new IntPtr(0xFFFF), ShowPanelMessage, IntPtr.Zero, IntPtr.Zero);

	public static double GetDpiScale(IntPtr hwnd)
	{
		try
		{
			var dpi = NativeMethods.GetDpiForWindow(hwnd);
			return dpi > 0 ? dpi / 96.0 : 1.0;
		}
		catch { return 1.0; }
	}

	public static Info GetInfo()
	{
		var tray = NativeMethods.FindWindowW("Shell_TrayWnd", null);
		if (tray == IntPtr.Zero)
		{
			// 极端情况（explorer 重启中）：退回主屏底部 48px 的假任务栏
			var mon = NativeMethods.MonitorFromWindow(IntPtr.Zero, 1);
			var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
			NativeMethods.GetMonitorInfoW(mon, ref mi);
			var fake = new RECT { Left = 0, Top = mi.rcMonitor.Bottom - 48, Right = mi.rcMonitor.Right, Bottom = mi.rcMonitor.Bottom };
			return new Info(false, fake, mi.rcMonitor, mi.rcWork, false, false, 1.0);
		}

		NativeMethods.GetWindowRect(tray, out var rect);
		var monitor = NativeMethods.MonitorFromWindow(tray, 2);
		var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
		NativeMethods.GetMonitorInfoW(monitor, ref info);

		var data = new NativeMethods.APPBARDATA { cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>() };
		var state = NativeMethods.SHAppBarMessage(4 /* ABM_GETSTATE */, ref data).ToInt64();
		var autoHide = (state & 1 /* ABS_AUTOHIDE */) != 0;
		var hidden = autoHide && rect.Top >= info.rcMonitor.Bottom - 4;

		return new Info(true, rect, info.rcMonitor, info.rcWork, autoHide, hidden, GetDpiScale(tray));
	}

	public static (int X, int Y, int Width, int Height) GetRect(IntPtr hwnd)
	{
		if (!NativeMethods.GetWindowRect(hwnd, out var r)) return (0, 0, 0, 0);
		return (r.Left, r.Top, r.Width, r.Height);
	}

	/// <summary>不显示在 Alt+Tab；noActivate 时同时不抢焦点（任务栏常驻条必须抢不到焦点）。</summary>
	public static void MakeToolWindow(IntPtr hwnd, bool noActivate)
	{
		try
		{
			var ex = NativeMethods.GetWindowLongPtrW(hwnd, -20 /* GWL_EXSTYLE */).ToInt64();
			ex |= 0x00000080L; // WS_EX_TOOLWINDOW
			if (noActivate) ex |= 0x08000000L; // WS_EX_NOACTIVATE
			NativeMethods.SetWindowLongPtrW(hwnd, -20, new IntPtr(ex));
		}
		catch (Exception ex)
		{
			Log.Warn("设置窗口样式失败：" + ex.Message);
		}
	}

	public static void SetTopmostPosition(IntPtr hwnd, int x, int y, bool noActivate = true)
	{
		const uint SWP_NOSIZE = 0x0001;
		const uint SWP_NOACTIVATE = 0x0010;
		NativeMethods.SetWindowPos(hwnd, new IntPtr(-1) /* HWND_TOPMOST */, x, y, 0, 0,
			SWP_NOSIZE | (noActivate ? SWP_NOACTIVATE : 0));
	}

	public static void SetTopmost(IntPtr hwnd) =>
		NativeMethods.SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOSIZE|NOMOVE|NOACTIVATE

	/// <summary>前台是否全屏应用（游戏/视频）—— 是的话常驻条应让位。</summary>
	public static bool IsForegroundFullscreen()
	{
		var fg = NativeMethods.GetForegroundWindow();
		if (fg == IntPtr.Zero) return false;
		if (fg == NativeMethods.FindWindowW("Shell_TrayWnd", null)) return false;

		var cls = new char[256];
		var n = NativeMethods.GetClassNameW(fg, cls, cls.Length);
		var clsName = new string(cls, 0, Math.Max(0, n));
		if (clsName is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
		if (!NativeMethods.IsWindowVisible(fg)) return false;

		if (!NativeMethods.GetWindowRect(fg, out var r)) return false;
		var mon = NativeMethods.MonitorFromWindow(fg, 2);
		var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
		if (!NativeMethods.GetMonitorInfoW(mon, ref info)) return false;

		const int TOL = 2;
		return r.Left <= info.rcMonitor.Left + TOL
			&& r.Top <= info.rcMonitor.Top + TOL
			&& r.Right >= info.rcMonitor.Right - TOL
			&& r.Bottom >= info.rcMonitor.Bottom - TOL;
	}

	private static class NativeMethods
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct MONITORINFO
		{
			public int cbSize;
			public RECT rcMonitor;
			public RECT rcWork;
			public uint dwFlags;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct APPBARDATA
		{
			public int cbSize;
			public IntPtr hWnd;
			public uint uCallbackMessage;
			public uint uEdge;
			public RECT rc;
			public IntPtr lParam;
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern int GetClassNameW(IntPtr hWnd, char[] lpClassName, int nMaxCount);

		[DllImport("user32.dll")]
		public static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern uint GetDpiForWindow(IntPtr hwnd);

		[DllImport("user32.dll")]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

		[DllImport("user32.dll")]
		public static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		public static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		[DllImport("shell32.dll")]
		public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern uint RegisterWindowMessageW(string lpString);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
	}
}
