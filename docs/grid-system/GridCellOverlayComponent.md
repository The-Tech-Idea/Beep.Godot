# GridCellOverlayComponent

Lightweight `Node2D` debug/placeholder renderer for `GridCellDataComponent`. It draws colored fills and outlines over grid cells (cleared, tilled, watered, planted, harvest-ready, blocked) so a farming/builder project has a legible visual of cell state before it has authored TileMap art for every state — the visual counterpart to `GridTileMapLayerBridgeComponent`, which does the same job through a real `TileMapLayer` instead of immediate-mode drawing.

It pairs with `GridProjectionComponent` for cell-corner geometry and `GridCellDataComponent` for state, resolving both by `NodePath` or scene-wide lookup. Two non-obvious things are called out in its own comments: `_Draw` iterates `GridCellDataComponent.EnumerateFlags()` rather than the public `GetCells()`, because `GetCells()` marshals a `Godot.Collections.Dictionary` per cell and this draw runs every editor frame and every runtime repaint; and the component listens to the cell data's `CellChanged`/`CellsChanged` signals at runtime (`ConnectCells`/`DisconnectCells`), not just the editor's per-frame `_Process` redraw — before that fix, tilled/watered state drawn once at startup went stale during play because nothing ever told the overlay to redraw outside the editor.

## Public API

- `[Export] public NodePath GridPath` / `[Export] public NodePath CellDataPath` — links to the `GridProjectionComponent` and `GridCellDataComponent` to read.
- `[Export] public bool DrawCells { get; set; } = true` — master switch for `_Draw`.
- `[Export] public bool DrawOutlines { get; set; } = true` — whether cell borders are drawn even when the fill is fully transparent.
- `[Export] public Color ClearedColor / TilledColor / WateredColor / PlantedColor / HarvestReadyColor / BlockedColor / OutlineColor` — per-state fill/outline colors.
- `[Export(PropertyHint.Range, "0.5,6,0.1")] public float OutlineWidth { get; set; } = 1.5f`.
- `public float EffectiveOutlineWidth` — `OutlineWidth` clamped to non-negative and substituted with `1.5f` if non-finite.
- `public override void _Ready()` — resolves references, enables `_Process` only in the editor (`SetProcess(Engine.IsEditorHint())`), and refreshes configuration warnings.
- `public override void _ExitTree()` — disconnects from the cell data component's signals.
- `public override void _Process(double delta)` — in the editor, calls `QueueRedraw()` every frame so edits made in the inspector show live.
- `public override string[] _GetConfigurationWarnings()` — warns if `GridPath` or `CellDataPath` is unset.
- `public override void _Draw()` — for every stored cell (via `EnumerateFlags()`), computes its fill color and draws it (and/or its outline) via `GridProjectionComponent.CellCorners`.
- `public int VisibleCellCount()` — count of stored cells whose resolved color has non-zero alpha.
- `public Color ColorForCell(Vector2I cell)` — resolves the cell's current flags and returns its color (`Colors.Transparent` if no cell data component).
- `public Color ColorForFlags(int flags)` — the priority table: `Blocked` > `HarvestReady` > `Planted` > `Watered` > `Tilled` > `Cleared` > transparent.

## Dependencies

- Resolves `GridProjectionComponent` at `GridPath` (or scene-wide via `EntityComponent.FindComponent`) and calls `CellCorners(Vector2I)` plus `Node2D.ToGlobal`/`ToLocal` to convert grid-local corners to this node's local space for drawing.
- Resolves `GridCellDataComponent` at `CellDataPath` (or scene-wide) and calls `EnumerateFlags()`, `GetFlags(Vector2I)`; subscribes to its `CellChanged`/`CellsChanged` signals.
- Not established from this batch alone whether anything calls into `GridCellOverlayComponent` itself — none of the other six files in this batch reference it.

## Notes

- `ColorForFlags`'s priority order (`Blocked` > `HarvestReady` > `Planted` > `Watered` > `Tilled` > `Cleared`) is the exact same table `GridTileMapLayerBridgeComponent.AtlasForCell` implements independently for its atlas-coordinate lookup — one `CellFlags` → "which state wins" decision, written twice.
- `_Process` is only ever scheduled to run when `Engine.IsEditorHint()` was true at `_Ready` (`SetProcess(Engine.IsEditorHint())`, never toggled again), yet its body re-checks `if (Engine.IsEditorHint()) QueueRedraw()` — that inner check is always true whenever `_Process` runs at all, a small redundant guard rather than a bug.
- `_GetConfigurationWarnings` only checks that the two `NodePath`s are non-empty, not that they actually resolve to the right component types — a wrong path surfaces later as a silent no-draw (`_grid == null || _cells == null` short-circuits `_Draw`), not as an editor warning.
