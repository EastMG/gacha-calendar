// 单进程测试入口：避免 `node --test <dir>` 在受限沙箱里 spawn 子进程（EPERM）。
// Node 自带的 node:test 在 import 时会注册用例，进程退出时汇总输出。
import "./config.test.mjs";
import "./snapshot.test.mjs";
import "./validate.test.mjs";
import "./service.test.mjs";
import "./scheduler.test.mjs";
import "./host.test.mjs";
