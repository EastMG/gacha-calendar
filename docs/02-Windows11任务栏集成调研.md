# 02 · Windows 11 任务栏集成调研

> 调研时间：2026-10。目标：把一枚二游日历小组件**常驻显示在 Windows 11 任务栏左侧**。
> 结论先行：**v1 采用「置顶停靠窗」方案**（读取 `Shell_TrayWnd` 几何，用 `WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW`
> 的 topmost 窗口贴在任务栏左侧），**不注入 explorer、不替换系统任务栏**。
> 注入方案作为 v2 实验项保留。

## 1. 四种可选路线

| 方案 | 做法 | 是否真正"在任务栏里" | 稳定性 | 成本 | 代表项目 |
|---|---|---|---|---|---|
| **A. 置顶停靠窗**（选定） | 读任务栏矩形，把无边框置顶窗贴在其左侧；不抢焦点 | 视觉上在任务栏内 | 高：只用公开 API 与窗口样式 | 低（C# P/Invoke 即可） | ElevenClock、Todobar |
| **B. 注入 explorer 的 XAML 任务栏** | 通过 XAML Diagnostics 找到任务栏视觉树，插入自定义元素 | 是（真正的任务栏子元素） | 低：依赖未公开结构，Windows 更新会破坏 | 高（C++/Rust + 严格兼容检查） | TaskbarWidgets |
| **C. 官方 Widgets Board Provider** | Windows App SDK `IWidgetProvider` + Adaptive Cards | 否：只出现在"小组件面板"里，不在任务栏常驻 | 高（微软接口） | 中（打包/WinRT/MSIX） | 微软文档、PWA 小组件 |
| **D. 重建整条任务栏** | 隐藏原生任务栏，自己画一条 | 否（替换） | 中：控制力最强、但与系统集成全断 | 很高 | sambar |

补充：**E. 托盘图标 + 弹出面板**——最小可用形态，任何方案都能保留作为兜底入口。

## 2. 逐个方案的依据

### A. 置顶停靠窗（本仓库采用）

- **ElevenClock** 用这个思路把自定义时钟放到 Windows 11 任务栏上（官方限制无法改系统时钟，
  所以它自己画一个贴在同一位置）：[marticliment/ElevenClock](https://github.com/marticliment/ElevenClock)（Apache-2.0）。
- **Todobar** 用 Tauri v2 实现"贴屏幕边缘的常驻侧栏"：无边框、always-on-top、边缘停靠、
  点穿透明区、单实例、托盘、开机自启，一套很接近本需求的桌面壳实践：
  [Leonxlnx/todobar](https://github.com/Leonxlnx/todobar)（Apache-2.0）。
- 关键技术点（本仓库已验证）：
  - `FindWindow("Shell_TrayWnd")` + `GetWindowRect` 取任务栏矩形；
  - `SHAppBarMessage(ABM_GETSTATE)` 判断自动隐藏；
  - 窗口扩展样式 `WS_EX_TOOLWINDOW`（不进 Alt+Tab）+ `WS_EX_NOACTIVATE`（不抢焦点）+ `HWND_TOPMOST`；
  - `GetDpiForWindow` 做 PerMonitorV2 缩放；
  - 定时轮询任务栏矩形（1s），覆盖分辨率/任务栏位置变化与 explorer 重启。
- **已知代价**：它只是"覆盖"在任务栏上，不是任务栏的子元素。因此需要自己处理：
  - 与原生"小组件"按钮同位置 → 建议用户关掉原生小组件（设置 → 个性化 → 任务栏 → 小组件），
    或在后续版本提供左右偏移；
  - 任务栏自动隐藏 / 全屏应用时让位；
  - 多显示器时选择"主屏任务栏"或"每屏一份"。

### B. XAML 注入（v2 实验，不在 v1 采用）

- **TaskbarWidgets** 是目前把"第三方小组件放进 Windows 11 任务栏"做得最完整的开源项目：
  它由 .NET loader、Tauri 设置页、Explorer 侧 native hook 三部分组成，hook 会
  "discover the Windows 11 taskbar XAML tree, lay out enabled widgets, render snapshots"：
  [pfcdev/TaskbarWidgets](https://github.com/pfcdev/TaskbarWidgets)（MIT）。
- 作者自己写明了代价：[windows-private-api-risks.md](https://github.com/pfcdev/TaskbarWidgets/blob/main/docs/windows-private-api-risks.md)
  ——依赖未公开的 XAML Diagnostics 与任务栏视觉树细节，**Windows 的任何累积更新都可能改名/移除/重排**；
  README 也标注 beta、并声明"不支持当前任务栏布局时自动禁用，而不是强行布局"。
- 它的构建要求：.NET 8 SDK + Rust + VS 2022 Build Tools (MSVC/Windows SDK/CMake) + NSIS；
  本仓库当前机器上 Rust/MSVC 工具链不可用，且这类方案的维护成本与杀软风险不符合 v1 目标。
- **结论**：v2 可以单独开一个 `experiments/taskbar-injection` 分支，按 TaskbarWidgets 的
  "兼容性检查 + 失败即禁用 + 退出可清理"原则做 PoC；**不作为默认安装形态**。

### C. 官方 Widgets Board（只作为补充入口）

- 微软官方文档明确：Widgets 显示在 **Widgets Board**（点任务栏 Widgets 图标 / Win+W 打开的浮层），
  **不是常驻任务栏**；provider 需要打包 Win32 应用或 PWA：
  [Widgets overview · Microsoft Learn](https://learn.microsoft.com/windows/apps/design/widgets/)。
- 因此它满足不了"任务栏常驻"这个核心诉求，但适合作为"第二入口"：
  用户按 Win+W 时也能看到一张二游日程卡片。v1 不做。

### D. 重建任务栏（不做）

- **sambar** 用 C# / WPF 重写一条状态栏并隐藏原生任务栏（要求 Windows 11 build 26100+）：
  [TheAjaykrishnanR/sambar](https://github.com/TheAjaykrishnanR/sambar)（GPL-3.0）。
- 它证明了"全自绘任务栏"可行，但会失去系统任务栏的全部原生行为（窗口分组、系统托盘、
  多显示器、全屏自动隐藏、输入法/通知区域），维护成本远超收益。只作为交互设计参考。

## 3. 选定方案的实现约束（v0.1 已落地）

| 约束 | 做法 | 代码位置 |
|---|---|---|
| 定位到任务栏左侧 | `Shell_TrayWnd` 矩形 + 8px 边距，垂直居中 | `Services/Taskbar.cs`、`Services/DockPositioner.cs` |
| 不抢焦点 / 不进 Alt+Tab | `WS_EX_NOACTIVATE`、`WS_EX_TOOLWINDOW`、`ShowActivated=false` | `Taskbar.MakeToolWindow` |
| 始终可见 | `HWND_TOPMOST` + `Topmost=true` | `Taskbar.SetTopmostPosition` |
| 高 DPI | `ApplicationHighDpiMode=PerMonitorV2` + `GetDpiForWindow` | `app.manifest`、`DockPositioner` |
| 自动隐藏任务栏 | `ABM_GETSTATE & ABS_AUTOHIDE` + 检测矩形是否移出屏幕，隐藏常驻条 | `Taskbar.GetInfo` |
| 全屏应用让位 | 前台窗口矩形是否覆盖所在显示器（排除桌面/任务栏类） | `Taskbar.IsForegroundFullscreen` |
| explorer 重启 | 每秒重查 `Shell_TrayWnd`；找不到时退回主屏底部假任务栏 | `Taskbar.GetInfo` |
| 多显示器 | 任务栏所在显示器的工作区用于夹取面板位置；v0.1.8 再扩"每屏一份" | `DockPositioner` |
| 面板不挡条 | 面板底边贴在常驻条上方 8px，并夹在当前显示器工作区内 | `DockPositioner.PositionFlyout` |

## 4. 已知限制（写清楚，避免被当 bug）

1. **不是任务栏子元素**：任务栏"显示桌面"按钮悬停、任务栏右键菜单等原生交互不会感知我们；
   常驻条所在区域的原生元素（如小组件按钮）会被遮挡。
2. **Windows 11 任务栏只支持底部**（官方 UI）；如果用户用第三方工具把任务栏移到顶部/侧边，
   v0.1 仍按"底部"处理，可能错位 → v0.1.8 处理。
3. **任务栏自动隐藏**时，常驻条会跟着隐藏（而不是浮在屏幕边缘）。
4. **全屏判定的边界**：无边框全屏且前台窗口非显示器尺寸的极端情况可能漏判。
5. **首次运行无数据**：v0.1 需要先跑 `tools/refresh.mjs`；内置示例快照只用于展示。
6. **不属于"官方组件"**：Windows 更新不会保证布局不变（虽然我们只用公开窗口，风险远低于注入方案）。

## 5. 参考项目清单

| 项目 | 许可 | 借鉴点 |
|---|---|---|
| [pfcdev/TaskbarWidgets](https://github.com/pfcdev/TaskbarWidgets) | MIT | 任务栏注入的完整工程结构、失败即禁用的安全边界 |
| [marticliment/ElevenClock](https://github.com/marticliment/ElevenClock) | Apache-2.0 | 贴任务栏的置顶窗、时钟类小组件的产品形态 |
| [Leonxlnx/todobar](https://github.com/Leonxlnx/todobar) | Apache-2.0 | Tauri 无边框停靠窗、托盘/自启/单实例、点穿透明区 |
| [zhongyang219/TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor) | 以仓库 LICENSE 为准 | Windows 任务栏显示监控信息的长期实践 |
| [TheAjaykrishnanR/sambar](https://github.com/TheAjaykrishnanR/sambar) | GPL-3.0 | 全自绘状态栏的交互与布局参考（**不复制代码**） |
| [Microsoft Widgets 文档](https://learn.microsoft.com/windows/apps/design/widgets/) | — | 官方小部件能力边界：只在 Widgets Board |

> 说明：本项目只借鉴思路与公开 API 用法，不复制上述项目的源代码；引用许可证仅用于合规提示。

## 6. 结论

- **v1 用方案 A + E**：公开 API、可本地构建验证、随 Windows 更新风险最低。
- **方案 B** 有真实价值（真正"在任务栏里"），但成本/风险/工具链都不适合作为第一个版本；
  等 v1 稳定后单独评估。
- **方案 C** 只能作为第二入口，不能替代任务栏常驻。
- **方案 D** 不做。
