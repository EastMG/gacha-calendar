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
			Set(res, "PanelBg", "#FAFFFFFF");
			Set(res, "PanelBorder", "#FFE2E5EA");
			Set(res, "Fg", "#FF1A1D21");
			Set(res, "FgDim", "#FF6B7280");
			Set(res, "FgFaint", "#FF9CA3AF");
			Set(res, "Accent", "#FF2563EB");
			Set(res, "Danger", "#FFD9484D");
			Set(res, "Warn", "#FFB45309");
			Set(res, "ChipBg", "#FFF2F4F7");
			Set(res, "ChipBorder", "#14000000");
			Set(res, "DockBg", "#F2FFFFFF");
			Set(res, "HoverBg", "#0D000000");
			Set(res, "Divider", "#FFF0F1F3");
		}
		else
		{
			Set(res, "PanelBg", "#F21F2124");
			Set(res, "PanelBorder", "#FF35383D");
			Set(res, "Fg", "#FFE8EAED");
			Set(res, "FgDim", "#FF9AA0A6");
			Set(res, "FgFaint", "#FF6B7075");
			Set(res, "Accent", "#FF7AB0FF");
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
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			return Convert.ToInt32(key?.GetValue("AppsUseLightTheme") ?? 1) != 0;
		}
		catch { return false; }
	}
}
