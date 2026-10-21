// 快照 / 配置的只读校验。用于 CLI、Host 诊断和 CI，不修改任何文件。
//
// 目标：在"外壳还没写"的阶段，也能用命令行确认 snapshot 是否符合契约，
// 并把问题定位到具体字段，而不是等 UI 白屏才发现。

import { validateConfig } from "./config.mjs";

const CONTENT_FIELDS = [
	"banner",
	"roles",
	"bannerDates",
	"bannerDatesRaw",
	"bannerHover",
	"event",
	"eventDates",
	"eventDatesRaw",
	"eventHover",
];

function isObject(value) {
	return value != null && typeof value === "object" && !Array.isArray(value);
}

function checkRecord(id, rec, errors, warnings) {
	if (!isObject(rec)) {
		errors.push(`${id}: result.games[${id}] 必须是对象`);
		return;
	}
	for (const field of CONTENT_FIELDS) {
		if (rec[field] != null && typeof rec[field] !== "string") {
			errors.push(`${id}: ${field} 必须是字符串`);
		}
	}
	for (const side of ["gachaFail", "eventFail"]) {
		const fail = rec[side];
		if (fail == null) continue;
		if (!isObject(fail)) {
			errors.push(`${id}: ${side} 必须是 null 或对象`);
			continue;
		}
		if (fail.kind !== "down" && fail.kind !== "nomatch") {
			errors.push(`${id}: ${side}.kind 只能是 down / nomatch`);
		}
		if (fail.reason != null && typeof fail.reason !== "string") {
			errors.push(`${id}: ${side}.reason 必须是字符串`);
		}
	}
	for (const field of ["gachaStale", "eventStale", "skipped"]) {
		if (rec[field] != null && typeof rec[field] !== "boolean") {
			errors.push(`${id}: ${field} 必须是布尔值`);
		}
	}
	if (rec.okAt != null && (!Number.isFinite(rec.okAt) || rec.okAt < 0)) {
		errors.push(`${id}: okAt 必须是非负数字`);
	}
	if (rec.name != null && typeof rec.name !== "string") {
		warnings.push(`${id}: name 不是字符串（可忽略，但建议修）`);
	}
}

/**
 * @returns {{ok:boolean, errors:string[], warnings:string[]}}
 */
export function validateSnapshot(snapshot) {
	const errors = [];
	const warnings = [];
	if (!isObject(snapshot)) return { ok: false, errors: ["snapshot 必须是 JSON 对象"], warnings };

	if (snapshot.schemaVersion !== 1) errors.push("snapshot.schemaVersion 必须为 1");
	if (!Number.isFinite(snapshot.generatedAt) || snapshot.generatedAt < 0) {
		errors.push("snapshot.generatedAt 必须是非负数字");
	}
	if (snapshot.coreVersion != null && typeof snapshot.coreVersion !== "string") {
		errors.push("snapshot.coreVersion 必须是字符串");
	}
	if (snapshot.config != null) {
		const cfgErrors = validateConfig(snapshot.config);
		for (const e of cfgErrors) errors.push("snapshot.config: " + e);
	}

	if (!Array.isArray(snapshot.games)) {
		errors.push("snapshot.games 必须是数组");
	} else {
		const ids = new Set();
		for (const [index, game] of snapshot.games.entries()) {
			const where = `snapshot.games[${index}]`;
			if (!isObject(game)) {
				errors.push(`${where} 必须是对象`);
				continue;
			}
			if (typeof game.id !== "string" || !game.id) errors.push(`${where}.id 不能为空`);
			if (typeof game.name !== "string" || !game.name) errors.push(`${where}.name 不能为空`);
			if (typeof game.hidden !== "boolean") errors.push(`${where}.hidden 必须是布尔值`);
			if (game.defaults != null && !isObject(game.defaults)) errors.push(`${where}.defaults 必须是对象`);
			if (typeof game.id === "string" && game.id) {
				if (ids.has(game.id)) errors.push(`snapshot.games 出现重复 id：${game.id}`);
				ids.add(game.id);
			}
		}
	}

	const result = snapshot.result;
	if (!isObject(result)) {
		errors.push("snapshot.result 必须是对象");
	} else {
		if (result.schemaVersion !== 1) errors.push("result.schemaVersion 必须为 1");
		if (!Number.isFinite(result.refreshedAt) || result.refreshedAt < 0) {
			errors.push("result.refreshedAt 必须是非负数字");
		}
		if (!isObject(result.games)) {
			errors.push("result.games 必须是对象");
		} else {
			for (const [id, rec] of Object.entries(result.games)) checkRecord(id, rec, errors, warnings);
			if (Array.isArray(snapshot.games)) {
				for (const game of snapshot.games) {
					if (game && !game.hidden && isObject(result.games) && !result.games[game.id]) {
						warnings.push(`${game.id}: 可见条目没有 result 记录（首轮未抓或源失败）`);
					}
				}
			}
		}
		if (result.parserVersions != null && !isObject(result.parserVersions)) {
			errors.push("result.parserVersions 必须是对象");
		}
	}

	return { ok: errors.length === 0, errors, warnings };
}
