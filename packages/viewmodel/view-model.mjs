// 壳无关的「视图模型」参考实现：把 snapshot + now 变成任何 UI 都能直接渲染的 JSON。
//
// 为什么需要它（外壳未定）：
//   - WPF 当前有一套 C# 实现（src/GachaCalendar/ViewModels/RowBuilder.cs + MainViewModel.cs）；
//   - 如果最终切 Tauri，前端需要同样的倒计时/失败三态/常驻条选择逻辑；
//   - 本文件把口径固化成纯函数 + 测试，两个外壳都应以此为准（或直接 import 本模块）。
//
// 约束：纯 JS，不依赖 DOM / Node API；时间一律用传入的 now（毫秒）。
// 输出结构见 docs/08-视图模型契约.md。

const BA_ROLE_IDS = new Set(["ba-cn", "ba-global", "ba-jp"]);

function isPositiveFinite(value) {
	return typeof value === "number" && Number.isFinite(value) && value >= 0;
}

function pad2(value) {
	return String(value).padStart(2, "0");
}

function formatMdHm(ts) {
	const d = new Date(ts);
	return `${pad2(d.getMonth() + 1)}-${pad2(d.getDate())} ${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
}

/** "MM-dd HH:mm ~ MM-dd HH:mm" → 离 now 最近的一期（与 C# 实现同口径）。 */
export function parseDisplayRange(raw, nowMs) {
	if (typeof raw !== "string") return null;
	const parts = raw.split("~").map((s) => s.trim());
	if (parts.length < 2) return null;
	const now = new Date(nowMs);
	const parsePoint = (text) => {
		const m = text.match(/^(\d{1,2})-(\d{1,2})(?:\s+(\d{1,2}):(\d{2}))?$/);
		if (!m) return null;
		const mo = Number(m[1]);
		const d = Number(m[2]);
		if (mo < 1 || mo > 12 || d < 1 || d > 31) return null;
		const h = m[3] ? Number(m[3]) : 0;
		const mi = m[4] ? Number(m[4]) : 0;
		const value = new Date(now.getFullYear(), mo - 1, d, h, mi, 0).getTime();
		return Number.isNaN(value) ? null : value;
	};
	const a = parsePoint(parts[0]);
	const b = parsePoint(parts[1]);
	if (a == null || b == null) return null;
	let start = a;
	let end = b;
	if (end < start) end = new Date(end).setFullYear(new Date(end).getFullYear() + 1);
	// 展示文本不带年份：试 去年 / 今年 / 明年，选离 now 最近的窗口
	let best = null;
	for (const shift of [-1, 0, 1]) {
		const s = new Date(start);
		s.setFullYear(s.getFullYear() + shift);
		const e = new Date(end);
		e.setFullYear(e.getFullYear() + shift);
		const st = s.getTime();
		const en = e.getTime();
		const distance = nowMs < st ? st - nowMs : nowMs > en ? nowMs - en : 0;
		if (!best || distance < best.distance) best = { start: st, end: en, distance };
	}
	return { start: best.start, end: best.end };
}

export function formatRemaining(targetMs, nowMs) {
	const diff = targetMs - nowMs;
	if (diff < 0) return "";
	const days = Math.floor(diff / 86400000);
	const hours = Math.floor((diff % 86400000) / 3600000);
	const mins = Math.floor((diff % 3600000) / 60000);
	return `${days}天${hours}小时${mins}分钟`;
}

export function formatShortRemaining(diffMs) {
	if (diffMs <= 0) return "已结束";
	if (diffMs >= 86400000) return `${Math.floor(diffMs / 86400000)}天`;
	if (diffMs >= 3600000) return `${Math.floor(diffMs / 3600000)}时`;
	return `${Math.max(1, Math.floor(diffMs / 60000))}分`;
}

export function displayTimeCell(raw, nowMs) {
	const window = parseDisplayRange(raw, nowMs);
	if (!window) return raw || "";
	if (nowMs < window.start) {
		const t = formatRemaining(window.start, nowMs);
		return t ? `还有 ${t} 开始` : raw || "";
	}
	if (nowMs > window.end) return "已结束";
	const t = formatRemaining(window.end, nowMs);
	return t ? `还剩 ${t}` : raw || "";
}

export function cleanRoleNames(roles, gameId) {
	if (!roles || typeof roles !== "string") return "";
	const isBa = BA_ROLE_IDS.has(gameId);
	return roles
		.split(/[、,，]/)
		.map((raw) => {
			let s = raw.replace(/[「」【】]/g, "");
			if (isBa) s = s.replace(/（/g, "(").replace(/）/g, ")");
			else s = s.replace(/[（(][^）)]*[）)]/g, "");
			s = s.trim();
			if (!s) return "";
			const m1 = s.match(/^([\u4e00-\u9fff]{2,10})·(.+)$/);
			if (m1 && m1[2].trim()) return m1[2].trim();
			const m2 = s.match(/^([\u4e00-\u9fff]{4})•(.+)$/);
			if (m2) return m2[2].trim();
			return s;
		})
		.filter(Boolean)
		.join("、");
}

export function normalizeFail(fail) {
	if (!fail) return null;
	if (fail.kind === "down") return { kind: "down", reason: fail.reason || "抓取异常" };
	if (fail.kind === "nomatch") return { kind: "nomatch" };
	return null;
}

export function sideFailText(side, fail) {
	const f = normalizeFail(fail);
	if (!f) return "";
	const label = side === "gacha" ? "卡池" : "活动";
	if (f.kind === "down") return `${label}失败：${f.reason}`;
	return side === "gacha" ? "新卡池未公布" : "新活动未公布";
}

export function withFailNote(baseText, side, fail, stale) {
	const text = sideFailText(side, fail);
	const note = text ? `（${text}）` : "";
	if (!note) return baseText || "";
	return stale && baseText ? `${baseText}\n${note}` : note;
}

function buildGachaTip(rec) {
	let head = rec.roles ? `${rec.banner}：${rec.roles}` : rec.banner || "";
	const dates = rec.bannerDatesRaw || rec.bannerDates || "";
	if (rec.bannerDates) head = head ? `${head}\n${dates}` : dates;
	return head;
}

function buildEventTip(rec) {
	if (!rec.event) return rec.eventDatesRaw || rec.eventDates || "";
	if (!rec.eventDates) return rec.event;
	return `${rec.event}\n${rec.eventDatesRaw || rec.eventDates}`;
}

function buildBadge(rec) {
	const parts = [];
	const g = normalizeFail(rec.gachaFail);
	const e = normalizeFail(rec.eventFail);
	if (g) parts.push({ kind: g.kind, text: sideFailText("gacha", g) });
	if (e) parts.push({ kind: e.kind, text: sideFailText("event", e) });
	const down = parts.find((p) => p.kind === "down");
	if (down) return { text: down.text, kind: "err" };
	if (parts.length > 0) return { text: parts[0].text, kind: "warn" };
	if (rec.gachaStale || rec.eventStale) return { text: "沿用旧数据", kind: "stale" };
	return null;
}

/**
 * 单行：对应 C# 的 RowBuilder.Build。
 */
export function buildRow(game, rec, nowMs) {
	const record = rec || {};
	const gachaText = record.roles ? cleanRoleNames(record.roles, game.id) : record.banner || "—";
	const eventText = record.event || "—";
	const bannerWindow = parseDisplayRange(record.bannerDates, nowMs);
	const eventWindow = parseDisplayRange(record.eventDates, nowMs);

	let activeEnd = null;
	let activeKind = "";
	if (bannerWindow && nowMs >= bannerWindow.start && nowMs <= bannerWindow.end) {
		activeEnd = bannerWindow.end;
		activeKind = "卡池";
	}
	if (eventWindow && nowMs >= eventWindow.start && nowMs <= eventWindow.end) {
		if (activeEnd == null || eventWindow.end < activeEnd) {
			activeEnd = eventWindow.end;
			activeKind = "活动";
		}
	}

	return {
		id: game.id,
		name: game.name,
		icon: game.icon || "",
		badge: buildBadge(record),
		gacha: {
			text: gachaText || "—",
			tip: withFailNote(buildGachaTip(record), "gacha", record.gachaFail, record.gachaStale),
		},
		gachaDates: {
			text: displayTimeCell(record.bannerDates || "", nowMs),
			tip: withFailNote(record.bannerDatesRaw || record.bannerDates || "", "gacha", record.gachaFail, record.gachaStale),
		},
		event: {
			text: eventText,
			tip: withFailNote(buildEventTip(record), "event", record.eventFail, record.eventStale),
		},
		eventDates: {
			text: displayTimeCell(record.eventDates || "", nowMs),
			tip: withFailNote(record.eventDatesRaw || record.eventDates || "", "event", record.eventFail, record.eventStale),
		},
		activeEnd,
		activeKind,
	};
}

function buildStatus(visibleGames, result, isSample) {
	const records = result.games || {};
	let ok = 0;
	const gachaDown = [];
	const eventDown = [];
	const gachaNomatch = [];
	const eventNomatch = [];
	for (const game of visibleGames) {
		const rec = records[game.id];
		if (!rec || rec.skipped) continue;
		const gDown = normalizeFail(rec.gachaFail)?.kind === "down";
		const eDown = normalizeFail(rec.eventFail)?.kind === "down";
		if (!gDown && !eDown) ok += 1;
		if (gDown) gachaDown.push(game.name);
		if (eDown) eventDown.push(game.name);
		if (normalizeFail(rec.gachaFail)?.kind === "nomatch") gachaNomatch.push(game.name);
		if (normalizeFail(rec.eventFail)?.kind === "nomatch") eventNomatch.push(game.name);
	}

	const prefix = isSample ? "示例数据" : ok > 0 ? "联网数据" : "尚未联网";
	const statusText = `${prefix} · 成功 ${ok}/${visibleGames.length}`;
	const lines = [];
	const add = (label, names) => {
		if (!names.length) return;
		const shown = names.slice(0, 8).join("、");
		const more = names.length > 8 ? ` 等 ${names.length} 个` : "";
		lines.push(`${label}：${shown}${more}`);
	};
	add("卡池失败", gachaDown);
	add("活动失败", eventDown);
	add("新卡池未公布", gachaNomatch);
	add("新活动未公布", eventNomatch);
	const statusTip = lines.length ? lines.join("\n") : "所有可见条目都抓到了当期内容";

	const ts = result.refreshedAt > 0 ? result.refreshedAt : 0;
	const updatedText = ts > 0 ? `${isSample ? "示例快照 · " : "更新："}${formatMdHm(ts)}` : "更新：—";
	return { ok, total: visibleGames.length, statusText, statusTip, updatedText };
}

function buildDockChips(visibleGames, result, nowMs, dockItems) {
	const records = result.games || {};
	const candidates = [];
	for (const game of visibleGames) {
		const rec = records[game.id];
		if (!rec) continue;
		const bw = parseDisplayRange(rec.bannerDates, nowMs);
		if (bw && nowMs >= bw.start && nowMs <= bw.end) candidates.push({ end: bw.end, game, rec, kind: "卡池" });
		const ew = parseDisplayRange(rec.eventDates, nowMs);
		if (ew && nowMs >= ew.start && nowMs <= ew.end) candidates.push({ end: ew.end, game, rec, kind: "活动" });
	}
	const byGame = new Map();
	for (const c of candidates) {
		const prev = byGame.get(c.game.id);
		if (!prev || c.end < prev.end) byGame.set(c.game.id, c);
	}
	const limit = Math.min(6, Math.max(1, Number(dockItems) || 3));
	const picked = [...byGame.values()].sort((a, b) => a.end - b.end).slice(0, limit);

	const chips = picked.map((c, index) => {
		const title = c.kind === "卡池" ? c.rec.banner || "—" : c.rec.event || "—";
		const dates = c.kind === "卡池" ? c.rec.bannerDates : c.rec.eventDates;
		const full = formatRemaining(c.end, nowMs);
		const short = c.game.name.length <= 9 ? c.game.name : `${c.game.name.slice(0, 8)}…`;
		return {
			id: c.game.id,
			name: short,
			fullName: c.game.name,
			kind: c.kind,
			remaining: formatShortRemaining(c.end - nowMs),
			accentIndex: index,
			tip: `${c.game.name}\n${c.kind}：${title}\n${dates || ""}\n还剩 ${full}`,
		};
	});
	return { chips, emptyText: "暂无进行中" };
}

/**
 * @param {object} input
 * @param {object} input.snapshot  本仓库 snapshot.json
 * @param {number} [input.now]
 * @param {boolean} [input.isSample]
 * @param {number} [input.dockItems] 常驻条显示条数（1–6）
 */
export function buildViewModel({ snapshot, now = Date.now(), isSample = false, dockItems = 3 }) {
	if (!snapshot || !Array.isArray(snapshot.games)) throw new Error("buildViewModel 需要合法 snapshot");
	const nowMs = typeof now === "number" ? now : new Date(now).getTime();
	const result = snapshot.result || { refreshedAt: 0, games: {} };
	const visibleGames = snapshot.games.filter((g) => !g.hidden);
	const records = result.games || {};

	const rows = visibleGames.map((game) => buildRow(game, records[game.id], nowMs));
	const status = buildStatus(visibleGames, result, isSample);
	const dock = buildDockChips(visibleGames, result, nowMs, dockItems);

	return {
		now: nowMs,
		header: {
			title: "二游日历",
			count: visibleGames.length,
			countText: `${visibleGames.length} 款`,
			statusText: status.statusText,
			statusTip: status.statusTip,
			updatedText: status.updatedText,
			isSample: !!isSample,
		},
		rows,
		dock,
		stats: { ok: status.ok, total: status.total },
	};
}
