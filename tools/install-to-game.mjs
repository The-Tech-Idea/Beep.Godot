#!/usr/bin/env node
/**
 * Install the Beep addons + a private MCP server into another Godot project.
 *
 *   node tools/install-to-game.mjs "C:/games/MyGame"
 *   node tools/install-to-game.mjs "C:/games/MyGame" --addons-only
 *   node tools/install-to-game.mjs "C:/games/MyGame" --minimal   (godot_mcp only)
 *
 * Each game gets its OWN copy of the server and its own .mcp.json. That is
 * deliberate: the alternative — one shared server registered globally — makes the
 * gate tools (beep_gate_build / beep_gate_scenes) run against whichever project the
 * SERVER lives in rather than the game you are working on, and report success for a
 * build that never touched your code.
 *
 * How the copied game finds its server: the path inside .mcp.json is RELATIVE, and
 * Claude Code launches a project-scoped server with cwd set to the project root. So
 * "tools/beep-mcp-server/prepare-and-start.mjs" resolves inside that game, with no
 * per-game editing.
 *
 * node_modules and dist are NOT copied — they are reinstalled and rebuilt on first
 * launch (~6s). Copying them risks shipping a stale build and a platform-specific
 * node_modules.
 */
import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SRC = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const target = args.find((a) => !a.startsWith("--"));
const addonsOnly = args.includes("--addons-only");
const minimal = args.includes("--minimal");

if (!target) {
  console.error(`Install the Beep addons + MCP server into a Godot project.

  node tools/install-to-game.mjs <game-folder> [--minimal] [--addons-only]

    --minimal       only addons/godot_mcp (the bridge), not the Beep game builder
    --addons-only   skip the MCP server and .mcp.json
`);
  process.exit(2);
}

const dst = resolve(target);
if (!existsSync(dst)) {
  console.error(`✗ ${dst} does not exist. Create the Godot project first.`);
  process.exit(1);
}
if (!existsSync(join(dst, "project.godot"))) {
  console.error(`✗ ${dst} has no project.godot — that is not a Godot project root.`);
  process.exit(1);
}
if (resolve(dst) === resolve(SRC)) {
  console.error("✗ Target is this repository. Pick a different game folder.");
  process.exit(1);
}

const done = [];
const copyDir = (rel) => {
  const from = join(SRC, rel);
  if (!existsSync(from)) return;
  cpSync(from, join(dst, rel), { recursive: true });
  done.push(rel);
};

// ── addons ──
copyDir("addons/godot_mcp");
if (!minimal) {
  copyDir("addons/beep_game_builder_cs");
  copyDir("addons/beep_ui");
}

// ── the server ──
if (!addonsOnly) {
  const serverDst = join(dst, "tools", "beep-mcp-server");
  mkdirSync(dirname(serverDst), { recursive: true });
  cpSync(join(SRC, "tools", "beep-mcp-server"), serverDst, {
    recursive: true,
    // Rebuilt on first launch. A copied node_modules can be platform-specific and a
    // copied dist can be older than the src beside it.
    filter: (p) => !/[\\/](node_modules|dist)([\\/]|$)/.test(p),
  });
  done.push("tools/beep-mcp-server");

  // ── .mcp.json — merge rather than clobber an existing one ──
  const mcpPath = join(dst, ".mcp.json");
  const entry = {
    command: "node",
    args: ["tools/beep-mcp-server/prepare-and-start.mjs"],
    env: {},
  };
  let config = { mcpServers: {} };
  if (existsSync(mcpPath)) {
    try {
      config = JSON.parse(readFileSync(mcpPath, "utf8"));
      config.mcpServers ??= {};
    } catch {
      console.error(`! ${mcpPath} is not valid JSON — writing a fresh one and keeping the old at .mcp.json.bak`);
      cpSync(mcpPath, mcpPath + ".bak");
      config = { mcpServers: {} };
    }
  }
  config.mcpServers["beep-godot"] = entry;
  writeFileSync(mcpPath, JSON.stringify(config, null, 2) + "\n");
  done.push(".mcp.json");
}

// ── report ──
console.log(`Installed into ${dst}:`);
for (const d of done) console.log(`  ✓ ${d}`);

console.log(`
Next:
  1. Open ${dst} in Godot once — it enables the plugins and imports the assets.
  2. Open Claude Code in ${dst} and approve 'beep-godot'. Confirm with /mcp.
  3. To let Claude EDIT the project, turn on
     Project Settings → godot_mcp → security → allow_editor_writes.
     (It cannot be enabled through MCP — setting it is itself a write.)

Note: each game runs its own server on port 8789, so work on one game at a time.
Open a second game and its Godot will take the connection from the first.`);

if (!addonsOnly && existsSync(join(dst, "tools", "beep-mcp-server", "node_modules"))) {
  rmSync(join(dst, "tools", "beep-mcp-server", "node_modules"), { recursive: true, force: true });
}
