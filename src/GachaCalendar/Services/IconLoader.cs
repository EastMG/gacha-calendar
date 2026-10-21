using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GachaCalendar.Services;

/// <summary>
/// 游戏图标加载：**不在 UI 层发网络请求**（仓库硬边界），
/// 统一由 Host 的 fetchBinary 取二进制，再在这里解码成 BitmapImage。
/// Host 不可用 / 取图失败时返回 null，RowVm 退化为首字方块。
/// </summary>
public static class IconLoader
{
	private static readonly ConcurrentDictionary<string, Task<ImageSource?>> Cache = new();
	private static Func<string, Task<byte[]?>>? _fetcher;

	public static void SetFetcher(Func<string, Task<byte[]?>>? fetcher)
	{
		_fetcher = fetcher;
		Cache.Clear();
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
		var fetcher = _fetcher;
		if (fetcher == null) return null;
		try
		{
			var bytes = await fetcher(uri.ToString());
			if (bytes == null || bytes.Length == 0) return null;

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
	}
}
