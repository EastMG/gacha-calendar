import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createFileService } from "../service.mjs";

function makeFakeEngine({ games, result, sources = [] }) {
	return {
		__test: { SOURCES: sources },
		async listGames() {
			return games;
		},
		async getCached() {
			return result;
		},
		async refresh() {
			return result;
		},
		async selfCheck() {
			return { total: games.length, summary: { ok: 0, nomatch: 0, down: 0, unconfigured: 0 }, problems: [], games: {} };
		},
	};
}

test("createFileService：配置持久化 + refresh 写出 snapshot", async () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "gc-service-"));
	try {
		const games = [
			{ id: "a", name: "A", defaults: { banner: "", roles: "", bannerDates: "", event: "", eventDates: "" } },
			{ id: "b", name: "B" },
		];
		const result = {
			schemaVersion: 1,
			refreshedAt: 111,
			parserVersions: { a: 1 },
			games: { a: { name: "A", banner: "A池", gachaFail: null, eventFail: null, okAt: 1 } },
		};

		const service = createFileService({
			dataDir: dir,
			createEngine: () => makeFakeEngine({ games, result, sources: [{ id: "b", defaultHidden: true }] }),
			transport: {},
			now: () => 1234,
			coreVersion: "test-core",
		});

		assert.equal(service.getConfig().refreshMinutes, 1440);
		const updated = service.updateConfig({ hidden: ["a"], customUrls: { a: "https://example.com/a" } });
		assert.deepEqual(updated.hidden, ["a"]);
		assert.deepEqual(JSON.parse(fs.readFileSync(service.paths.config, "utf8")).hidden, ["a"]);
		assert.equal(service.getConfig().customUrls.a, "https://example.com/a");

		const snap = await service.refresh();
		assert.equal(snap.generatedAt, 1234);
		assert.equal(snap.coreVersion, "test-core");
		assert.equal(snap.games.find((g) => g.id === "a").hidden, true);
		assert.equal(snap.games.find((g) => g.id === "b").hidden, true); // defaultHidden 且未 shown
		assert.equal(snap.result.refreshedAt, 111);

		// 不联网重建：缓存结果 + 当前显隐/顺序立即生效
		const rebuilt = await service.rebuildSnapshot();
		assert.equal(rebuilt.result.refreshedAt, 111);
		assert.equal(rebuilt.games.find((g) => g.id === "a").hidden, true);
		assert.equal(rebuilt.games.find((g) => g.id === "b").hidden, true);

		const onDisk = JSON.parse(fs.readFileSync(service.paths.snapshot, "utf8"));
		assert.deepEqual(onDisk.config.hidden, ["a"]);
		assert.deepEqual(service.getSnapshot().games.map((g) => g.id), snap.games.map((g) => g.id));

		const diag = service.getDiagnostics();
		assert.equal(diag.ok, true);
		assert.equal(diag.files.snapshot.exists, true);
		assert.equal(service.validateSnapshot().ok, true);
		assert.deepEqual(service.validateConfig(), []);

		// 条目视图 + 显隐/排序/删除
		const entries = await service.listEntries();
		const byId = Object.fromEntries(entries.map((e) => [e.id, e]));
		assert.equal(byId.a.visible, false); // hidden: ["a"]
		assert.equal(byId.b.defaultHidden, true);
		assert.equal(byId.b.visible, false); // defaultHidden 且 shown 为空

		const afterShow = service.setEntryVisibility("b", true);
		assert.deepEqual(afterShow.shown, ["b"]);
		assert.equal((await service.listEntries()).find((e) => e.id === "b").visible, true);

		const afterOrder = service.setOrder(["b", "a"]);
		assert.deepEqual(afterOrder.order, ["b", "a"]);

		const afterRemove = await service.removeEntry("a");
		assert.deepEqual(afterRemove.removed, ["a"]);
		assert.equal(afterRemove.hidden.includes("a"), false);
		const afterRestore = service.restoreEntry("a");
		assert.deepEqual(afterRestore.removed, []);

		// 旧格式导入：JSON 字符串字段 + 忽略缓存键
		const imported = service.importConfig({
			customUrls: JSON.stringify({ a: "https://example.com/a" }),
			hidden: JSON.stringify(["b"]),
			lastData: "{}",
			lastRefresh: 123,
		});
		assert.deepEqual(imported.customUrls, { a: "https://example.com/a" });
		assert.deepEqual(imported.hidden, ["b"]);
		assert.equal(imported.lastData, undefined);
		assert.equal(service.getConfig().hidden[0], "b");
	} finally {
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

test("createFileService：坏配置校验失败时抛 invalid-config", () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "gc-service-bad-"));
	try {
		const service = createFileService({ dataDir: dir, createEngine: () => ({}), transport: {}, now: () => 0 });
		assert.throws(() => service.updateConfig({ order: "not-array" }), (err) => err.code === "invalid-config");
	} finally {
		fs.rmSync(dir, { recursive: true, force: true });
	}
});
