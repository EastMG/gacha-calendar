# CHANGELOG

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
