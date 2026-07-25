# Phase 4 — Autonomy

**Goal:** the agent can close its own loop — change, verify, read the result, correct —
without a human relaying build output.

**Status:** ✅ built and **live-verified** — `addons/godot_mcp/…Lifecycle.cs` +
`tools/beep-mcp-server/src/autonomy.ts`. `npm run live` runs 14 checks against a real
Godot 4.7 editor, including all four Phase 4 tools. · [back to roadmap](MCP_ROADMAP.md)

---

## Why

This repo has two automated gates and a hard rule about them:

> `dotnet build` and `validate_scenes.sh`. **Neither gate runs the game.** Compile-clean +
> validator-PASS says the code loads, not that it works.

Both are shell commands an agent may already be able to run *outside* MCP — but the ones
that matter most, it cannot run at all: **it cannot make Godot import, reload, or play
anything.** After writing a script it cannot trigger the build Godot needs to register the
class. After baking textures it cannot force the filesystem scan that makes them loadable.
After editing a scene it cannot play it and look.

That gap is why the honesty rule in `CLAUDE.md` exists at all:

> **A ✅ in this repo's docs has historically meant "I wrote it", not "I ran it."**

Phase 4 makes "I ran it" achievable, which is the only thing that retires that rule.

## Deliverables

### 4.1 Editor lifecycle — `editor.*`

```
editor.rescan_filesystem     → EditorFileSystem.Scan()   (baked PNGs become loadable)
editor.reload_scripts        → C# hot reload
editor.build                 → trigger Godot's C# build; return errors/warnings structured
editor.save_all
```

`editor.build` is the keystone. Today an agent writes C# and simply hopes; with this it
gets the compiler's answer — file, line, code, message — as data.

### 4.2 Play control — `play.*`

```
play.scene   { path }        // play a specific scene
play.current
play.stop
play.state                   → running? which scene? elapsed?
```

Combined with Phase 3's `view.capture` and `log.tail`, this is the **first time anything in
this repo can verify that a scene actually works** rather than merely loads. Every "compile
verified, not run" caveat in this project's history traces to the absence of these four
calls.

### 4.3 Gate runners — `gate.*`

```
gate.validate_scenes    → run validate_scenes.sh, return per-check pass/fail + failing lines
gate.build              → dotnet build, structured diagnostics
gate.all                → both, ordered, short-circuiting
```

Parse the output rather than returning a wall of text: `validate_scenes.sh` prints one
section per check and a final `PASS:`/`FAIL:`, which maps cleanly to
`{check, ok, failures[]}`. An agent should be able to ask "did the texture check pass" and
get a boolean.

### 4.4 Headless verification — `headless.*`

```
headless.run { scene, seconds, capture_at[] }
  → { exit_code, warnings[], errors[], captures[] }
```

Run a scene in a headless Godot for N seconds, collect warnings/errors and screenshots at
given timestamps, and exit. This is the CI-shaped primitive: it turns "does the main menu
come up without warnings" into one call, and it is what would have caught the inert save
system, the unthemed screens, and the dead texture pipeline years earlier.

Needs the Godot binary path — take it from an `BEEP_GODOT_BIN` env var and fail with a
clear message when unset. **Note:** no Godot binary is currently on this machine's PATH,
so this deliverable will need one installed to be verifiable at all.

### 4.5 The verify loop, as a documented recipe

Not code — a documented sequence in the server README, because an agent that has these
tools still needs to know the order:

```
1. scene.snapshot                          (Phase 3)
2. bridge.batch { dry_run: true }          (Phase 1)  — predict
3. bridge.batch                            (Phase 1)  — apply, one undo entry
4. gate.build → gate.validate_scenes       (4.3)      — do the gates still pass?
5. play.scene + view.capture + log.tail    (4.2/3.3)  — does it actually work?
6. scene.diff                              (Phase 3)  — did only what I intended change?
7. on failure: Ctrl-Z equivalent via undo  (Phase 1)
```

Step 7 is only possible because Phase 1 routed writes through `EditorUndoRedoManager` — the
loop is safe to run unattended precisely because every step is reversible.

## Tasks

- [x] `editor.rescan_filesystem` / `editor.reload_scripts` / `editor.save_all`
- [x] `play.scene` / `play.current` / `play.stop` / `play.state`
- [x] `beep_gate_build` — **structured** C# diagnostics (file, line, column, code,
      severity), deduped across target frameworks. Warnings are counted, not listed: this
      project carries ~148 pre-existing nullable warnings that would bury the errors.
- [x] `beep_gate_scenes` — `validate_scenes.sh` parsed per check, so "did the texture
      check pass" is a boolean rather than a grep
- [x] `beep_gate_all` — short-circuiting; a validator run against code that does not
      compile tells you nothing
- [x] `beep_headless_run` — real Godot, warnings/errors returned as data, with
      `import_only` for the after-a-bake case. Names `BEEP_GODOT_BIN` when unset.
- [x] `npm run live` — 14 checks against a live editor, Phase 4 included
- [x] Verify-loop recipe in the server README

**Design note — the gates deliberately do NOT go through the bridge.** `beep_gate_build`,
`beep_gate_scenes` and `beep_headless_run` spawn host processes from the MCP server. An
agent needs them *precisely* when Godot is closed, mid-crash, or refusing to load the addon
after a bad script — exactly when the bridge cannot answer. Routing them through Godot
would make them useless in the only situations that matter.

**`editor.build` is NOT a bridge method.** Triggering Godot's own C# build from inside a
`[Tool]` script running in that same assembly is a reload-during-execution hazard;
`beep_gate_build` shells out to `dotnet build` instead, which is what Godot does anyway.

## Verification

1. **Build feedback:** introduce a deliberate C# error, call `editor.build`, and confirm the
   response names the file and line. Fix it; confirm clean.
2. **Rescan:** bake textures, call `editor.rescan_filesystem`, and confirm a previously
   unloadable PNG now loads (`ResourceLoader.Exists` true).
3. **Gate parsing:** break one `theme_type_variation`, run `gate.validate_scenes`, and
   confirm the failing check is identified **by name** with the offending line. Restore;
   confirm PASS.
4. **Play + see:** `play.scene` on `main_menu.tscn`, `view.capture`, `log.tail` — the
   capture shows the menu and the log is warning-free.
5. **Full loop:** run steps 1–7 on a real change (e.g. restyle a header), and confirm the
   undo at step 7 restores the `.tscn` byte-for-byte.
6. **Headless:** `headless.run {scene: main_menu, seconds: 5}` returns exit 0 and an empty
   error list.

## Out of scope

CI infrastructure (GitHub Actions etc.) — this phase provides the primitives a pipeline
would call, not the pipeline. Deciding *what* to build: these tools verify, they do not
design.
