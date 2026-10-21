// 只读诊断：配置文件 / 快照 / 调度状态 / 文件存在性。
// 不联网、不写文件；Host 与 CLI 共用。

import fs from "node:fs";
import { validateConfig } from "./config.mjs";
import { validateSnapshot } from "./validate.mjs";

function fileInfo(file) {
	try {
		const stat = fs.statSync(file);
		return { exists: true, size: stat.size, mtimeMs: stat.mtimeMs };
	} catch {
		return { exists: false, size: 0, mtimeMs: 0 };
	}
}

/**
 * @param {object} input
 * @param {string} input.dataDir
 * @param {object} input.paths
 * @param {string} input.coreVersion
 * @param {object} input.config
 * @param {object|null} input.snapshot
 * @param {object} input.schedule  service.getStatus() 的结果
 */
export function buildDiagnostics({ dataDir, paths, coreVersion, config, snapshot, schedule }) {
	const configErrors = validateConfig(config);
	const snapshotResult = snapshot ? validateSnapshot(snapshot) : { ok: false, errors: ["snapshot.json 不存在"], warnings: [] };

	const files = {
		config: fileInfo(paths.config),
		cache: fileInfo(paths.cache),
		snapshot: fileInfo(paths.snapshot),
	};
	const warnings = [];
	if (!files.cache.exists) warnings.push("cache.json 不存在（首次运行正常）");
	if (!files.snapshot.exists) warnings.push("snapshot.json 不存在（尚未 refresh）");

	const checks = [
		{ id: "config", ok: configErrors.length === 0, detail: configErrors.join("；") || "ok" },
		{ id: "snapshot", ok: snapshotResult.ok, detail: snapshotResult.errors.join("；") || "ok" },
		{ id: "snapshot.warnings", ok: snapshotResult.warnings.length === 0, detail: snapshotResult.warnings.join("；") || "ok", level: "warn" },
		{ id: "files", ok: files.snapshot.exists, detail: files.snapshot.exists ? "snapshot 存在" : "snapshot 缺失", level: "warn" },
	];

	return {
		ok: configErrors.length === 0 && snapshotResult.ok,
		dataDir,
		coreVersion,
		files,
		config: { value: config, errors: configErrors },
		snapshot: snapshotResult,
		schedule,
		warnings,
		checks,
	};
}
