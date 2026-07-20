# Master TODO Tracker — Enhancement Review Round 4 (new ground)

> Fourth sweep — but NOT a re-review of the ecs/ components (rounds 1–3 exhausted those). Round 4
> deliberately targets the areas **no prior round covered**: the `core/` generators, editor dock,
> utility/data classes, the MCP bridge, and the entire `beep_ui` GDScript addon — plus a skin-JSON
> validation and a round-3 regression re-check. Per-phase detail beside this file.
>
> **Result:** the untouched areas held ~15 real bugs (several user-facing); the well-reviewed areas +
> skin JSON came back clean and all round-3 fixes verified HELD.
>
> **Scope:** framework only. The `godot_mcp` addon is portable/project-agnostic — fix its own bugs but
> never reference Beep types from it.

---

## How this review was produced

Four parallel passes over previously-unreviewed ground:

| Pass | Coverage | Result |
|---|---|---|
| core/ generators + editor dock | 10 files | 5 bugs, 1 gap, polish |
| core/ utilities + MCP bridge | 15 files + godot_mcp | 7 bugs, 4 gaps, polish |
| **beep_ui GDScript addon** (never reviewed) | 29 `.gd` files | 3 gaps, polish |
| Skin JSON + docs + round-3 regression | 50 themes/350 palettes, docs, 8 fixes | JSON **clean**, all 8 HELD, 3 doc fixes, 1 base-call straggler |

**Round-3 verified: all 8 fixes HELD.** Skin catalog is 100% schema-valid. The C# component surface,
scenes, and signals (rounds 1–3) remain clean. Everything below is in code the component reviews
never reached.

---

## Progress

- [x] **Phase 0 — Review** — the 4-pass sweep above; findings archived per phase doc.
- [x] **[Phase 1 — core/ utility bugs](phase-1-core-bugs.md)** — **done, build 0 errors, validator PASS.**
      FormBuilder Vector2 crash + submit-button-vanishes + setter warning fixed; TreeView phantom root
      deleted + POCO-metadata read via `.Obj`; DataGrid rows rebuilt as PanelContainer overlays (stripe +
      full-row click) + `ParseSize`/count guards; DebugConsole `AcceptEvent` moved into Up/Down only (now
      typeable) + BBCode line-buffer trim; GridNavigator ragged-grid infinite loop bounded; MCP bridge
      `ProjectSettings.Save()` editor-gated; `Str` coerces via `ToString()`.
- [x] **[Phase 2 — Generation & save/load correctness](phase-2-generation.md)** — **done, build 0 errors,
      validator PASS.** `StampProject` now removes `TurnManager`/`GameStateManager` autoloads when their
      flags are false (real-time genre no longer inherits the turn axis); `EntityStateData` +
      `PlayerStateData` positions decomposed to `position_x`/`position_y` (survive JSON now); dock Save
      persists Palette + Geometry and Reload restores the palette after the theme cascade; `"Modern"` theme
      match is case-insensitive; `RefreshFilesystem` + `MainScene` guarded; stale run-scene hint softened.
- [x] **[Phase 3 — beep_ui GDScript effects/widgets](phase-3-beepui.md)** — **done (GDScript-only, verified
      by reading; C# build/validator unaffected & still green).** Typewriter retires finished entries and
      emits `effect_completed` (no more every-frame re-assign); Bob zeros its offset in `stop()`; toast
      host centers on the host's own width + factory host anchored full-rect; Theme Studio calls
      `_update_preview()` on ready; theme_applier disconnects hover/press handlers in `_exit_tree`.
- [x] **[Phase 4 — Gaps: dead API surfaces & missing wiring](phase-4-gaps.md)** — **done, build 0 errors,
      validator PASS.** `BeepDropdown` now builds the search `LineEdit` (filter-as-you-type works);
      `BeepDataBinder` two-way writes back via target signals + `RefreshToSource`, `OneWayToSource` polls
      the correct direction; (`BeepTreeView` POCO metadata already handled in Phase 1);
      `SaveEncrypted` returns `bool` + warns; A* uses octile cost/heuristic and refuses corner-cuts,
      `Rfc2898DeriveBytes` disposed; `BeepStateMachine` warns on unknown state + snapshots the EventBus
      listener list; `SceneTransitionComponent` calls `base._ExitTree()`.
- [x] **[Phase 5 — Polish & docs](phase-5-polish-docs.md)** — **done, build 0 errors, validator PASS.**
      `FILE_FORMATS.md` now documents the `dialog` texture slot (14 total) + geometry 13-numbers;
      `CLAUDE.md` component counts refreshed (206 `[GlobalClass]`, WorldComponent 18); `WeatherForecast`
      enum renamed `Rainy`/`Stormy` → `Rain`/`Storm` to match `WeatherSystemComponent`, with
      `WeatherForecastUI` color/icon lookups updated in lockstep (legacy names kept as aliases).
      (StateMachine/EventBus/A*/Rfc2898 landed in Phase 4.)

**All 5 phases complete — build 0 errors, validator PASS throughout. ~15 real bugs + ~8 gaps/polish +
3 doc fixes resolved, all in code the ecs/-focused rounds 1–3 never reached.**

## Verification gates (every phase)

1. `dotnet build` → 0 errors.
2. `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` → PASS.
3. GDScript changes can't be compile-checked by `dotnet` — they're verified by reading + the editor
   pass; note that in the phase.

## Decisions

| # | Decision | Phase |
|---|---|---|
| A | `BeepDropdown` filter-as-you-type: wire a search `LineEdit` into the popup, or drop the claim? (recommend wire) | 4 |
| B | `GameStateData.Position` JSON fix — decompose to `position_x/position_y` (recommend, mirrors `PlayerMovementStateData`). Verify vs 4.7. | 2 |

## Cross-references

- **enhancement-review-1/-2/-3** — the ecs/ component + scene + signal work, all complete & verified.
  Round 4 is the complement: the generation/utility/GDScript layers those never touched.
