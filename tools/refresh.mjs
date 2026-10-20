#!/usr/bin/env node
// 开发期抓取工具：调用 gacha-calendar-core 真抓一轮，产出桌面端可读的 snapshot.json。
//
// 为什么放在 Node 里而不是 C# 里（v0.1）：
//   core 是纯 JS（零宿主依赖），直接 `import` 即可复用全部 28 款游戏的解析器；
//   计划中的 v0.2 会用内置 WebView2 CoreHost 把这一层搬进应用内，本工具继续保留给开发/CI 用。
//
// 用法：
//   npm run refresh                      → ../data/snapshot.json
//   npm run sample                       → ../data/sample-snapshot.json（示例快照，随仓库提交）
//   node refresh.mjs --out ../data/x.json --config ./config.json --only genshin,hsr
//   node refresh.mjs --selfcheck        → 只跑解析器自检，不写文件
//
// 退出码：0 正常；1 抓取抛错；2 一轮全失败（okCount = 0）。

import { createRequire } from "node:module";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, isAbsolute, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createEngine } from "gacha-calendar-core";

const HERE = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);

// ---------------------------------------------------------------- CLI

function parseArgs(argv) {
	const out = { out: resolve(HERE, "..", "data", "snapshot.json"), config: null, only: null, selfcheck: false };
	for (let i = 0; i < argv.length; i++) {
		const a = argv[i];
		if (a === "--out") {
			const v = argv[++i];
			out.out = isAbsolute(v) ? v : resolve(process.cwd(), v);
		} else if (a === "--config") {
			out.config = resolve(process.cwd(), argv[++i]);
		} else if (a === "--only") {
			out.only = String(argv[++i] || "").split(",").map((s) => s.trim()).filter(Boolean);
		} else if (a === "--selfcheck") {
			out.selfcheck = true;
		} else if (a === "-h" || a === "--help") {
			console.log("用法：node refresh.mjs [--out <file>] [--config <json>] [--only id1,id2] [--selfcheck]");
			process.exit(0);
		}
	}
	return out;
}

// ---------------------------------------------------------------- 传输层
// 桌面端没有 CORS 限制，直接用 Node fetch；但要模拟浏览器 UA，并把 core 要求的 Referer / 额外头带上。

const UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36";

const transport = {
	/** 直连：core 传进来的 headers 已含 Accept / Referer（见 core 的 rawHeaders） */
	async fetchRaw(url, opts = {}) {
		return fetch(url, {
			headers: opts.headers,
			signal: opts.signal,
			redirect: "follow"
		});
	},
	/** 经代理：桌面端直接发原生请求；返回目标站原始 body 字符串 */
	async fetchViaProxy(url, { referer, headers, body, contentType } = {}) {
		const h = Object.assign({ "User-Agent": UA, Accept: "application/json, text/html, */*" }, headers || {});
		if (referer) h.Referer = referer;
		const init = { headers: h, redirect: "follow" };
		if (body !== undefined) {
			init.method = "POST";
			h["Content-Type"] = contentType || "application/json; charset=utf-8";
			init.body = typeof body === "string" ? body : JSON.stringify(body);
		}
		const res = await fetch(url, init);
		if (!res.ok) throw new Error("HTTP " + res.status);
		return await res.text();
	}
};

// ---------------------------------------------------------------- 主流程

const args = parseArgs(process.argv.slice(2));

const config = args.config && existsSync(args.config) ? JSON.parse(readFileSync(args.config, "utf8")) : {};
const store = new Map(Object.entries(config));

const storage = {
	async get(key) {
		return store.get(key);
	},
	async set(key, value) {
		store.set(key, value);
	}
};

const engine = createEngine({ transport, storage, now: () => Date.now() });

const startedAt = Date.now();

function coreVersion() {
	try {
		const pkgPath = require.resolve("gacha-calendar-core/package.json");
		return JSON.parse(readFileSync(pkgPath, "utf8")).version;
	} catch {
		return "unknown";
	}
}

/** 与 core 的 isEntryHidden 同口径：hidden 里有 → 隐；defaultHidden 且 shown 里没有 → 隐。 */
function visibilityOf(games, settings) {
	const sources = new Map((engine.__test?.SOURCES || []).map((s) => [s.id, s]));
	const hidden = new Set(Array.isArray(settings.hidden) ? settings.hidden : []);
	const shown = new Set(Array.isArray(settings.shown) ? settings.shown : []);
	return (id) => hidden.has(id) || (!!sources.get(id)?.defaultHidden && !shown.has(id));
}

function fmtRemaining(ms) {
	if (ms <= 0) return "已结束";
	const d = Math.floor(ms / 86400000);
	const h = Math.floor((ms % 86400000) / 3600000);
	const m = Math.floor((ms % 3600000) / 60000);
	if (d > 0) return `${d}天${h}小时`;
	if (h > 0) return `${h}小时${m}分`;
	return `${m}分`;
}

async function main() {
	const games = await engine.listGames();

	if (args.only) {
		// 只抓指定条目：把其它条目全部当成 hidden（core 的刷新范围只包含可见条目）。
		const keep = new Set(args.only);
		const hidden = new Set(Array.isArray(config.hidden) ? config.hidden : []);
		for (const g of games) if (!keep.has(g.id)) hidden.add(g.id);
		store.set("hidden", [...hidden]);
	}

	if (args.selfcheck) {
		const t0 = Date.now();
		const report = await engine.selfCheck();
		console.log(`自检 ${report.total} 个条目，用时 ${((Date.now() - t0) / 1000).toFixed(1)}s`);
		console.log(`汇总：ok=${report.summary.ok} nomatch=${report.summary.nomatch} down=${report.summary.down} unconfigured=${report.summary.unconfigured ?? 0}`);
		for (const p of report.problems) console.log("  - " + p);
		process.exit(report.summary.down > 0 ? 3 : 0);
	}

	let result;
	try {
		result = await engine.refresh();
	} catch (err) {
		console.error("抓取失败：" + ((err && err.stack) || err));
		process.exit(1);
	}

	const elapsed = ((Date.now() - startedAt) / 1000).toFixed(1);
	const records = result.games || {};
	const isHidden = visibilityOf(games, store);
	const shown = games.filter((g) => !isHidden(g.id));
	const okCount = shown.filter((g) => {
		const r = records[g.id];
		if (!r || r.skipped) return false;
		return !(r.gachaFail && r.gachaFail.kind === "down") && !(r.eventFail && r.eventFail.kind === "down");
	}).length;

	const ordered = Array.isArray(config.order) && config.order.length
		? sortByOrder(games, config.order)
		: games;

	const snapshot = {
		schemaVersion: 1,
		generatedAt: Date.now(),
		coreVersion: coreVersion(),
		games: ordered.map((g) => ({
			id: g.id,
			name: g.name,
			hidden: isHidden(g.id),
			defaults: g.defaults
		})),
		result
	};

	mkdirSync(dirname(args.out), { recursive: true });
	writeFileSync(args.out, JSON.stringify(snapshot, null, "\t") + "\n", "utf8");

	console.log(`抓取完成：${shown.length} 个可见条目，成功 ${okCount}/${shown.length}，用时 ${elapsed}s`);
	console.log(`snapshot → ${args.out}（core ${snapshot.coreVersion}）`);
	for (const g of shown) {
		const r = records[g.id];
		if (!r) {
			console.log(`  ${g.name.padEnd(10, " ")} 无记录`);
			continue;
		}
		const end = soonestEnd(r);
		const tail = end ? `（最近结束 ${fmtRemaining(end - Date.now())}）` : "";
		const fail = [r.gachaFail, r.eventFail].filter((f) => f && f.kind === "down").length;
		console.log(`  ${g.name.padEnd(10, " ")} ${(r.banner || "—").slice(0, 18)} ${tail}${fail ? ` [${fail} 处报错]` : ""}`);
	}

	if (okCount === 0) {
		console.error("一轮全部失败：请检查网络或稍后重试。");
		process.exit(2);
	}
}

/** 按 order 重排；未列出的条目保持原序接在后面。 */
function sortByOrder(list, order) {
	const byId = new Map(list.map((g) => [g.id, g]));
	const out = [];
	for (const id of order) if (byId.has(id)) out.push(byId.get(id));
	for (const g of list) if (!out.includes(g)) out.push(g);
	return out;
}

/** 从 Result JSON 的展示字符串里估"最近结束时刻"（MM-dd HH:mm ~ MM-dd HH:mm），仅用于命令行摘要。 */
function soonestEnd(rec) {
	let best = 0;
	for (const raw of [rec.bannerDates, rec.eventDates]) {
		const m = String(raw || "").match(/(\d{1,2})-(\d{1,2})\s+(\d{1,2}):(\d{2})/g);
		if (!m) continue;
		const last = m[m.length - 1].match(/(\d{1,2})-(\d{1,2})\s+(\d{1,2}):(\d{2})/);
		const now = new Date();
		const end = new Date(now.getFullYear(), Number(last[1]) - 1, Number(last[2]), Number(last[3]), Number(last[4]));
		if (end.getTime() < now.getTime() - 180 * 86400000) end.setFullYear(end.getFullYear() + 1);
		if (!best || end.getTime() < best) best = end.getTime();
	}
	return best;
}

main().catch((err) => {
	console.error(err && err.stack ? err.stack : err);
	process.exit(1);
});
