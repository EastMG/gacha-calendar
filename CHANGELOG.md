# CHANGELOG

## v0.1.34 · 2026-10-27

- 启动日志增加 exe、dataDir；托盘创建日志；设置页错误显示 Engine.LastError
- 新增 `run-debug.cmd`：固定数据目录和构建目录，便于定位实际运行实例
- 版本 0.1.34

## v0.1.33 · 2026-10-27

- Node Host 启动路径增加用户级/系统级 Node 常见安装位置探测；
  从资源管理器双击时不再完全依赖 PATH
- 版本 0.1.33

## v0.1.32 · 2026-10-27

- 托盘图标增加启动后重注册（避免 Explorer 通知区未就绪时 Visible 被吞）
- 设置页新增「退出应用」按钮：托盘图标被 Windows 收进隐藏区时也能退出
- 版本 0.1.32

## v0.1.31 · 2026-10-27

- 设置页在 Node Host 尚未就绪时不再直接显示“引擎不可用”并留空：
  先显示“正在启动数据引擎…”，每秒重试，就绪后自动读取设置
- 版本 0.1.31

## v0.1.29 · 2026-10-27

- 按 Windows 官方 type ramp 修正字号：正文 14/行高 20、caption 12；
  面板 760×720，表格行距加大到 40px
- 保留六列总览结构，不新增按钮/入口
- 日程视图同步正文/说明字号；设置页随全局 14px 正文调整
- 版本 0.1.29

## v0.1.28 · 2026-10-27

- **撤下**面板顶部「总览 / 日程」切换按钮（按反馈，不再额外塞入口）；
  日程视图继续由设置页「日程视图（预览）」开关控制
- Fluent 收敛：面板 720×700、标题 20、表头左对齐、行距/字号加大
- 主面板倒计时从全蓝改为中性主文字；彩色噪音大幅减少
- 日程视图：紧迫时间改中性色、条形降饱和、焦点卡改用 surface 底色
- 胶囊：类型标签中性色，剩余时间统一 accent 蓝
- 设置页：去掉强调色外框，删除按钮改中性色
- 更新浅/深 100%/150% 截图与视觉回归；版本 0.1.28

## v0.1.27 · 2026-10-27

- 视觉从 DSH/Apple 观感切到 **Windows 11 Fluent 2**：系统蓝 `#0067C0`/`#60CDFF`、
  中性层叠表面、8px 卡片圆角、Subtle hover、Segoe UI Variable 字体栈
- 面板新增**总览 / 日程**分段切换（始终可见，切换写回 `newPanel`）
- 设置页新增**任务栏胶囊**区：显示开关、自动轮播开关、12/24/48/72 小时轮播范围
- 胶囊轮播改为「开始/结束在 N 小时内」筛选；状态会显示「还剩 X」或「X后开池/开始」
- 主面板/设置页/胶囊/日程视图同步 Fluent 间距、控件高度和描边
- 版本 0.1.27

## v0.1.26 · 2026-10-27

- 任务栏常驻条重构为**单行按钮轮播**（参考 dsh 版侧栏按钮轮播）：
  - 固定 272px 胶囊，贴合任务栏高度；游戏图标 + 卡池/活动类型 + 事项名 + 剩余时间
  - 每 5 秒竖向翻页到下一项，320ms 缓动；翻页时旧行滑出、新行进入
  - 鼠标悬停暂停；右键菜单/点击展开面板保持不变
  - 没有可展示条目时回到「二游日历」 idle 行
  - 条目名按可用宽度省略，剩余时间与类型永不截断
- 新增 `DockLineVm`，`ChipVm` 补充事项名/类型/状态/图标与异步图标加载
- 更新浅/深 100%/150% 任务栏截图（`dock.png` / `dock-150.png` / `dock-dark*.png`）
- 截图模式冻结在第一格，避免截到翻页中间帧
- 版本 0.1.26

## v0.1.25 · 2026-10-21

- 字体：`TextFormattingMode` 改为 Ideal，配合 Segoe UI Variable/雅黑回退，低 DPI 更柔和
- 开关：设置页「自动刷新 / 实验性新版 UI」改为胶囊 Switch
- 图标：删除改为垃圾桶、上/下移改为 chevron、newPanel 刷新改为图标+文字
- 迷你条当天提示：刻度当天数字加主题色胶囊，条形当天列加主题色描边
- 修复严重 bug：面板入场动画设置 Window.RenderTransform 导致
  `InvalidOperationException: 转换对于 Window 无效`，面板打不开/截图为空白；改为只做透明度淡入
- 更新浅/深 newPanel、设置、视觉回归截图；版本 0.1.25

## v0.1.24 · 2026-10-21

- newPanel 探头：不再鼠标一移出单元格就关闭；改为「移到另一格才切换 / 点击外部关闭」，
  关闭时复位 IsProbeOpen
- 设置页：颜色色块缩小到 20×18、行 hover 背景、颜色列收紧
- 更新设置页浅/深截图与 2×2 视觉回归图
- 版本 0.1.24

## v0.1.23 · 2026-10-21

- 主面板/newPanel 倒计时与日期列启用 tabular numerals，数字不再跳动
- 面板打开增加 180ms 淡入 + 上移动效（DSH 面板 shell-in 的轻量版）
- 版本 0.1.23

## v0.1.22 · 2026-10-21

- 主面板内容改为显式 10/10/10/8 内边距，避免自定义 ScrollViewer 模板吃掉 Padding
- `--capture-ui` 新增 PrintWindow 真窗口截图，验证深色系统标题栏
- 新增 `settings-dark-titlebar.png`；更新主面板/设置深色截图
- 版本 0.1.22

## v0.1.21 · 2026-10-21

- 设置页：17px 自定义 CheckBox、细滚动条；载入中显示「正在读取设置…」
- 截图链路改为 12s 等待，避免 Host 启动/刷新期间截到空设置页
- 新增深色设置页截图与 2×2 视觉回归图 `docs/images/visual-regression.png`
- 版本 0.1.21

## v0.1.20 · 2026-10-21

- newPanel 同格多档期：解析 `bannerHover` / `eventHover`，悬停表格单元格弹出「同格档期」卡片
  （标题 / 剩余时间 / 源站起止；最多去重显示）
- 修复同格多档期完全不可见的问题；版本 0.1.20

## v0.1.19 · 2026-10-21

- 设置页下拉框改为自绘圆角控件：白底、细边框、自绘箭头、圆角下拉面板；
  选中项显示 Label，下拉项自定义 hover/选中态
- `SourceOption` / `RefreshOption` 覆写 `ToString()`，UIAutomation 名称可读
- 更新设置页截图；版本 0.1.19

## v0.1.18 · 2026-10-21

- 字体渲染精修：主面板/newPanel/设置/常驻条统一 Segoe UI Variable + 雅黑回退，
  开启 Display 文本渲染、布局取整与设备像素对齐
- newPanel 间距/层级重排：近期行更高、焦点卡白底+细环、表格行距/条形加粗、底栏分隔线
- newPanel 使用独立细滚动条，深色下不再出现系统浅色滚动条
- 截图工具修复：capture 模式不自动隐藏窗口；隐藏窗口先 Show 再渲染，100/150 截图不再空白
- 更新浅/深 newPanel、主面板、设置页截图；版本 0.1.18

## v0.1.17 · 2026-10-21

- newPanel 焦点卡图标外圈加主题色细环，层级更清晰
- 设置页下拉项增加内边距（ComboBoxItem），选项更易读
- 版本 0.1.17

## v0.1.16 · 2026-10-21

- 视觉打磨：newPanel 焦点卡改为显示池/活动名，图标缩小、详情卡不再裁切第三项；
  活动条改用 42% 混浅色，近期倒计时按紧迫度着色，空/同游戏项去重
- 设置页控件统一：自定义圆角 CheckBox、ComboBox/TextBox 尺寸与配色、颜色列
- 修复：newPanel 切回表格视图后循环定时器仍运行；取色器改为以设置窗口为 owner；
  设置窗口按工作区限制 780×780，避免小屏溢出
- 更新设置页/newPanel 100%/150% 截图；版本 0.1.16

## v0.1.15 · 2026-10-21

- newPanel：近期列表超过 10 条时每 4 秒循环翻页（悬停暂停），按钮显示 `当前页 / 总页数`；
  展开全部后停住
- newPanel：刷新失败横条（来源级失败/沿用旧数据）+ 重试
- 新增 newPanel 浅/深色 100%/150% 截图
- 版本 0.1.15

## v0.1.14 · 2026-10-21

- 深色主题下设置窗口使用 DWM 深色标题栏
- 分发雏形：新增 `tools/publish.ps1`，发布 WPF 应用并把
  `engine/host.mjs`、`gacha-calendar-core`、`node.exe`、示例数据一起打包成 zip
- `EngineHostClient` 优先使用包内 `engine/node.exe`；发布版实测用内置 Node 完成刷新
- `--capture-ui <dir>`：同时渲染 96 DPI / 144 DPI（100%/150%）面板截图
- 版本 0.1.14

## v0.1.13 · 2026-10-21

- 设置页新增「颜色」列与主题色取色器：写入 `gameColors`，`newPanel` 行色/详情卡即时生效
- 颜色选择使用系统 ColorDialog；写回后 `rebuildSnapshot` + 面板重载
- 版本 0.1.13

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
