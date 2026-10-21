import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { validateSnapshot } from "../validate.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SAMPLE = path.resolve(HERE, "../../../data/sample-snapshot.json");

test("仓库自带的 sample-snapshot.json 必须通过契约校验", () => {
	const snapshot = JSON.parse(fs.readFileSync(SAMPLE, "utf8"));
	const report = validateSnapshot(snapshot);
	assert.deepEqual(report.errors, []);
	assert.equal(report.ok, true);

	assert.ok(Array.isArray(snapshot.games));
	assert.ok(snapshot.games.length >= 20, "示例快照应覆盖 20+ 条目");
	const visible = snapshot.games.filter((g) => !g.hidden);
	assert.ok(visible.length >= 10, "示例快照应有 10+ 可见条目");
	assert.ok(Object.keys(snapshot.result.games || {}).length >= 10, "应有 10+ 条抓取记录");
	assert.equal(snapshot.config.schemaVersion, 1);
});
