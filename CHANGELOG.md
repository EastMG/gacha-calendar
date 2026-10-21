# CHANGELOG

## v0.1.12 · 2026-10-21

- 实验性 newPanel 基础版：设置页新增「实验性新版 UI（日程视图）」开关（`newPanel` 配置）
- 新增 `ScheduleView` / `SchedulePanelVm`：日期胶囊、24小时/7天总览、近期紧迫列表、
  悬停详情卡（大图标/剩余天数/日期/其他近期项）、14 天时间轴日程表与底部更新/刷新
- 快照 `config.newPanel` 透传到 WPF；设置切换后 `rebuildSnapshot` 立即切换视图
- 配置 schema / 示例配置 / 测试补 `newPanel`；版本 0.1.12

## v0.1.11 · 2026-10-21

- 主面板改用 DSH 风格 8px 细滚动条，深色主题不再出现系统浅色滚动条；
  保留滚轮滚动（实测滚动比例 0 → 34%）
- 版本 0.1.11

## v0.1.10 · 2026-10-21

- 视觉 token 向 DSH 面板对齐：白底/`#1F2329` 主文字/`#6B7280` 次文字/`#8A9099` 弱文字/
  `#E5E6EB` 分隔线/`#3B5BFD` 业务蓝；深色同步调整
- 新增 `GACHA_CALENDAR_THEME=light|dark` 覆盖，便于截图/视觉回归
- 新增浅色、深色面板截图 `docs/images/flyout.png`、`flyout-dark.png`
- 新增 [11-DSH面板复刻验收](docs/11-DSH面板复刻验收.md)：逐项列实现与证据，并明确
  实验性 `newPanel`、取色器、滚动条像素回归未纳入
- 版本 0.1.10

## v0.1.9 · 2026-10-21

- 主面板交互贴近 DSH：单元格悬停跑马灯（仅文字超宽时滚动）、刷新按钮旋转、单元格裁剪/marquee 状态恢复
- 修复 Node Host 并发写 stdin 崩溃（图标 fetchBinary 与 refreshIfDue 同时写入触发
  “stream in use”）；所有 Host 请求写串行化
- 图标加载完全改走 Host `fetchBinary`，移除 WPF 层 HttpClient，遵守“UI 层不发网络请求”硬边界；
  Host 不可用时图标降级首字方块
- Service/Host 补 `rebuildSnapshot` 测试；版本 0.1.9

## v0.1.8 · 2026-10-21

- 设置页按 DSH 插件设置页重做：自动刷新/频率、条目管理表（展示、卡池来源、活动来源、↑↓、删除）、
  恢复排序/恢复条目、解析器自检、自定义条目表单
- 设置写回 Host：`listEntries` / `setEntryVisibility` / `setOrder` / `removeEntry` /
  `updateConfig` / `selfCheck`；改显隐/排序后 Host 无网重建快照，面板立即更新
- Host 新增 `rebuildSnapshot`；Service 同步实现
- README 增加设置页截图；版本 0.1.8

## v0.1.7 · 2026-10-21

- 应用内刷新：WPF 启动 `packages/engine/host.mjs`（Node JSON-lines Host），复用 `gacha-calendar-core`，
  不再依赖手动跑 `npm run refresh`；启动/打开面板时 `refreshIfDue`，每分钟自动检查，手动刷新按钮强制刷新
- 面板按 DSH 插件风格重做：六列表格（图标/游戏/当前卡池/卡池起止/当前活动/活动起止）、
  标题行、数据状态/刷新时间/抓取提示、刷新按钮与设置入口；窗口 690×660
- 游戏图标：快照新增 `icon` 字段；WPF 加载外链图标，失败退化为首字方块；受限环境可经 Host `fetchBinary` 取图
- Host 新增 `fetchBinary`（仅 http/https、≤4MB）
- 示例快照重新生成（含图标 URL）；README 截图更新
- 测试 34 项保持全绿；版本 0.1.7

## v0.1.6 · 2026-10-21

- 配置版本化：`config.schemaVersion`；缺省按 v1；未来版本拒绝加载（`unsupported-config-version`）
- 新增 `importLegacyConfig()` / Service `importConfig` / Host `importConfig` / CLI `config import --file`，兼容旧扩展/DSH 的 JSON 字符串字段
- 示例快照纳入测试（`sample-snapshot.test.mjs` 做契约校验）
- 新增 view-model 跨语言 golden fixture（`packages/viewmodel/test/fixtures/basic.json`）
- 测试 30 → 34 项

## v0.1.5 · 2026-10-21

- 新增 `packages/viewmodel/`：壳无关的展示层纯函数（行文案/倒计时/失败三态/角色名精简/顶部状态/常驻条选择）
- 新增 `docs/08-视图模型契约.md` 与 `packages/viewmodel/cli.mjs`（无 UI 渲染 view-model）
- 6 项 view-model 测试；测试总数 24 → 30
- 与 WPF 的 C# 实现口径对齐（外壳选型前两边必须同步）

## v0.1.4 · 2026-10-21

- 新增 `entries.mjs`：设置页条目视图（合并 `listGames` / `defaultHidden` / `hidden` / `shown` → `visible`）
- Service 新增 `listEntries` / `setEntryVisibility`（自动维护 `shown`）/ `setOrder` / `removeEntry` / `restoreEntry`
- Host 与 CLI 同步暴露这些设置基础操作；CLI 新增 `entries` / `show` / `hide` / `order` / `remove` / `restore`
- 测试 21 → 24 项

## v0.1.3 · 2026-10-21

- 新增 `validate.mjs`：snapshot 契约校验（字段类型、失败三态、重复 id、时间戳、可见条目缺记录 warning）
- 新增 `diagnostics.mjs` 与 `service.getDiagnostics()`：配置 / 快照 / 文件 / 调度状态只读诊断
- Host 新增 `getDiagnostics` / `validateConfig` / `validateSnapshot`
- 新增壳无关 CLI：`status` / `config get|set` / `refresh [--if-due]` / `validate` / `diagnostics` / `info`
- 测试 18 → 21 项；`npm run check` 覆盖新增模块

## v0.1.2 · 2026-10-21

- 新增刷新调度：`service.refreshIfDue()` 按配置与上次成功时间判断是否该刷，外壳不再自己算 due
- 新增失败指数退避（5m / 15m / 45m … 上限 6h），成功后清零；状态持久化在 `cache.json` 的 `refreshState`
- 新增并发保护：同一时刻只允许一轮刷新；手动 `refresh()` 绕过退避
- Host 新增 `refreshIfDue` / `getStatus`；调度状态含 `lastSuccessAt` / `nextDueAt` / `nextAllowedAt` / `failures` / `lastError`
- 测试从 15 项增加到 18 项（新增调度用例）

## v0.1.1 · 2026-10-21

外壳技术栈未定，先落地不依赖外壳的基础功能。

- 新增 `packages/engine/`：壳无关的配置层（显隐三态 / 排序 / 删除 / 自定义条目与来源 / 刷新间隔）、
  快照构建、文件 service、JSON-lines Host 协议。
- 新增 `data/config.example.json` 与 `docs/07-壳无关基础层.md`；`tools/refresh.mjs` 改走同一套
  config / snapshot / transport 层，快照新增可选的 `config` 字段（旧外壳可忽略）。
- 新增 15 项单进程测试（config / snapshot / service / host），CI 增加 `engine` job。
- 配置归一化包含安全裁剪与原子写；`customUrls` 等对象字段只在 `toEngineSettings()` 转 JSON 字符串，
  避免各层重复写转换逻辑。

## v0.1.0 · 2026-10-21

首个可运行骨架（预览版）。

- 新增 Windows 11 任务栏左侧常驻条：读取 `Shell_TrayWnd` 几何，`WS_EX_NOACTIVATE` + `TOPMOST`，
  支持高 DPI、自动隐藏让位、全屏应用隐藏。
- 新增展开面板：28 款游戏（默认显示 23 款）的卡池 / 卡池起止 / 活动 / 活动起止，
  倒计时、失败三态（报错 / 未公布 / 沿用旧数据）、悬停明细。
- 新增系统托盘、设置窗、单实例、开机自启、深/浅色跟随。
- 新增 `tools/refresh.mjs`：调用 `gacha-calendar-core@0.12.5` 真抓 28 款，产出 `data/snapshot.json`；
  提交真实抓取的 `data/sample-snapshot.json` 作为首次运行兜底。
- 新增 `tools/capture-shot.ps1`：`PrintWindow` 截图（DPI 已处理），生成 README 图。
- 新增文档：产品定义、任务栏集成调研、架构、路线图、风险与合规、数据契约。
- 新增 GitHub Actions：Windows 构建 + 抓取工具语法检查。
- 已知限制：v0.1 的应用内刷新尚未接入（需运行 `npm run refresh`）；任务栏仅按"底部"处理；
  多显示器/每屏一份、图标、轮播、安装包在路线图中。
