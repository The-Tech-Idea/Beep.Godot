# FEAT-07 — Seasons the terrain can see

**Type:** feature (genre standard: Settlers/Anno 1800 winter, Banished snow cover, Stardew seasonal palettes) · **Area:** `GridCalendarComponent` (seasons exist), `TerrainPaintedRendererComponent` (`TerrainMapArt`), `TerrainTileRendererComponent`/`TerrainIsometricRendererComponent` (variant frames), `TerrainFeatureRendererComponent` (canopy frames via `TerrainFeatureFrameBindings`), `GridCropCatalogComponent` (already seasonal), `GridNavigationComponent` costs · **Status:** proposed 2026-09-08 · **Effort:** M (3–5 days; art-dependent) · **Risk:** low (presentation + an optional cost table; opt-in)

## Gap

`GridCalendarComponent` has four seasons (`GridSeason`) with `SeasonChanged`, and `GridCropDefinition` already gates planting by season — the *gameplay* half of seasons exists. The *world* ignores them: the painted view's palette (`TerrainMapArt` colours, `terrain_splat` slots), the tile/iso variant tables and the feature canopy frames have no season input, and navigation costs are constant. A farm game with a calendar HUD shows "Winter, day 3" over green grass and full-leaf trees.

## Design

Opt-in, presentation-first, no generation change:

1. **`TerrainSeasonComponent`** (`ecs/terrain/`, orchestrated by the calendar): listens to `GridCalendarComponent.SeasonChanged` and publishes `SeasonProgress` (season index + 0..1 within it, from `GridWorkClockComponent.DayProgress01` and the calendar's day-in-season) to the renderers through one uniform set (`season_index`, `season_blend`) and a per-season `TerrainMapArt` override list (`SeasonArt[4]`, each optional — an unset season keeps the base art). The painted shader lerps ground colours/textures between adjacent seasons' art over the transition week; snow line by elevation in winter (`snow_line_elevation`) — the shade/elevation textures already exist.
2. **Tile and isometric variants:** `TerrainTileSets`/the kind registry (DUP-13) gain optional per-season atlas coordinates per kind; renderers pick the season's frame if present. Feature canopies: `TerrainFeatureFrameBindings` entries can be season-qualified (`"grass@winter=4,5"`), falling back to the unqualified binding.
3. **Gameplay hooks (arriving with the feature):** `GridNavigationComponent` optional `SeasonCostMultipliers[4]` (winter mud/snow slower), `GridCalendarComponent` already exposes the season to crops; `GridResourceNodeComponent` optional `SeasonYieldMultiplier` (berries in summer). All default to 1.0.
4. **Sandbox default:** without a `TerrainSeasonComponent` nothing changes.

## Guards (fail first)

- Probe (headless texture read): set season Winter with `SeasonArt[Winter].GrassColor` white → painted id/shade sample of a grass cell is white-tinted; set Summer → base colour. **Mutation:** skip the uniform → colour unchanged.
- Probe: feature binding `"grass@winter=4"` → in winter the woods sprite frame index is 4; other seasons use the unqualified binding.
- Probe: `SeasonCostMultipliers[Winter] = 2` → a 10-cell winter path costs 2× the summer path; path cells identical.
- Existing calendar smoke (season change signals, `RestoreState` emits) stays green.

## Dependencies / collisions

DUP-02 (water material — winter ice tint is a water dial), DUP-03 (feature sheets), DUP-13 (per-kind season frames). Art is required for the tile/iso variants; the painted view needs colours only, so it ships first.

## Out of scope

Weather (`WeatherSystemComponent` owns fog/rain/snowfall particles), freezing water for pathing (a `Crossing`-like seasonal flag — follow-on to FEAT-04), crop growth rules.
