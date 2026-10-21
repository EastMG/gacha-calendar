import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
	DEFAULT_CONFIG,
	normalizeConfig,
	mergeConfig,
	toEngineSettings,
	visibilityOf,
	sortByOrder,
	visibilityPatch,
	validateConfig,
	loadConfigFile,
	saveConfigFile,
} from "../config.mjs";

test("normalizeConfig 补默认值并保留未知字段", () => {
	const cfg = normalizeConfig({ hidden: ["a", "a", 1], order: ["x"], foo: "bar" });
	assert.equal(cfg.schemaVersion, 1);
	assert.deepEqual(cfg.hidden, ["a"]);
	assert.deepEqual(cfg.order, ["x"]);
	assert.equal(cfg.autoRefresh, true);
	assert.equal(cfg.refreshMinutes, DEFAULT_CONFIG.refreshMinutes);
	assert.equal(cfg.foo, "bar");
});

test("refreshMinutes 被裁剪到 60..60480", () => {
	assert.equal(normalizeConfig({ refreshMinutes: 5 }).refreshMinutes, 60);
	assert.equal(normalizeConfig({ refreshMinutes: 999999 }).refreshMinutes, 60480);
	assert.equal(normalizeConfig({ refreshMinutes: "abc" }).refreshMinutes, DEFAULT_CONFIG.refreshMinutes);
});

test("可见性三态：hidden / defaultHidden+shown / 默认显示", () => {
	const games = [{ id: "a" }, { id: "b" }, { id: "c" }];
	const sources = [{ id: "b", defaultHidden: true }];
	const hidden = visibilityOf(games, { hidden: ["a"], shown: [] }, sources);
	assert.equal(hidden("a"), true);
	assert.equal(hidden("b"), true);
	assert.equal(hidden("c"), false);

	const shown = visibilityOf(games, { hidden: ["a"], shown: ["b"] }, sources);
	assert.equal(shown("b"), false);
});

test("sortByOrder：已知 id 前置，其余保持原序", () => {
	const games = [{ id: "a" }, { id: "b" }, { id: "c" }];
	assert.deepEqual(sortByOrder(games, ["c", "a"]).map((g) => g.id), ["c", "a", "b"]);
	assert.deepEqual(sortByOrder(games, []).map((g) => g.id), ["a", "b", "c"]);
	assert.deepEqual(sortByOrder(games, ["nope"]).map((g) => g.id), ["a", "b", "c"]);
});

test("toEngineSettings 把对象序列化成 core 需要的 JSON 字符串", () => {
	const settings = toEngineSettings({
		customUrls: { a: "https://example.com/a" },
		customEntries: [{ id: "x", name: "X" }],
		gameColors: { a: "#ff0000" },
		hidden: ["a"],
	});
	assert.equal(typeof settings.customUrls, "string");
	assert.equal(typeof settings.customEntries, "string");
	assert.equal(typeof settings.gameColors, "string");
	assert.deepEqual(JSON.parse(settings.customUrls), { a: "https://example.com/a" });
	assert.deepEqual(settings.hidden, ["a"]);
});

test("mergeConfig：数组整段替换、未知字段保留", () => {
	const base = normalizeConfig({ hidden: ["a"], foo: 1 });
	const merged = mergeConfig(base, { hidden: ["b"], bar: 2 });
	assert.deepEqual(merged.hidden, ["b"]);
	assert.equal(merged.foo, 1);
	assert.equal(merged.bar, 2);
});

test("visibilityPatch：默认隐藏项打开要写 shown", () => {
	assert.deepEqual(visibilityPatch({ hidden: [], shown: [] }, "x", true, true), { hidden: [], shown: ["x"] });
	assert.deepEqual(visibilityPatch({ hidden: [], shown: ["x"] }, "x", false, true), { hidden: ["x"], shown: [] });
	assert.deepEqual(visibilityPatch({ hidden: ["x"], shown: [] }, "x", true, false), { hidden: [], shown: [] });
});

test("validateConfig 能报出明显错误", () => {
	assert.deepEqual(validateConfig({}), []);
	assert.equal(validateConfig({ order: "a" }).length, 1);
	assert.equal(validateConfig({ refreshMinutes: 1 }).length, 1);
	assert.equal(validateConfig("nope").length, 1);
});

test("save/load 往返，文件不存在返回默认", () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "gc-config-"));
	try {
		const file = path.join(dir, "config.json");
		assert.deepEqual(loadConfigFile(file), normalizeConfig(DEFAULT_CONFIG));
		const saved = saveConfigFile(file, { hidden: ["a"], refreshMinutes: 120 });
		assert.deepEqual(loadConfigFile(file), saved);
		assert.equal(JSON.parse(fs.readFileSync(file, "utf8")).hidden[0], "a");
	} finally {
		fs.rmSync(dir, { recursive: true, force: true });
	}
});
