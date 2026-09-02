# TerrainWorldStatusComponent

Game-facing component: a thin UI adapter that pushes `TerrainWorldComponent.StatusLine()` into a `Label` whenever the world finishes building.

Per its header comment, this replaces per-demo status-line formatting that disagreed with itself (one demo printed "continents", another "landmasses", a third omitted lakes) — `TerrainWorldComponent.StatusLine()` is now the one description, and this component's entire job is displaying it.

## Public API

- `NodePath WorldPath` `[Export]` — the `TerrainWorldComponent` subscribed to in `_Ready()`.
- `NodePath LabelPath` `[Export]` — the `Label` whose `.Text` this component owns.
- `string PendingText` `[Export]` (default `"generating..."`) — text shown immediately in `_Ready()`, before any `WorldBuilt` signal has fired.
- `_Ready()` — no-ops in the editor; resolves `_world`/`_label` from their NodePaths, sets `_label.Text = PendingText` if a label was found, subscribes to `_world.WorldBuilt`.
- `_ExitTree()` — unsubscribes from `WorldBuilt` if `_world` is still a valid instance.
- `_GetConfigurationWarnings()` — warns separately for an empty `WorldPath` and an empty `LabelPath`.
- `OnWorldBuilt(Vector2I size)` *(private, signal handler)* — discards `size`; sets `_label.Text = _world.StatusLine()` when both `_label` and `_world` are present.

## Dependencies

Reads `TerrainWorldComponent.WorldBuilt` (signal) and `TerrainWorldComponent.StatusLine()`, both defined in `TerrainWorldComponent.cs`.

## Notes

- A `WorldPath`/`LabelPath` that resolves to the wrong node type, or a typo'd path, fails silently: `_GetConfigurationWarnings()` only catches the *empty* case, and `GetNodeOrNull<T>` just returns null on a bad path — the label then never updates, with no runtime error or warning surfaced anywhere.
- Structurally near-identical to `TerrainWorldCameraComponent`'s `_Ready`/`_ExitTree`/`WorldBuilt`-subscribe pattern (same idiom, both correctly guard the unsubscribe with `GodotObject.IsInstanceValid`) — a recognizable repeated shape across this batch, not a defect.
