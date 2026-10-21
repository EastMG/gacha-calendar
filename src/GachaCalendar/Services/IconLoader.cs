using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GachaCalendar.Services;

/// <summary>
/// 游戏图标加载：外链图标 + 内存缓存 + 失败降级（交给 RowVm 显示首字方块）。
/// 图标版权归原权利方，本项目只做展示、不打包进仓库。
/// </summary>
public static class IconLoader
{
	private static readonly HttpClient Http = CreateClient();
	private static readonly ConcurrentDictionary<string, Task<ImageSource?>> Cache = new();
	private static readonly SemaphoreSlim Gate = new(6, 6);
	private static Func<string, Task<byte[]?>>? _fallback;

	/// <summary>
	/// 受限环境（DSH 沙箱等）里 .NET 的 TLS 可能不可用；这时改走 Node Host 取二进制。
	/// 设置 fallback 会清空缓存，让已失败过的图标重试一次。
	/// </summary>
	public static void SetFallback(Func<string, Task<byte[]?>>? fallback)
	{
		_fallback = fallback;
		Cache.Clear();
	}

	private static HttpClient CreateClient()
	{
		var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
		client.DefaultRequestHeaders.TryAddWithoutValidation(
			"User-Agent",
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36");
		client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/png,image/*,*/*;q=0.8");
		return client;
	}

	public static Task<ImageSource?> GetAsync(string? url)
	{
		if (string.IsNullOrWhiteSpace(url)) return Task.FromResult<ImageSource?>(null);
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
			return Task.FromResult<ImageSource?>(null);
		return Cache.GetOrAdd(url, _ => LoadAsync(uri));
	}

	private static async Task<ImageSource?> LoadAsync(Uri uri)
	{
		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			byte[]? bytes = null;
			try
			{
				using var response = await Http.GetAsync(uri).ConfigureAwait(false);
				if (response.IsSuccessStatusCode)
					bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
				else
					Log.Warn($"图标 HTTP {(int)response.StatusCode}：{uri}");
			}
			catch (Exception ex)
			{
				Log.Warn($"图标直连失败，尝试 Host 回退：{uri} {ex.Message}");
			}

			if ((bytes == null || bytes.Length == 0) && _fallback != null)
			{
				try { bytes = await _fallback(uri.ToString()).ConfigureAwait(false); }
				catch (Exception ex) { Log.Warn($"图标 Host 回退失败：{uri} {ex.Message}"); }
			}
			if (bytes == null || bytes.Length == 0) return null;

			// BitmapImage 可在线程池创建并 Freeze，之后绑定到 UI 是安全的
			using var stream = new MemoryStream(bytes);
			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.StreamSource = stream;
			bitmap.EndInit();
			bitmap.Freeze();
			return bitmap;
		}
		catch (Exception ex)
		{
			Log.Warn($"图标加载失败：{uri} {ex.Message}");
			return null;
		}
		finally
		{
			Gate.Release();
		}
	}
}
