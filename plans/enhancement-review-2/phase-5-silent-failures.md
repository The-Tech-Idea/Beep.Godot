# Phase 5 — Silent-failure warnings (round 2)

## Why

The "never fail silently" sweep in round 1 caught most of the class, but the deeper pass found more
null-resolves and null-export paths that disable a feature without a word. Same one-to-three-line
fix each: `GD.PushWarning` naming what it got, what it needed, and what to do — or a working default.

## The work

| Component | Site | What goes silent |
|---|---|---|
| `AttackComponent` | `ecs/AttackComponent.cs:91-103` | ranged attack requested (`IsRanged`/`weapon.IsRanged`) but `projScene` is null → **silently falls through to `DealMeleeDamage`** (a point query that usually hits nothing). Warn when `ranged && projScene == null` — a null export disabling the feature. |
| `DoorSwitchComponent` | `ecs/DoorSwitchComponent.cs:43-45` | `DoorPath` set but unresolved → `_door` null; `Toggle()` emits `SwitchToggled` controlling nothing. Warn when `DoorPath` is non-empty but unresolved (empty = legitimate signal-only mode). |
| `BeepGenreScene` | `ecs/BeepGenreScene.cs:167-169` | `ResourceLoader.Load<PackedScene>` returns null after a passing `Exists` check → silent. Warn. |
| `BootComponent` | `ecs/BootComponent.cs:42-48` | `SettingsComponent.Instance` null → saved audio/display/locale silently not applied. Warn. |
| `AchievementToastComponent` | `ecs/ui/AchievementToastComponent.cs:29` | `ListenToGameApp` true but `GameApp.Instance` null at `_Ready` (autoload order) → subscribes to nothing silently. Warn. |
| `BuildMenu`/`Research`/`UnitPanel`/`Crafting` | `ecs/scenes/…` `WireBuild`/`WireTech`/`WireAction`/`WireRecipe` | `GetNodeOrNull` silently skips a missing item button (sibling `shooter/LevelUpChoice.cs:33` PushWarns). Add `else GD.PushWarning` so a future scene-rename fails loud, not silent. |

## Decision D — level-complete parity

`strategy`, `citybuilder`, `cardgame` declare no `nav_wiring.LevelCompletePath`, while the other 7
genres route level-complete to a results/summary screen. If `GameFlowComponent` ever fires
level-complete in those three, it falls through to the game-over (loss) screen — the exact trap
`level_summary.tscn` was added to fix. Likely intentional (sandbox genres have no "level complete"),
but decide: add a `LevelCompletePath` for parity, or document these three as deliberately
completion-less. (`catalogs/skins/{strategy,citybuilder,cardgame}/genre.json`.)

## Gotchas

- Warn **once**, not per frame — these are `_Ready`/`Interact`-time resolves, so a single warning is
  natural; only `AttackComponent` is in a hot path (gate it so it doesn't spam every attack — warn on
  the first null-projScene attack via a latched flag).
- `AttackComponent`: the fix pairs with round 1's "only emit `Attacked` when it fired" — a null-ranged
  attack that falls through to a no-op melee should warn AND not claim a hit.
- Don't warn on *optional* seams that already have working defaults — this rule targets features that
  silently disable.

## Verify

1. Build + validator.
2. Editor: an `AttackComponent` with `IsRanged=true` and no `ProjectileScene` → one warning, no
   pretend-melee.
3. A `DoorSwitchComponent` with a bad `DoorPath` → warning names the path.
4. Rename an item button in `build_menu.tscn` → the wire helper warns instead of silently skipping.
