#!/usr/bin/env node
/**
 * One entry point for MCP hosts: prepare, then run — in a single process.
 *
 * This is what `.mcp.json` points at, so a fresh clone needs no `npm install`
 * and no `npm run build` before Claude can talk to Godot. Opening the project
 * is enough.
 *
 * Why not point .mcp.json at start.cmd / start.sh? An MCP host launches the
 * command directly, not through a shell, so a .cmd is not portable and a .sh is
 * not executable on Windows. A .mjs runs identically everywhere `node` does —
 * and `node` is already required.
 *
 * Nothing here may write to stdout: that is the MCP protocol channel.
 */
import { spawnSync } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));

// Build/install first, synchronously, so the server only starts once it can run.
const prep = spawnSync(process.execPath, [resolve(HERE, "prepare.mjs")], {
  cwd: HERE,
  stdio: ["ignore", "inherit", "inherit"],
});
if (prep.status !== 0) {
  process.stderr.write("[beep-mcp] preparation failed — not starting.\n");
  process.exit(prep.status ?? 1);
}

// Hand this process over to the server. Importing rather than spawning keeps
// stdio wired straight through to the MCP host with no relaying in between.
//
// pathToFileURL is required: on Windows a bare absolute path makes the ESM
// loader read "C:" as a URL scheme and throw ERR_UNSUPPORTED_ESM_URL_SCHEME.
await import(pathToFileURL(resolve(HERE, "dist", "index.js")).href);
