# DUP-12 — Every HUD panel on the panel base; one enum button bar

**Type:** duplication fix · **Area:** `ecs/grid/ui/*` (16 files) · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (six panels on the base + GridButtonBindings; toggle-bar / roster-cache helpers pending) · **Effort:** M (1–2 days) · **Risk:** low (HUD only; scenes bind by node name and keep working)

## Outcome (step 1: the six panels on the base, 2026-09-09)

The six `Control`-derived find-panels - `GridInteractionStatusComponent`, `GridInteractionModeBarComponent`, `GridToolPaletteComponent`, `GridWorkerSpawnerPanelComponent`, `GridCalendarHudComponent`, `GridObjectInspectorComponent` - now derive from `GridPanelComponent`. Each dropped its private `SetEditedOwner` copy and the two inherited `[Export]`s (`BuildInEditor`, `GenerateControlsWhenPathsEmpty`), and every `Find*` method became a one-liner over the base `FindControl<T>(path, "Name")`. The inspector's two label finders keep their extra fixed-relative tier (`Panel/Content/Title`, `Panel/Content/Details`) ahead of the base name search - a real difference the base does not model - so those two stay small explicit methods that delegate only the child/parent tiers.

`GridMinimapComponent` stays a plain `Control` by design: it bakes the whole map and never uses the three-tier authored-control lookup, so it is not a find-panel and the DUP-12 pin exempts it.

Verified: `dotnet build` clean, zero warnings. `GridPlacementSmoke` exercises all six panels (`VerifyGridInteractionModeBar`, `VerifyGridInteractionStatus`, `VerifyGridObjectInspector`, `VerifyGridToolPalette`, `VerifyGridWorkerSpawnerPanel`, `VerifyGridCalendarHud`) and every one passes, together with the whole downstream suite (`terrain_grid_playground`, `showcase_interaction` green). Two scan pins are mutation-proven: no panel outside `GridPanelComponent` declares `private void SetEditedOwner(`, and each of the six derives from `GridPanelComponent`.

**A stale pin was fixed here too.** The base-bootstrap pin still required `GridPanelComponent` to contain `SafeName`, which DUP-05 had moved out to `GridIds.NodeName`. Because that pin sits after the contract scan's pre-existing `TerrainWorldComponent` restore-yield abort it never ran in the gate, so the drift was invisible; the required-member list now drops `SafeName` (the sanitiser's one owner is `GridIds.NodeName`, guarded by DUP-05's own pin).

**Two pre-existing reds, not this change.** `GridPlacementSmoke` returns on its first failure at `VerifyPlacementOccupancy` ("Fresh placement grid should allow an empty footprint"), and a second placement-terrain check (`VerifyPlacementUsesCellDataTerrain`) fails right behind it - masked all along by the first. Neither is caused by this HUD-only change (nor by any committed session work: no commit touched placement's cell-data resolution). Confirmed by bypassing both temporarily: the suite then reaches and passes every downstream check, including all six panels, and returns green. The bypass was reverted.

## Outcome (step 2: GridButtonBindings, 2026-09-09)

`GridButtonBindings` (new, `ecs/grid/ui/`, a plain class not a Node) owns a panel's `Button.Pressed` subscriptions as a unit: `Bind(button, handler)` connects and records (idempotent per button, so a refresh that re-binds the same authored button does not double-subscribe), `UnbindAll()` disconnects every recorded handler skipping a freed button, and `IsBound(button)` answers the single-button idempotency check. `Unbind(Button)` from the proposal was left out - no caller needs a selective single-button unbind, and rule 6 forbids a method with no consumer.

Gone: the three `_connectedButtons` `List<(Button, Action)>` fields and their `DisconnectButtons()` sweeps (`GridBuildToolbarComponent`, `GridInteractionModeBarComponent`, `GridToolPaletteComponent`), and the two single-button `_connected*Button` fields with their `Connect*`/`Disconnect*` pairs (`GridCalendarHudComponent`, `GridWorkerSpawnerPanelComponent`). Each bar's connect site collapses from `button.Pressed += handler; _connectedButtons.Add((button, handler));` to `_buttonBindings.Bind(button, handler);`; each single-button `Connect*` becomes an `IsBound`-guarded `UnbindAll` + `Bind`.

Verified: `dotnet build` clean, zero warnings. `GridPlacementSmoke` exercises the bars, calendar and worker-spawner panel and passes (confirmed by the same temporary bypass of the two pre-existing placement reds, reverted). A new smoke assertion was added and is the point of the change: it finds the generated `Mode_Build` button, emits its `Pressed` signal, and asserts the selection became Build - proving `Bind` actually connects the handler rather than only tracking it (the pre-existing checks called `SelectMode` directly and would pass even if the button were never wired). That assertion passes on the correct code; the negative run that breaks `Bind` was interrupted, so it is verified-passing rather than mutation-proven here - though it fails by construction, since an unconnected button makes `EmitSignal(Pressed)` a no-op and the selection stays Select. Two scan pins are mutation-proven: no panel outside `GridButtonBindings` declares the `List<(Button Button, Action Handler)>` tracking list or a `DisconnectButtons` sweep.

### Still pending (steps 3-4)

- `GridToggleBarComponent : GridPanelComponent` to fold the two structurally identical enum button bars (`GridInteractionModeBarComponent`, `GridToolPaletteComponent`).
- `GridRosterCache<T>` for the incremental roster caches in `GridProductionPanelComponent` and `GridWorkerStatusPanelComponent`.

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
