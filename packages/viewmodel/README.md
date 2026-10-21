# packages/viewmodel

壳无关的展示层参考实现：`snapshot + now` → 视图模型 JSON。输出契约见
[docs/08-视图模型契约.md](../../docs/08-视图模型契约.md)。

| 文件 | 职责 |
|---|---|
| `view-model.mjs` | 行文案/倒计时/失败三态/角色名精简/顶部状态/常驻条选择（纯函数） |
| `cli.mjs` | 把 snapshot 渲染成 view-model JSON，便于无 UI 调试 |

```bash
node packages/viewmodel/cli.mjs --snapshot data/sample-snapshot.json --dock-items 3
```

> 当前 WPF 的 C# 实现（`src/GachaCalendar/ViewModels/`）与此模块口径一致；
> 外壳未定期间两边必须同步修改，测试以本模块为准。
