# Phase 4 — Autonomy

**Goal:** the agent can close its own loop — change, verify, read the result, correct —
without a human relaying build output.

**Status:** ⬜ not started · depends on Phases 1–3 · [back to roadmap](MCP_ROADMAP.md)

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

- [ ] `editor.rescan_filesystem` / `reload_scripts` / `build` / `save_all`
- [ ] Structured C# diagnostics from `editor.build` (file, line, code, severity)
- [ ] `play.scene` / `play.current` / `play.stop` / `play.state`
- [ ] `gate.validate_scenes` with per-check parsing
- [ ] `gate.build` + `gate.all`
- [ ] `headless.run` (+ `BEEP_GODOT_BIN` discovery and a clear error when missing)
- [ ] Verify-loop recipe in `tools/beep-mcp-server/README.md`
- [ ] Roadmap note: which historical defects this loop would have caught

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
