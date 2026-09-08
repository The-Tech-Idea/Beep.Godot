# DUP-03 — One feature-sheet loader for the flat and isometric feature renderers

**Type:** duplication fix (with a latent bug) · **Area:** `TerrainFeatureRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainFeatureFrameBindings`, `TerrainPropSizing` · **Status:** proposed 2026-09-08 · **Effort:** S (1 day) · **Risk:** low

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
