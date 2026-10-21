# 09 · 外壳选型：已决定 WPF

> 2026-10-21 状态：**用户拍板，Windows 外壳继续 WPF**。
> 壳无关基础层已完成（见 `docs/04` 的 v0.1.1–v0.1.6）；Fluent 风格外观已列入
> 仓库根目录的 [`BACKLOG.md`](../BACKLOG.md)。

## 1. 决定

**Windows 外壳：WPF（.NET 10，当前原型）**，不再评估 Tauri 重写。

数据、配置、调度、校验、CLI、Host 协议、视图模型契约都已抽成壳无关层；
继续用 WPF 意味着这些资产全部保留，任务栏/托盘/单实例/自启也可以沿用现有实现。

## 2. 为什么选 WPF

1. **本机可编译、可运行、可截图验证**——迭代成本最低；
2. 任务栏覆盖窗已实测可用（`Shell_TrayWnd` 定位、`WS_EX_NOACTIVATE`、topmost、PerMonitorV2）；
3. 托盘、单实例、开机自启、DPI 都有直接 Win32 路径，不需要额外框架胶水；
4. 无 WebView2 时内存更省，适合"常驻任务栏的小组件"；
5. 现有 C# 外壳和面板不用推倒重来。

## 3. 为什么当前不选 Tauri

- 任务栏覆盖窗在 Tauri 里同样要写 Rust + Win32，省不掉这部分工作；
- 本机没有 Rust + MSVC Build Tools，只能靠 CI 出包，迭代反馈慢；
- 跨平台桌面目前不是近期目标；Windows 任务栏小组件本身就是 Windows 专属形态；
- 如果将来真要做 macOS/Linux 桌面版，再评估 Tauri 即可，壳无关层不会阻塞。

## 4. 下一步（按顺序）

1. **WPF 接入 Host**：启动 `packages/engine/host.mjs` 子进程（或 C# 实现同协议），
   用 `refreshIfDue` / `getStatus` / `updateConfig`；
2. **设置页接入**：`entries` + `setEntryVisibility` / `setOrder` / `removeEntry` / `restoreEntry`；
3. **Fluent 风格外观**：按 `BACKLOG.md` 的清单做（不承诺版本，属于 0.1.x 待排期）；
4. 托盘、自启、多显示器、安装包按路线图推进。

## 5. 什么情况下重新评估 Tauri

- macOS/Linux 桌面版成为 6–12 个月内的明确目标；
- 希望用同一套 Web UI 覆盖桌面端，且愿意承担 Rust 工具链与 CI 出包的开发成本；
- 到那时再评估，不影响当前 Windows 版的代码。

## 6. 不做的事

- 不为了"技术先进"把已经能跑的 WPF 外壳推倒重来；
- 不在没有明确指令时打 tag / 发 Release。

> 多方案对比（WPF / WPF+WebView2 / WinUI 3 / Tauri / Electron / Avalonia / WinForms）
> 见 [10-外壳技术方案对比](10-外壳技术方案对比.md)。
