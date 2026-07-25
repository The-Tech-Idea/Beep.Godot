/**
 * LIVE verification — the real MCP server against a REAL headless Godot editor.
 *
 * `npm run smoke` proves the server's own logic against a simulated addon. This proves the
 * two halves actually fit: it launches Godot for real, waits for the addon to dial in, and
 * exercises the surface end to end.
 *
 * It is what caught three protocol bugs simulation could not see — game.command gating
 * READS behind write permission, the server sending `name` where the bridge reads
 * `command`, and discovery probing a status key that never existed. A simulation tests the
 * protocol you believe you have; only this tests the one you have.
 *
 *   BEEP_GODOT_BIN   path to Godot 4.7 mono. Required.
 *   BEEP_PROJECT     project root (defaults to two levels up from this file).
 */
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT = process.env.BEEP_PROJECT ?? resolve(HERE, "..", "..");
const SERVER = resolve(HERE, "dist", "index.js");
const GODOT = process.env.BEEP_GODOT_BIN ?? "";

if (!GODOT || !existsSync(GODOT)) {
  console.error(
    `BEEP_GODOT_BIN is not set to a Godot binary${GODOT ? ` (got '${GODOT}')` : ""}.\n` +
      `  BEEP_GODOT_BIN="H:/dev/Godot/Godot_v4.7-stable_mono_win64.exe" npm run live`,
  );
  process.exit(2);
}

let failures = 0;
const check = (label, cond, extra = "") => {
  console.log(`${cond ? "PASS" : "FAIL"}  ${label}${extra ? " — " + String(extra).replace(/\s+/g, " ").slice(0, 120) : ""}`);
  if (!cond) failures++;
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ── MCP server over stdio. Phase 4's host tools need the binary + root too. ──
const srv = spawn(process.execPath, [SERVER], {
  stdio: ["pipe", "pipe", "pipe"],
  env: { ...process.env, BEEP_GODOT_BIN: GODOT, BEEP_PROJECT: PROJECT },
});
let buf = "";
const waiters = new Map();
srv.stdout.on("data", (d) => {
  buf += d.toString();
  let i;
  while ((i = buf.indexOf("\n")) >= 0) {
    const line = buf.slice(0, i).trim(); buf = buf.slice(i + 1);
    if (!line) continue;
    let m; try { m = JSON.parse(line); } catch { continue; }
    if (m.id != null && waiters.has(m.id)) { waiters.get(m.id)(m); waiters.delete(m.id); }
  }
});
srv.stderr.on("data", (d) => process.stderr.write(`  (srv) ${d}`));

let id = 1;
const rpc = (method, params) => new Promise((res) => {
  const n = id++; waiters.set(n, res);
  srv.stdin.write(JSON.stringify({ jsonrpc: "2.0", id: n, method, params }) + "\n");
});
const tool = (name, args = {}) => rpc("tools/call", { name, arguments: args });
const textOf = (r) => {
  const c = r?.result?.content ?? [];
  return c.filter((b) => b.type === "text").map((b) => b.text).join("\n") || JSON.stringify(r?.error ?? r);
};

await sleep(600);
await rpc("initialize", { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "live", version: "0" } });
srv.stdin.write(JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" }) + "\n");

// ── real headless Godot editor; the addon dials our socket on its own timer ──
console.log("starting headless Godot editor…");
const godot = spawn(GODOT, ["--headless", "--editor", "--path", PROJECT], { stdio: ["ignore", "pipe", "pipe"] });

let connected = false;
for (let i = 0; i < 45; i++) {
  await sleep(1000);
  if (/"editor":\s*\{/.test(textOf(await tool("godot_status")))) { connected = true; break; }
}
check("live Godot editor connects to the MCP server", connected);

if (connected) {
  const st = textOf(await tool("godot_status"));
  check("status reports the real bridge + Godot version", /godot-mcp-csharp/.test(st) && /4\.7/.test(st), st.slice(0, 150));

  const caps = textOf(await tool("godot_capabilities"));
  check("bridge.capabilities answers from real Godot", /"batch": true/.test(caps) && /"structured_errors": true/.test(caps));

  check("classdb.describe returns real Godot properties",
        /max_value/.test(textOf(await tool("godot_class_describe", { class: "ProgressBar" }))));

  const cat = await tool("beep_command", { name: "beep.catalog" });
  check("beep.catalog executes in real Godot (a READ, with writes disabled)",
        !cat?.result?.isError && /platformer|racing/.test(textOf(cat)), textOf(cat).slice(0, 90));

  // Write gate is OFF in project.godot — a write must be REFUSED, not silently succeed.
  const refused = await tool("godot_node_create", { parent: ".", type: "Node", name: "ShouldNotExist" });
  check("write refused while allow_editor_writes=false",
        refused?.result?.isError === true && /disabled/i.test(textOf(refused)), textOf(refused).slice(0, 110));

  const disc = textOf(await tool("beep_list_commands"));
  check("discovery reads the real project_commands registry",
        /beep\.catalog/.test(disc) && !/did not report/.test(disc), disc.slice(0, 100));

  const logT = textOf(await tool("godot_log_tail", { level: "warning", limit: 40 }));
  check("log.tail reads Godot's real log file", /"available": true/.test(logT), logT.slice(0, 110));
  check("NO missing-texture warnings after the bake", !/SkinCatalog\].*does not exist/.test(logT));
}

// ── Phase 4: gates + headless run. These bypass the bridge on purpose — an agent
// needs them exactly when Godot is closed or refusing to load the addon. ──
const build = textOf(await tool("beep_gate_build"));
check("gate_build parses a real dotnet build", /"ok": true/.test(build) && /"error_count": 0/.test(build), build.slice(0, 110));

const scenes = textOf(await tool("beep_gate_scenes"));
check("gate_scenes parses validate_scenes.sh per check",
      /"failed_count": 0/.test(scenes) && /"check_count": \d\d/.test(scenes), scenes.slice(0, 120));
check("gate_scenes names the skin-texture check", /texture\/background files/.test(scenes));

const headless = textOf(await tool("beep_headless_run", { import_only: true }));
check("headless_run drives a real Godot and reports as data", /"exit_code": 0/.test(headless), headless.slice(0, 110));
check("headless import is error-free (RemoveAutoload fix)", /"error_count": 0/.test(headless), headless.slice(0, 140));

console.log(failures === 0 ? "\nALL LIVE CHECKS PASSED" : `\n${failures} LIVE CHECK(S) FAILED`);
try { godot.kill(); } catch {}
try { srv.kill(); } catch {}
await sleep(300);
process.exit(failures === 0 ? 0 : 1);
