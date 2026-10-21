import test from "node:test";
import assert from "node:assert/strict";
import { buildEntries } from "../entries.mjs";

const games = [
	{ id: "a", name: "A", defaults: { banner: "A池" } },
	{ id: "b", name: "B" },
	{ id: "c", name: "C", custom: true },
];
const sources = [{ id: "b", defaultHidden: true }];

test("buildEntries：visible/defaultHidden/hidden/shown 三态合并", () => {
	const entries = buildEntries({ games, sources, config: { hidden: ["a"], shown: ["b"] } });
	const byId = Object.fromEntries(entries.map((e) => [e.id, e]));
	assert.equal(byId.a.visible, false);
	assert.equal(byId.a.hidden, true);
	assert.equal(byId.b.defaultHidden, true);
	assert.equal(byId.b.shown, true);
	assert.equal(byId.b.visible, true);
	assert.equal(byId.c.visible, true);
	assert.equal(byId.c.custom, true);
	assert.equal(byId.a.defaults.banner, "A池");
});

test("buildEntries：默认隐藏项未 shown 时不可见", () => {
	const entries = buildEntries({ games, sources, config: {} });
	const byId = Object.fromEntries(entries.map((e) => [e.id, e]));
	assert.equal(byId.b.defaultHidden, true);
	assert.equal(byId.b.visible, false);
});

test("buildEntries：removed 标记与空输入", () => {
	const entries = buildEntries({ games, sources: [], config: { removed: ["a"] } });
	assert.equal(entries.find((e) => e.id === "a").removed, true);
	assert.deepEqual(buildEntries({ games: [], config: {} }), []);
});
