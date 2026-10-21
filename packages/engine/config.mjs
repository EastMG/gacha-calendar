// 壳无关的配置层：读写 config.json、可见性规则、顺序、传给 core 的 storage 形状。
//
// 这一层不依赖 Windows / WPF / WebView2 / Tauri，只依赖 Node 的文件系统；
// 外壳（无论最终用 WPF 还是 Tauri）都可以复用它，或者按同一份 schema 实现自己的 UI。
//
// 注意：core 的 storage 里 `customUrls` / `customEventUrls` / `customEntries` / `gameColors`
// 是 **JSON 字符串**（历史约定）；而 config.json 对外用更友好的对象/数组表示。
// 两者之间的转换统一放在 `toEngineSettings()`，不要在各处手写 JSON.stringify。

import fs from "node:fs";
import path from "node:path";

export const CONFIG_SCHEMA_VERSION = 1;

/** 用户可见的默认配置。refreshMinutes 默认 1440（1 天），与 core 的 DEFAULT_SETTINGS 一致。 */
export const DEFAULT_CONFIG = Object.freeze({
	autoRefresh: true,
	refreshMinutes: 1440,
	order: [],
	hidden: [],
	shown: [],
	removed: [],
	customEntries: [],
	customUrls: {},
	customEventUrls: {},
	gameColors: {},
});

const ARRAY_KEYS = ["order", "hidden", "shown", "removed"];
const OBJECT_KEYS = ["customUrls", "customEventUrls", "gameColors"];

function isPlainObject(value) {
	return value != null && typeof value === "object" && !Array.isArray(value);
}

function uniqueStrings(value) {
	if (!Array.isArray(value)) return [];
	const out = [];
	const seen = new Set();
	for (const item of value) {
		if (typeof item !== "string") continue;
		const s = item.trim();
		if (!s || seen.has(s)) continue;
		seen.add(s);
		out.push(s);
	}
	return out;
}

function plainObject(value) {
	if (!isPlainObject(value)) return {};
	const out = {};
	for (const [k, v] of Object.entries(value)) {
		if (typeof k === "string" && k) out[k] = v;
	}
	return out;
}

/**
 * 归一化配置：补默认值、去重、裁剪刷新间隔、保留未知字段（向前兼容）。
 * 对损坏输入不抛错（除 refreshMinutes 外全部可恢复），保证"配置坏了应用还能起来"。
 */
export function normalizeConfig(input = {}) {
	const src = isPlainObject(input) ? input : {};
	const out = { ...src };
	out.schemaVersion = CONFIG_SCHEMA_VERSION;

	for (const key of ARRAY_KEYS) out[key] = uniqueStrings(src[key]);
	for (const key of OBJECT_KEYS) out[key] = plainObject(src[key]);
	out.customEntries = Array.isArray(src.customEntries)
		? src.customEntries.filter(isPlainObject)
		: [];

	out.autoRefresh = src.autoRefresh !== false;
	const minutes = Number(src.refreshMinutes);
	out.refreshMinutes = Number.isFinite(minutes)
		? Math.min(42 * 24 * 60, Math.max(60, Math.round(minutes)))
		: DEFAULT_CONFIG.refreshMinutes;

	return out;
}

/** 合并补丁（数组/对象整段替换，不做深合并——避免"删不掉"的交互 bug）。 */
export function mergeConfig(base, patch) {
	return normalizeConfig({ ...normalizeConfig(base), ...(isPlainObject(patch) ? patch : {}) });
}

/** 转成 core 的 storage 值（注意 JSON 字符串字段）。 */
export function toEngineSettings(config) {
	const cfg = normalizeConfig(config);
	return {
		autoRefresh: cfg.autoRefresh,
		refreshMinutes: cfg.refreshMinutes,
		order: cfg.order,
		hidden: cfg.hidden,
		shown: cfg.shown,
		removed: cfg.removed,
		customEntries: JSON.stringify(cfg.customEntries),
		customUrls: JSON.stringify(cfg.customUrls),
		customEventUrls: JSON.stringify(cfg.customEventUrls),
		gameColors: JSON.stringify(cfg.gameColors),
	};
}

/**
 * 可见性三态（与 core / 浏览器扩展同口径）：
 *   hidden 包含 id → 隐藏
 *   条目 defaultHidden 且 shown 不含 id → 隐藏
 *   否则 → 显示
 *
 * @param {{id:string}[]} games
 * @param {object} config
 * @param {{id:string, defaultHidden?:boolean}[]} sources core 的来源表（engine.__test.SOURCES）
 * @returns {(id:string)=>boolean}
 */
export function visibilityOf(games, config, sources = []) {
	const cfg = normalizeConfig(config);
	const byId = new Map((sources || []).map((s) => [s.id, s]));
	const hidden = new Set(cfg.hidden);
	const shown = new Set(cfg.shown);
	return (id) => hidden.has(id) || (!!byId.get(id)?.defaultHidden && !shown.has(id));
}

/** 按 order 排序；未列出的保持原序接在后面。 */
export function sortByOrder(games, order = []) {
	const list = Array.isArray(games) ? games : [];
	const ids = uniqueStrings(order);
	if (ids.length === 0) return [...list];
	const byId = new Map(list.map((g) => [g.id, g]));
	const out = [];
	for (const id of ids) if (byId.has(id)) out.push(byId.get(id));
	for (const game of list) if (!out.includes(game)) out.push(game);
	return out;
}

/** 设置某个条目显示/隐藏，返回补丁（同时维护 hidden 与 shown）。 */
export function visibilityPatch(config, id, visible, defaultHidden = false) {
	const cfg = normalizeConfig(config);
	const hidden = new Set(cfg.hidden);
	const shown = new Set(cfg.shown);
	if (visible) {
		hidden.delete(id);
		if (defaultHidden) shown.add(id);
	} else {
		shown.delete(id);
		hidden.add(id);
	}
	return { hidden: [...hidden], shown: [...shown] };
}

/** 简单校验，返回人话错误列表；空数组表示通过。 */
export function validateConfig(input) {
	const errors = [];
	if (!isPlainObject(input)) return ["配置必须是 JSON 对象"];
	if (input.order != null && !Array.isArray(input.order)) errors.push("order 必须是字符串数组");
	if (input.refreshMinutes != null) {
		const minutes = Number(input.refreshMinutes);
		if (!Number.isFinite(minutes)) errors.push("refreshMinutes 必须是数字");
		else if (minutes < 60) errors.push("refreshMinutes 不能小于 60（1 小时）");
		else if (minutes > 42 * 24 * 60) errors.push("refreshMinutes 不能超过 42 天");
	}
	return errors;
}

/** 读 config.json；文件不存在返回默认配置；JSON 坏了抛错（调用方决定是否回退）。 */
export function loadConfigFile(file) {
	try {
		const text = fs.readFileSync(file, "utf8");
		return normalizeConfig(JSON.parse(text));
	} catch (err) {
		if (err && err.code === "ENOENT") return normalizeConfig(DEFAULT_CONFIG);
		throw new Error(`读取配置失败（${file}）：${err.message}`);
	}
}

/** 原子写：临时文件 + rename，避免写一半留下坏 JSON。 */
export function writeFileAtomic(file, text) {
	fs.mkdirSync(path.dirname(file), { recursive: true });
	const tmp = `${file}.${process.pid}.${Date.now()}.tmp`;
	fs.writeFileSync(tmp, text, "utf8");
	try {
		fs.renameSync(tmp, file);
	} catch {
		// Windows 上目标已存在时 rename 可能失败：退化为覆盖写
		fs.copyFileSync(tmp, file);
		fs.unlinkSync(tmp);
	}
}

export function saveConfigFile(file, config) {
	const cfg = normalizeConfig(config);
	writeFileAtomic(file, JSON.stringify(cfg, null, "\t") + "\n");
	return cfg;
}
