using Microsoft.Win32;

namespace GachaCalendar.Services;

/// <summary>开机自启：HKCU Run 键，不需要管理员权限。</summary>
public static class Autostart
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "GachaCalendar";

	public static bool IsEnabled()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(RunKey);
			return key?.GetValue(ValueName) is string s && s.Length > 0;
		}
		catch { return false; }
	}

	public static bool Set(bool enabled)
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
			if (key == null) return false;
			if (enabled)
			{
				var exe = Environment.ProcessPath;
				if (string.IsNullOrEmpty(exe)) return false;
				key.SetValue(ValueName, $"\"{exe}\" --autostart");
			}
			else
			{
				key.DeleteValue(ValueName, throwOnMissingValue: false);
			}
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("设置开机自启失败", ex);
			return false;
		}
	}
}
