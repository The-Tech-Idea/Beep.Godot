# DUP-03 — One feature-sheet loader for the flat and isometric feature renderers

**Type:** duplication fix (with a latent bug) · **Area:** `TerrainFeatureRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainFeatureFrameBindings`, `TerrainPropSizing` · **Status:** **IMPLEMENTED 2026-09-08** · **Effort:** S (took ~half a day) · **Risk:** low

## Outcome

Landed as `ecs/terrain/TerrainFeatureSheets.cs`; both renderers lost `TryDescribe`, `LoadSheets`, `Add`, their `_sheets` dictionary, path cache and `_woodsFrames` field (63 lines from the flat view, ~45 from the isometric one) and hold one `TerrainFeatureSheets` each. The isometric view gained the six per-sheet layout exports it lacked. Verified: `dotnet build` clean, the three existing feature probes green (`terrain_feature_grid`, `terrain_feature_streaming`, `terrain_iso_feature_streaming`), the new `tests/terrain_feature_sheets_probe.gd` green in the gate, and **5 of 5 mutations trip a guard** (3 on the scan pin, 2 on the probe).

What differed from the plan, and why:

1. **Zero inherits the woods layout.** The plan said "add per-sheet columns/rows exports" and left the default open. A default of 4×4 would have *broken* `terrain_iso_demo.tscn`: it cuts an `iso_marsh_8x1` sheet on `WoodsColumns = 8` and says nothing about marsh columns, so the old cut-everything-on-woods behaviour was, for that scene, correct by coincidence. The new exports use range `0..16`, default `0`, and `TerrainFeatureSheets.Resolve` makes zero inherit the woods grid — the `TerrainMaterialTiling` convention ("zero inherits") already used elsewhere in the engine. Every existing scene keeps cutting exactly as before; the dial is simply there now.

2. **The frame bindings moved inside the sheets object** rather than staying a sibling field, because their frame count depends on the woods layout — the two facts cannot be owned apart.

3. **The bug was latent, not visible.** The shipped isometric demo's sheets happen to share one grid, so nothing in the tree rendered wrongly before this change. The probe reproduces the defect with fixture sheets on different grids (a 2×1 marsh against a 4×1 woods) and measures the slicing through the *aspect ratio of the drawn stamps*, which both views preserve — so it needs no display and no new API on either renderer.

4. **Cache key widened.** The old loaders re-read sheets only when a *path* changed, so editing a sheet's columns did nothing until its path also changed. The shared loader keys on the full layout.

Noted while verifying, **not caused by this change**: `tests/terrain_prop_sizing_probe.gd` (in `run_terrain_integration.ps1`, not the addon gate) fails its line-40 assertion that `TerrainWorldComponent` pushed `PropSizing` onto the feature renderers. This change's diff never mentions `PropSizing`; the push lives in `TerrainWorldComponent.Drawing.cs:36-38`, a file the level/recipe session is editing and which already trips the contract scan's restore-yield pin. Most likely the lab's generation is now asynchronous and the probe reads the renderers two frames in, before `Draw()` ran. Left to that session.

## Finding

Both feature renderers load the same six prop sheets (woods, forest, jungle, marsh, oasis, rocks) with the same three-method shape:

- `LoadSheets()` — `TerrainTextures.Load` per exported path, `TerrainFeatureFrameBindings.Load` for woods.
- `Add(kind, texture, columns, rows)` — registers a sheet.
- `TryDescribe(kind, out sheet, out columns, out rows)` — resolves a feature kind to its sheet.

The flat renderer keeps per-sheet `Columns`/`Rows` exports. The isometric copy at `TerrainIsometricFeatureRendererComponent.cs:378-395` resolves **every** sheet with `WoodsColumns`/`WoodsRows` — the per-sheet layout was never copied across. A marsh or rock sheet authored with a different grid is sliced wrong in the isometric view and right in the flat view of the same map. This is the "accepted, stored, ignored" defect class: the isometric renderer exposes `MarshColumns` etc. in the Inspector and reads none of them for slicing.

`TerrainPropSizing.VisibleRegion(texture, columns, rows, index)` already caches visible frames per `(texture, columns, rows)` and `TerrainFeatureScatter.Fill` already provides shared anchors — the sheet *description* is the only unshared piece.

## Design

`TerrainFeatureSheets` (plain class, `ecs/terrain/`):

```csharp
public sealed class TerrainFeatureSheets
{
    public readonly record struct Sheet(Texture2D Texture, int Columns, int Rows, int[]? Frames);
    public void Load(TerrainFeatureSheetPaths paths, string owner);   // paths = the exports, as one record
    public bool TryGet(string featureKind, string terrainKind, out Sheet sheet);
    public int FrameFor(Sheet sheet, Vector2I cell, int seed);        // uses TerrainGeometry.Hash01 + Frames weights
}
```

Both renderers hold one `TerrainFeatureSheets`, keep their own exports (they may legitimately diverge in *which* art they point at — flat vs isometric sprites), and pass them as a `TerrainFeatureSheetPaths` record. Per-sheet columns/rows come from the record, fixing the isometric bug by construction.

## Steps

1. Extract from the flat renderer (the correct copy).
2. Replace the isometric renderer's `LoadSheets/Add/TryDescribe`; wire its per-sheet `*Columns/*Rows` exports into the record.
3. `TerrainFeatureFrameBindings` moves inside `TerrainFeatureSheets` (it is only ever used with a sheet).

## Guards

- Probe: author a marsh sheet with `MarshColumns = 3, MarshRows = 1` on both renderers; place a marsh cell; assert the isometric sprite's `RegionRect.Size.X == texture.Width / 3`. **Mutation:** revert to `WoodsColumns` → assert fails (this is the guard that proves the bug existed).
- Pin: `TryDescribe(` declared in no renderer file.

## Dependencies / collisions

None. Sits well after DUP-01 but does not require it.

## Out of scope

Prop scatter density, `TerrainPropResidency` (see ENH-06), art.
