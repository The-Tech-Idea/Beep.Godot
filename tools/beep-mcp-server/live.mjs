/**
 * LIVE verification — the real MCP server against a REAL headless Godot editor.
 *
 * `npm run smoke` proves the server's own logic against a simulated addon. This proves the
 * halves actually fit: it launches Godot for real, waits for the addon to dial in, and
 * exercises the surface end to end. It is what caught three protocol bugs that simulation
 * could not see -- game.command gating reads behind WRITE permission, the server sending
 * `name` where the bridge reads `command`, and discovery looking for a status key that
 * never existed.
 *
 *   BEEP_GODOT_BIN   path to Godot 4.7 mono (defaults to the author's install)
 *   BEEP_PROJECT     project root (defaults to two levels up from this file)
 */
import { spawn } from "node:child_process";

const GODOT = "H:\\dev\\Godot\\Godot_v4.7-stable_mono_win64.exe";
const PROJECT = "C:\\Users\\f_ald\\source\\repos\\The-Tech-Idea\\Beep.Godot";
const SERVER = PROJECT + "\\tools\\beep-mcp-server\\dist\\index.js";

let failures = 0;
const check = (label, cond, extra = "") => {
  console.log(`${cond ? "PASS" : "FAIL"}  ${label}${extra ? " — " + String(extra).replace(/\s+/g, " ").slice(0, 120) : ""}`);
  if (!cond) failures++;
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ── MCP server over stdio ──
const srv = spawn(process.execPath, [SERVER], { stdio: ["pipe", "pipe", "pipe"], env: { ...process.env } });
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
let godotOut = "";
godot.stdout.on("data", (d) => { godotOut += d.toString(); });
godot.stderr.on("data", (d) => { godotOut += d.toString(); });

// Wait for the bridge to connect (it retries every 2s).
let connected = false;
for (let i = 0; i < 45; i++) {
  await sleep(1000);
  const st = await tool("godot_status");
  if (/"editor":\s*\{/.test(textOf(st))) { connected = true; break; }
}
check("live Godot editor connects to the MCP server", connected);

if (connected) {
  const st = await tool("godot_status");
  check("status reports the real bridge + Godot version", /godot-mcp-csharp/.test(textOf(st)) && /4\.7/.test(textOf(st)), textOf(st).slice(0, 160));

  const caps = await tool("godot_capabilities");
  const capsT = textOf(caps);
  check("bridge.capabilities answers from real Godot", /"batch": true/.test(capsT) && /"structured_errors": true/.test(capsT));
  check("capabilities lists the beep.* command registry", /beep\.inspect_scene/.test(capsT) || /beep\.catalog/.test(capsT), capsT.slice(0, 120));

  const cls = await tool("godot_class_describe", { class: "ProgressBar" });
  check("classdb.describe returns real Godot properties", /max_value/.test(textOf(cls)));

  const cmds = await tool("beep_command", { name: "beep.catalog" });
  check("beep.catalog executes in real Godot", !cmds?.result?.isError && /platformer|racing/.test(textOf(cmds)), textOf(cmds).slice(0, 100));

  // Write gate is OFF in project.godot — a write must be REFUSED, not silently succeed.
  const refused = await tool("godot_node_create", { parent: ".", type: "Node", name: "ShouldNotExist" });
  check("write refused while allow_editor_writes=false",
        refused?.result?.isError === true && /WRITE_DISABLED|disabled/i.test(textOf(refused)), textOf(refused).slice(0, 120));

  // The texture pipeline: does SkinCatalog now find the baked art?
  const skin = await tool("beep_command", { name: "beep.list_themes", args: { genre: "racing" } });
  check("beep.list_themes works against the live catalog", !skin?.result?.isError, textOf(skin).slice(0, 90));

  const disc = await tool("beep_list_commands");
  check("discovery reads the real project_commands registry",
        /beep\.catalog/.test(textOf(disc)) && !/did not report/.test(textOf(disc)), textOf(disc).slice(0, 110));

  const logs = await tool("godot_log_tail", { level: "warning", limit: 40 });
  const logT = textOf(logs);
  check("log.tail reads Godot's real log file", /"available": true/.test(logT), logT.slice(0, 120));
  const missingTex = /SkinCatalog\].*does not exist/.test(logT);
  check("NO missing-texture warnings after the bake", !missingTex,
        missingTex ? "still warning about missing textures!" : "clean");
}

console.log(failures === 0 ? "\nALL LIVE CHECKS PASSED" : `\n${failures} LIVE CHECK(S) FAILED`);
try { godot.kill(); } catch {}
try { srv.kill(); } catch {}
await sleep(300);
process.exit(failures === 0 ? 0 : 1);
