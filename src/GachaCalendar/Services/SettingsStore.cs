using System.IO;
using System.Text.Json;
using GachaCalendar.Models;

namespace GachaCalendar.Services;

public sealed class SettingsStore
{
	public AppSettings Load()
	{
		try
		{
			if (File.Exists(AppPaths.SettingsFile))
			{
				var json = File.ReadAllText(AppPaths.SettingsFile);
				var s = JsonSerializer.Deserialize<AppSettings>(json, Json.Read) ?? new AppSettings();
				s.Normalize();
				return s;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("读取设置失败，使用默认值：" + ex.Message);
		}
		return new AppSettings();
	}

	public bool Save(AppSettings settings)
	{
		try
		{
			settings.Normalize();
			Directory.CreateDirectory(AppPaths.LocalDataDir);
			File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Json.Write));
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("保存设置失败", ex);
			return false;
		}
	}
}
