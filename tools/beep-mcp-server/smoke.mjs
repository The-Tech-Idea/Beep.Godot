/**
 * Phase 0 smoke test — drives the real MCP server over stdio, exactly as Claude would,
 * and additionally impersonates Godot on the WebSocket side so the connected path is
 * exercised too. No Godot binary required.
 */
import { spawn } from "node:child_process";
import { WebSocket } from "ws";

const SERVER = process.argv[2] ?? "dist/index.js";
const proc = spawn(process.execPath, [SERVER], {
  stdio: ["pipe", "pipe", "pipe"],
  env: { ...process.env, BEEP_MCP_PORT: "8799" },
});

let buf = "";
const waiters = new Map();
proc.stdout.on("data", (d) => {
  buf += d.toString();
  let i;
  while ((i = buf.indexOf("\n")) >= 0) {
    const line = buf.slice(0, i).trim();
    buf = buf.slice(i + 1);
    if (!line) continue;
    let msg;
    try { msg = JSON.parse(line); } catch { continue; }
    if (msg.id != null && waiters.has(msg.id)) {
      waiters.get(msg.id)(msg);
      waiters.delete(msg.id);
    }
  }
});
proc.stderr.on("data", (d) => process.stderr.write(`  (server) ${d}`));

let nextId = 1;
function rpc(method, params) {
  const id = nextId++;
  return new Promise((res) => {
    waiters.set(id, res);
    proc.stdin.write(JSON.stringify({ jsonrpc: "2.0", id, method, params }) + "\n");
  });
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const textOf = (r) => r?.result?.content?.[0]?.text ?? JSON.stringify(r?.error ?? r);

let failures = 0;
function check(label, cond, extra = "") {
  console.log(`${cond ? "PASS" : "FAIL"}  ${label}${extra ? " — " + extra : ""}`);
  if (!cond) failures++;
}

await sleep(700);

// 1. handshake
const init = await rpc("initialize", {
  protocolVersion: "2024-11-05",
  capabilities: {},
  clientInfo: { name: "smoke", version: "0" },
});
check("initialize returns serverInfo", init?.result?.serverInfo?.name === "beep-godot",
      JSON.stringify(init?.result?.serverInfo));
proc.stdin.write(JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" }) + "\n");

// 2. tool list
const tools = await rpc("tools/list", {});
const names = (tools?.result?.tools ?? []).map((t) => t.name).sort();
check("tools/list exposes the Phase 0 surface", names.length >= 15, `${names.length} tools`);
check("beep_command present", names.includes("beep_command"));
check("godot_status present", names.includes("godot_status"));

// 3. Godot CLOSED: status answers, others report NOT_CONNECTED
const st = await rpc("tools/call", { name: "godot_status", arguments: {} });
const stText = textOf(st);
check("godot_status answers with Godot closed", !st?.result?.isError && stText.includes("No Godot process"),
      stText.slice(0, 80));

const tree = await rpc("tools/call", { name: "godot_scene_tree", arguments: {} });
const treeText = textOf(tree);
check("editor tool reports NOT_CONNECTED, actionably",
      tree?.result?.isError === true && treeText.includes("NOT_CONNECTED") && treeText.includes("Open the project in Godot"),
      treeText.slice(0, 90));

const stateR = await rpc("tools/call", { name: "beep_state", arguments: { name: "beep.game_state" } });
check("runtime tool names the runtime fix (press Play)",
      textOf(stateR).includes("Press Play"), textOf(stateR).slice(0, 90));

// 4. Impersonate Godot: connect as editor exactly as McpWebSocketClient does
const ws = new WebSocket("ws://127.0.0.1:8799/editor");
await new Promise((r, j) => { ws.once("open", r); ws.once("error", j); });
ws.send(JSON.stringify({
  method: "hello",
  params: { token: "", bridge: "godot-mcp-csharp", version: "0.2.0", role: "editor",
            editor_hint: true, godot_version: "4.7.0" },
}));
// Answer requests the way GodotMcpBridgeController does.
ws.on("message", (raw) => {
  const req = JSON.parse(raw.toString());
  if (req.method === "status.get") {
    ws.send(JSON.stringify({ id: req.id, ok: true,
      result: { commands: ["beep.catalog", "beep.inspect_scene"], allow_editor_writes: false } }));
  } else if (req.method === "tree.serialize") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: { name: "RaceResults", type: "CanvasLayer" } }));
  } else if (req.method === "game.command" && req.params?.name === "beep.set_node_property") {
    ws.send(JSON.stringify({ id: req.id, ok: false,
      error: "beep.set_node_property edits the open scene. Enable godot_mcp/security/allow_editor_writes first.",
      error_type: "InvalidOperationException" }));
  } else if (req.method === "ping") {
    /* never answer: reserved for the disconnect test below */
  } else {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: { echoed: req.method } }));
  }
});
await sleep(400);

const st2 = await rpc("tools/call", { name: "godot_status", arguments: {} });
check("status flips to connected after hello", textOf(st2).includes('"editor"') && textOf(st2).includes("4.7.0"),
      textOf(st2).replace(/\s+/g, " ").slice(0, 110));

const tree2 = await rpc("tools/call", { name: "godot_scene_tree", arguments: {} });
check("editor tool now returns the tree", textOf(tree2).includes("RaceResults"), textOf(tree2).slice(0, 60));

const disc = await rpc("tools/call", { name: "beep_list_commands", arguments: {} });
check("discovery surfaces beep.* commands", textOf(disc).includes("beep.inspect_scene"));

// 5. Godot-side refusal must arrive as an error, not a cheerful success
const refused = await rpc("tools/call", {
  name: "beep_command",
  arguments: { name: "beep.set_node_property", args: { node: ".", property: "x", value: 1 } },
});
check("write gate refusal surfaces as isError with the fix",
      refused?.result?.isError === true && textOf(refused).includes("allow_editor_writes"),
      textOf(refused).slice(0, 90));

// 6. Disconnect mid-flight rejects rather than hanging
const hang = rpc("tools/call", { name: "godot_ping", arguments: {} });
await sleep(120);
ws.terminate();
const hangRes = await Promise.race([hang, sleep(6000).then(() => null)]);
check("in-flight request rejects on disconnect (no hang)",
      hangRes !== null && /DISCONNECTED_MID_REQUEST|NOT_CONNECTED|TIMEOUT/.test(textOf(hangRes)),
      hangRes ? textOf(hangRes).slice(0, 70) : "TIMED OUT WAITING");

console.log(failures === 0 ? "\nALL CHECKS PASSED" : `\n${failures} CHECK(S) FAILED`);
proc.kill();
process.exit(failures === 0 ? 0 : 1);
