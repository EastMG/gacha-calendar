#!/usr/bin/env node
// 把 snapshot 渲染成 view-model JSON，用于无 UI 调试 / 跨语言对照。
//
// 用法：
//   node packages/viewmodel/cli.mjs --snapshot data/sample-snapshot.json
//   node packages/viewmodel/cli.mjs --snapshot data/snapshot.json --now 2026-10-21T06:00:00+08:00 --dock-items 2

import fs from "node:fs";
import path from "node:path";
import { buildViewModel } from "./view-model.mjs";

function parseArgs(argv) {
	const out = {
		snapshot: path.resolve("data/snapshot.json"),
		now: Date.now(),
		dockItems: 3,
		isSample: false,
	};
	for (let i = 0; i < argv.length; i++) {
		const a = argv[i];
		if (a === "--snapshot") out.snapshot = path.resolve(argv[++i]);
		else if (a === "--now") {
			const v = argv[++i];
			const n = Number(v);
			out.now = Number.isFinite(n) ? n : Date.parse(v);
		} else if (a === "--dock-items") out.dockItems = Number(argv[++i]);
		else if (a === "--sample") out.isSample = true;
		else if (a === "-h" || a === "--help") out.help = true;
	}
	return out;
}

const args = parseArgs(process.argv.slice(2));
if (args.help) {
	process.stdout.write(
		"用法：node packages/viewmodel/cli.mjs [--snapshot <file>] [--now <ISO|ms>] [--dock-items 1-6] [--sample]\n",
	);
	process.exit(0);
}
if (!Number.isFinite(args.now)) {
	process.stderr.write("--now 不是合法时间\n");
	process.exit(1);
}

try {
	const snapshot = JSON.parse(fs.readFileSync(args.snapshot, "utf8"));
	const vm = buildViewModel({ snapshot, now: args.now, isSample: args.isSample, dockItems: args.dockItems });
	process.stdout.write(JSON.stringify(vm, null, 2) + "\n");
} catch (err) {
	process.stderr.write(String(err?.stack || err) + "\n");
	process.exit(1);
}
