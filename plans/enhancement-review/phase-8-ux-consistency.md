# Phase 8 — UX, accessibility & template consistency

## Why

Everything here makes the framework *feel* finished rather than fixes breakage: widgets a
controller-only player can't operate, scene templates that differ for no stated reason, and
small export additions. Last because nothing above depends on it — but it's the layer a
Godot developer evaluates first.

## The work

### Keyboard / gamepad accessibility (all mouse-only today)

| Widget | Site | Add |
|---|---|---|
| `CarouselComponent` | `ecs/ui/CarouselComponent.cs:88-89` | `ui_left`/`ui_right` handling driving `Next()`/`Previous()` when focused |
| `ChipComponent` | `ecs/ui/ChipComponent.cs:76-80` | focus mode + `ui_accept` activation/removal |
| `RatingComponent` | `ecs/ui/RatingComponent.cs` (click-only, `:61`) | focus + `ui_left`/`ui_right` to adjust stars |
| `ModalComponent` | `ecs/ui/ModalComponent.cs:47-74` | grab focus on `Open()`; `ui_cancel` closes |
| `LoadGameMenuComponent` | `ecs/ui/LoadGameMenuComponent.cs:31-45` | initial focus on first slot / Load button — a controller-only player currently can't load a save |

(SaveGameMenuComponent gets the same initial-focus treatment as its Load sibling.)

### Scene-template consistency

- **Six genre mains omit `HudComponent`** (**Decision D5**) — `rpg_main:22-52`,
  `survival_main:26-56`, `racing_main:21-51`, `strategy_main:27-48`, `cardgame_main:27-48`,
  `citybuilder_main:27-48` all carry a `GameFlowComponent` (which tracks Score) plus a
  *static* stats HUD, while puzzle/platformer/shooter/topdown ship a live `Hud`. Defensible
  (their stats are genre-specific), but make it deliberate: add a `HudComponent` where a
  generic score label exists, or a "developer wires stats here" doc note so the dead-looking
  labels aren't mistaken for a binding bug. Coordinate with Phase 5's HUD decision.
- **Empty `UI` CanvasLayers** — `platformer_main:61`, `shooter_main:76`, `topdown_main:88`
  each ship an empty `UI` layer nothing populates (the other mains don't). Use it or drop it.
- **Generation-time-only path** — `topdown_main:124` sets
  `GameFlow.PauseMenuPathOverride = "res://scenes/ui/topdown/pause_subscreen.tscn"`, which
  exists only after project generation; only topdown does this. Confirm the generator emits
  it (it does ship `pause_subscreen.tscn`) and add a comment marking the override
  generation-time-only, so running the template raw isn't mistaken for a bug.

### Small API surface additions

- `BossHealthBarComponent` — `ecs/ui/BossHealthBarComponent.cs:41` — boss name hardcoded
  `"BOSS"`; add a `BossName` `[Export]`.
- `HungerStaminaComponent` — `ecs/HungerStaminaComponent.cs:116` — thirst's temperature
  drain is gated on `TemperatureAffectsHunger`; add `TemperatureAffectsThirst` (or rename to
  cover both and doc it).
- `SettingsComponent` — `ecs/ui/SettingsComponent.cs:146-149` — a corrupt
  `user://settings.cfg` is treated identically to a fresh install, silently. Warn when the
  file exists but fails to parse (parse-failure ≠ absence — the save/load lesson).

## Gotchas

- Focus handling must not steal focus from the game: only grab when the widget's own UI is
  the active surface (modal open, menu scene) — never from `_Ready` of an in-game HUD widget.
- D5's "add HudComponent" option must not invent per-genre stats (speed, laps, resources) —
  that's the developer's canvas; only a generic score label qualifies. See CLAUDE.md § Scope.

## Verify

1. Build + validator.
2. Editor, controller-only pass (unplug the mouse): main menu → settings → save → load →
   carousel/rating widgets in a scratch scene — everything reachable and operable.
3. Corrupt `user://settings.cfg` deliberately → one warning, defaults load, file not
   silently treated as fresh.
4. Open each of the six genre mains → either a live HUD binds, or the doc note is visible on
   the stats node.
