#!/usr/bin/env node
/**
 * Set this game up for Claude. Run from the game folder — no arguments, no paths.
 *
 * Assumes it is sitting at <game>/tools/beep-mcp-server/, which is where it lands
 * when you copy that folder into your game. The game is two directories up.
 *
 * What it does, all of which is otherwise manual:
 *   1. writes .mcp.json               so Claude finds this server
 *   2. creates a .csproj if missing   without one the C# addon NEVER LOADS, silently
 *   3. enables the addon plugins
 *   4. turns on allow_editor_writes   so Claude can edit (the bridge cannot set this
 *                                     itself — setting it is a write)
 *   5. runs dotnet build              so the addon is live immediately
 */
import { existsSync, readFileSync, readdirSync, writeFileSync, cpSync } from "node:fs";
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

// 1. .mcp.json — merged, never clobbered: the game may have other MCP servers.
{
  const p = join(GAME, ".mcp.json");
  let cfg = { mcpServers: {} };
  if (existsSync(p)) {
    try {
      cfg = JSON.parse(readFileSync(p, "utf8"));
      cfg.mcpServers ??= {};
    } catch {
      cpSync(p, p + ".bak");
      console.log("  ! existing .mcp.json was not valid JSON — kept a copy as .mcp.json.bak");
      cfg = { mcpServers: {} };
    }
  }
  cfg.mcpServers["beep-godot"] = {
    command: "node",
    args: ["tools/beep-mcp-server/prepare-and-start.mjs"],
    env: {},
  };
  writeFileSync(p, JSON.stringify(cfg, null, 2) + "\n");
  did.push(".mcp.json");
}

// 2 + 3 + 4. project.godot and the C# project.
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

console.log(`  Done (${did.join(", ")}).

  Now, in THIS folder:
      claude
  and say YES when it asks about 'beep-godot'.
`);
if (noWrites) console.log("  Claude can look but not edit (--no-writes).\n");
