using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;

namespace GachaCalendar.Services;

/// <summary>系统托盘图标与菜单（WinForms NotifyIcon；WPF 没有原生托盘 API）。</summary>
public sealed class TrayIcon : IDisposable
{
	private readonly NotifyIcon _notify;
	private readonly ToolStripMenuItem _dockItem;

	public TrayIcon(Action openPanel, Action toggleDock, Action openSettings, Action reloadData, Action exit)
	{
		_dockItem = new ToolStripMenuItem("隐藏任务栏小组件");
		_dockItem.Click += (_, _) => toggleDock();

		var menu = new ContextMenuStrip();
		menu.Items.Add(new ToolStripMenuItem("打开二游日历", null, (_, _) => openPanel()));
		menu.Items.Add(_dockItem);
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripMenuItem("设置…", null, (_, _) => openSettings()));
		menu.Items.Add(new ToolStripMenuItem("重新读取数据", null, (_, _) => reloadData()));
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => exit()));

		_notify = new NotifyIcon
		{
			Text = "二游日历 · 任务栏小组件",
			Icon = LoadIcon(),
			Visible = true,
			ContextMenuStrip = menu,
		};
		_notify.DoubleClick += (_, _) => openPanel();

		// 某些启动顺序下 Explorer 通知区尚未就绪，Visible=true 会被吞掉；
		// 做几次轻量重注册，不阻塞 UI 线程。
		var retry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
		var attempts = 0;
		retry.Tick += (_, _) =>
		{
			attempts++;
			if (attempts > 3)
			{
				retry.Stop();
				return;
			}
			try
			{
				_notify.Visible = false;
				_notify.Visible = true;
			}
			catch { /* 托盘不可用时静默 */ }
		};
		retry.Start();
	}

	public void SetDockVisible(bool visible) =>
		_dockItem.Text = visible ? "隐藏任务栏小组件" : "显示任务栏小组件";

	private static Icon LoadIcon()
	{
		try
		{
			var exe = Environment.ProcessPath;
			if (!string.IsNullOrEmpty(exe))
			{
				var icon = Icon.ExtractAssociatedIcon(exe);
				if (icon != null) return icon;
			}
		}
		catch { /* 用系统图标兜底 */ }
		return SystemIcons.Application;
	}

	public void Dispose()
	{
		try
		{
			_notify.Visible = false;
			_notify.Dispose();
		}
		catch { /* 退出路径不抛 */ }
	}
}
