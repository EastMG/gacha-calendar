// 设置页需要的"条目视图"：把 core 的 listGames() + 来源表 + 用户配置合并成
// 一份不含抓取内容的元信息列表。外壳（WPF / Tauri / CLI）都用它渲染显隐/排序。
//
// 关键点：core 的 listGames().hidden 只反映用户 hidden 数组；defaultHidden 在来源表里。
// 这里把两者与 shown 合并成最终 visible，避免每个外壳各写一遍三态判定。

import { normalizeConfig, visibilityOf } from "./config.mjs";

/**
 * @param {object} input
 * @param {{id:string,name:string,icon?:string,source?:string,eventSource?:string,altSources?:any[],eventAltSources?:any[],custom?:boolean,defaults?:object}[]} input.games
 * @param {{id:string,defaultHidden?:boolean}[]} [input.sources]
 * @param {object} input.config
 */
export function buildEntries({ games, sources = [], config }) {
	const cfg = normalizeConfig(config);
	const isHidden = visibilityOf(games || [], cfg, sources || []);
	const hidden = new Set(cfg.hidden);
	const shown = new Set(cfg.shown);
	const removed = new Set(cfg.removed);
	const sourceById = new Map((sources || []).map((s) => [s.id, s]));

	return (games || []).map((game) => ({
		id: game.id,
		name: game.name,
		icon: game.icon || "",
		source: game.source || "",
		eventSource: game.eventSource || "",
		altSources: game.altSources || [],
		eventAltSources: game.eventAltSources || [],
		custom: !!game.custom,
		defaultHidden: !!sourceById.get(game.id)?.defaultHidden,
		hidden: hidden.has(game.id),
		shown: shown.has(game.id),
		removed: removed.has(game.id),
		visible: !isHidden(game.id),
		defaults: game.defaults || {
			banner: "",
			roles: "",
			bannerDates: "",
			event: "",
			eventDates: "",
		},
	}));
}
