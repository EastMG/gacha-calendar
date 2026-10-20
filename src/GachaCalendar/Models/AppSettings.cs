namespace GachaCalendar.Models;

/// <summary>桌面端本地设置（<c>%LOCALAPPDATA%\GachaCalendar\settings.json</c>）。</summary>
public sealed class AppSettings
{
	/// <summary>是否在任务栏左侧常驻显示小组件条。</summary>
	public bool DockEnabled { get; set; } = true;

	/// <summary>常驻条上最多同时显示几个条目（1–6）。</summary>
	public int DockItems { get; set; } = 3;

	/// <summary>独立的数据文件路径；为空时按默认顺序查找。</summary>
	public string? DataFile { get; set; }

	/// <summary>全屏应用（游戏/视频）前台时隐藏常驻条。</summary>
	public bool HideWhenFullscreen { get; set; } = true;

	/// <summary>点击常驻条时展开面板。</summary>
	public bool OpenPanelOnClick { get; set; } = true;

	/// <summary>开机自动启动（HKCU Run）。</summary>
	public bool StartWithWindows { get; set; }

	public void Normalize()
	{
		DockItems = Math.Clamp(DockItems, 1, 6);
		if (string.IsNullOrWhiteSpace(DataFile)) DataFile = null;
	}
}
