import test from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { PassThrough } from "node:stream";
import { dispatch, startHost } from "../host.mjs";

function makeService() {
	return {
		coreVersion: "0.12.5",
		paths: { config: path.join("tmp", "gc", "config.json") },
		getConfig: () => ({ refreshMinutes: 1440 }),
		updateConfig: (patch) => ({ merged: patch }),
		listGames: async () => [{ id: "a" }],
		getCached: async () => ({ games: {} }),
		refresh: async () => ({ refreshed: true }),
		refreshIfDue: async () => ({ refreshed: false, reason: "not-due", status: { due: false } }),
		getStatus: () => ({ due: false, failures: 0 }),
		getSnapshot: () => ({ snapshot: true }),
		selfCheck: async () => ({ summary: {} }),
	};
}

test("dispatch：hello / getConfig / updateConfig / refresh / 未知方法", async () => {
	const service = makeService();
	const hello = await dispatch(service, "hello");
	assert.equal(hello.protocol, 1);
	assert.equal(hello.coreVersion, "0.12.5");
	assert.equal(hello.dataDir, path.dirname(service.paths.config));

	assert.deepEqual(await dispatch(service, "getConfig"), { refreshMinutes: 1440 });
	assert.deepEqual(await dispatch(service, "updateConfig", { patch: { hidden: ["a"] } }), { merged: { hidden: ["a"] } });
	assert.deepEqual(await dispatch(service, "updateConfig", { hidden: ["b"] }), { merged: { hidden: ["b"] } });
	assert.deepEqual(await dispatch(service, "refresh"), { refreshed: true });
	assert.deepEqual(await dispatch(service, "refreshIfDue"), { refreshed: false, reason: "not-due", status: { due: false } });
	assert.deepEqual(await dispatch(service, "getStatus"), { due: false, failures: 0 });
	assert.deepEqual(await dispatch(service, "shutdown"), { bye: true });

	await assert.rejects(() => dispatch(service, "nope"), (err) => err.code === "unknown-method");
});

test("startHost：逐行 JSON、错误响应、shutdown 关闭", async () => {
	const service = makeService();
	const input = new PassThrough();
	const output = new PassThrough();
	let text = "";
	output.on("data", (chunk) => {
		text += chunk.toString("utf8");
	});

	let shutdown = false;
	const host = startHost({ service, input, output, onShutdown: () => (shutdown = true) });
	input.write(JSON.stringify({ id: 1, method: "hello" }) + "\n");
	input.write("not json\n");
	input.write(JSON.stringify({ id: 2, method: "nope" }) + "\n");
	input.write(JSON.stringify({ id: 3, method: "shutdown" }) + "\n");

	await new Promise((resolve) => setTimeout(resolve, 80));
	host.close();

	const lines = text
		.split("\n")
		.filter(Boolean)
		.map((line) => JSON.parse(line));
	assert.equal(lines.length, 4);
	assert.equal(lines[0].id, 1);
	assert.equal(lines[0].ok, true);
	assert.equal(lines[1].id, null);
	assert.equal(lines[1].ok, false);
	assert.equal(lines[1].error.code, "bad-json");
	assert.equal(lines[2].id, 2);
	assert.equal(lines[2].error.code, "unknown-method");
	assert.deepEqual(lines[3], { id: 3, ok: true, result: { bye: true } });
	assert.equal(shutdown, true);
});
