# DSH 面板复刻验收清单（v0.1.x）

> 对照对象：`dsh-gacha-calendar` 插件的**默认面板**（六列表格）与**插件设置页**。
> 插件的实验性 `newPanel`（日期胶囊 + 时间轴 + 悬停卡片）不属于本次默认面板范围，
> 记录在文末「未纳入」中，避免把“默认面板完成”误报成“实验视图也完成”。

## 1. 主面板

| DSH 行为 / 视觉 | WPF 实现 | 证据 |
|---|---|---|
| 690px 宽、圆角卡片、边框、阴影、10/12 内边距 | `FlyoutWindow` 690×660；`PanelBg` / `PanelBorder` | `docs/images/flyout.png` |
| 标题 13px semibold | 标题行 `二游日历` + 刷新/设置按钮 | 截图 |
| 数据状态 / 刷新时间 / 抓取提示一行 | `DataStatusText` / `UpdatedText` / `ScrapeText` + `StatusTip` | 截图 |
| 六列表格：图标、游戏、当前卡池、卡池起止、当前活动、活动起止 | 相同 `Grid` 列宽（22/96/\*/150/1.3\*/150），居中 | 截图 |
| 外链图标 + 首字方块降级 | 快照 `icon` → Host `fetchBinary` → `IconLoader`；失败显示首字 | 截图；`IconLoader.cs` |
| 单元格悬停跑马灯（仅超宽滚动） | `Controls/Marquee.cs` + 每列 `ClipToBounds` | hover 实测命中截断单元格；无 UI 异常 |
| 刷新按钮旋转 / 禁用态 | `IsRefreshing` → `RotateTransform` 动画 | 代码 + v0.1.9 |
| 悬停 tooltip（卡池全名/时间/失败说明） | `GachaTip` / `GachaDateTip` / `EventTip` / `EventDateTip`；`RowBuilder.WithFailNote` | 代码 |
| 失败三态：down / nomatch / stale | 头部抓取提示 + 单元格 tooltip；`RowBuilder` 口径 | `docs/08`；引擎测试 |
| 点击面板外 / Esc 关闭 | `Deactivated` + `PreviewKeyDown` | 代码 |
| 滚动 / 细滚动条 | 自定义 `DshScrollViewer` + 8px 滑块；滚轮实测 0→34% | 深色截图；v0.1.11 |
| 深浅色 | `ThemeManager` token；`GACHA_CALENDAR_THEME=light/dark` 可强制 | `docs/images/flyout.png`、`flyout-dark.png` |

## 2. 实验性 newPanel（基础版 v0.1.12）

| DSH 行为 | WPF 实现 | 证据 |
|---|---|---|
| 设置页实验开关（默认关） | `NewPanelCheck` → `updateConfig({newPanel})` + 快照透传 | 代码；开关切换后 `rebuildSnapshot` |
| 日期胶囊 + 24小时/7天总览 | `SchedulePanelVm.GlanceText` / `DatePill` | 代码 |
| 近期紧迫列表（前 10 + 展开分页） | `Urgent` + `ToggleExpand`；>10 条每 4 秒循环、悬停暂停 | `flyout-newpanel-100.png` |
| 悬停详情卡 | `Focus` + `FocusOthers`（显示池/活动名、大图标、剩余天数、日期、其他项） | 同上 |
| 14 天时间轴日程表 | 158px 游戏列 + 卡池/活动两栏 + 14 格条形；活动条混浅色 | 同上 |
| 刷新失败横条 + 重试 | `FailureText` / `FailureTip` + 重试按钮 | 代码 |
| 底栏状态/上次更新/刷新 | `ScrapeText` / `UpdatedAgo` / 刷新按钮 | 同上 |
| 同格多档期探头展开 | 解析 hover 原文，悬停单元格弹出「同格档期」卡片；移到另一格切换、点击外部关闭 | v0.1.24 |

## 2.1 任务栏轮播（v0.1.26）

| 行为 | WPF 实现 | 证据 |
|---|---|---|
| 单行胶囊、固定宽度、贴合任务栏高度 | `DockStripWindow` 272px 宽，`DockPositioner` 按任务栏高度定位 | `dock.png` / `dock-150.png` |
| 游戏图标 + 类型 + 事项名 + 剩余时间 | `ChipVm`（ItemName/Kind/Status/Icon）→ `DockLineVm` | 浅/深截图 |
| 每 5 秒竖向翻页、320ms 缓动 | `DispatcherTimer` + 双 `ContentControl` 上下滑入/滑出 | 代码；悬停实测暂停 |
| 条目名省略、类型/剩余时间不截断 | Grid Auto/Auto/\*/Auto + `TextTrimming` | `dock.png` |
| 无条目时显示「二游日历」 | `DockLineVm.Idle` | 代码 |
| 点击展开 / 右键菜单不变 | `ToggleFlyout` + ContextMenu | 代码 |

## 2.2 Fluent 视觉改版（v0.1.27–0.1.28）

| 行为 | WPF 实现 | 证据 |
|---|---|---|
| Windows 11 Fluent 2 色板 | 系统蓝 `#0067C0`（浅）/`#60CDFF`（深）、中性层叠表面、8px 圆角 | `docs/images/flyout*.png`、`settings*.png` |
| 字体栈 | Segoe UI Variable Text + 微软雅黑回退；Ideal 渲染 | 截图 |
| 面板标题/表格 | 20px 标题、720×700、表头左对齐、行距加大 | `flyout*.png` |
| 减少彩色噪音 | 倒计时/紧迫时间中性色、条形降饱和、设置去掉强调外框 | 截图 |
| 面板切换按钮 | **v0.1.28 已撤下**（按用户反馈）；日程视图仍由设置开关控制 | 不放入口 |
| 任务栏胶囊设置 | 设置页「任务栏胶囊」区：显示、自动轮播、12/24/48/72h | `settings*.png` |
| 胶囊范围语义 | 开始/结束在 N 小时内才进入轮播；状态显示「还剩 X」/「X后开池」 | `dock*.png`；`MainViewModel.RebuildChips` |

## 3. 设置页

| DSH 行为 | WPF 实现 | 证据 |
|---|---|---|
| 自动刷新开关 | `AutoRefreshCheck` → `updateConfig({autoRefresh})` | UIAutomation：`autoRefresh:false` 落盘 |
| 刷新频率 1/5/7/15/21/30/42 天 | `RefreshIntervalCombo` → `updateConfig({refreshMinutes})` | 截图 |
| 条目表：展示 / 卡池来源 / 活动来源 / ↑↓ / 删除 | 动态行 + Host 写回 | 截图；UIAutomation `hidden:["genshin"]` |
| 恢复默认顺序 / 恢复条目 | `setOrder([])` / `updateConfig({removed:[],customEntries:[]})` | 代码 |
| 卡池/活动来源切换 + 自定义 URL | 默认/备选/自定义 ComboBox + 自定义输入行 | 截图 |
| 颜色列 / 取色器 | 系统 ColorDialog → `gameColors`；newPanel 即时生效 | v0.1.13 |
| 自定义条目 | 名称/图标/卡池 URL/活动 URL 表单 → `customEntries` | 代码 |
| 解析器自检 | `selfCheck` + summary/problems 展示 | UIAutomation 实测 10s 完成 |
| 写失败可见 | `SaveErrorText` | 代码 |

## 4. 数据链路

| 链路 | 实现 |
|---|---|
| 应用内刷新 | WPF 启动 `packages/engine/host.mjs`；`refreshIfDue` / `refresh` |
| 自动刷新 | 每分钟检查 + Host 退避；启动/打开面板触发 |
| 设置立即生效 | Host `rebuildSnapshot`（缓存 + 配置，不联网）+ 面板重载 |
| 面板 ↔ 常驻条联动 | 共用一个 `MainViewModel`；点击常驻条 `ToggleFlyout`；刷新后两边同步 |
| UI 层网络边界 | 图标也只走 Host `fetchBinary`；WPF 无 HttpClient |
| Host 并发 | 请求写 stdin 串行化（修复 `stream in use`） |

## 5. DPI / 深色标题栏

- WPF 以 DIP 布局，`--capture-ui <dir>` 用 RenderTargetBitmap 同时输出 96 DPI（100%）与 144 DPI（150%）：
  `docs/images/flyout-100.png` / `flyout-150.png`，newPanel 同名 `flyout-newpanel-*`；
  深色 newPanel 另有 `flyout-newpanel-dark-100.png` / `-150.png`；
- 设置窗口深色主题调用 DWM `DWMWA_USE_IMMERSIVE_DARK_MODE`，避免系统白色标题栏；
- 自包含运行时发布尚未验证（框架依赖 zip 已验证可启动并使用包内 Node）。

## 6. 验收命令

```powershell
dotnet build src\GachaCalendar\GachaCalendar.csproj -c Release   # 0 警告 0 错误
npm test                                                         # 34/34
npm run check
```

- CI：`build-windows` / `identity` / `engine` / `tools` 全绿。
- `docs/images/flyout.png`、`flyout-dark.png`、`settings.png`（100% 渲染）/`settings-150.png`；
- 深色设置页：`settings-dark-100.png` / `settings-dark-150.png`；
- 深色标题栏真窗口：`settings-dark-titlebar.png`（PrintWindow）；
- 2×2 视觉回归：`visual-regression.png`（newPanel/设置 × 浅/深）。

## 7. 未纳入 / 后续

- newPanel：同格档期探头已做（悬停卡片）；滚动跟随、探头停留细节可继续打磨；
- 自包含（self-contained）运行时发布；当前 zip 需目标机装 .NET 10 Desktop Runtime；
- 深色标题栏/100% 截图已做，但未覆盖所有 Windows 版本与第三方缩放工具。
