// 壳无关的引擎服务：core + 文件存储（config.json / cache.json / snapshot.json）。
//
// 用途：
//   1) 给 Node 参考 Host（host.mjs）使用；
//   2) 给测试使用（可注入假 engine）；
//   3) 给未来任何外壳当"服务层契约"参考 —— WPF / Tauri 都可以按同样语义实现。
//
// 注意：这层用 Node 的 fs，不是浏览器代码；浏览器/WebView2 外壳需要把 storage 换成自己的实现。

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { createEngine as defaultCreateEngine } from "gacha-calendar-core";
import {
	loadConfigFile,
	saveConfigFile,
	toEngineSettings,
	normalizeConfig,
	mergeConfig,
	validateConfig,
	writeFileAtomic,
} from "./config.mjs";
import { buildSnapshot } from "./snapshot.mjs";
import { createNodeTransport } from "./transport.mjs";

const CONFIG_KEYS = new Set([
	"autoRefresh",
	"refreshMinutes",
	"order",
	"hidden",
	"shown",
	"removed",
	"customEntries",
	"customUrls",
	"customEventUrls",
	"gameColors",
]);

const CACHE_KEYS = new Set(["lastData", "lastRefresh", "lastSource", "lastVersion"]);

function readJson(file, fallback) {
	try {
		return JSON.parse(fs.readFileSync(file, "utf8"));
	} catch {
		return fallback;
	}
}

function readCoreVersion() {
	try {
		const req = createRequire(import.meta.url);
		const pkg = req.resolve("gacha-calendar-core/package.json");
		return JSON.parse(fs.readFileSync(pkg, "utf8")).version || "unknown";
	} catch {
		return "unknown";
	}
}

/**
 * @param {object} options
 * @param {string} options.dataDir 数据目录（config.json / cache.json / snapshot.json）
 * @param {object} [options.transport] core transport；默认 Node fetch
 * @param {() => number} [options.now]
 * @param {Function} [options.createEngine] 测试可注入
 * @param {string} [options.coreVersion]
 */
export function createFileService(options) {
	if (!options || !options.dataDir) throw new Error("createFileService 需要 dataDir");

	const dataDir = path.resolve(options.dataDir);
	fs.mkdirSync(dataDir, { recursive: true });
	const paths = {
		config: path.join(dataDir, "config.json"),
		cache: path.join(dataDir, "cache.json"),
		snapshot: path.join(dataDir, "snapshot.json"),
	};

	const coreVersion = options.coreVersion || readCoreVersion();
	const now = options.now || (() => Date.now());
	const transport = options.transport || createNodeTransport();
	const createEngine = options.createEngine || defaultCreateEngine;

	let config = loadConfigFile(paths.config);
	let cache = readJson(paths.cache, {});
	if (!cache || typeof cache !== "object" || Array.isArray(cache)) cache = {};

	const writeCache = () => {
		writeFileAtomic(paths.cache, JSON.stringify(cache, null, "\t") + "\n");
	};

	const storage = {
		async get(key) {
			if (CONFIG_KEYS.has(key)) return toEngineSettings(config)[key];
			if (CACHE_KEYS.has(key)) return cache[key];
			return undefined;
		},
		async set(key, value) {
			if (CACHE_KEYS.has(key)) {
				cache[key] = value;
				writeCache();
				return;
			}
			// core 只会写缓存键；配置由外壳通过 updateConfig() 写，避免 JSON 字符串/对象形状混淆
			if (CONFIG_KEYS.has(key)) throw new Error(`拒绝由 core 直接写配置键：${key}`);
		},
	};

	const engine = createEngine({ transport, storage, now });

	const writeSnapshot = (snapshot) => {
		writeFileAtomic(paths.snapshot, JSON.stringify(snapshot, null, "\t") + "\n");
	};

	const service = {
		paths,
		coreVersion,
		getConfig() {
			return normalizeConfig(config);
		},
		/** @param {object|string} patch 对象补丁；字符串会先按 JSON 解析 */
		updateConfig(patch) {
			const value = typeof patch === "string" ? JSON.parse(patch) : patch;
			const errors = validateConfig(value);
			if (errors.length) {
				const err = new Error("配置校验失败：" + errors.join("；"));
				err.code = "invalid-config";
				throw err;
			}
			const next = mergeConfig(config, value);
			config = saveConfigFile(paths.config, next);
			return normalizeConfig(config);
		},
		async listGames() {
			return engine.listGames();
		},
		async getCached() {
			return engine.getCached();
		},
		async selfCheck() {
			return engine.selfCheck();
		},
		async refresh() {
			const result = await engine.refresh();
			const games = await engine.listGames();
			const sources = engine.__test?.SOURCES || [];
			const snapshot = buildSnapshot({ games, result, config, sources, coreVersion, now: now() });
			writeSnapshot(snapshot);
			return snapshot;
		},
		/** 最近一次写入的 snapshot；没有则返回 null。 */
		getSnapshot() {
			return readJson(paths.snapshot, null);
		},
	};

	return service;
}
