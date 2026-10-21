# packages/engine

壳无关基础层的 Node 参考实现，详见 [docs/07-壳无关基础层.md](../../docs/07-壳无关基础层.md)。

| 文件 | 职责 |
|---|---|
| `config.mjs` | `config.json` schema、归一化、可见性三态、排序、core storage 转换 |
| `snapshot.mjs` | core 结果 + config → `snapshot.json` |
| `service.mjs` | core + 文件存储（config/cache/snapshot） |
| `host.mjs` | 逐行 JSON 的 Host 协议（stdin/stdout），供任何外壳接入 |
| `entries.mjs` | 设置页条目视图（`defaultHidden` / `hidden` / `shown` → `visible`） |
| `validate.mjs` | snapshot 契约校验（字段/三态/重复 id/时间戳） |
| `diagnostics.mjs` | 只读诊断（配置 + 快照 + 文件 + 调度） |
| `cli.mjs` | 壳无关 CLI（status/config/refresh/validate/diagnostics） |
| `transport.mjs` | core transport 的 Node fetch 实现 |

```bash
npm install                  # 仓库根目录
npm test                     # 15 项测试
node packages/engine/host.mjs --data-dir data
```

Host 协议示例：

```json
{"id":1,"method":"hello"}
{"id":2,"method":"getStatus"}
{"id":2,"method":"refreshIfDue"}
{"id":3,"method":"getConfig"}
{"id":4,"method":"shutdown"}
```
