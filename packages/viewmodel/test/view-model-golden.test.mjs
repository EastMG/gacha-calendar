import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { buildViewModel } from "../view-model.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const FIXTURE = JSON.parse(fs.readFileSync(path.join(HERE, "fixtures", "basic.json"), "utf8"));

test("view-model golden fixture：JS 输出与冻结期望一致（未来 C# 也用同一份）", () => {
	const now = new Date(FIXTURE.nowIso).getTime();
	assert.ok(Number.isFinite(now), "fixture nowIso 必须可解析");
	const snapshot = structuredClone(FIXTURE.snapshot);
	snapshot.result.refreshedAt = now;

	const vm = buildViewModel({ snapshot, now, dockItems: FIXTURE.dockItems });
	const expect = FIXTURE.expect;

	assert.equal(vm.header.count, expect.header.count);
	assert.equal(vm.header.countText, expect.header.countText);
	assert.equal(vm.header.statusText, expect.header.statusText);
	assert.equal(vm.header.updatedText, expect.header.updatedText);

	for (const expectedRow of expect.rows) {
		const row = vm.rows.find((r) => r.id === expectedRow.id);
		assert.ok(row, "缺少行：" + expectedRow.id);
		assert.deepEqual(row.badge, expectedRow.badge);
		assert.equal(row.gacha.text, expectedRow.gachaText);
		if (expectedRow.gachaDates) assert.equal(row.gachaDates.text, expectedRow.gachaDates);
		assert.equal(row.event.text, expectedRow.eventText);
		if (expectedRow.eventDates) assert.equal(row.eventDates.text, expectedRow.eventDates);
	}

	assert.deepEqual(
		vm.dock.chips.map((chip) => ({ id: chip.id, kind: chip.kind, remaining: chip.remaining })),
		expect.dock,
	);
});
