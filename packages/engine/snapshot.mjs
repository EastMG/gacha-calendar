// 壳无关的快照构建：core 的 listGames/refresh 结果 + 用户配置 → snapshot.json。
//
// 任何外壳（WPF / Tauri / Node CLI）都应当产出同一种 snapshot：
//   { schemaVersion, generatedAt, coreVersion, config, games[], result }
// 其中 games[] 的 hidden 已按三态规则算好，UI 不需要再理解 defaultHidden。

import { normalizeConfig, sortByOrder, visibilityOf } from "./config.mjs";

export const SNAPSHOT_SCHEMA_VERSION = 1;

/**
 * @param {object} input
 * @param {{id:string,name:string,defaults?:object}[]} input.games
 * @param {object} input.result core 的 Result JSON（schemaVersion=1）
 * @param {object} input.config 用户配置
 * @param {{id:string, defaultHidden?:boolean}[]} [input.sources] core 的来源表
 * @param {string} [input.coreVersion]
 * @param {number} [input.now]
 */
export function buildSnapshot({ games, result, config, sources = [], coreVersion = "unknown", now = Date.now() }) {
	const cfg = normalizeConfig(config);
	const isHidden = visibilityOf(games, cfg, sources);
	const ordered = sortByOrder(games, cfg.order);

	return {
		schemaVersion: SNAPSHOT_SCHEMA_VERSION,
		generatedAt: now,
		coreVersion,
		config: cfg,
		games: ordered.map((game) => ({
			id: game.id,
			name: game.name,
			icon: game.icon || "",
			hidden: isHidden(game.id),
			defaults: game.defaults || {
				banner: "",
				roles: "",
				bannerDates: "",
				event: "",
				eventDates: "",
			},
		})),
		result: result || { schemaVersion: 1, refreshedAt: 0, parserVersions: {}, games: {} },
	};
}
