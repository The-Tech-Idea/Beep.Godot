#!/usr/bin/env node
/**
 * Set this game up for Claude. Run from the game folder — no arguments, no paths.
 *
 * Assumes it is sitting at <game>/tools/beep-mcp-server/, which is where it lands
 * when you copy that folder into your game. The game is two directories up.
 *
 * What it does, all of which is otherwise manual:
 *   1. creates a .csproj if missing   without one the C# addon NEVER LOADS, silently
 *   2. enables the addon plugins
 *   3. turns on allow_editor_writes   so Claude can edit (the bridge cannot set this
 *                                     itself — setting it is a write)
 *   4. runs dotnet build              so the addon is live immediately
 *   5. registers with Claude          local scope: this game only, no approval prompt
 */
import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { basename, join, resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const HERE = dirname(fileURLToPath(import.meta.url));
const GAME = resolve(HERE, "..", "..");
const noWrites = process.argv.includes("--no-writes");

console.log(`\n  Setting up: ${GAME}\n`);

if (!existsSync(join(GAME, "project.godot"))) {
  console.error(`  [X] No project.godot in ${GAME}

      This script expects to live at <your game>/tools/beep-mcp-server/.
      Copy the whole 'tools/beep-mcp-server' folder into your game, then run
      it from there.\n`);
  process.exit(1);
}
if (!existsSync(join(GAME, "addons", "godot_mcp"))) {
  console.error(`  [X] addons/godot_mcp is missing from ${GAME}

      Copy the 'addons/godot_mcp' folder into your game as well — that is the
      part that talks to Godot.\n`);
  process.exit(1);
}

const did = [];

// 1. project.godot and the C# project.
{
  const file = join(GAME, "project.godot");
  let text = readFileSync(file, "utf8");

  // .csproj — the silent killer. No .csproj means Godot compiles nothing, so the
  // C# addon is present and completely inert, with no error anywhere.
  const csprojs = readdirSync(GAME).filter((f) => f.endsWith(".csproj"));
  const csprojName = csprojs[0] ?? `${(basename(GAME) || "Game").replace(/[^A-Za-z0-9_.]/g, "")}.csproj`;
  if (!csprojs.length) {
    writeFileSync(
      join(GAME, csprojName),
      `<Project Sdk="Godot.NET.Sdk/4.7.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
`,
    );
    did.push(csprojName);
  }
  if (!/^\[dotnet\]$/m.test(text)) text += `\n[dotnet]\n\nproject/assembly_name="${csprojName.replace(/\.csproj$/, "")}"\n`;

  const featRe = /^config\/features=PackedStringArray\((.*)\)$/m;
  const fm = text.match(featRe);
  if (fm && !/["']C#["']/.test(fm[1])) text = text.replace(featRe, `config/features=PackedStringArray(${fm[1]}, "C#")`);

  // Enable whichever addons are actually present.
  const wanted = ["godot_mcp", "beep_game_builder_cs", "beep_ui"]
    .filter((a) => existsSync(join(GAME, "addons", a, "plugin.cfg")))
    .map((a) => `res://addons/${a}/plugin.cfg`);
  const enRe = /^enabled=PackedStringArray\((.*)\)$/m;
  const em = text.match(enRe);
  if (em) {
    const merged = [...new Set([...[...em[1].matchAll(/"([^"]+)"/g)].map((x) => x[1]), ...wanted])];
    text = text.replace(enRe, `enabled=PackedStringArray(${merged.map((s) => `"${s}"`).join(", ")})`);
  } else {
    text += `\n[editor_plugins]\n\nenabled=PackedStringArray(${wanted.map((s) => `"${s}"`).join(", ")})\n`;
  }

  // Write permission. Only ADD when absent — never overrule a decision already made.
  const v = noWrites ? "false" : "true";
  if (!/^\[godot_mcp\]$/m.test(text)) {
    text += `\n[godot_mcp]\n\nsecurity/allow_editor_writes=${v}\nsecurity/allow_runtime_writes=${v}\n`;
  } else {
    for (const k of ["security/allow_editor_writes", "security/allow_runtime_writes"]) {
      if (!new RegExp(`^${k.replace("/", "\\/")}=`, "m").test(text))
        text = text.replace(/^\[godot_mcp\]$/m, `[godot_mcp]\n\n${k}=${v}`);
    }
  }

  writeFileSync(file, text);
  did.push("project.godot");
}

// 5. Build, so the addon is live without opening the editor first.
{
  const r = spawnSync("dotnet", ["build"], { cwd: GAME, stdio: ["ignore", "pipe", "pipe"] });
  if (r.error) {
    console.log("  ! dotnet not found — install the .NET 8 SDK, then run: dotnet build");
  } else if (r.status === 0) {
    did.push("dotnet build");
  } else {
    console.log("  ! dotnet build failed — the addon stays inactive until it compiles:");
    const out = (r.stdout?.toString() ?? "") + (r.stderr?.toString() ?? "");
    for (const l of out.split("\n").filter((l) => /error/i.test(l)).slice(0, 4)) console.log("      " + l.trim());
  }
}

// 6. Register with Claude, LOCAL scope.
//
// Scope matters here and the wrong one causes real damage:
//   user    — one entry for ALL projects. Two games would fight over the name and
//             whichever registered last would win. Never use it for this.
//   project — the .mcp.json above. Correct, but it sits at "Pending approval" until
//             you accept a workspace-trust dialog AND an approval prompt. That is
//             exactly why running setup looked like it did nothing.
//   local   — stored in ~/.claude.json under THIS game's path. Loads only in this
//             game, invisible in the others, and needs no approval. This one.
//
// cwd must be GAME: local scope keys off the current directory.
{
  const entry = join(GAME, "tools", "beep-mcp-server", "prepare-and-start.mjs");
  // `claude` is a .cmd shim on Windows, so it needs a shell. Pass ONE command string
  // rather than shell:true plus an args array — that combination prints a DEP0190
  // deprecation warning, which looks like a failure to anyone reading the output.
  const sh = (cmd) => spawnSync(cmd, { cwd: GAME, stdio: ["ignore", "pipe", "pipe"], shell: true });
  const addCmd = `claude mcp add --scope local beep-godot -- node "${entry}"`;

  const probe = sh("claude --version");
  if (probe.error || probe.status !== 0) {
    console.log("  ! 'claude' is not on PATH — install Claude Code, then run this again.");
  } else {
    // Remove first so re-running updates the path instead of erroring on a duplicate.
    sh("claude mcp remove --scope local beep-godot");
    if (sh(addCmd).status === 0) did.push("registered with Claude");
    else {
      console.log("  ! could not register with Claude automatically. Run this yourself:");
      console.log(`      ${addCmd}`);
    }
  }
}

console.log(`  Done (${did.join(", ")}).

  Now run:  claude
`);
if (noWrites) console.log("  Claude can look but not edit (--no-writes).\n");
