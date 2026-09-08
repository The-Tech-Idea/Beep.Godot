# FEAT-03 — Fog of war and exploration (surface visibility layer)

**Type:** feature (genre standard: Civ/AoE/Settlers black-fog + explored-grey; Factorio charted chunks) · **Area:** new `GridVisibilityComponent`, `GridCellDataComponent` (per-chunk explored bits), `TerrainPaintedRendererComponent` shader input, `TerrainMapOverlayComponent`, `ui/GridMinimapComponent`, `GridSelectionComponent`/`GridObjectInspectorComponent` (hide unseen), `TerrainWorldStatusComponent` · **Status:** proposed 2026-09-08 · **Effort:** L (1 week) · **Risk:** medium (a rendering pass in every view; must not touch generation or navigation)

## Gap

The only "hidden information" the grid has is **underground**: `GridProspectingComponent` reveals subsurface deposits (`GridSubsurfaceStoreComponent`, survey overlay). The **surface** is fully visible from frame one in every view: the painted shader has no visibility input (`fog` hits in `terrain_splat.gdshader` are atmosphere/weather fog), the minimap bakes the whole map, the inspector shows any object, and nothing tracks what a faction has seen. Every strategy/settlement genre the addon ships (strategy, citybuilder) expects at minimum "unexplored is black, explored-but-unseen is dimmed".

## Design

Three states per cell per faction, stored **per chunk as bit planes** (not on `CellRecord` — visibility is dense, faction-scoped and cheap): `Unexplored` / `Explored` / `Visible`.

1. **`GridVisibilityComponent`** (one per faction or one with a faction index): keeps `Dictionary<Vector2I chunk, ulong[] explored>` (32×32 bits = 16 `ulong`s) and a transient `visible` plane recomputed per frame from **revealers** — any `GridObjectComponent`/actor with `SightRadius` (a new export on `GridObjectComponent` and `ActorComponent`, default 0 = none). Reveal is a disc (optionally relief-blocked: mountains block, hills extend — `TerrainDataLayersComponent.ReliefAt`). Explored bits are persistent and ride in the archive as chunk metadata (`GridCellDataComponent.SetChunkMetadata`) so eviction/reload and saves keep them; `ExplorationChanged(chunks)` fires when bits flip; `Visible` changes are frame-local and published as a texture, not a signal.
2. **Rendering:** one `visibility_map` texture (R = explored 0/1, G = visible 0/1) at cell resolution, updated per chunk (ENH-05's windowed upload). The painted shader multiplies: unexplored → black, explored-not-visible → 0.55 grey-desaturate, visible → 1. Tile/isometric views apply the same texture through a `CanvasItemMaterial`-free overlay: a `TerrainVisibilityOverlay` (a `Polygon2D` per resident chunk group with a shader reading the same texture — the `TerrainSurfaceStreamingComponent` quad mechanism). Props under unexplored cells are hidden by the renderer reading the plane (per-chunk check, cheap).
3. **Gameplay consumers (rule 6):** `GridSelectionComponent` cannot select an unexplored cell; `GridObjectInspectorComponent` hides objects on non-visible cells; `GridPlacementComponent.RequireExplored` (default off); `GridObjectiveDefinition` kind `explore_cells` (count/percentage) and `reveal_cell`; the minimap paints unexplored black (ENH-13 bake hook). `TerrainWorldStatusComponent` reports explored %.
4. **Sandbox default:** the component is opt-in (absent → everything visible, exactly today). The two reference grid templates get it wired but with `Enabled = false` so shipped demos are unchanged until a game turns it on.

## Guards (fail first)

- Probe: 128×80 world, one revealer radius 6 at (40,40), `Enabled = true` → `IsExplored((40,40))` true, `((80,10))` false; painted `visibility_map` texel (80,10) == black. **Mutation:** skip the shader multiply → texel unchanged (visible).
- Probe: move the revealer 20 cells → old disc `Explored && !Visible`, new disc `Visible`; `ExplorationChanged` fired once per newly explored chunk.
- Save/evict round-trip: explored bits survive `EvictSavedChunk` + reload and `Save/Load`.
- Probe: selection on an unexplored cell rejected (`selection_unexplored`); inspector `SelectedObject == null` for an object on a non-visible cell.
- Headless: probes read the texture back via `GetImage` (the headless memory note applies — recreate, do not `Update`).

## Dependencies / collisions

ENH-01 (kinds), ENH-05 (texture windowing), ENH-07 (chunk-group quads for tile/iso overlays), ENH-13 (minimap). Chunk metadata storage in the archive is the other session's area — coordinate. FEAT-02 (territory) and this share the "faction" notion — introduce `GridFactionCatalog` once, in whichever lands first.

## Out of scope

Line-of-sight ray casting per pixel, fog for actors' own vision cones (actors layer), multiplayer authority.
