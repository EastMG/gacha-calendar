import test from "node:test";
import assert from "node:assert/strict";
import { buildSnapshot } from "../snapshot.mjs";

const games = [
	{
		id: "a",
		name: "A",
		defaults: { banner: "A池", roles: "A角", bannerDates: "", event: "", eventDates: "" },
	},
	{ id: "b", name: "B" },
	{ id: "c", name: "C" },
];

const sources = [{ id: "b", defaultHidden: true }];

const result = {
	schemaVersion: 1,
	refreshedAt: 111,
	parserVersions: { a: 1 },
	games: { a: { name: "A", banner: "A池", gachaFail: null, eventFail: null, okAt: 1 } },
};

test("buildSnapshot：排序、隐藏三态、config 原样带出", () => {
	const snap = buildSnapshot({
		games,
		result,
		config: { order: ["c", "a"], hidden: ["a"], shown: ["b"] },
		sources,
		coreVersion: "0.12.5",
		now: 999,
	});

	assert.equal(snap.schemaVersion, 1);
	assert.equal(snap.generatedAt, 999);
	assert.equal(snap.coreVersion, "0.12.5");
	assert.deepEqual(snap.games.map((g) => g.id), ["c", "a", "b"]);
	assert.equal(snap.games.find((g) => g.id === "a").hidden, true);
	assert.equal(snap.games.find((g) => g.id === "b").hidden, false);
	assert.equal(snap.games.find((g) => g.id === "c").hidden, false);
	assert.equal(snap.games.find((g) => g.id === "a").defaults.banner, "A池");
	assert.equal(snap.games.find((g) => g.id === "a").icon, "");
	assert.equal(snap.result.refreshedAt, 111);
	assert.deepEqual(snap.config.order, ["c", "a"]);
});

test("buildSnapshot：缺失 defaults / result 时形状稳定", () => {
	const snap = buildSnapshot({ games: [{ id: "x", name: "X" }], config: {}, now: 1 });
	assert.deepEqual(snap.games[0].defaults, {
		banner: "",
		roles: "",
		bannerDates: "",
		event: "",
		eventDates: "",
	});
	assert.equal(snap.result.schemaVersion, 1);
	assert.deepEqual(snap.result.games, {});
});
