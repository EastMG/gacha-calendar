namespace GachaCalendar.Services;

/// <summary>单实例：第二个进程不启动第二份小组件，而是让已在运行的实例展开面板。</summary>
public static class SingleInstance
{
	private const string MutexName = @"Local\GachaCalendar.SingleInstance.v1";
	private static Mutex? _mutex;

	public static bool Acquire()
	{
		try
		{
			_mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
			return createdNew;
		}
		catch (Exception ex)
		{
			Log.Warn("创建单实例互斥体失败：" + ex.Message);
			return true;
		}
	}

	public static void NotifyExisting() => Taskbar.BroadcastShowPanel();
}
