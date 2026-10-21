# BACKLOG

> 未排期条目，不承诺版本号；版本号仍按 0.1.x 递增。
> 已完成的历史见 [CHANGELOG.md](CHANGELOG.md)，阶段性计划见 [docs/04-路线图.md](docs/04-路线图.md)。

## 外观：Fluent 风格（WPF）

> 2026-10-21 用户指定加入待办。跟踪 issue：
> [#1 外观：Fluent 风格（WPF）](https://github.com/EastMG/gacha-calendar/issues/1)。
> 目标：在不牺牲"任务栏常驻、轻、快"的前提下，
> 让面板/设置页/常驻条呈现 Windows 11 Fluent 观感；不做无意义的重动画。

- [ ] **先定实现方式**：自写 XAML 样式 vs 引入 WPF Fluent 库（如 WPF-UI / ModernWpf）；
      评估依赖体积、许可证、维护活跃度、是否支持 .NET 10 / 自定义主题
- [ ] **设计 token**：颜色/中性色阶、字体（Segoe UI Variable）、字号、间距、圆角、阴影/elevation
- [ ] **窗口材质**：DWM `Mica` / `Acrylic` / 圆角（`DwmSetWindowAttribute`），面板与常驻条分别定策略
- [ ] **常驻条**：半透明/模糊背景在浅色壁纸、深色壁纸、任务栏上的可读性；不做过度玻璃导致文字发灰
- [ ] **控件样式**：Button、ComboBox、ScrollBar、CheckBox/ToggleSwitch、ToolTip、ContextMenu 的
      Fluent 悬停/按下/焦点态；键盘焦点可见
- [ ] **深浅色实时跟随**：系统主题切换时重载画刷（不再只是启动时读一次）
- [ ] **动效**：面板展开/收起、悬停、倒计时变化的轻量过渡；尊重"减少动态效果"设置
- [ ] **设置页重构**：分组卡片、说明文字层级、控件对齐；与主面板同一套 token
- [ ] **无障碍**：高对比度、200% 缩放、屏幕阅读器名称、Tab 顺序
- [ ] **视觉回归**：100% / 150% DPI × 浅色/深色 的截图基线，放进 `docs/images/`
- [ ] **验收**：不增加常驻内存明显开销（目标 +20MB 以内）、启动不明显变慢、
      与现有任务栏/面板交互不冲突

## 外壳接入（WPF 已定）

- [ ] WPF 启动/管理 `packages/engine/host.mjs` 子进程（或 C# 同协议实现）
- [ ] 面板接入 `refreshIfDue` / `getStatus` / `updateConfig`
- [ ] 设置页接入 `entries` / 可见性 / 排序 / 删除 / 恢复
- [ ] 刷新状态、失败退避、数据来源在 UI 上可见
- [ ] 配置导入（旧扩展 / DSH 配置）

## 工程 / 发布

- [ ] 安装包（Inno Setup 或 MSIX）+ 卸载清理自启项
- [ ] 代码签名（SmartScreen）
- [ ] 更新检测
- [ ] 多显示器 / 任务栏在顶部或侧边
- [ ] 图标加载与本地缓存
