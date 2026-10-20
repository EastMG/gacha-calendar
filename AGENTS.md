# AGENTS.md — 本仓库的开发约定

> 面向接手本仓库的 AI 会话/贡献者。**先读这个，再动代码。**
> 这些约定与姊妹仓库 `dsh-gacha-calendar` / `gacha-calendar-extension` 保持一致口径。

## 0. 这个仓库是什么

Windows 11 任务栏常驻的**二游卡池 / 活动日历小组件**。它**不重复实现抓取**：
数据来自独立核心包 [`gacha-calendar-core`](https://www.npmjs.com/package/gacha-calendar-core)
（源码在 [`dsh-gacha-calendar`](https://github.com/EastMG/dsh-gacha-calendar) 仓库的 `packages/core/`）。
本仓库负责：Windows 原生外壳、任务栏停靠、界面、设置、打包。

## 1. 目录职责（唯一真源）

| 目录 | 职责 | 允许手改 |
|---|---|---|
| `src/GachaCalendar/` | WPF 应用（C#） | ✅ 唯一真源 |
| `tools/refresh.mjs` | 开发期抓取工具：调用 core，把 Result JSON 落成 `data/snapshot.json` | ✅ |
| `data/sample-snapshot.json` | **示例快照**（真实抓取产物，供首次运行/无网络时展示） | ⚠️ 由 `npm run sample` 生成，别手改 |
| `docs/` | 调研 / 架构 / 路线图 / 数据契约 | ✅ |
| `data/snapshot.json` | 本地运行期快照（`.gitignore`，不入库） | ❌ |

## 2. 构建与验证门禁（每次改动都要绿）

```powershell
# 1) 构建 WPF 应用
dotnet build src/GachaCalendar/GachaCalendar.csproj -c Release

# 2) 抓取工具语法自检
node --check tools/refresh.mjs

# 3)（可选，需要联网）真抓一轮，验证数据链路
cd tools; npm run refresh
```

CI 跑 1 + 2；联网抓取不进 CI（受源站可用性影响）。

## 3. 版本号与发布纪律

- **代码改动 = 升 `Version`**（`GachaCalendar.csproj` 的 `<Version>`），并在 `CHANGELOG.md` 记一行。
- **纯文档改动不升版本号。**
- `git tag` / GitHub Release 只有在用户**当轮明确点名**时才做；"改完了""继续"不算授权。
- 提交信息格式：`<type>: vX.Y.Z <一句中文>`（type ∈ feat/fix/refactor/docs/chore/build/style），
  body 用 `- ` 分条，最后一条写验证结果。

## 4. 硬边界（别踩）

- **不把抓取逻辑复制进本仓库**。解析器/来源表只在 core 里改；本仓库升级依赖版本即可。
- **不在 UI 层发网络请求**（v0.1 由 `tools/refresh.mjs` 生成快照；v0.2 起由内置 CoreHost 统一负责）。
  原因见 `docs/03-架构设计.md`：UI 层受 CORS / Referer 限制，会重蹈浏览器扩展的覆辙。
- **任务栏集成不要一上来就注入 explorer**。默认走"置顶停靠窗"方案，原因与备选见
  `docs/02-Windows11任务栏集成调研.md`。
- **不收集任何用户数据**、不加遥测。隐私口径见 `docs/05-风险与合规.md`。
- 数据来自游戏官方公告 / 官方 Wiki / 第三方站，图标与排期版权归原权利方；只做信息聚合展示。

## 5. 数据契约

`docs/06-数据契约.md` 是 core 的 Result JSON 与本仓库 `snapshot.json` 的冻结说明。
**改快照结构必须同时改文档并升 `snapshot schemaVersion`。**
