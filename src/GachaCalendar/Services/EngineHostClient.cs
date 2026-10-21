using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GachaCalendar.Services;

/// <summary>
/// 应用内刷新：启动 packages/engine/host.mjs（Node JSON-lines Host），
/// 由 Host 复用 gacha-calendar-core，避免在 C# 里重写抓取。
///
/// 这是 v0.1.7 的开发期接法；最终分发时可以把 Node 运行时/或 WebView2 CoreHost 一起打包，
/// 但 Host 协议不变（见 docs/07-壳无关基础层.md）。
/// </summary>
public sealed class EngineHostClient : IAsyncDisposable
{
	private readonly object _gate = new();
	private readonly Dictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
	private Process? _process;
	private StreamWriter? _stdin;
	private Task? _stdoutTask;
	private Task? _stderrTask;
	private long _nextId;
	private bool _started;
	private bool _disposed;

	public bool IsAvailable => _started && _process is { HasExited: false } && _stdin != null;
	public string? LastError { get; private set; }
	public string? NodePath { get; private set; }
	public string? HostScript { get; private set; }

	/// <summary>在仓库（或安装目录）里找 host.mjs；找不到返回 null。</summary>
	public static string? FindHostScript()
	{
		var explicitPath = Environment.GetEnvironmentVariable("GACHA_CALENDAR_HOST");
		if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;

		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		for (var i = 0; i < 10 && dir != null; i++)
		{
			var candidate = Path.Combine(dir.FullName, "packages", "engine", "host.mjs");
			if (File.Exists(candidate)) return candidate;
			candidate = Path.Combine(dir.FullName, "engine", "host.mjs"); // 未来安装包内的布局
			if (File.Exists(candidate)) return candidate;
			dir = dir.Parent;
		}
		return null;
	}

	public static string ResolveNodePath()
	{
		var explicitNode = Environment.GetEnvironmentVariable("GACHA_CALENDAR_NODE");
		if (!string.IsNullOrWhiteSpace(explicitNode)) return explicitNode;
		return "node";
	}

	public async Task<bool> StartAsync(string dataDir)
	{
		if (_disposed) throw new ObjectDisposedException(nameof(EngineHostClient));
		if (IsAvailable) return true;

		HostScript = FindHostScript();
		if (HostScript == null)
		{
			LastError = "找不到 packages/engine/host.mjs（开发期请从仓库运行）";
			return false;
		}
		NodePath = ResolveNodePath();
		Directory.CreateDirectory(dataDir);

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = NodePath,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8,
				WorkingDirectory = Path.GetDirectoryName(HostScript) ?? AppContext.BaseDirectory,
			};
			psi.ArgumentList.Add(HostScript);
			psi.ArgumentList.Add("--data-dir");
			psi.ArgumentList.Add(dataDir);
			psi.Environment["GACHA_CALENDAR_DATA_DIR"] = dataDir;

			_process = new Process { StartInfo = psi, EnableRaisingEvents = true };
			_process.Exited += (_, _) => OnProcessExited();
			if (!_process.Start())
			{
				LastError = "Node 进程启动失败";
				return false;
			}

			_stdin = _process.StandardInput;
			_stdin.AutoFlush = true;
			_started = true;
			LastError = null;
			_stdoutTask = Task.Run(ReadStdoutAsync);
			_stderrTask = Task.Run(ReadStderrAsync);

			// 握手：确认 Host 能正常响应
			await CallAsync("hello", null, TimeSpan.FromSeconds(8));
			return true;
		}
		catch (Exception ex)
		{
			LastError = "Node Host 启动失败：" + ex.Message;
			Log.Error("EngineHostClient 启动失败", ex);
			CleanupProcess();
			return false;
		}
	}

	private async Task ReadStdoutAsync()
	{
		try
		{
			while (_process != null && !_process.HasExited && _process.StandardOutput.ReadLine() is { } line)
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				JsonNode? root;
				try { root = JsonNode.Parse(line); }
				catch { Log.Warn("Host 输出不是 JSON：" + line); continue; }
				var id = root?["id"]?.GetValue<long?>();
				if (id == null) continue;
				TaskCompletionSource<JsonNode?>? tcs;
				lock (_gate)
				{
					if (!_pending.Remove(id.Value, out tcs)) continue;
				}
				if (root?["ok"]?.GetValue<bool>() == true)
				{
					tcs!.TrySetResult(root["result"]?.DeepClone());
				}
				else
				{
					var message = root?["error"]?["message"]?.GetValue<string>() ?? "Host 返回错误";
					var code = root?["error"]?["code"]?.GetValue<string>();
					tcs!.TrySetException(new EngineHostException(message, code));
				}
			}
		}
		catch (Exception ex)
		{
			if (!_disposed) Log.Warn("Host stdout 读取结束：" + ex.Message);
		}
		finally
		{
			FailAllPending(new EngineHostException("Host 已退出"));
			_started = false;
		}
	}

	private async Task ReadStderrAsync()
	{
		try
		{
			while (_process != null && !_process.HasExited && _process.StandardError.ReadLine() is { } line)
			{
				if (!string.IsNullOrWhiteSpace(line)) Log.Info("[engine] " + line);
			}
		}
		catch { /* 退出路径不抛 */ }
	}

	private void OnProcessExited()
	{
		_started = false;
		FailAllPending(new EngineHostException("Host 进程已退出"));
	}

	private void FailAllPending(Exception ex)
	{
		List<TaskCompletionSource<JsonNode?>> pending;
		lock (_gate)
		{
			pending = _pending.Values.ToList();
			_pending.Clear();
		}
		foreach (var tcs in pending) tcs.TrySetException(ex);
	}

	public async Task<JsonNode?> CallAsync(string method, JsonNode? parameters = null, TimeSpan? timeout = null)
	{
		if (!IsAvailable || _stdin == null)
			throw new EngineHostException(LastError ?? "引擎 Host 不可用");

		var id = Interlocked.Increment(ref _nextId);
		var request = new JsonObject
		{
			["id"] = id,
			["method"] = method,
		};
		if (parameters != null) request["params"] = parameters;

		var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (_gate) _pending[id] = tcs;

		try
		{
			await _stdin.WriteLineAsync(request.ToJsonString());
			var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(90);
			return await tcs.Task.WaitAsync(effectiveTimeout);
		}
		catch (TimeoutException)
		{
			lock (_gate) _pending.Remove(id);
			throw new EngineHostException($"Host 命令超时：{method}");
		}
		catch
		{
			lock (_gate) _pending.Remove(id);
			throw;
		}
	}

	public Task<JsonNode?> RefreshAsync() => CallAsync("refresh");

	public async Task<RefreshDecision> RefreshIfDueAsync()
	{
		var result = await CallAsync("refreshIfDue", null, TimeSpan.FromSeconds(120));
		var refreshed = result?["refreshed"]?.GetValue<bool>() == true;
		var reason = result?["reason"]?.GetValue<string>() ?? "";
		var error = result?["error"]?["message"]?.GetValue<string>();
		return new RefreshDecision(refreshed, reason, error);
	}

	public Task<JsonNode?> GetStatusAsync() => CallAsync("getStatus");

	public async Task<byte[]?> FetchBinaryAsync(string url)
	{
		var result = await CallAsync("fetchBinary", new JsonObject { ["url"] = url }, TimeSpan.FromSeconds(25));
		if (result?["error"] != null) return null;
		var base64 = result?["base64"]?.GetValue<string>();
		if (string.IsNullOrWhiteSpace(base64)) return null;
		try { return Convert.FromBase64String(base64); }
		catch { return null; }
	}

	public Task<JsonNode?> GetConfigAsync() => CallAsync("getConfig");

	public Task<JsonNode?> UpdateConfigAsync(JsonNode patch) =>
		CallAsync("updateConfig", new JsonObject { ["patch"] = patch.DeepClone() });

	public Task<JsonNode?> ListEntriesAsync() => CallAsync("listEntries");

	public Task<JsonNode?> ImportConfigAsync(JsonNode config) =>
		CallAsync("importConfig", new JsonObject { ["config"] = config.DeepClone() });

	public Task<JsonNode?> SelfCheckAsync() => CallAsync("selfCheck", null, TimeSpan.FromSeconds(180));

	public async Task StopAsync()
	{
		if (!_started || _stdin == null) return;
		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
			await CallAsync("shutdown", null, TimeSpan.FromSeconds(2));
		}
		catch { /* 退出时忽略 */ }
		CleanupProcess();
	}

	private void CleanupProcess()
	{
		_started = false;
		try { _stdin?.Dispose(); } catch { }
		_stdin = null;
		try
		{
			if (_process != null && !_process.HasExited)
			{
				_process.Kill(entireProcessTree: true);
				_process.WaitForExit(2000);
			}
		}
		catch { }
		try { _process?.Dispose(); } catch { }
		_process = null;
	}

	public void Kill() => CleanupProcess();

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;
		_disposed = true;
		await StopAsync();
	}
}

public sealed record RefreshDecision(bool Refreshed, string Reason, string? Error);

public sealed class EngineHostException : Exception
{
	public string? Code { get; }
	public EngineHostException(string message, string? code = null) : base(message) => Code = code;
}
