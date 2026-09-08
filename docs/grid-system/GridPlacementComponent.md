# GridPlacementComponent

`Node2D` that drives the full grid-placement interaction: a preview object that follows the mouse cell, click-to-place, cancel, footprint occupancy, cost charging, and terrain-based placement validity. It pairs with `GridProjectionComponent` for grid math, and UI is expected to call `BeginPlacement` after a toolbar/build-menu selection while this component owns preview movement, snapping, and input for the rest of the interaction.

Occupancy and navigation blocking are independent: a walkable garden can still occupy building space. A build definition's nonempty `AllowedTerrainKinds` replaces the scene's terrain-kind policy, allowing offshore structures without weakening rules for ordinary buildings. Terrain kind and relief come from the live cell store, not generated data layers. Placed objects bind explicitly to the creating grid, placement controller and navigation component; their `GridObjectComponent` reserves and releases the footprint and follows grid geometry changes.

## Public API

- `public enum PlacementState { Idle, Placing }`.
- `[Signal] PlacementStartedEventHandler(string id)` / `PlacementMovedEventHandler(string id, int x, int y, bool valid)` / `PlacementPlacedEventHandler(string id, Node2D placed, int x, int y)` / `PlacementCancelledEventHandler(string id)` / `PlacementRejectedEventHandler(string id, int x, int y, string reason)`.
- `GridPath`, `PlacementRootPath`, `ResourceWalletPath`, `CellDataPath`, `NavigationPath`: explicit paths resolve against the current scene on every placement query. Missing explicit grid/cell/navigation/root bindings reject placement rather than reusing an old node or a fallback root.
- `RequireLevelFootprint` defaults to true. Every footprint cell must have the same live `terrain_relief` level. Disable it for intentionally slope-spanning structures. This does not flatten terrain or construct supports.
- `[Export] public PackedScene? PlacementScene` / `public Texture2D? PreviewTexture` / `public string PlacementId` / `public Vector2I Footprint` — what to place and its footprint size.
- `[Export] public bool UseMouseInput`, `ChargeCostOnConfirm`, `KeepPlacingAfterConfirm`, `MarkPlacedCellsOccupied`, `MarkPlacedCellsBlockedInNavigation`, `TreatCellDataBlockedAsUnplaceable`, `TreatBlockedTerrainKindsAsUnplaceable` — placement policy toggles.
- `[Export] public Godot.Collections.Array<string> BlockedTerrainKinds` (defaults to `GridTerrainRules.DefaultBlockedTerrainKinds()`) / `public Godot.Collections.Array<string> AllowedTerrainKinds` (empty = allow all).
- `[Export] public bool SetZIndexFromY` / `public int ZIndexOffset` — Y-sorted draw order for the placed node and its preview.
- `[Export] public Color ValidPreviewColor / InvalidPreviewColor`.
- `public Vector2I EffectiveFootprint` — `Footprint` clamped to at least 1 on each axis.
- `public PlacementState State { get; }` / `public Vector2I CurrentCell { get; }` / `public bool CurrentCellValid { get; }`.
- `public void BeginPlacement(PackedScene scene, string id = "")` — ad hoc scene placement with no cost/navigation wiring.
- `public void BeginPlacement(GridBuildDefinition definition, bool chargeCostOnConfirm = true)` — pulls scene, preview, id, footprint, occupancy/navigation policy, allowed terrain, display metadata, and costs off the definition.
- `public void BeginPlacement(string id = "")` — the common entry both overloads funnel through; resets "active" placement state unless it was reached from the definition overload (tracked via a private `_pendingDefinitionPlacement` flag).
- `public void CancelPlacement()` — returns to `Idle`, clears the preview, emits `PlacementCancelled`.
- `public Node2D? ConfirmPlacement()` — validates the current cell, optionally charges cost from the resolved `GridResourceWalletComponent` (refunding on any later failure), instantiates and places the scene, marks occupancy/navigation per policy, configures a `GridObjectComponent` on it, emits `PlacementPlaced`, then either rebuilds the preview (`KeepPlacingAfterConfirm`) or finishes. Emits `PlacementRejected` with a reason string (`"occupied"`, `"missing_resource_wallet"`, `"missing_resources"`, `"missing_scene"`, `"scene_root_not_node2d"`) on each failure path instead of throwing.
- `public bool MovePreviewToCell(Vector2I cell)` — programmatic preview move (for non-mouse-driven UI); returns whether the resulting cell is valid.
- `CanPlace(anchorCell)` checks every footprint cell for occupancy, terrain policy, finite projection, navigation bounds (when navigation is wired), and level foundations. Invalid cell sentinels are rejected. `ConfirmPlacement` runs these checks again before charging or creating an object; preview validity is not authorization to build.
- `public void SetOccupied(Vector2I cell, bool occupied)` / `public bool IsOccupied(Vector2I cell)` / `public void ClearOccupied()` / `public void SetFootprintOccupied(Vector2I anchorCell, bool occupied)` / `public Godot.Collections.Array<Vector2I> GetOccupiedCells()` — the placement occupancy set, exposed for external inspection and save/restore.

## Dependencies

- Resolves `GridProjectionComponent` (`MouseCell()`, `CellToWorld(Vector2I)`), a placement-root `Node` (defaults to `GetParent()`), `GridResourceWalletComponent` (`Spend`/`Refund`), `GridCellDataComponent` (`GetTerrainKind` through the static `GridCellRules.TerrainKindAt`, `HasFlag(Blocked)`), and `GridNavigationComponent` (`SetBlocked`). The terrain engine's data layers are never consulted for kind — cells are the one owner of the live map.
- Calls `GridTerrainRules.Normalize`/`.IsAllowed`/`.MatchesAny`/`.DefaultBlockedTerrainKinds()` (`GridTerrainRules.cs`) for every terrain-kind check.
- Reads `GridBuildDefinition` fields (`BuildId`, `Scene`, `PreviewTexture`, `EffectiveFootprint`, `OccupiesCells`, `BlocksNavigation`, `AllowedTerrainKinds`, `SetZIndexFromY`, `DisplayName`, `Category`, `Costs`) — outside this batch.
- On confirm, finds-or-creates a `GridObjectComponent` on the placed node (`EntityComponent.FindComponent`, `AddChild`) and calls its `Configure(...)`, also setting `ReservePlacementFootprint`, `ReserveNavigationFootprint`, and `ReserveFootprintOnReady = true` directly — `GridObjectComponent` is read in this same batch, confirming this call.
- Consumed by `GridWorldStateComponent` (also in this batch), which calls `GetOccupiedCells()`/`SetOccupied()`/`ClearOccupied()` to save and restore the occupancy set.

## Notes

- With `UseMouseInput = false`, frame updates do not overwrite a programmatic preview cell. Call `MovePreviewToCell` from the application's input controller.
- Navigation walkability is not itself a building rule: offshore structures may occupy water. Bounds, live terrain policy and occupancy remain distinct checks.
- Verification: `tests/terrain_placement_live_probe.gd` exercises square/isometric projection, negative cells, live flooding, relief restrictions, broken explicit bindings, confirmation recovery and demolition cleanup.

- `ConfirmPlacement`'s failure paths all report via signal (`PlacementRejected` with a reason string) rather than an exception or return-value error — a caller that ignores the signal and only checks the `Node2D?` return value still learns placement failed (null return), but not why.
- `BeginPlacement(string id = "")`'s reset-unless-from-definition logic (`_pendingDefinitionPlacement`) is the one place state threads across the three overloads; calling the parameterless/`string` overload directly after manually setting `PlacementScene`/`Footprint`/etc. on the exported properties (rather than through one of the other two overloads) is a supported but easy-to-miss fourth path — it behaves like the `PackedScene` overload (cost/navigation policy reset to defaults).
- Input handling (`_UnhandledInput`) is gated on `UseMouseInput && State == Placing`, and both `_Process`/`_UnhandledInput` are disabled entirely in the editor (`SetProcess(!Engine.IsEditorHint())`), so a `[Tool]` script instance never fights the editor's own mouse handling.
