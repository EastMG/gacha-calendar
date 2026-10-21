// 壳无关的引擎 Host：逐行 JSON 协议（newline-delimited JSON）包住 core。
//
// 用途：外壳（WPF / Tauri / 未来的 Kotlin/Swift）不直接 import core，
// 而是启动这个 Node 进程，用 stdin/stdout 发命令。等外壳技术栈定了：
//   - Tauri：可以直接用 Rust/Tauri sidecar 启动它，或把同样协议搬进 WebView2；
//   - WPF：可以启动它，或用 C# 实现同样的 JSON 协议；
// 协议本身不变，因此这是"外壳未定"阶段可以先把基础功能做完的原因。
//
// 协议（每行一个 JSON）：
//   请求  { "id": 1, "method": "refresh", "params": {} }
//   响应  { "id": 1, "ok": true, "result": { ... } }
//   错误  { "id": 1, "ok": false, "error": { "code": "...", "message": "..." } }
//
// 方法：hello / getConfig / updateConfig / listGames / getCached /
//       refresh / getSnapshot / selfCheck / shutdown

import path from "node:path";
import { fileURLToPath } from "node:url";
import readline from "node:readline";
import { createFileService } from "./service.mjs";

export const HOST_PROTOCOL_VERSION = 1;

/**
 * 处理一个请求（不涉及 stdin/stdout，方便单测）。
 */
export async function dispatch(service, method, params = {}) {
	switch (method) {
		case "hello":
			return {
				protocol: HOST_PROTOCOL_VERSION,
				coreVersion: service.coreVersion,
				dataDir: service.paths?.config ? path.dirname(service.paths.config) : undefined,
			};
		case "getConfig":
			return service.getConfig();
		case "updateConfig": {
			const patch = params && typeof params === "object" && "patch" in params ? params.patch : params;
			return service.updateConfig(patch);
		}
		case "listGames":
			return service.listGames();
		case "getCached":
			return service.getCached();
		case "refresh":
			return service.refresh();
		case "getSnapshot":
			return service.getSnapshot();
		case "selfCheck":
			return service.selfCheck();
		case "shutdown":
			return { bye: true };
		default: {
			const err = new Error("未知方法：" + method);
			err.code = "unknown-method";
			throw err;
		}
	}
}

/**
 * @param {object} options
 * @param {object} options.service
 * @param {NodeJS.ReadableStream} [options.input]
 * @param {NodeJS.WritableStream} [options.output]
 * @param {() => void} [options.onShutdown]
 */
export function startHost({ service, input = process.stdin, output = process.stdout, onShutdown }) {
	const rl = readline.createInterface({ input, crlfDelay: Infinity });
	let chain = Promise.resolve();

	const send = (message) => {
		output.write(JSON.stringify(message) + "\n");
	};

	rl.on("line", (line) => {
		const text = String(line || "").trim();
		if (!text) return;
		chain = chain.then(async () => {
			let request;
			try {
				request = JSON.parse(text);
			} catch {
				send({ id: null, ok: false, error: { code: "bad-json", message: "请求不是合法 JSON" } });
				return;
			}
			const id = request?.id ?? null;
			try {
				const result = await dispatch(service, request?.method, request?.params || {});
				send({ id, ok: true, result });
				if (request?.method === "shutdown") {
					rl.close();
					if (onShutdown) onShutdown();
				}
			} catch (err) {
				send({
					id,
					ok: false,
					error: { code: err?.code || "error", message: err?.message || String(err) },
				});
			}
		});
	});

	return { close: () => rl.close() };
}

function parseArgs(argv) {
	const out = { dataDir: process.env.GACHA_CALENDAR_DATA_DIR || path.resolve("data") };
	for (let i = 0; i < argv.length; i++) {
		if (argv[i] === "--data-dir") out.dataDir = path.resolve(argv[++i]);
		else if (argv[i] === "--help" || argv[i] === "-h") out.help = true;
	}
	return out;
}

const isEntry = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isEntry) {
	const args = parseArgs(process.argv.slice(2));
	if (args.help) {
		process.stdout.write("用法：node packages/engine/host.mjs [--data-dir <dir>]\n");
		process.exit(0);
	}
	const service = createFileService({ dataDir: args.dataDir });
	process.stderr.write(`[gacha-calendar-host] dataDir=${args.dataDir} core=${service.coreVersion}\n`);
	startHost({
		service,
		onShutdown: () => {
			setTimeout(() => process.exit(0), 20);
		},
	});
}
