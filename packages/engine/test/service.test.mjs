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
			return { schemaVersion: 1, refreshedAt: 0, parserVersions: {}, games: {} };
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

		const onDisk = JSON.parse(fs.readFileSync(service.paths.snapshot, "utf8"));
		assert.deepEqual(onDisk.config.hidden, ["a"]);
		assert.deepEqual(service.getSnapshot().games.map((g) => g.id), snap.games.map((g) => g.id));
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
