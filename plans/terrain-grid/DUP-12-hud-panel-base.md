# DUP-12 — Every HUD panel on the panel base; one enum button bar

**Type:** duplication fix · **Area:** `ecs/grid/ui/*` (16 files) · **Status:** proposed 2026-09-08 · **Effort:** M (1–2 days) · **Risk:** low (HUD only; scenes bind by node name and keep working)

## Finding

`GridPanelComponent` (`ui/GridPanelComponent.cs`) was introduced as the base "every one of them" uses, owning `SetEditedOwner`, `FindControl<T>` (three-tier lookup) and `SafeName`. Six of sixteen panels still derive from `Control` and carry private copies:

| Panel | Private `SetEditedOwner` | Private three-tier `Find*` methods |
|---|---|---|
| `GridObjectInspectorComponent` | 1060-1066 | `FindPanel`, `FindTitleLabel`, `FindDetailsLabel` (964-1001) |
| `GridCalendarHudComponent` | 760-766 | `FindDateLabel`, `FindDayProgress`, `FindAdvanceButton` (694-725) |
| `GridInteractionModeBarComponent` | 670-676 | `FindModeButton` (590-601) |
| `GridInteractionStatusComponent` | 311-317 | `FindStatusLabel` (193-202) |
| `GridToolPaletteComponent` | 315-320 | `FindToolButton` (253-263) |
| `GridWorkerSpawnerPanelComponent` | 283-289 | `FindTitleLabel`, `FindCountLabel`, `FindSpawnButton` (216-247) |

Beyond the base bypass, four further patterns are copied:

- **Enum button bar.** `GridInteractionModeBarComponent` (~300 lines) and `GridToolPaletteComponent` (~320 lines) are structurally identical: `Bound*Names`/`Bound*Paths` exports, `Visible*()` iterator over `Show*` flags, `Add*Button`, `Find*Button`, `Bind*Button`, `TryParse*` (an `Enum.TryParse` then a whitespace/dash-stripped compare — byte-identical apart from the enum type), `RefreshSelection` via `SetPressedNoSignal`, `_connectedButtons` bookkeeping.
- **`_connectedButtons` + `DisconnectButtons()`** in `GridBuildToolbarComponent`, `GridInteractionModeBarComponent`, `GridToolPaletteComponent` (3×).
- **Single-button connect/disconnect pair** (`Connect*Button`/`Disconnect*Button`/`_connected*Button`) in `GridCalendarHudComponent` and `GridWorkerSpawnerPanelComponent` (2×).
- **Incremental roster cache** (`_cached*`, `Rebuild*Cache`, `PruneInvalid*`, `Invalidate*Cache`, `On*Spawned/Placed` append, `Connect/Disconnect` source) in `GridProductionPanelComponent` and `GridWorkerStatusPanelComponent` (2×).

Also noted while reading: `GridInteractionStatusComponent.ConnectSignals` runs once (`_connected`) in `_Ready`; a reference that resolves later (empty path filled by scene-wide search) never gets its signals connected — the status stops updating for that source. See ENH-14.

## Design

1. **Move the six panels onto `GridPanelComponent`.** Their `Find*` methods become one-liners over `FindControl<T>(path, "Name")`; the private `SetEditedOwner` copies go.
2. **`GridEnumButtonBarComponent<TEnum>`** is not possible as a Godot `[GlobalClass]` (generics). Instead a non-generic `GridToggleBarComponent : GridPanelComponent` owning the bar mechanics (bound names/paths, generated row, bind/find/connect, `RefreshSelection(int selectedIndex)`, `TryParseName`), with the two concrete panels supplying `IReadOnlyList<(string Name, string Label, string Tooltip)> Options`, `int SelectedIndex`, and `void Select(int index)`. The two panels drop to ~60 lines each.
3. **`GridButtonBindings`** (small helper on the base): `Bind(Button, Action)`, `Unbind(Button)`, `UnbindAll()` — replaces the three `_connectedButtons` lists and the two single-button pairs.
4. **`GridRosterCache<T>`** (plain generic class, not a Node): `Rebuild(Func<IEnumerable<T>>)`, `Prune()`, `Append(T)`, `Invalidate()`, used by the production and worker panels.

## Guards

- Pin: every `ui/*Component.cs` derives from `GridPanelComponent` (or `GridListPanelComponent`/`GridToggleBarComponent`); `private void SetEditedOwner(` appears only in `GridPanelComponent.cs`. Mutation: revert one panel to `: Control` → fails.
- Pin: `TryParseMode(`/`TryParseAction(` declared nowhere; `_connectedButtons` declared only in the base helper.
- Existing HUD smoke assertions (`VisibleModeButtonCount`, `VisibleToolButtonCount`, `SelectedModeName`, generated-vs-authored binding) stay green; add one: an authored `Mode_Build` button bound by convention still selects Build after the refactor.

## Dependencies / collisions

`ecs/grid/ui/` — the other session's scope is `ecs/grid/` core; the panels are lower-collision but still coordinate. Assumes DUP-05 (`GridIds.NodeName`) for `SafeName`. Note the standing rule: grid components orchestrate, kit renders — this plan only moves plumbing, it does not change how the panels look.

## Out of scope

Replacing panel rows with kit widgets (`KitRow`) — that is the UI-kit plan's item 5b and stays there.
