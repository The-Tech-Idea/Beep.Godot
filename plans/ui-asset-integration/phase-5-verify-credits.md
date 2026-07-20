# Phase 5 — Verify, extras & credits

## Why

Textures are visual — the only real test is the editor. This phase is the eyes-on pass, the
fallback-safety confirmation, the credits/licensing hygiene, and the optional cursor polish.

## The work

### 5a. Editor verification (the real gate)
Per genre (all 10):
1. Generate a project for the genre (dock → Genres → Generate), or open the genre's `*_main.tscn`.
2. Open `main_menu` / `pause_menu` / a results screen.
3. Dock → pick the genre + each of its themes → **Apply to all ThemePresetComponents in open scene**.
4. Confirm: buttons/panels show Kenney art; corners crisp at the scene's real sizes; hover/press/
   disabled states read correctly; pixel themes are sharp.
5. F5 the genre main and click through the menu loop — textures persist through navigation and the
   `ThemePresetComponent` button animations (offset-transform hover/press) still play over the
   texture.

### 5b. Fallback safety (must stay intact)
- Rename one theme's `textures/<genre>/<theme>/` folder aside → reload → that theme renders the
  procedural `StyleBoxFlat`, **no error** in Output → C# (the guard is `SkinCatalog.cs:513`).
- This proves the scope extension is additive: a developer can delete the UI textures and still ship.

### 5c. Credits & licensing
- `textures/_kenney/LICENSE_Kenney_CC0.txt` present (Phase 1).
- Add a **CREDITS** line to `addons/beep_game_builder_cs/README.md` and the root `README.md`:
  *"UI textures: Kenney (kenney.nl), CC0."* Not required by CC0, but correct and kind.
- Note the source pack + version in the credits so an updater knows where they came from.

### 5d. Optional extras (opt-in, Decision E)
- **Per-genre mouse cursor** (Cursor Pack / Cursor Pixel Pack): a genre could set a themed hardware
  cursor via `Input.SetCustomMouseCursor(tex)`. If included, wire it through a small
  `CursorComponent` or a `GameApp` setting, not baked per scene. Pixel genres → Cursor Pixel Pack.
- **Mobile Controls** (touch dpad/buttons): out of scope for desktop genres; note as a future
  "mobile export" initiative — the pack is available if the framework grows a touch-input layer.

## Gotchas

- **`Input.SetCustomMouseCursor` is global** — set it on scene enter and clear/restore on exit, or it
  bleeds across scenes (same lifecycle discipline as the enhancement-review Phase 1 leaks).
- **Don't let cursors creep into every scene** — it's an opt-in polish, gated behind a setting, or
  the framework starts dictating game feel (scope).
- **Export size:** importing only the selected slot PNGs (Phase 1) keeps the addon's texture
  footprint modest. If it balloons, that's a sign the copy script pulled whole packs — trim.

## Verify (final, whole-initiative)

1. `dotnet build` → 0 errors; `validate_scenes.sh` → PASS.
2. All 10 genres, spot-checked in editor, show themed Kenney UI that matches the theme name.
3. Fallback confirmed (5b) — no hard dependency on the textures.
4. Credits + license shipped.
5. Update the tracker's Progress list; record any theme that ended up procedural (Decision B/D) so
   it's a known state, not a silent gap.
