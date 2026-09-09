# DUP-01 — One terrain renderer lifecycle contract

**Type:** duplication fix · **Area:** `ecs/terrain/*Renderer*`, `TerrainTransitionLayerComponent`, `TerrainMapOverlayComponent`, `TerrainWorldComponent.Drawing.cs` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (rebuild coalescer landed; the Draw() loop investigated and declined - see below; source resolution deferred) · **Effort:** M (2–3 days) · **Risk:** medium (touches every renderer; `TerrainWorldComponent` is being edited by another session)

## Outcome (rebuild coalescer)

`TerrainRendererComponent : Node2D` now owns the one piece of lifecycle every renderer copied byte-for-byte: the deferred-rebuild coalescer (`QueueRebuild`), the `_rebuildQueued` flag it guards, the `HasRebuildAttempt` flag, the `RefreshOnReady` export, and the visibility `_Notification` that re-queues a rebuild when a drawn renderer becomes visible again. The nine `Node2D` renderers derive from it and their private copies are gone. This is the seam ENH-01 asked for: the eviction-aware invalidation lands in `QueueRebuild` once, not in nine places.

Two renderers keep their own coalescing shape through one override each, rather than a copied method: the painted view declines while it is preparing a visual snapshot off-thread (`CanQueueRebuild` returns `_snapshotPreparation is null`), and the isometric autotile view time-slices a large map instead of rebuilding inline (`PerformQueuedRebuild` calls `RequestRebuild` when a paint is in flight or the map exceeds 65 536 cells). Both were previously expressed as extra clauses inside the copied `QueueRebuild`; they are the only real variation, and they are now named.

Verified: `dotnet build` clean, zero warnings; the rendering-probe coverage batch green — `renderer_reporting` (the plan's guard: all nine renderers report when they cannot draw), the four world publication probes, both live views, the variant, autotile-staleness, view-grid, painted-origin and relief/feature streaming probes, and the generation-output probes. Two batch entries were not regressions: `terrain_material_scale_probe` asserts a non-headless display and never runs headless, and `terrain_water_surface_probe` failed a pre-existing erosion-parity check that the earlier neighbour-order commit had drifted, fixed separately in 965b49ab. Both scan-pin mutations trip (re-declaring `QueueRebuild` in a renderer, and renaming a base member), and restore. The pin requires the seven base members to exist and rejects a `QueueRebuild`, `_rebuildQueued` or `_hasRebuildAttempt` re-declared in any other terrain file.

**Deliberately not done here, and why.**

- **Source resolution (`ResolveCells`/`DisconnectCells`/`ResolveGenerator`/`_EnterTree`) stays per-renderer.** The plan counted these as duplication, but read closely they diverge in ways a shared base would have to special-case one by one, which is more machinery than the copies: the resource and relief views also subscribe to a `GridProjectionComponent.GeometryChanged`; the resource view binds a live-resource view inside `ResolveGenerator`; the isometric block view subscribes to cell data inside its own `Rebuild`/`ResolveSurface`, not in `_Ready`; the isometric feature view follows another renderer's `SurfaceRebuilt` rather than any cell data and has no `CellDataPath` at all (nor do the resource and overlay views - giving them one would be a setting nothing reads, rule 7); and the tile view routes a cell change to a water-only requeue (`QueueCoast`), not a full rebuild. This is rule 3's narrow exception - genuinely different purposes whose split does not cleanly resolve - and the honest move is to leave it until ENH-01 defines the `TerrainChangeKind` hook, which is the thing that would justify a cell-aware subclass carrying a real shared signature.
- **`TerrainWorldComponent.Draw()` was investigated and deliberately NOT rewritten (2026-09-09).** The collision reason for the earlier deferral is gone - only this session edits `TerrainWorldComponent` now - but the other two reasons stand and, read against the code, settle it:
  - **The `ShownIn`-filtered loop cannot remove the renderer type names without breaking interface segregation.** `Drawing.cs` is not just show/hide: `BindGameplayGrid`, `StartPositionView`, `PreviewExtent` and `FlatViewTileSize` each query projection-specific APIs that live on 1-3 renderer types and nowhere else - `GetTerrainLayer` (painted/tiles/autotile), `SurfacePosition`/`SurfaceExtent` (iso), `CellPosition` (autotile/overlay/relief), `AtlasTileSize` (tiles), `GridExtent` (autotile). The base `TerrainRendererComponent` declares none of them (verified). Making `Draw()` type-name-free would force every one of these onto the shared base - a general renderer base carrying each view's private query - which is exactly the "widen a general interface to carry one feature's needs" that rule 4 forbids, and the same "genuinely different purposes whose split does not cleanly resolve" that this plan already accepted when it left source resolution per-renderer.
  - **The remaining risk is a visibility/z-order regression the headless suite cannot catch.** `Draw()`'s own comment documents the class of bug at stake (a feature renderer left out of the projection wiring drew "flat trees standing on the open ocean") and warns that every renderer is named on purpose. `Draw()` sets `Visible`/`Rebuild` per renderer with non-uniform conditions and a deliberate two-phase order (grid bound between the projection views and the flat overlays); only a display-backed render probe could prove a restructure preserved the result, and those are unreliable in this environment (see [[godot-capture-bridge-unusable]]).
  - **What a real change here would take:** either an accepted base-class widening (the projection queries become base virtuals, most a no-op for most renderers) plus display-backed before/after render comparison per projection - a redesign, not a dedup - or nothing. Left as the explicit, well-commented orchestration it is. This is the owner's call if the interface-segregation trade is wanted; it is not landed on the implementer's judgement.
- **`TerrainTransitionLayerComponent` (a `Node`, not `Node2D`) and `TerrainCollisionComponent`** stay off the base: the transition layer does per-cell dual-grid painting rather than a whole rebuild, and the collision component is a chunked `SetProcess` builder with a different lifecycle (`QueueFull`/`Queue`, not `QueueRebuild`). Neither shares the coalescer this base owns.

## Finding

Every terrain renderer carries its own copy of the same four pieces of lifecycle plumbing, and the world orchestrator hand-wires each renderer by name because there is no contract to call.

| Copy | Count | Files |
|---|---|---|
| `private void QueueRebuild()` deferred coalescer (`_rebuildQueued` flag + `CallDeferred`) | 9 | `TerrainFeatureRendererComponent`, `TerrainIsometricAutotileRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainMapOverlayComponent`, `TerrainPaintedRendererComponent`, `TerrainReliefRendererComponent`, `TerrainResourceRendererComponent`, `TerrainTileRendererComponent` |
| `private void ResolveCells()` / `DisconnectCells()` — subscribe/unsubscribe `CellChanged`/`CellsChanged` with the same `IsInstanceValid` dance | 4 / 7 | the same set plus `TerrainTransitionLayerComponent`, `GridCellOverlayComponent` |
| `_EnterTree` re-resolve + `_Notification(NotificationVisibilityChanged)` → rebuild | ~8 | the same set |
| `ResolveGenerator()` with `IsInstanceValid` re-check and `TerrainBoundsCheck.WarnIfMismatched` | 9 | the same set |

`TerrainWorldComponent.Drawing.cs:32-229` `Draw()` then sets `BoundsOrigin`, `BoundsSize`, `Visible` and calls `Rebuild()`/`Refresh()` on each of nine renderers individually, with a `switch` on `TerrainProjection` deciding which are shown. Adding a tenth renderer means editing the orchestrator, the projection switch and copying the four blocks above.

`docs/ENGINE_ENHANCEMENT_PLAN.md` Phase 2/3 already consolidated `TerrainTextures`, `TerrainAuthoring.EnsureLayer` and `TerrainLayers` for exactly this reason ("three views, three answers to one question"); the lifecycle plumbing was left.

## Why it matters

- The nine `QueueRebuild` copies are where ENH-01 (eviction-aware invalidation) has to land. Landing it nine times is how the painted renderer became the only chunk-aware listener while eight others stayed eviction-blind.
- `TerrainWorldComponent.Draw()` is the single largest hand-maintained wiring block in the terrain engine and is a live collision point with the level/campaign work.
- A renderer that forgets `DisconnectCells` in `_ExitTree` keeps a freed node subscribed to `GridCellDataComponent` — the class of bug Phase 1.9 fixed for the orchestrator's own references.

## Design

One abstract base, `TerrainRendererComponent : Node2D` (or `Node` for the non-drawing ones), owning:

```csharp
public abstract partial class TerrainRendererComponent : Node2D
{
    [Export] public NodePath TerrainGeneratorPath { get; set; }
    [Export] public NodePath CellDataPath { get; set; }
    [Export] public Vector2I BoundsOrigin { get; set; }
    [Export] public Vector2I BoundsSize { get; set; }
    [Export] public bool RefreshOnReady { get; set; } = true;

    protected TerrainGeneratorComponent? Generator { get; private set; }
    protected GridCellDataComponent? Cells { get; private set; }

    /// One deferred rebuild per frame however many changes arrive.
    protected void QueueRebuild();
    /// Chunk-scoped hook; default calls QueueRebuild. ENH-01 lands here once.
    protected virtual void OnCellsChanged(TerrainChangeKind kind, IReadOnlyList<Vector2I> chunks) => QueueRebuild();
    protected virtual void OnCellChanged(Vector2I cell, TerrainChangeKind kind) => QueueRebuild();
    public abstract void Rebuild();
    /// What the world orchestrator calls instead of hand-setting three properties.
    public void Configure(Vector2I origin, Vector2I size, bool visible);
}
```

`_Ready`/`_EnterTree`/`_ExitTree`/`_Notification` live on the base; subclasses override `Rebuild()` and, where they already are chunk-aware (painted, collision), `OnCellsChanged`.

`TerrainWorldComponent.Draw()` becomes a loop over `[Export] Godot.Collections.Array<NodePath> Renderers` (or discovery of `TerrainRendererComponent` children) filtered by a `Projection` property each renderer declares (`TerrainProjection[] ShownIn`). The projection switch disappears; the orchestrator no longer knows renderer type names.

`TerrainTransitionLayerComponent` (per-biome dual-grid, owned by the tile renderer) and `TerrainCollisionComponent` join the base as well; `GridCellOverlayComponent`/`GridTileMapLayerBridgeComponent` (grid side) get the same `OnCellsChanged` signature through a small grid-side listener helper so the signal contract is one shape on both sides.

## Steps

1. Add `TerrainRendererComponent` with the four blocks; port `TerrainResourceRendererComponent` first (smallest, already has `Rebuild`).
2. Port the remaining eight renderers one by one; delete their private copies (compiler is the sweep — `no-legacy` rule).
3. Add `ShownIn` per renderer; rewrite `TerrainWorldComponent.Draw()` as the loop. Keep `GeneratorPath`/`PaintedRendererPath` exports working by resolving them into the renderer list (scene files unchanged).
4. Regenerate `docs/terrain-engine/*` pages for the moved members.

## Guards (each must fail before the fix)

- Contract-scan pin: no file under `ecs/terrain/` other than `TerrainRendererComponent.cs` declares `private void QueueRebuild(`, `ResolveCells(` or `DisconnectCells(`. Mutation: re-add one copy → scan fails.
- Pin: `TerrainWorldComponent.Drawing.cs` contains no renderer type name (`TerrainTileRendererComponent`, …) — only the base type. Mutation: reintroduce one `_tile.Rebuild()` → fails.
- `renderer_reporting_probe.gd` (existing, 9 renderers) stays green; add: free a renderer mid-scene, edit a cell, assert no `ObjectDisposedException`/error log (the `_ExitTree` unsubscribe). Mutation: remove the base's `DisconnectCells` → probe logs the error.

## Dependencies / collisions

- Prerequisite for **ENH-01** and **ENH-02** (they add `TerrainChangeKind` to the hook the base introduces).
- **Collision:** `TerrainWorldComponent` is being edited by the level/campaign session (`TerrainRecipe`, `NewWorldOnReady`). Land the base first; rewrite `Draw()` only after coordinating.
- `SeededTerrainPropScatterComponent` and `TerrainDataLayersComponent` are deliberately *not* renderers (one is authoring, one is a query surface) and stay off the base.

## Out of scope

Rendering behaviour, materials, z-order (`TerrainLayers`) — unchanged. Genre-specific renderers remain separate classes; only lifecycle plumbing moves.
