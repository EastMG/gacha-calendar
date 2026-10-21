using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace GachaCalendar.Services;

/// <summary>跟随系统浅/深色切换配色（启动时读取一次；后续可加 UserPreferenceChanged 监听）。</summary>
public static class ThemeManager
{
	public static bool IsLight { get; private set; }

	public static void Apply()
	{
		IsLight = ReadIsLight();
		var res = System.Windows.Application.Current.Resources;
		if (IsLight)
		{
			// 对齐 DSH 面板的浅色 token：白底、#1F2329 主文字、#6B7280 次文字、#8A9099 弱文字、
			// #E5E6EB 分隔线、#3B5BFD 业务蓝。
			Set(res, "PanelBg", "#FFFFFFFF");
			Set(res, "PanelBorder", "#FFE5E6EB");
			Set(res, "Fg", "#FF1F2329");
			Set(res, "FgDim", "#FF6B7280");
			Set(res, "FgFaint", "#FF8A9099");
			Set(res, "Accent", "#FF3B5BFD");
			Set(res, "Danger", "#FFD9484D");
			Set(res, "Warn", "#FFB45309");
			Set(res, "ChipBg", "#FFF7F8FA");
			Set(res, "ChipBorder", "#14000000");
			Set(res, "DockBg", "#F2FFFFFF");
			Set(res, "HoverBg", "#0D000000");
			Set(res, "Divider", "#FFE5E6EB");
		}
		else
		{
			Set(res, "PanelBg", "#FF1F2124");
			Set(res, "PanelBorder", "#FF35383D");
			Set(res, "Fg", "#FFE8EAED");
			Set(res, "FgDim", "#FF9AA0A6");
			Set(res, "FgFaint", "#FF6B7075");
			Set(res, "Accent", "#FF7A9BFF");
			Set(res, "Danger", "#FFFF7B7B");
			Set(res, "Warn", "#FFE0A458");
			Set(res, "ChipBg", "#14FFFFFF");
			Set(res, "ChipBorder", "#22FFFFFF");
			Set(res, "DockBg", "#E61F2124");
			Set(res, "HoverBg", "#1AFFFFFF");
			Set(res, "Divider", "#FF2B2E32");
		}
	}

	public static Brush GetBrush(string key) => (Brush)System.Windows.Application.Current.Resources[key];

	private static void Set(ResourceDictionary res, string key, string hex)
	{
		res[key] = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
	}

	private static bool ReadIsLight()
	{
		var forced = Environment.GetEnvironmentVariable("GACHA_CALENDAR_THEME");
		if (string.Equals(forced, "light", StringComparison.OrdinalIgnoreCase)) return true;
		if (string.Equals(forced, "dark", StringComparison.OrdinalIgnoreCase)) return false;
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			return Convert.ToInt32(key?.GetValue("AppsUseLightTheme") ?? 1) != 0;
		}
		catch { return false; }
	}
}
