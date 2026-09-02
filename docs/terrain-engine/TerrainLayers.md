# TerrainLayers

Pipeline position: **support utility / shared world-data model** — a static, stateless definition of the single draw-order stack (levels and their z-indices) every renderer in the pipeline must agree on; it decides no terrain and draws nothing itself.

`TerrainLayers` is a `public static class` holding the canonical bottom-to-top layer order (floor, seabed steps, sea, ground, hills, mountains, summits, props, markers) as a set of `const int` level ids and pure functions mapping a level or terrain kind to a z-index. Its class comment explains this was extracted because three renderers previously answered "what order do things stack in" three different ways — one hardcoded it locally, one reached into another renderer's internals for it, and one couldn't reach it at all — so only one could ever be fixed at a time. It also documents a specific historical bug: an earlier version of this same file described props as interleaved with terrain levels in its comments long after `ZForProps` had stopped doing that, which it flags as worse than no comment because the next reader would implement what the stale doc said.

## Public API

- `const int Sea = 0`, `const int Ground = 1`, `const int Hills = 2`, `const int Mountains = 3`, `const int Summits = 4` — the five terrain levels, in stack order.
- `const int Count = 5` — total level count, sea included.
- `const int FirstLand = Ground` — the lowest land level; everything below it is water.
- `static int ZFor(int level)` — `level * 2`; every terrain level owns an even z, leaving the odd slot below it for the layer that draws that level and the even slot for anything drawn over it at the same level.
- `static int ZForSeabed(int step)` — `ZFor(Sea) - 2 - step`; seabed step 0 sits just under the sea surface, each further step draws behind (further from camera than) the last.
- `static int ZForProps(int level)` — `(Count * 2) + level`; ALL terrain draws first, then props follow in level order above every level (not interleaved per-level) — the comment explains interleaving was wrong because higher terrain is not always further from camera (a hill behind a tree still draws after it, cutting the tree off at the trunk).
- `static int ZForFloor()` — `ZForSeabed(Count) - 1`; the bottom of the world, below the seabed, for a filled base layer or a single-pass composited view whose one quad already represents bed+sea+land together.
- `static int ZForMarkers()` — `ZForProps(Count)`; above all props, so a UI marker/icon is never hidden by a tree.
- `static int LevelFor(string terrain, int relief)` — returns `Sea` for `"deep_water"`/`"shallow_water"`/`"water"`; otherwise `Mountains` when `relief >= 2`, `Hills` when `relief > 0`, else `Ground`. Used where an explicit relief value is available (e.g. per-cell from the generator).
- `static int LevelForKind(string terrain)` — returns `Sea` for the water kinds, `Hills` for `"gravel"`, `Mountains` for `"rock"`, else `Ground`. Used by flat views that draw one terrain kind per layer and have no relief field to consult; some kinds (gravel, rock) are treated as their own relief regardless of what a relief map would say.
- `static int ZForKind(string terrain)` — `ZFor(LevelForKind(terrain)) - 1`; the z a flat-view layer for a given terrain kind draws at (just under that level's even slot).
- `static string NameFor(int level)` — maps a level constant to its diagnostic name (`"sea"`, `"ground"`, `"hills"`, `"mountains"`, `"summits"`, else `"unknown"`).

## Dependencies

- None. This file has no dependency on any other file in `addons/beep_game_builder_cs/ecs/terrain/` — it is pure data/functions over primitives (`string`, `int`) with no Godot node or generator reference.
- It is depended ON by other files in this batch: `TerrainIsometricRendererComponent` reads `Count`, `Sea`, `Ground`, `Hills`, `Mountains`, `Summits`, `ZFor`, `ZForProps`, `ZForSeabed`, and `LevelFor` directly.

## Notes

- `LevelFor` and `LevelForKind` are two different classification rules by design (one relief-aware, one kind-only fallback for flat/no-relief views) — this is deliberate duplication-by-necessity documented in the doc comments, not an accidental second copy of the same logic; both are actively read (`LevelFor` by `TerrainIsometricRendererComponent`, per this batch).
- The class's own comment calls out a real historical defect (stale doc describing interleaved props after `ZForProps` changed to non-interleaved) as a cautionary example; the current comment and the current `ZForProps` implementation agree with each other as of this read — no live staleness found here.
- `ZForFloor()` and `ZForMarkers()` are exported but not called by any file read in this batch (`TerrainIsometricRendererComponent` does not use a floor layer or markers) — plausibly consumed by the painted/flat renderer or a markers component outside this batch; not evidence of dead code on its own since only five of the many terrain files were read.
