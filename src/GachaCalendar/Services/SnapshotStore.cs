using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using GachaCalendar.Models;

namespace GachaCalendar.Services;

public sealed record SnapshotLoad(Snapshot? Snapshot, string? Path, string? Error, bool IsSample)
{
	public bool Ok => Snapshot != null;
}

/// <summary>
/// 读取并监听快照文件。v0.1 的抓取在应用外（tools/refresh.mjs）完成；
/// 应用只读文件，所以这里同时充当"开发热重载"：文件一变就重新载入。
/// </summary>
public sealed class SnapshotStore : IDisposable
{
	private readonly DispatcherTimer _debounce;
	private FileSystemWatcher? _watcher;
	private AppSettings _settings = new();
	private bool _disposed;

	public event Action<SnapshotLoad>? Changed;
	public SnapshotLoad Last { get; private set; } = new(null, null, null, false);

	public SnapshotStore()
	{
		_debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
		_debounce.Tick += (_, _) =>
		{
			_debounce.Stop();
			if (_disposed) return;
			var load = Reload(_settings);
			Changed?.Invoke(load);
		};
	}

	public SnapshotLoad Reload(AppSettings settings)
	{
		_settings = settings;
		var load = Read(settings);
		Last = load;
		SetupWatcher(load.Path);
		return load;
	}

	private static SnapshotLoad Read(AppSettings settings)
	{
		foreach (var (path, isSample) in CandidatePaths(settings))
		{
			if (!File.Exists(path)) continue;
			try
			{
				var json = File.ReadAllText(path);
				var snap = JsonSerializer.Deserialize<Snapshot>(json, Json.Read);
				if (snap == null) return new SnapshotLoad(null, path, "快照文件为空或格式不正确", isSample);
				if (snap.Games == null) snap.Games = new();
				if (snap.Result == null) snap.Result = new();
				return new SnapshotLoad(snap, path, null, isSample);
			}
			catch (Exception ex)
			{
				Log.Error($"读取快照失败：{path}", ex);
				return new SnapshotLoad(null, path, "读取快照失败：" + ex.Message, isSample);
			}
		}

		// 最后兜底：编译进程序的示例快照（首次运行 / 无网络时也有内容可看）
		try
		{
			using var stream = AppPaths.OpenEmbeddedSample();
			if (stream != null)
			{
				using var reader = new StreamReader(stream);
				var snap = JsonSerializer.Deserialize<Snapshot>(reader.ReadToEnd(), Json.Read);
				if (snap != null)
				{
					snap.Games ??= new();
					snap.Result ??= new();
					return new SnapshotLoad(snap, "（内置示例快照）", null, true);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("读取内置示例快照失败", ex);
		}

		return new SnapshotLoad(null, null, "找不到快照文件。请先运行 `cd tools; npm run refresh` 生成 data/snapshot.json。", false);
	}

	private static IEnumerable<(string Path, bool IsSample)> CandidatePaths(AppSettings settings)
	{
		var real = new List<string>();
		var samples = new List<string>();

		if (!string.IsNullOrWhiteSpace(settings.DataFile)) real.Add(settings.DataFile!);
		var env = Environment.GetEnvironmentVariable("GACHA_CALENDAR_DATA");
		if (!string.IsNullOrWhiteSpace(env)) real.Add(env!);
		real.Add(AppPaths.DefaultSnapshotFile);
		real.Add(Path.Combine(AppContext.BaseDirectory, "data", "snapshot.json"));

		foreach (var root in Roots())
		{
			real.Add(Path.Combine(root, "data", "snapshot.json"));
			samples.Add(Path.Combine(root, "data", "sample-snapshot.json"));
		}

		foreach (var p in real) yield return (p, false);
		foreach (var p in samples) yield return (p, true);
	}

	private static IEnumerable<string> Roots()
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
		{
			var dir = new DirectoryInfo(start);
			for (var i = 0; i < 8 && dir != null; i++)
			{
				if (seen.Add(dir.FullName)) yield return dir.FullName;
				dir = dir.Parent;
			}
		}
	}

	private void SetupWatcher(string? path)
	{
		_watcher?.Dispose();
		_watcher = null;
		if (string.IsNullOrWhiteSpace(path) || path.StartsWith("（", StringComparison.Ordinal)) return;
		try
		{
			var dir = Path.GetDirectoryName(path);
			if (dir == null || !Directory.Exists(dir)) return;
			_watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
			{
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
				EnableRaisingEvents = true,
			};
			_watcher.Changed += (_, _) => Schedule();
			_watcher.Created += (_, _) => Schedule();
			_watcher.Renamed += (_, _) => Schedule();
		}
		catch (Exception ex)
		{
			Log.Warn("监听快照文件失败：" + ex.Message);
		}
	}

	private void Schedule()
	{
		_debounce.Stop();
		_debounce.Start();
	}

	public void Dispose()
	{
		_disposed = true;
		_debounce.Stop();
		_watcher?.Dispose();
		_watcher = null;
	}
}
