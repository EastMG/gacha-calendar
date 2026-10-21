import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createFileService } from "../service.mjs";

function makeEngine(state) {
	return {
		__test: { SOURCES: [] },
		async listGames() {
			return [{ id: "a", name: "A" }];
		},
		async getCached() {
			return { schemaVersion: 1, refreshedAt: 0, parserVersions: {}, games: {} };
		},
		async refresh() {
			state.attempts += 1;
			if (state.fail) throw new Error("boom");
			return { schemaVersion: 1, refreshedAt: state.now, parserVersions: { a: 1 }, games: { a: { name: "A" } } };
		},
		async selfCheck() {
			return { total: 1, summary: { ok: 1, nomatch: 0, down: 0, unconfigured: 0 }, problems: [], games: {} };
		},
	};
}

test("调度：首次 due、成功后按间隔、失败退避、手动刷新绕过退避、状态持久化", async () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "gc-sched-"));
	const state = { now: 1_000_000, fail: false, attempts: 0 };
	try {
		const service = createFileService({
			dataDir: dir,
			createEngine: () => makeEngine(state),
			transport: {},
			now: () => state.now,
			coreVersion: "test",
		});
		service.updateConfig({ refreshMinutes: 60, autoRefresh: true });

		// 首次：没有成功记录 → due
		assert.equal(service.getStatus().due, true);
		const first = await service.refreshIfDue();
		assert.equal(first.refreshed, true);
		assert.equal(state.attempts, 1);
		assert.equal(service.getStatus().failures, 0);
		assert.equal(service.getStatus().lastSuccessAt, state.now);
		assert.equal(service.getStatus().due, false);

		// 未到间隔 → not-due
		state.now += 60 * 60 * 1000 - 1000;
		const early = await service.refreshIfDue();
		assert.equal(early.refreshed, false);
		assert.equal(early.reason, "not-due");
		assert.equal(state.attempts, 1);

		// 到点 + 失败 → 记录退避
		state.now += 2000;
		state.fail = true;
		const failed = await service.refreshIfDue();
		assert.equal(failed.refreshed, false);
		assert.equal(failed.reason, "error");
		assert.equal(state.attempts, 2);
		let status = service.getStatus();
		assert.equal(status.failures, 1);
		assert.equal(status.backoffActive, true);
		assert.equal(status.nextAllowedAt, state.now + 5 * 60 * 1000);
		assert.equal(status.due, false);

		// 退避期间 → backoff
		state.now += 60 * 1000;
		const cooling = await service.refreshIfDue();
		assert.equal(cooling.reason, "backoff");
		assert.equal(state.attempts, 2);

		// 手动刷新不受退避限制，成功后清零
		state.fail = false;
		await service.refresh();
		assert.equal(state.attempts, 3);
		status = service.getStatus();
		assert.equal(status.failures, 0);
		assert.equal(status.backoffActive, false);
		assert.equal(status.lastSuccessAt, state.now);

		// 持久化：新 service 读同一个 dataDir 能看到成功状态
		const service2 = createFileService({
			dataDir: dir,
			createEngine: () => makeEngine(state),
			transport: {},
			now: () => state.now,
			coreVersion: "test",
		});
		assert.equal(service2.getStatus().lastSuccessAt, state.now);
		assert.equal(service2.getStatus().failures, 0);
	} finally {
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

test("调度：autoRefresh=false 时 refreshIfDue 返回 manual-only", async () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "gc-sched2-"));
	const state = { now: 5, fail: false, attempts: 0 };
	try {
		const service = createFileService({
			dataDir: dir,
			createEngine: () => makeEngine(state),
			transport: {},
			now: () => state.now,
		});
		service.updateConfig({ autoRefresh: false });
		const result = await service.refreshIfDue();
		assert.equal(result.refreshed, false);
		assert.equal(result.reason, "manual-only");
		assert.equal(state.attempts, 0);
	} finally {
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

test("调度：并发 refreshIfDue 只发起一次刷新", async () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "gc-sched3-"));
	const state = { now: 10, fail: false, attempts: 0 };
	let release;
	const gate = new Promise((resolve) => (release = resolve));
	try {
		const engine = makeEngine(state);
		const origRefresh = engine.refresh;
		engine.refresh = async () => {
			await gate;
			return origRefresh();
		};
		const service = createFileService({
			dataDir: dir,
			createEngine: () => engine,
			transport: {},
			now: () => state.now,
		});
		const p1 = service.refreshIfDue();
		const p2 = service.refreshIfDue();
		release();
		const [a, b] = await Promise.all([p1, p2]);
		const refreshedCount = [a, b].filter((r) => r.refreshed).length;
		assert.equal(refreshedCount, 1);
		assert.equal(state.attempts, 1);
	} finally {
		fs.rmSync(dir, { recursive: true, force: true });
	}
});
