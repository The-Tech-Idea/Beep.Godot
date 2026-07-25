/**
 * Make the server runnable: install dependencies if missing or stale, compile if
 * the sources are newer than the build. Idempotent — a second run does nothing.
 *
 * Shared by start.cmd and start.sh so the logic lives in ONE place rather than
 * being written twice in two shell dialects that drift apart.
 *
 * Everything it prints goes to STDERR. When Claude Code launches the server,
 * stdout is the MCP protocol channel; one stray line there corrupts the stream.
 */
import { spawnSync } from "node:child_process";
import { existsSync, statSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const log = (m) => process.stderr.write(`[beep-mcp] ${m}\n`);

/** Newest mtime under a directory, or 0 when it does not exist. */
function newestMtime(dir) {
  if (!existsSync(dir)) return 0;
  let newest = 0;
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, entry.name);
    const t = entry.isDirectory() ? newestMtime(p) : statSync(p).mtimeMs;
    if (t > newest) newest = t;
  }
  return newest;
}

function run(cmd, args, label) {
  // Windows needs a shell to launch npm at all — Node 20+ throws EINVAL on a
  // bare .cmd/.bat. But passing an args ARRAY alongside shell:true emits
  // DEP0190 on every single run, so the command goes in as one string instead.
  // Both the command and the args are fixed literals here, so there is nothing
  // to escape.
  const useShell = process.platform === "win32" && /\.(cmd|bat)$/i.test(cmd);
  const command = useShell ? [cmd, ...args].join(" ") : cmd;
  const argv = useShell ? undefined : args;

  // stdio "inherit" for stderr only: npm and tsc both write progress to stdout,
  // which must not reach the MCP channel.
  const r = spawnSync(command, argv, { cwd: HERE, stdio: ["ignore", "pipe", "inherit"], shell: useShell });
  if (r.error) {
    log(`${label} failed to launch: ${r.error.message}`);
    return false;
  }
  if (r.status !== 0) {
    // On failure the swallowed stdout is exactly what explains why.
    if (r.stdout?.length) process.stderr.write(r.stdout.toString());
    log(`${label} failed (exit ${r.status}).`);
    return false;
  }
  return true;
}

const npm = process.platform === "win32" ? "npm.cmd" : "npm";
const nodeModules = resolve(HERE, "node_modules");
const pkg = resolve(HERE, "package.json");
const tsc = resolve(nodeModules, "typescript", "bin", "tsc");
const dist = resolve(HERE, "dist", "index.js");

// ── dependencies ──
if (!existsSync(nodeModules)) {
  log("installing dependencies (first run, ~20s)…");
  if (!run(npm, ["install", "--silent"], "npm install")) process.exit(1);
} else if (statSync(pkg).mtimeMs > statSync(nodeModules).mtimeMs) {
  log("package.json changed — reinstalling dependencies…");
  if (!run(npm, ["install", "--silent"], "npm install")) process.exit(1);
}

// ── build ──
if (!existsSync(tsc)) {
  log("TypeScript is missing from node_modules; try deleting node_modules and rerunning.");
  process.exit(1);
}
if (!existsSync(dist) || newestMtime(resolve(HERE, "src")) > statSync(dist).mtimeMs) {
  log("building…");
  if (!run(process.execPath, [tsc], "tsc")) process.exit(1);
}

log(`ready — bridge will listen on ws://127.0.0.1:${process.env.BEEP_MCP_PORT ?? 8789}`);
