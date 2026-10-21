#!/usr/bin/env node
// 壳无关 CLI：不启动任何 UI，直接操作 dataDir 里的 config/cache/snapshot。
// 用途：在 Windows 外壳还没做设置页之前，先用命令行验证配置、刷新、诊断。
//
// 用法：
//   node packages/engine/cli.mjs --data-dir data status
//   node packages/engine/cli.mjs config get
//   node packages/engine/cli.mjs config set --json '{"hidden":["genshin"]}'
//   node packages/engine/cli.mjs refresh            # 强制刷新
//   node packages/engine/cli.mjs refresh --if-due   # 按调度/退避决定
//   node packages/engine/cli.mjs diagnostics
//   node packages/engine/cli.mjs validate           # 只读校验，退出码 2 = 不合法
//   node packages/engine/cli.mjs info
//
// 输出均为 JSON（方便脚本/AI 消费）；日志写 stderr。

import fs from "node:fs";
import path from "node:path";
import { createFileService } from "./service.mjs";

function parseArgs(argv) {
	const out = {
		dataDir: process.env.GACHA_CALENDAR_DATA_DIR || path.resolve("data"),
		json: null,
		file: null,
		ifDue: false,
		positional: [],
	};
	for (let i = 0; i < argv.length; i++) {
		const a = argv[i];
		if (a === "--data-dir") out.dataDir = path.resolve(argv[++i]);
		else if (a === "--json") out.json = argv[++i];
		else if (a === "--file") out.file = path.resolve(argv[++i]);
		else if (a === "--if-due") out.ifDue = true;
		else if (a === "-h" || a === "--help") out.help = true;
		else out.positional.push(a);
	}
	return out;
}

function print(value) {
	process.stdout.write(JSON.stringify(value, null, 2) + "\n");
}

function summarizeSnapshot(snapshot) {
	if (!snapshot) return null;
	const visible = (snapshot.games || []).filter((g) => !g.hidden);
	const records = snapshot.result?.games || {};
	const ok = visible.filter((g) => {
		const r = records[g.id];
		if (!r || r.skipped) return false;
		return r.gachaFail?.kind !== "down" && r.eventFail?.kind !== "down";
	}).length;
	return {
		generatedAt: snapshot.generatedAt,
		coreVersion: snapshot.coreVersion,
		visible: visible.length,
		ok,
		hidden: (snapshot.games || []).length - visible.length,
		resultRefreshedAt: snapshot.result?.refreshedAt || 0,
	};
}

function readConfigInput(args) {
	if (args.json) return JSON.parse(args.json);
	if (args.file) return JSON.parse(fs.readFileSync(args.file, "utf8"));
	throw new Error("config set 需要 --json '<json>' 或 --file <path>");
}

async function main() {
	const args = parseArgs(process.argv.slice(2));
	const [command, sub] = args.positional;
	const service = createFileService({ dataDir: args.dataDir });

	if (args.help || !command) {
		process.stdout.write(
			[
				"用法：node packages/engine/cli.mjs [--data-dir <dir>] <command>",
				"  status",
				"  entries",
				"  show <id> / hide <id>",
				"  order <id1,id2,...>",
				"  remove <id> / restore <id>",
				"  config get | config set --json '<json>' [--file <path>]",
				"  refresh [--if-due]",
				"  diagnostics",
				"  validate",
				"  info",
				"",
			].join("\n"),
		);
		return;
	}

	switch (command) {
		case "status":
			print(service.getStatus());
			return;
		case "entries":
			print(await service.listEntries());
			return;
		case "show":
		case "hide": {
			const id = args.positional[1];
			if (!id) throw new Error(`${command} 需要条目 id`);
			print(service.setEntryVisibility(id, command === "show"));
			return;
		}
		case "order": {
			const order = (args.positional[1] || "").split(",").map((s) => s.trim()).filter(Boolean);
			print(service.setOrder(order));
			return;
		}
		case "remove": {
			const id = args.positional[1];
			if (!id) throw new Error("remove 需要条目 id");
			print(await service.removeEntry(id));
			return;
		}
		case "restore": {
			const id = args.positional[1];
			if (!id) throw new Error("restore 需要条目 id");
			print(service.restoreEntry(id));
			return;
		}
		case "config": {
			if (sub === "get" || !sub) {
				print(service.getConfig());
				return;
			}
			if (sub === "set") {
				print(service.updateConfig(readConfigInput(args)));
				return;
			}
			throw new Error("config 支持 get / set");
		}
		case "refresh": {
			if (args.ifDue) {
				const result = await service.refreshIfDue();
				print({
					refreshed: result.refreshed,
					reason: result.reason,
					error: result.error || null,
					status: result.status,
					snapshot: summarizeSnapshot(result.snapshot),
				});
				if (result.reason === "error") process.exitCode = 1;
				return;
			}
			const snapshot = await service.refresh();
			print(summarizeSnapshot(snapshot));
			return;
		}
		case "diagnostics":
			print(service.getDiagnostics());
			return;
		case "validate": {
			const report = service.getDiagnostics();
			print({ ok: report.ok, config: report.config.errors, snapshot: report.snapshot, warnings: report.warnings });
			if (!report.ok) process.exitCode = 2;
			return;
		}
		case "info":
			print({
				dataDir: args.dataDir,
				coreVersion: service.coreVersion,
				paths: service.paths,
				status: service.getStatus(),
			});
			return;
		default:
			throw new Error("未知命令：" + command);
	}
}

main().catch((err) => {
	process.stderr.write(String(err?.stack || err) + "\n");
	process.exitCode = 1;
});
