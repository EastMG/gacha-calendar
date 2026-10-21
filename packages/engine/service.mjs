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
	validateConfig as validateConfigValue,
	importLegacyConfig,
	visibilityPatch,
	writeFileAtomic,
} from "./config.mjs";
import { buildSnapshot } from "./snapshot.mjs";
import { buildEntries } from "./entries.mjs";
import { buildDiagnostics } from "./diagnostics.mjs";
import { validateSnapshot as validateSnapshotValue } from "./validate.mjs";
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

	// 调度状态：与 core 的 lastRefresh 分开存。core 的 lastRefresh 只表示"抓过一轮"，
	// 不一定代表成功；退避与"下次该不该刷"需要自己的失败计数。
	const DEFAULT_REFRESH_STATE = {
		lastAttemptAt: 0,
		lastSuccessAt: 0,
		failures: 0,
		nextAllowedAt: 0,
		lastError: null,
	};
	let refreshState = {
		...DEFAULT_REFRESH_STATE,
		...(cache.refreshState && typeof cache.refreshState === "object" ? cache.refreshState : {}),
	};
	if (!refreshState.lastSuccessAt && Number(cache.lastRefresh) > 0) {
		// 迁移：老 cache 里只有 core 的 lastRefresh，先当作上次成功时间
		refreshState.lastSuccessAt = Number(cache.lastRefresh);
	}

	const BACKOFF_BASE_MS = 5 * 60 * 1000;
	const BACKOFF_MAX_MS = 6 * 60 * 60 * 1000;
	const backoffMs = (failures) =>
		Math.min(BACKOFF_MAX_MS, BACKOFF_BASE_MS * Math.pow(3, Math.max(0, failures - 1)));

	const writeCache = () => {
		cache.refreshState = refreshState;
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

	let refreshInFlight = null;

	/** 实际执行一轮刷新（会写 cache / snapshot / 调度状态）。手动和自动共用。 */
	function runRefresh() {
		if (refreshInFlight) return refreshInFlight;
		refreshInFlight = (async () => {
			refreshState.lastAttemptAt = now();
			refreshState.lastError = null;
			try {
				const result = await engine.refresh();
				const games = await engine.listGames();
				const sources = engine.__test?.SOURCES || [];
				const snapshot = buildSnapshot({ games, result, config, sources, coreVersion, now: now() });
				writeSnapshot(snapshot);
				refreshState.failures = 0;
				refreshState.lastSuccessAt = now();
				refreshState.nextAllowedAt = 0;
				writeCache();
				return snapshot;
			} catch (err) {
				refreshState.failures += 1;
				refreshState.lastError = err?.message || String(err);
				refreshState.nextAllowedAt = now() + backoffMs(refreshState.failures);
				writeCache();
				throw err;
			} finally {
				refreshInFlight = null;
			}
		})();
		return refreshInFlight;
	}

	/** 调度状态（外壳只读它来决定"现在要不要触发刷新"）。 */
	function getStatus() {
		const at = now();
		const intervalMs = config.refreshMinutes * 60 * 1000;
		const lastSuccessAt = Number(refreshState.lastSuccessAt) || 0;
		const nextDueAt = lastSuccessAt ? lastSuccessAt + intervalMs : 0;
		const backoffActive = refreshState.nextAllowedAt > at;
		const due =
			!!config.autoRefresh &&
			!refreshInFlight &&
			!backoffActive &&
			(nextDueAt === 0 || at >= nextDueAt);
		return {
			now: at,
			autoRefresh: config.autoRefresh,
			refreshMinutes: config.refreshMinutes,
			lastRefresh: Number(cache.lastRefresh) || 0,
			lastSuccessAt,
			lastAttemptAt: Number(refreshState.lastAttemptAt) || 0,
			nextDueAt,
			nextAllowedAt: Number(refreshState.nextAllowedAt) || 0,
			failures: Number(refreshState.failures) || 0,
			lastError: refreshState.lastError || null,
			backoffActive,
			due,
			refreshInFlight: !!refreshInFlight,
		};
	}

	const service = {
		paths,
		coreVersion,
		getConfig() {
			return normalizeConfig(config);
		},
		/** @param {object|string} patch 对象补丁；字符串会先按 JSON 解析 */
		updateConfig(patch) {
			const value = typeof patch === "string" ? JSON.parse(patch) : patch;
			const errors = validateConfigValue(value);
			if (errors.length) {
				const err = new Error("配置校验失败：" + errors.join("；"));
				err.code = "invalid-config";
				throw err;
			}
			const next = mergeConfig(config, value);
			config = saveConfigFile(paths.config, next);
			return normalizeConfig(config);
		},
		/** 导入旧格式配置（对象字段可能是 JSON 字符串），整体替换当前配置。 */
		importConfig(input) {
			const next = importLegacyConfig(typeof input === "string" ? JSON.parse(input) : input);
			config = saveConfigFile(paths.config, next);
			return normalizeConfig(config);
		},
		async listGames() {
			return engine.listGames();
		},
		/** 设置页用的条目视图：listGames + 来源表 + 显隐三态。 */
		async listEntries() {
			const games = await engine.listGames();
			const sources = engine.__test?.SOURCES || [];
			return buildEntries({ games, sources, config });
		},		/** 显示/隐藏一个条目；自动处理 defaultHidden → shown，避免"勾了不生效"。 */
		setEntryVisibility(id, visible) {
			if (typeof id !== "string" || !id) {
				const err = new Error("setEntryVisibility 需要 id");
				err.code = "invalid-params";
				throw err;
			}
			const sources = engine.__test?.SOURCES || [];
			const defaultHidden = !!sources.find((s) => s.id === id)?.defaultHidden;
			return service.updateConfig(visibilityPatch(config, id, !!visible, defaultHidden));
		},
		/** 设置展示顺序（null / [] = 恢复默认顺序）。 */
		setOrder(order) {
			if (order != null && !Array.isArray(order)) {
				const err = new Error("order 必须是字符串数组");
				err.code = "invalid-config";
				throw err;
			}
			return service.updateConfig({ order: Array.isArray(order) ? order : [] });
		},
		/** 删除条目：自定义条目从 customEntries 移除，内置条目记进 removed。 */
		async removeEntry(id) {
			if (typeof id !== "string" || !id) {
				const err = new Error("removeEntry 需要 id");
				err.code = "invalid-params";
				throw err;
			}
			const games = await engine.listGames();
			const target = games.find((g) => g.id === id);
			const patch = {
				order: config.order.filter((x) => x !== id),
				hidden: config.hidden.filter((x) => x !== id),
				shown: config.shown.filter((x) => x !== id),
			};
			if (target?.custom) {
				patch.customEntries = config.customEntries.filter((e) => e && e.id !== id);
			} else {
				patch.removed = [...new Set([...config.removed, id])];
			}
			return service.updateConfig(patch);
		},
		/** 恢复已删除的内置条目。 */
		restoreEntry(id) {
			if (typeof id !== "string" || !id) {
				const err = new Error("restoreEntry 需要 id");
				err.code = "invalid-params";
				throw err;
			}
			return service.updateConfig({ removed: config.removed.filter((x) => x !== id) });
		},
		async getCached() {
			return engine.getCached();
		},
		async selfCheck() {
			return engine.selfCheck();
		},
		/** 手动刷新：无视调度与退避，失败会抛错（并记录退避状态）。 */
		async refresh() {
			return runRefresh();
		},
		/**
		 * 按配置判断是否该刷；不满足条件时返回 skipped 结果，不联网。
		 * 外壳在启动时/定时器里调用即可，无需自己算 due/backoff。
		 */
		async refreshIfDue() {
			if (refreshInFlight) {
				return { refreshed: false, reason: "in-flight", status: getStatus() };
			}
			const status = getStatus();
			if (!status.autoRefresh) {
				return { refreshed: false, reason: "manual-only", status };
			}
			if (!status.due) {
				return { refreshed: false, reason: status.backoffActive ? "backoff" : "not-due", status };
			}
			try {
				const snapshot = await runRefresh();
				return { refreshed: true, snapshot, status: getStatus() };
			} catch (err) {
				return {
					refreshed: false,
					reason: "error",
					error: { code: err?.code || "refresh-failed", message: err?.message || String(err) },
					status: getStatus(),
				};
			}
		},
		getStatus() {
			return getStatus();
		},
		/** 只读校验当前配置；传值则校验该值。 */
		validateConfig(value) {
			return validateConfigValue(value === undefined ? config : value);
		},
		/** 只读校验当前 snapshot 文件。 */
		validateSnapshot() {
			const snapshot = readJson(paths.snapshot, null);
			return snapshot
				? validateSnapshotValue(snapshot)
				: { ok: false, errors: ["snapshot.json 不存在"], warnings: [] };
		},
		/** 只读诊断（配置 + 快照 + 文件 + 调度状态）。 */
		getDiagnostics() {
			const snapshot = readJson(paths.snapshot, null);
			return buildDiagnostics({
				dataDir,
				paths,
				coreVersion,
				config: normalizeConfig(config),
				snapshot,
				schedule: getStatus(),
			});
		},
		/** 最近一次写入的 snapshot；没有则返回 null。 */
		getSnapshot() {
			return readJson(paths.snapshot, null);
		},
	};

	return service;
}
