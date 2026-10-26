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
			// Fluent 2 / Windows 11 浅色：层叠表面、中性描边、系统蓝强调色。
			Set(res, "PanelBg", "#FFF9F9F9");
			Set(res, "PanelBorder", "#FFE5E5E5");
			Set(res, "Fg", "#FF1A1A1A");
			Set(res, "FgDim", "#FF5D5D5D");
			Set(res, "FgFaint", "#FF8A8A8A");
			Set(res, "Accent", "#FF0067C0");
			Set(res, "Danger", "#FFC42B1C");
			Set(res, "Warn", "#FF9D5D00");
			Set(res, "ChipBg", "#FFFFFFFF");
			Set(res, "ChipBorder", "#FFE5E5E5");
			Set(res, "DockBg", "#FFF9F9F9");
			Set(res, "HoverBg", "#0A000000");
			Set(res, "Divider", "#FFE5E5E5");
		}
		else
		{
			// Fluent 2 / Windows 11 深色。
			Set(res, "PanelBg", "#FF202020");
			Set(res, "PanelBorder", "#FF3A3A3A");
			Set(res, "Fg", "#FFFFFFFF");
			Set(res, "FgDim", "#FFC7C7C7");
			Set(res, "FgFaint", "#FF9D9D9D");
			Set(res, "Accent", "#FF60CDFF");
			Set(res, "Danger", "#FFFF99A4");
			Set(res, "Warn", "#FFFCE100");
			Set(res, "ChipBg", "#FF2B2B2B");
			Set(res, "ChipBorder", "#FF3A3A3A");
			Set(res, "DockBg", "#FF202020");
			Set(res, "HoverBg", "#0FFFFFFF");
			Set(res, "Divider", "#FF3A3A3A");
		}
	}

	public static Brush GetBrush(string key) => (Brush)System.Windows.Application.Current.Resources[key];

	/// <summary>DWM 材质可用时把表面改成半透明层叠；失败时保持不透明。</summary>
	public static void UseBackdropMaterials()
	{
		var res = System.Windows.Application.Current.Resources;
		if (IsLight)
		{
			SetGradient(res, "PanelBg", "#D9FCFCFC", "#CCF2F2F2");
			SetGradient(res, "ChipBg", "#F2FFFFFF", "#E8FBFBFB");
			SetGradient(res, "DockBg", "#F2FFFFFF", "#E6F7F7F7");
		}
		else
		{
			SetGradient(res, "PanelBg", "#D9262626", "#CC1E1E1E");
			SetGradient(res, "ChipBg", "#F22E2E2E", "#E8272727");
			SetGradient(res, "DockBg", "#F22C2C2C", "#E6212121");
		}
	}

	private static void Set(ResourceDictionary res, string key, string hex)
	{
		res[key] = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
	}

	private static void SetGradient(ResourceDictionary res, string key, string from, string to)
	{
		var a = (Color)System.Windows.Media.ColorConverter.ConvertFromString(from)!;
		var b = (Color)System.Windows.Media.ColorConverter.ConvertFromString(to)!;
		var brush = new LinearGradientBrush(a, b, 90);
		brush.Freeze();
		res[key] = brush;
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
