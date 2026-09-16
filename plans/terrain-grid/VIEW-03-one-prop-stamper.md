# VIEW-03 — One prop stamp core; two thin views

**Type:** duplication fix (three copies of the stamp math with different salts, clumps, jitter and anchors: the same world grows different forests per view) · **Area:** new `ecs/terrain/TerrainPropStamper.cs`, `TerrainFeatureRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainReliefRendererComponent`, `TerrainPropSizing` · **Status:** proposed 2026-09-15 · **Effort:** M (2–3 days) · **Risk:** medium (the block view's look changes where its salts differed; its art baseline is re-recorded once)

## Gap

The generator decides where woods, bushes, reeds and rocks stand; three renderers then each decide, separately, which frame, how many, how large and where inside the cell:

| | Flat features | Isometric features | Relief props |
|---|---|---|---|
| Sprites per tile / forest extra | `1` / `1` (`TerrainFeatureRendererComponent.cs:58,66`) | `2` / `2` (`TerrainIsometricFeatureRendererComponent.cs:76-77`) | – |
| Position / scale jitter | `0.85` / `0.18` (`:67-68`) | `0.30` / `0.16` (`:78-79`) | – |
| Clump | `:250-251` | `:239-241` | – |
| Frame salt | `Seed + 811 + slot*97` (`:258`, `:277`) | `Seed + 5101 + slot*83` (`:349`) | `Seed + 4111 + slot*83` (`TerrainReliefRendererComponent.cs:242`); texture salt `4177 + slot*83` (`:205`) |
| Scale salt | `Seed + 907 + slot*89` (`:299`) | `Seed + 5227 + slot*79` (`:357`) | `Seed + 4231 + slot*79` (`:248`) |
| Anchor | `SpriteAnchor` export `(0.5, 0.92)` (`:57`, used `:308`) | hard-coded `drawn.X*0.5f, drawn.Y*0.92f` (`:367`) | grid corners (`:226-236`) |
| Placement | `CellToWorld/CellCorners` (`:283-294`) | `SurfacePosition/SurfaceCorners` per level (`:225-227,235-238`) | `CellCorners` (`:226-236`) |

Painted and Isometric views of one seed therefore show a different tree on the same cell, twice as many of them, at a different size — not a genre variant (projection is not a genre), a second implementation of one decision. DUP-03 unified the *sheet loading* (`TerrainFeatureSheets`, landed 2026-09-08) and left the stamp math; DUP-07 plans one shared stamp record for the residency façade (`DUP-07-prop-residency-facade.md`, "the three current stamp types already carry the same fields"); ENH-06 wants residency changes to land once. The shared prop dimensions already have one owner — `TerrainPropSizing` ("independent of art and projection", `TerrainPropSizing.cs:7`, per-kind ranges `:16-21`, `SizeInCells :25-39`) pushed to all three renderers by `TerrainWorldComponent.Draw()` (`TerrainWorldComponent.Drawing.cs:37-39`) — so the split is only in the stamping.

## Design

1. **`TerrainPropStamper`** (static, `ecs/terrain/`): `Build(in PropPlacement placement, TerrainFeatureSheets sheets, string kind, int slot, TerrainPropSizing sizing, int seed) → TerrainPropStamp`. One frame salt, one scale salt, one position jitter, one `ClumpFor(kind, sizing)`, one anchor. `PropPlacement` carries the cell, its surface centre, its four corners and a level; `TerrainPropStamp` is DUP-07's shared record (`Sheet, Region, Target, Anchor, Level, Tint`) introduced here so DUP-07 folds the façade around a record that already exists.
2. **Views supply placement only.** The flat feature renderer passes `CellToWorld/CellCorners` (`:283-294`) at level 0; the isometric one passes `SurfacePosition/SurfaceCorners` and the tier (`:225-238`) and keeps its per-level draw (`LevelProps`, `:93`); the relief renderer passes the same flat placement with rock kinds (`small_rock`/`large_rock`, already in `SizeInCells :30-33`). Each view keeps its own draw path and its own residency until DUP-07.
3. **Dials move to the owner.** `SpritesPerTile`, `ForestExtraSprites`, `PositionJitter`, `ScaleJitter` and `SpriteAnchor` become `TerrainPropSizing` exports (one Resource, one `.tres`, already pushed at `:37-39`); the per-renderer exports are deleted and any scene that authored them is edited to the resource. The flat values win (`1 / 1 / 0.85 / 0.18 / (0.5, 0.92)`): the painted view is the reference look and every flat baseline stays byte-identical.
4. **Salts.** The flat salts (`811/97`, `907/89`) are the stamper's; the isometric and relief salts go. The block view's forests therefore change frame and count once; the relief renderer keeps its texture-choice salt (`4177`) as a *kind* decision, which the stamper also owns.
5. **Docs:** the three renderer pages state that stamping is `TerrainPropStamper`'s and that the view owns placement and draw only.

## Guards (fail first)

- Headless: one Tiny world (seed 31415) drawn Painted then Isometric → for every wooded cell the stamp count, every frame index and every size-in-cells agree between views; only the position differs, and by projection alone (`CellToWorld(cell)` ↔ `SurfacePosition(cell)`). **Mutation:** re-salt the isometric path with `5101` → frames differ on the first wooded cell.
- Pin (declaration-shaped, both directions per the 2026-09-05 rename lesson): `TerrainGeometry.Hash01(` beside `Seed +` appears in `TerrainPropStamper.cs` and in none of the three renderer files. **Mutation:** re-add one salt to a renderer → the forbid half trips.
- `tests/terrain_feature_grid_probe.gd` keeps its anchor/scale stability checks (`:57-76`) — it now authors `SpriteAnchor` on the sizing resource; `terrain_feature_streaming`, `terrain_iso_feature_streaming`, `terrain_feature_sheets` green; the rendered `iso_art` capture is re-baselined once with the reason recorded (a pre-existing failure on 2026-09-15 per `run_terrain_integration.ps1`, so it is made to pass before it is re-recorded).

## Dependencies / collisions

DUP-03 (landed, sheets); DUP-04 (`Hash01`); DUP-07 lands *after* this and consumes `TerrainPropStamp`; ENH-06 after DUP-07. VIEW-02 makes `EffectiveTileSize` the flat placement's size source. Collision: `TerrainPropResidency` is the streaming session's — this item does not touch it.

## Out of scope

Art and sheet layouts; residency and LOD (ENH-06); z-order; `MapArt` tinting (`TerrainWorldComponent.Drawing.cs:40-42`); the autotile view's companions (VIEW-01 makes it draw the flat renderer).
