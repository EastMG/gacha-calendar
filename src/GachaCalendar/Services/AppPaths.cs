using System.IO;
using System.Reflection;

namespace GachaCalendar.Services;

/// <summary>运行期路径约定：所有可变数据都在 %LOCALAPPDATA%\GachaCalendar，卸载不碰安装目录。</summary>
public static class AppPaths
{
	public static string LocalDataDir { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GachaCalendar");

	public static string SettingsFile => Path.Combine(LocalDataDir, "settings.json");
	public static string DefaultSnapshotFile => Path.Combine(LocalDataDir, "snapshot.json");
	public static string LogFile => Path.Combine(LocalDataDir, "logs", "app.log");

	public static string EnsureLocalDataDir()
	{
		try { Directory.CreateDirectory(LocalDataDir); } catch { /* 只读环境也要能跑 */ }
		return LocalDataDir;
	}

	public static Stream? OpenEmbeddedSample()
	{
		var asm = Assembly.GetExecutingAssembly();
		return asm.GetManifestResourceStream("sample-snapshot.json");
	}
}

/// <summary>极简日志：只在本地写，不外发；失败静默（日志不该拖垮应用）。</summary>
public static class Log
{
	private static readonly object Gate = new();

	public static void Info(string message) => Write("INFO", message);
	public static void Warn(string message) => Write("WARN", message);
	public static void Error(string message, Exception? ex = null) =>
		Write("ERROR", ex == null ? message : message + Environment.NewLine + ex);

	private static void Write(string level, string message)
	{
		try
		{
			lock (Gate)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LogFile)!);
				File.AppendAllText(AppPaths.LogFile,
					$"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
			}
		}
		catch { /* 忽略 */ }
	}
}
