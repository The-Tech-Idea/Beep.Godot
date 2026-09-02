# Low-Poly Mountain Prefab Generator

## Direction

Build mountains from coherent, height-aware prefab sprites. Do not construct a mountain by stacking isolated plateau images or placing a separate ramp image over a cliff.

The supplied files in `Art\TopDownTileSets\lowPoly_tiles` are visual references and source material. Their geometry, scale, and sockets are inconsistent, so they cannot be treated as a directly compatible terrain kit.

## Prefab Contract

Every accepted mountain prefab must be one unified terrain object containing:

- Clearly separated walkable height levels.
- Cliff support beneath every raised floor.
- A route physically integrated into the terrain.
- A ground-to-Level-0 entrance ramp at the front edge.
- Flush lower and upper ramp landings.
- No overlapping slabs, floating platforms, pasted paths, gaps, or hidden shelves.
- Transparent background and no baked checkerboard.

Godot metadata remains separate from the bitmap:

- One walkable polygon per floor.
- One climbable polygon per ramp.
- Explicit `from_level` and `to_level` route edges.
- Player, summit, and castle anchors.

## Current Proof

`two_level_transition.png` is the first accepted structural proof. It contains one broad lower floor, one supported upper floor, a ground entrance ramp, and one continuous internal ramp.

`three_level_mountain.png` is the approved default mountain. It contains three broad floors, a ground entrance ramp, two sequential internal ramps, a middle landing, and a castle-ready summit.

The default Godot template loads the three-level prefab. Headless probes verify both assets, their floor polygons, entrance and internal ramp areas, connected level graphs, and placement anchors.

## Next Work

1. Review the two-level transition visually before creating more art.
2. Generate a three-level prefab using the same continuous construction rule.
3. Generate compact, wide, and castle-top silhouettes as complete coherent prefabs, not compositions of incompatible source islands.
4. Add props only after the walkable floor and ramp polygons are validated.
5. Build an atlas from accepted complete prefabs after each sprite passes visual and gameplay checks.
