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
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync, rmSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const SRC = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const target = args.find((a) => !a.startsWith("--"));
const addonsOnly = args.includes("--addons-only");
const minimal = args.includes("--minimal");
// Writes ON by default: running this installer against your own game IS the consent,
// and it removes the one step that cannot be automated afterwards.
const allowWrites = !args.includes("--no-writes");
// --verbose lists every file touched. Off by default: a first-time user wants "done",
// not an inventory.
const verbose = args.includes("--verbose");

if (!target) {
  console.error(`Install the Beep addons + MCP server into a Godot project.

  node tools/install-to-game.mjs <game-folder> [--minimal] [--addons-only]

    --minimal       only addons/godot_mcp (the bridge), not the Beep game builder
    --addons-only   skip the MCP server and .mcp.json
    --no-writes     leave allow_editor_writes off (default: ON, so Claude can edit)
    --verbose       list every file installed
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

  // ── register with Claude, LOCAL scope ──
  //
  // Deliberately NOT a .mcp.json. That is project scope, and it stalls at
  // "⏸ Pending approval" behind a trust dialog plus an approval prompt. Local scope is
  // stored in ~/.claude.json under this game's own path: loads here, invisible in the
  // user's other games, no approval. (User scope would be one entry shared by every
  // project — the second game installed would clobber the first.)
  // cwd must be the game folder: local scope keys off the current directory.
  const entry = join(dst, "tools", "beep-mcp-server", "prepare-and-start.mjs");
  // One command string, not shell:true + args array — the latter prints a DEP0190
  // deprecation warning that reads like a failure.
  const sh = (cmd) => spawnSync(cmd, { cwd: dst, stdio: ["ignore", "pipe", "pipe"], shell: true });
  const addCmd = `claude mcp add --scope local beep-godot -- node "${entry}"`;

  const probe = sh("claude --version");
  if (probe.error || probe.status !== 0) {
    console.error("! 'claude' is not on PATH — install Claude Code, then run:");
    console.error(`    cd "${dst}" && ${addCmd}`);
  } else {
    // Remove first so re-installing updates the path instead of erroring on a duplicate.
    sh("claude mcp remove --scope local beep-godot");
    if (sh(addCmd).status === 0) done.push("registered with Claude");
    else console.error(`! could not register with Claude — run: ${addCmd}`);
  }
}

// ── the C# project ──
//
// godot_mcp and beep_game_builder_cs are C#. Without a .csproj Godot compiles
// nothing, so the plugin never loads — and it does so IN COMPLETE SILENCE: the
// editor starts, the import succeeds, exit code is 0, and there is simply no bridge.
// Verified: a game without a .csproj produced zero plugin log lines where this repo
// produced ten. Creating one is the difference between "installed" and "works".
if (!addonsOnly) ensureCsproj();

function ensureCsproj() {
  const existing = readdirSync(dst).filter((f) => f.endsWith(".csproj"));
  const name = (basename(dst) || "Game").replace(/[^A-Za-z0-9_.]/g, "");
  const csprojName = existing[0] ?? `${name}.csproj`;

  if (!existing.length) {
    writeFileSync(
      join(dst, csprojName),
      `<Project Sdk="Godot.NET.Sdk/4.7.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <!-- Created by Beep's installer. Godot.NET.Sdk auto-includes every .cs under the
       project directory, so the addons under addons/ compile with no explicit items. -->
</Project>
`,
    );
    done.push(csprojName + " (created — C# addons need it)");
  }

  // Godot needs [dotnet] project/assembly_name to match, or it will not load the
  // built assembly even when the .csproj is right.
  const file = join(dst, "project.godot");
  let text = readFileSync(file, "utf8");
  const assembly = csprojName.replace(/\.csproj$/, "");
  if (!/^\[dotnet\]$/m.test(text)) {
    text += `\n[dotnet]\n\nproject/assembly_name="${assembly}"\n`;
    writeFileSync(file, text);
  }

  // "C#" in config/features is what makes the editor treat this as a .NET project.
  text = readFileSync(file, "utf8");
  const featRe = /^config\/features=PackedStringArray\((.*)\)$/m;
  const fm = text.match(featRe);
  if (fm && !/["']C#["']/.test(fm[1])) {
    text = text.replace(featRe, `config/features=PackedStringArray(${fm[1]}, "C#")`);
    writeFileSync(file, text);
  }
}

// ── enable the plugins and the write gate in project.godot ──
//
// This is the one step that CANNOT be done through MCP later: turning on
// allow_editor_writes is itself a write, so the bridge refuses it. Doing it here is
// legitimate — running this installer against your own game IS the consent — and it
// is the difference between "works" and "works after you go hunting in Project
// Settings". Pass --no-writes to leave it off.
if (!addonsOnly) patchProjectGodot();

function patchProjectGodot() {
  const file = join(dst, "project.godot");
  let text = readFileSync(file, "utf8");
  const before = text;

  // 1. Enable the plugins we just installed.
  const plugins = ['res://addons/godot_mcp/plugin.cfg'];
  if (!minimal) plugins.push('res://addons/beep_game_builder_cs/plugin.cfg', 'res://addons/beep_ui/plugin.cfg');
  const wanted = plugins.filter((p) => existsSync(join(dst, p.replace("res://", ""))));

  const enabledRe = /^enabled=PackedStringArray\((.*)\)$/m;
  const m = text.match(enabledRe);
  if (m) {
    const have = [...m[1].matchAll(/"([^"]+)"/g)].map((x) => x[1]);
    const merged = [...new Set([...have, ...wanted])];
    text = text.replace(enabledRe, `enabled=PackedStringArray(${merged.map((s) => `"${s}"`).join(", ")})`);
  } else {
    text += `\n[editor_plugins]\n\nenabled=PackedStringArray(${wanted.map((s) => `"${s}"`).join(", ")})\n`;
  }

  // 2. Security flags. Only ADD them when absent — never downgrade a setting the
  //    developer has already made a decision about.
  const flags = [
    ["security/allow_editor_writes", allowWrites ? "true" : "false"],
    ["security/allow_runtime_writes", allowWrites ? "true" : "false"],
  ];
  if (!/^\[godot_mcp\]$/m.test(text)) {
    text += `\n[godot_mcp]\n\n${flags.map(([k, v]) => `${k}=${v}`).join("\n")}\n`;
  } else {
    for (const [k, v] of flags) {
      const re = new RegExp(`^${k.replace("/", "\\/")}=.*$`, "m");
      if (!re.test(text)) text = text.replace(/^\[godot_mcp\]$/m, `[godot_mcp]\n\n${k}=${v}`);
    }
  }

  if (text !== before) {
    writeFileSync(file, text);
    done.push(`project.godot (plugins enabled${allowWrites ? ", writes ON" : ""})`);
  }
}

// ── build the C# once, so the addon is live immediately ──
//
// The Godot GUI builds on open, but a headless run does not — and until something
// builds, the plugin is present and silent. Doing it here means the install is
// finished when the script finishes, rather than after a first editor launch nobody
// told you about.
if (!addonsOnly) buildOnce();

function buildOnce() {
  const r = spawnSync("dotnet", ["build"], { cwd: dst, stdio: ["ignore", "pipe", "pipe"], shell: false });
  if (r.error) {
    console.log("\n! dotnet not found — skipping the C# build.");
    console.log("  The addon stays inactive until it compiles. Install the .NET 8 SDK,");
    console.log(`  then run:  cd "${dst}" && dotnet build`);
    return;
  }
  if (r.status === 0) {
    done.push("dotnet build (addon compiled and active)");
  } else {
    console.log("\n! dotnet build failed — the addon will not load until it compiles:");
    const out = (r.stdout?.toString() ?? "") + (r.stderr?.toString() ?? "");
    for (const line of out.split("\n").filter((l) => /error/i.test(l)).slice(0, 5)) {
      console.log("    " + line.trim());
    }
  }
}

// ── report ──
//
// Short on purpose. Someone installing this for the first time needs to know it
// worked and what to do next — not the port number or the trade-offs. The details
// live in tools/beep-mcp-server/README.md for when they are actually wanted.
console.log(`\n  Done. Installed ${done.length} things into your game.`);
if (verbose) for (const d of done) console.log(`    · ${d}`);

console.log(`
  Next:
    1. Open a terminal in your game folder
    2. Run:  claude
    3. Say YES when it asks about 'beep-godot'

  Then just talk to Claude about your game. Open it in Godot too, and Claude
  can see and edit the scenes you have open.`);

if (!allowWrites && !addonsOnly) {
  console.log(`
  Note: Claude can LOOK but not EDIT, because you passed --no-writes.
  To change that: Godot → Project → Project Settings → godot_mcp → security
                 → allow_editor_writes = on`);
}

if (!addonsOnly && existsSync(join(dst, "tools", "beep-mcp-server", "node_modules"))) {
  rmSync(join(dst, "tools", "beep-mcp-server", "node_modules"), { recursive: true, force: true });
}
