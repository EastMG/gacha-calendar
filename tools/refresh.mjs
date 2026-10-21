#!/usr/bin/env node
// 开发期抓取工具：调用 gacha-calendar-core 真抓一轮，产出桌面端可读的 snapshot.json。
//
// 为什么放在 Node 里而不是 C# 里（v0.1）：
//   core 是纯 JS（零宿主依赖），直接 `import` 即可复用全部 28 款游戏的解析器；
//   配置 / 快照 / 可见性逻辑已抽到 `packages/engine/`，与最终外壳技术栈无关。
//
// 用法：
//   npm run refresh                      → ../data/snapshot.json
//   npm run sample                       → ../data/sample-snapshot.json（示例快照，随仓库提交）
//   node refresh.mjs --out ../data/x.json --config ./config.json --only genshin,hsr
//   node refresh.mjs --selfcheck        → 只跑解析器自检，不写文件
//
// 配置：默认读取 `--out` 同目录的 config.json（不存在则用默认配置）；
//       显式 `--config <file>` 可指定。配置 schema 见 docs/07-壳无关基础层.md。
//
// 退出码：0 正常；1 抓取抛错；2 一轮全失败（okCount = 0）。

import { createRequire } from "node:module";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createEngine } from "gacha-calendar-core";
import { createNodeTransport } from "../packages/engine/transport.mjs";
import { buildSnapshot } from "../packages/engine/snapshot.mjs";
import {
	loadConfigFile,
	normalizeConfig,
	toEngineSettings,
	DEFAULT_CONFIG,
} from "../packages/engine/config.mjs";

const HERE = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);

// ---------------------------------------------------------------- CLI

function parseArgs(argv) {
	const out = {
		out: resolve(HERE, "..", "data", "snapshot.json"),
		config: null,
		only: null,
		selfcheck: false,
	};
	for (let i = 0; i < argv.length; i++) {
		const a = argv[i];
		if (a === "--out") {
			const v = argv[++i];
			out.out = isAbsolute(v) ? v : resolve(process.cwd(), v);
		} else if (a === "--config") {
			out.config = resolve(process.cwd(), argv[++i]);
		} else if (a === "--only") {
			out.only = String(argv[++i] || "")
				.split(",")
				.map((s) => s.trim())
				.filter(Boolean);
		} else if (a === "--selfcheck") {
			out.selfcheck = true;
		} else if (a === "-h" || a === "--help") {
			console.log("用法：node refresh.mjs [--out <file>] [--config <json>] [--only id1,id2] [--selfcheck]");
			process.exit(0);
		}
	}
	return out;
}

export function coreVersion() {
	try {
		const pkg = require.resolve("gacha-calendar-core/package.json");
		return JSON.parse(readFileSync(pkg, "utf8")).version || "unknown";
	} catch {
		return "unknown";
	}
}

// ---------------------------------------------------------------- 主流程

const args = parseArgs(process.argv.slice(2));
const outDir = dirname(args.out);
const defaultConfigPath = join(outDir, "config.json");
const configPath = args.config || (existsSync(defaultConfigPath) ? defaultConfigPath : null);
let config = configPath ? loadConfigFile(configPath) : normalizeConfig(DEFAULT_CONFIG);

let store = new Map(Object.entries(toEngineSettings(config)));
const storage = {
	async get(key) {
		return store.get(key);
	},
	async set(key, value) {
		store.set(key, value);
	}
};

const engine = createEngine({
	transport: createNodeTransport(),
	storage,
	now: () => Date.now(),
});

const startedAt = Date.now();

function applyOnlyFilter(allGames) {
	if (!args.only || args.only.length === 0) return;
	const keep = new Set(args.only);
	const hidden = new Set(config.hidden);
	for (const game of allGames) if (!keep.has(game.id)) hidden.add(game.id);
	config = normalizeConfig({ ...config, hidden: [...hidden] });
	store = new Map(Object.entries(toEngineSettings(config)));
}

async function main() {
	const gamesBefore = await engine.listGames();
	applyOnlyFilter(gamesBefore);

	if (args.selfcheck) {
		const t0 = Date.now();
		const report = await engine.selfCheck();
		console.log(`自检 ${report.total} 个条目，用时 ${((Date.now() - t0) / 1000).toFixed(1)}s`);
		console.log(
			`汇总：ok=${report.summary.ok} nomatch=${report.summary.nomatch} down=${report.summary.down} unconfigured=${report.summary.unconfigured ?? 0}`,
		);
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

	const games = await engine.listGames();
	const sources = engine.__test?.SOURCES || [];
	const snapshot = buildSnapshot({
		games,
		result,
		config,
		sources,
		coreVersion: coreVersion(),
		now: Date.now(),
	});

	mkdirSync(outDir, { recursive: true });
	writeFileSync(args.out, JSON.stringify(snapshot, null, "\t") + "\n", "utf8");

	const visible = snapshot.games.filter((g) => !g.hidden);
	const records = result.games || {};
	const okCount = visible.filter((g) => {
		const r = records[g.id];
		if (!r || r.skipped) return false;
		return !(r.gachaFail && r.gachaFail.kind === "down") && !(r.eventFail && r.eventFail.kind === "down");
	}).length;
	const elapsed = ((Date.now() - startedAt) / 1000).toFixed(1);

	console.log(
		`抓取完成：${visible.length} 个可见条目，成功 ${okCount}/${visible.length}，用时 ${elapsed}s`,
	);
	console.log(
		`snapshot → ${args.out}（core ${snapshot.coreVersion}${configPath ? "，config " + configPath : "，默认配置"}）`,
	);
	for (const g of visible) {
		const r = records[g.id];
		const fail = [r?.gachaFail, r?.eventFail].filter((f) => f && f.kind === "down").length;
		const end = r ? soonestEnd(r) : 0;
		const tail = end ? `（最近结束 ${fmtRemaining(end - Date.now())}）` : "";
		console.log(`  ${g.name.padEnd(10, " ")} ${(r?.banner || "—").slice(0, 18)} ${tail}${fail ? ` [${fail} 处报错]` : ""}`);
	}

	if (okCount === 0) {
		console.error("一轮全部失败：请检查网络或稍后重试。");
		process.exit(2);
	}
}

/** 从 Result JSON 的展示字符串里估"最近结束时刻"，仅用于命令行摘要。 */
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

function fmtRemaining(ms) {
	if (ms <= 0) return "已结束";
	const d = Math.floor(ms / 86400000);
	const h = Math.floor((ms % 86400000) / 3600000);
	const m = Math.floor((ms % 3600000) / 60000);
	if (d > 0) return `${d}天${h}小时`;
	if (h > 0) return `${h}小时${m}分`;
	return `${m}分`;
}

main().catch((err) => {
	console.error(err && err.stack ? err.stack : err);
	process.exit(1);
});
