# GridResourceBarComponent

Compact HUD strip (a `Control`, `[Tool][GlobalClass]`) for `GridResourceWalletComponent`: displays every non-zero wallet entry as a `"{resourceId}: {amount}"` label, refreshing whenever the wallet's `ResourcesChanged` signal fires.

This is the only file in the batch with a genuine three-tier binding fallback rather than the usual two-tier "bind existing or generate" shape: `RebuildBar()` first tries `BindExistingLabels()` (an explicit parallel `BoundResourceIds`/`BoundLabelPaths` array binding into a `_boundLabels` dictionary, refreshed in place without ever creating/destroying labels), then falls back to `BindExistingRow()` (an authored or previously-generated `HBoxContainer` found by `RowPath` or by the `ResourceBar`/`GeneratedResourceBar` naming convention), and only generates a new row as a last resort when `GenerateControlsWhenPathsEmpty` is set. The row-based path manages its own generated `Label`s by tagging them with `SetMeta("beep_generated_resource_label", true)` so it can tell "a label I created and can reuse or free" apart from any other unrelated child `Control` that might share the row.

## Public API

- `[Export] NodePath ResourceWalletPath/RowPath` — component/control wiring.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool HideZeroAmounts = true` — skip (bound mode: hide) entries at or below zero.
- `[Export] bool SortByResourceId = true` — alphabetical vs. wallet-iteration order for the row-based path.
- `[Export] Vector2 BadgeMinimumSize = new(96, 32)`.
- `[Export] string[] BoundResourceIds` / `[Export] NodePath[] BoundLabelPaths` — explicit parallel binding of resource ids to label paths; must be the same length.
- `public override void _Ready()` — resolves references, defers `RebuildBar()` per `BuildInEditor`, subscribes `RebuildBar` itself (not just a refresh) to the wallet's `ResourcesChanged` at runtime.
- `public override void _ExitTree()` — unsubscribes `ResourcesChanged`; frees the generated row if this component created one.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `ResourceWalletPath`, mismatched `BoundResourceIds`/`BoundLabelPaths` lengths, or (without bound ids, without generation enabled) no findable resource row.
- `public void RebuildBar()` — tries `BindExistingLabels()`, then `BindExistingRow()` + `RefreshRowLabels()`, then (if allowed) `BuildGeneratedRow()` + `RefreshRowLabels()`.
- `public int VisibleResourceCount()` — count of visible bound labels, or visible generated `Label` children of the row.
- `public string TextForResource(string resourceId)` — checks bound labels first, then falls back to the generated-row `Label` named `Resource_{SafeName(resourceId)}`.
- `public bool UsesSceneControls()` — true if `BoundResourceIds` is non-empty or a resource row can be found by convention.

## Dependencies

- Reads `GridResourceWalletComponent.GetAmounts()` (a `Godot.Collections.Dictionary`), `.GetAmount(resourceId)`, and its `ResourcesChanged` signal (outside this batch).
- Uses `GridVariantReader.Int(...)` (outside this batch) to read wallet amount values with a fallback.
- Uses `EntityComponent.FindComponent<GridResourceWalletComponent>(...)` as the scene-fallback lookup, same pattern as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridResourceBarComponent` — none of the other 12 UI files reference it, though `GridBuildToolbarComponent` in this same batch independently resolves and reads from the same `GridResourceWalletComponent` for affordability, so the two panels read overlapping wallet state through separate resolution paths.

## Notes

- `RebuildBar()` re-subscribes `RebuildBar` itself (not a lighter `RefreshRowLabels`/`RefreshBoundLabels`) to `ResourcesChanged`, so every wallet change re-runs the full three-tier bind resolution, not just a value refresh — more work per change than the bound-mode `RefreshBoundLabels()` path alone would need.
- `RefreshRowLabels()` sets a stale generated label's `Visible = false` immediately before `QueueFree()`; since `QueueFree()` defers actual removal to the end of the frame, this looks like a deliberate one-frame-flash guard, though nothing in the file states that explicitly.
- The bound-array mode (`BoundResourceIds`/`BoundLabelPaths`) never creates or destroys labels — it only toggles `Visible` per `HideZeroAmounts` — while the row-based mode actively frees stale labels and recreates them on demand; a game switching between the two binding modes at runtime would see different lifecycle behavior for what looks like the same feature.
