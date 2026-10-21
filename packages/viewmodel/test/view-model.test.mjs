import test from "node:test";
import assert from "node:assert/strict";
import { buildViewModel, cleanRoleNames, displayTimeCell, parseDisplayRange } from "../view-model.mjs";

const NOW = new Date(2026, 9, 21, 6, 0, 0).getTime(); // 2026-10-21 06:00 本地时间

function makeSnapshot() {
	return {
		schemaVersion: 1,
		generatedAt: NOW,
		coreVersion: "test",
		config: {},
		games: [
			{ id: "a", name: "A", hidden: false, defaults: {} },
			{ id: "b", name: "B", hidden: false, defaults: {} },
			{ id: "c", name: "C", hidden: true, defaults: {} },
		],
		result: {
			schemaVersion: 1,
			refreshedAt: NOW,
			parserVersions: {},
			games: {
				a: {
					name: "A",
					banner: "A池",
					roles: "「霜星·凛冬」、爱可菲（冰）",
					bannerDates: "10-01 12:00 ~ 10-22 12:00",
					bannerDatesRaw: "2026/10/01 12:00 ~ 2026/10/22 12:00",
					bannerHover: "",
					event: "A活动",
					eventDates: "10-10 12:00 ~ 10-25 12:00",
					eventDatesRaw: "",
					eventHover: "",
					gachaFail: null,
					eventFail: null,
					gachaStale: false,
					eventStale: false,
					okAt: NOW,
				},
				b: {
					name: "B",
					banner: "B池",
					roles: "",
					bannerDates: "12-30 10:00 ~ 01-05 10:00",
					bannerDatesRaw: "",
					bannerHover: "",
					event: "B活动",
					eventDates: "10-20 10:00 ~ 10-23 10:00",
					eventDatesRaw: "",
					eventHover: "",
					gachaFail: { kind: "down", reason: "HTTP 403" },
					eventFail: { kind: "nomatch" },
					gachaStale: false,
					eventStale: false,
					okAt: 0,
				},
			},
		},
	};
}

test("buildViewModel：隐藏条目被过滤、计数/更新文案正确", () => {
	const vm = buildViewModel({ snapshot: makeSnapshot(), now: NOW, isSample: false });
	assert.equal(vm.rows.length, 2);
	assert.equal(vm.header.count, 2);
	assert.equal(vm.header.countText, "2 款");
	assert.equal(vm.stats.total, 2);
	assert.ok(vm.header.statusText.includes("成功 1/2"));
	assert.ok(vm.header.updatedText.startsWith("更新："));
});

test("buildViewModel：角色名精简与失败三态", () => {
	const vm = buildViewModel({ snapshot: makeSnapshot(), now: NOW });
	const a = vm.rows.find((r) => r.id === "a");
	const b = vm.rows.find((r) => r.id === "b");
	assert.equal(a.gacha.text, "凛冬、爱可菲");
	assert.equal(a.badge, null);
	assert.equal(b.badge.kind, "err");
	assert.ok(b.badge.text.includes("卡池失败：HTTP 403"));
	assert.ok(a.eventDates.text.includes("还剩"));
});

test("buildViewModel：跨年区间取离 now 最近的一期", () => {
	const window = parseDisplayRange("12-30 10:00 ~ 01-05 10:00", NOW);
	assert.ok(window);
	assert.ok(window.start < window.end);
	assert.ok(window.start > NOW, "10 月应取今年 12-30 开始的未来一期");
	const cell = displayTimeCell("12-30 10:00 ~ 01-05 10:00", NOW);
	assert.ok(cell.includes("还有"));
});

test("buildViewModel：常驻条按最近结束排序、limit 生效", () => {
	const vm = buildViewModel({ snapshot: makeSnapshot(), now: NOW, dockItems: 1 });
	assert.equal(vm.dock.chips.length, 1);
	assert.equal(vm.dock.chips[0].id, "a"); // A 卡池 10-22 结束，早于 B 活动 10-23
	assert.equal(vm.dock.chips[0].kind, "卡池");
	assert.ok(vm.dock.chips[0].remaining.endsWith("天") || vm.dock.chips[0].remaining.endsWith("时"));

	const empty = buildViewModel({
		snapshot: { ...makeSnapshot(), result: { ...makeSnapshot().result, games: {} } },
		now: NOW,
	});
	assert.equal(empty.dock.chips.length, 0);
	assert.equal(empty.dock.emptyText, "暂无进行中");
});

test("buildViewModel：示例数据前缀", () => {
	const vm = buildViewModel({ snapshot: makeSnapshot(), now: NOW, isSample: true });
	assert.ok(vm.header.statusText.startsWith("示例数据"));
	assert.ok(vm.header.updatedText.startsWith("示例快照 · "));
});

test("cleanRoleNames：BA 保留换装括号，其它游戏去掉属性后缀", () => {
	assert.equal(cleanRoleNames("桔梗（泳装）、白子", "ba-cn"), "桔梗(泳装)、白子");
	assert.equal(cleanRoleNames("「霜星·凛冬」、爱可菲（冰）", "genshin"), "凛冬、爱可菲");
});
