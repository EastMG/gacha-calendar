// 壳无关的 Node 传输层：给 core 的 transport 契约提供参考实现。
//
// core 只要求两个方法：
//   fetchRaw(url, opts) → WHATWG Response 形态
//   fetchViaProxy(url, { referer, headers, body, contentType }) → 目标站原始 body 字符串
//
// 在浏览器里 fetchRaw 用原生 fetch、fetchViaProxy 走 background/代理；
// 在 Node / Tauri sidecar 里两者都直接用 undici 的 fetch + 自定义头。
// WPF 如果坚持原生 C#，则需要用 HttpClient 实现同样两个方法（协议语义见本文件）。

export const DEFAULT_USER_AGENT =
	"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36";

/**
 * @param {{ userAgent?: string, fetchImpl?: typeof fetch }} [options]
 */
export function createNodeTransport(options = {}) {
	const userAgent = options.userAgent || DEFAULT_USER_AGENT;
	const doFetch = options.fetchImpl || fetch;

	return {
		/** 直连：core 已通过 rawHeaders() 带上 Accept / Referer。 */
		async fetchRaw(url, opts = {}) {
			return doFetch(url, {
				headers: opts.headers,
				signal: opts.signal,
				redirect: "follow",
			});
		},

		/** 经代理：桌面端直接发原生请求；返回原始 body 字符串（不是 {status, body}）。 */
		async fetchViaProxy(url, { referer, headers, body, contentType } = {}) {
			const merged = Object.assign({ "User-Agent": userAgent, Accept: "application/json, text/html, */*" }, headers || {});
			if (referer) merged.Referer = referer;
			const init = { headers: merged, redirect: "follow" };
			if (body !== undefined) {
				init.method = "POST";
				merged["Content-Type"] = contentType || "application/json; charset=utf-8";
				init.body = typeof body === "string" ? body : JSON.stringify(body);
			}
			const res = await doFetch(url, init);
			if (!res.ok) throw new Error("HTTP " + res.status);
			return await res.text();
		},
	};
}
