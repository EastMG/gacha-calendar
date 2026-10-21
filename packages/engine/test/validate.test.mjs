import test from "node:test";
import assert from "node:assert/strict";
import { buildSnapshot } from "../snapshot.mjs";
import { validateSnapshot } from "../validate.mjs";

function makeValidSnapshot() {
	return buildSnapshot({
		games: [
			{ id: "a", name: "A" },
			{ id: "b", name: "B" },
		],
		sources: [{ id: "b", defaultHidden: true }],
		config: { hidden: ["b"] },
		result: {
			schemaVersion: 1,
			refreshedAt: 123,
			parserVersions: { a: 1 },
			games: {
				a: {
					name: "A",
					banner: "A池",
					roles: "A角",
					bannerDates: "10-01 10:00 ~ 10-10 10:00",
					bannerDatesRaw: "",
					bannerHover: "",
					event: "A活动",
					eventDates: "",
					eventDatesRaw: "",
					eventHover: "",
					gachaFail: null,
					eventFail: { kind: "nomatch" },
					gachaStale: false,
					eventStale: false,
					okAt: 123,
				},
			},
		},
		now: 999,
	});
}

test("validateSnapshot：合法快照通过", () => {
	const report = validateSnapshot(makeValidSnapshot());
	assert.equal(report.ok, true);
	assert.deepEqual(report.errors, []);
});

test("validateSnapshot：可见条目缺记录只产生 warning", () => {
	const snap = makeValidSnapshot();
	snap.result.games = {};
	const report = validateSnapshot(snap);
	assert.equal(report.ok, true);
	assert.ok(report.warnings.some((w) => w.includes("可见条目没有 result")));
});

test("validateSnapshot：结构/字段错误能被定位", () => {
	const cases = [
		["schemaVersion", (s) => (s.schemaVersion = 2)],
		["generatedAt", (s) => (s.generatedAt = "x")],
		["snapshot.games 必须是数组", (s) => (s.games = {})],
		["重复 id", (s) => (s.games[1].id = "a")],
		["hidden", (s) => (s.games[0].hidden = "no")],
		["kind", (s) => (s.result.games.a.eventFail = { kind: "oops" })],
		["banner", (s) => (s.result.games.a.banner = 123)],
		["refreshedAt", (s) => (s.result.refreshedAt = -1)],
	];
	for (const [needle, mutate] of cases) {
		const snap = makeValidSnapshot();
		mutate(snap);
		const report = validateSnapshot(snap);
		assert.equal(report.ok, false, `应失败：${needle}`);
		assert.ok(
			report.errors.some((e) => e.includes(needle)),
			`错误里应包含「${needle}」，实际：${JSON.stringify(report.errors)}`,
		);
	}
});
