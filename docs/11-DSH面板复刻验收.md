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
| 同格多档期探头展开 | 解析 hover 原文，悬停单元格弹出「同格档期」卡片 | v0.1.20 |

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
