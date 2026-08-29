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
  } else if (req.method === "bridge.capabilities") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: {
      methods: ["bridge.batch", "node.set_property_safe"],
      features: { batch: true, dry_run: true, undo: true, structured_errors: true },
      error_codes: ["SNAKE_CASE_EXPORT", "UNKNOWN_PROPERTY", "BATCH_ABORTED"] } }));
  } else if (req.method === "node.set_property_safe" && req.params?.property === "title_label_path") {
    // The snake_case [Export] trap, reported the way McpBridgeException does.
    ws.send(JSON.stringify({ id: req.id, ok: false,
      error: "'title_label_path' is a C# [Export] written snake_case. Godot silently DROPS that spelling.",
      error_type: "McpBridgeException", code: "SNAKE_CASE_EXPORT",
      fix: "Use 'TitleLabelPath'.", detail: { given: "title_label_path", expected: "TitleLabelPath" } }));
  } else if (req.method === "node.set_property_safe") {
    ws.send(JSON.stringify({ id: req.id, ok: true,
      result: { updated: true, undoable: true, property: req.params?.property,
                dry_run: req.params?.dry_run === true } }));
  } else if (req.method === "bridge.batch") {
    const ops = req.params?.ops ?? [];
    const bad = ops.findIndex((o) => o?.params?.property === "nope");
    if (bad >= 0 && (req.params?.atomic ?? true) && !req.params?.dry_run) {
      ws.send(JSON.stringify({ id: req.id, ok: true, result: {
        ok: false, aborted_at: bad, committed: false, code: "BATCH_ABORTED",
        error: `Batch aborted at op ${bad}; nothing was applied.` } }));
    } else {
      ws.send(JSON.stringify({ id: req.id, ok: true, result: {
        ok: true, dry_run: req.params?.dry_run === true,
        committed: req.params?.dry_run !== true, undoable: req.params?.dry_run !== true,
        count: ops.length } }));
    }
  } else if (req.method === "classdb.describe") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: {
      class: req.params?.class, parent: "Range", instantiable: true,
      properties: [{ name: "value", type: "Float" }, { name: "max_value", type: "Float" }],
      signals: ["value_changed"] } }));
  } else if (req.method === "animation.add_track" && ["position","scale","rotation"].includes(req.params?.property)) {
    // The container-overwrites-transform guard, as McpBridgeException reports it.
    ws.send(JSON.stringify({ id: req.id, ok: false,
      error: `'${req.params.property}' on a Control inside a VBoxContainer is overwritten every layout pass.`,
      error_type: "McpBridgeException", code: "CONTAINER_OVERWRITES_TRANSFORM",
      fix: `Animate 'offset_transform_${req.params.property}' instead.`,
      detail: { suggested: `offset_transform_${req.params.property}` } }));
  } else if (req.method === "animation.add_track") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: { track: 0, path: `${req.params?.node_path}:${req.params?.property}` } }));
  } else if (req.method === "theme.add_type_variation") {
    const known = ["BeepTitle","BeepSubtitle","BeepValue","BeepCaption"];
    const r = { theme: req.params?.path, variation: req.params?.variation };
    if (!known.includes(req.params?.variation)) r.warning = "not one of Beep's registered variations; validate_scenes.sh will fail on it";
    ws.send(JSON.stringify({ id: req.id, ok: true, result: r }));
  } else if (req.method === "resource.create" && req.params?.properties && "patch_margin" in req.params.properties) {
    ws.send(JSON.stringify({ id: req.id, ok: false,
      error: "'patch_margin' is a C# [Export] written snake_case.", error_type: "McpBridgeException",
      code: "SNAKE_CASE_EXPORT", fix: "Use 'PatchMargin'." }));
  } else if (req.method === "resource.create") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: { created: req.params?.path, type: req.params?.type } }));
  } else if (req.method === "signal.connect" && req.params?.method === "no_such_method") {
    ws.send(JSON.stringify({ id: req.id, ok: false,
      error: "'Target' has no method 'no_such_method' -- the connection would fire into nothing.",
      error_type: "McpBridgeException", code: "UNKNOWN_METHOD" }));
  } else if (req.method === "view.capture" && req.params?.target === "node" && req.params?.node === "Empty") {
    ws.send(JSON.stringify({ id: req.id, ok: false,
      error: "'Empty' has a 0x0 rect -- there is nothing to capture.", error_type: "McpBridgeException",
      code: "EMPTY_RECT", fix: "That is usually the defect: check custom_minimum_size and size_flags." }));
  } else if (req.method === "view.capture") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: {
      target: req.params?.target ?? "viewport", format: "png", width: 1, height: 1,
      base64: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==" } }));
  } else if (req.method === "view.layout") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: {
      count: 2,
      problems: [{ path: "Margin/VBox/Header/BackButton", code: "ZERO_HEIGHT",
                   message: "Button has height 0. A control with no height is invisible and unclickable." }],
      controls: [] } }));
  } else if (req.method === "log.tail") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: { available: true, total_lines: 42, count: 1,
      entries: [{ line: 41, level: "warning", text: "WARNING: [SkinCatalog] racing/arcade slot 'panel' points at ... which does not exist" }] } }));
  } else if (req.method === "scene.diff") {
    ws.send(JSON.stringify({ id: req.id, ok: true, result: {
      from: "before", added: [], removed: [], total_changes: 1,
      changed: [{ path: "Margin/VBox/Header/BackButton", before: "Button rect=0,0,120,0", after: "Button rect=0,0,120,44" }] } }));
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

// 5b. Phase 1 — capabilities, batch, dry run, and the snake_case refusal
const caps = await rpc("tools/call", { name: "godot_capabilities", arguments: {} });
check("capabilities advertises batch/dry_run/undo",
      /"batch": true/.test(textOf(caps)) && /"undo": true/.test(textOf(caps)));

const snake = await rpc("tools/call", {
  name: "godot_node_set_property",
  arguments: { path: "GameInfoBinder", property: "title_label_path", value: "x" },
});
check("snake_case [Export] refused with the PascalCase fix",
      snake?.result?.isError === true && /SNAKE_CASE_EXPORT/.test(textOf(snake)) && /TitleLabelPath/.test(textOf(snake)),
      textOf(snake).slice(0, 95));

const dry = await rpc("tools/call", {
  name: "godot_batch",
  arguments: { dry_run: true, label: "preview", ops: [ { method: "node.set_property", params: { path: ".", property: "x", value: 1 } } ] },
});
check("dry-run batch reports dry_run and commits nothing",
      /"dry_run": true/.test(textOf(dry)) && /"committed": false/.test(textOf(dry)));

const applied = await rpc("tools/call", {
  name: "godot_batch",
  arguments: { label: "restyle", ops: [ { method: "node.set_property", params: { path: ".", property: "x", value: 1 } } ] },
});
check("applied batch is committed and undoable",
      /"committed": true/.test(textOf(applied)) && /"undoable": true/.test(textOf(applied)));

const aborted = await rpc("tools/call", {
  name: "godot_batch",
  arguments: { ops: [
    { method: "node.set_property", params: { path: ".", property: "ok", value: 1 } },
    { method: "node.set_property", params: { path: ".", property: "nope", value: 1 } } ] },
});
check("atomic batch aborts at the failing index, committing nothing",
      /BATCH_ABORTED/.test(textOf(aborted)) && /"aborted_at": 1/.test(textOf(aborted)) && /"committed": false/.test(textOf(aborted)),
      textOf(aborted).replace(/\s+/g, " ").slice(0, 100));

// 5c. Phase 2 — authoring
const cls = await rpc("tools/call", { name: "godot_class_describe", arguments: { class: "ProgressBar" } });
check("classdb.describe returns real properties", /max_value/.test(textOf(cls)));

const badTrack = await rpc("tools/call", {
  name: "godot_animation_add_track",
  arguments: { player_path: "Anim", name: "pulse", node_path: "Btn", property: "scale" },
});
check("animating scale in a Container is refused, suggesting offset_transform",
      badTrack?.result?.isError === true && /CONTAINER_OVERWRITES_TRANSFORM/.test(textOf(badTrack))
        && /offset_transform_scale/.test(textOf(badTrack)),
      textOf(badTrack).slice(0, 95));

const goodTrack = await rpc("tools/call", {
  name: "godot_animation_add_track",
  arguments: { player_path: "Anim", name: "pulse", node_path: "Btn", property: "offset_transform_scale" },
});
check("offset_transform track is accepted", !goodTrack?.result?.isError && /offset_transform_scale/.test(textOf(goodTrack)));

const badVar = await rpc("tools/call", {
  name: "godot_theme_add_variation",
  arguments: { path: "res://x.tres", variation: "BeepHeading" },
});
check("unregistered theme variation warns about the validator", /warning/.test(textOf(badVar)));

const snakeRes = await rpc("tools/call", {
  name: "godot_resource_create",
  arguments: { type: "GameInfo", path: "res://s.tres", properties: { game_name: "Demo" } },
});
check("resource property snake_case [Export] refused",
      snakeRes?.result?.isError === true && /SNAKE_CASE_EXPORT/.test(textOf(snakeRes)) && /GameName/.test(textOf(snakeRes)));

const badSig = await rpc("tools/call", {
  name: "godot_signal_connect",
  arguments: { path: "Btn", signal: "pressed", to: "Target", method: "no_such_method" },
});
check("connecting to a missing method is refused",
      badSig?.result?.isError === true && /UNKNOWN_METHOD/.test(textOf(badSig)));

// 5d. Phase 3 — perception
const cap = await rpc("tools/call", { name: "godot_capture", arguments: {} });
const capBlocks = cap?.result?.content ?? [];
check("capture returns real MCP image content (not base64 in text)",
      capBlocks.some((b) => b.type === "image" && b.mimeType === "image/png" && typeof b.data === "string"),
      capBlocks.map((b) => b.type).join("+"));

const emptyCap = await rpc("tools/call", { name: "godot_capture", arguments: { target: "node", node: "Empty" } });
check("zero-size control reports EMPTY_RECT rather than a blank image",
      emptyCap?.result?.isError === true && /EMPTY_RECT/.test(textOf(emptyCap)));

const layout = await rpc("tools/call", { name: "godot_layout", arguments: {} });
check("layout flags a zero-height button", /ZERO_HEIGHT/.test(textOf(layout)) && /BackButton/.test(textOf(layout)));

const logs = await rpc("tools/call", { name: "godot_log_tail", arguments: { level: "warning" } });
check("log tail surfaces a PushWarning", /SkinCatalog/.test(textOf(logs)) && /does not exist/.test(textOf(logs)));

const diff = await rpc("tools/call", { name: "godot_scene_diff", arguments: { from: "before" } });
check("scene diff reports the changed rect", /120,0/.test(textOf(diff)) && /120,44/.test(textOf(diff)));

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
