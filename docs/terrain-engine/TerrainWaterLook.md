# TerrainWaterLook

One sea for one world, in every view that draws one (VIEW-04, implemented 2026-09-16). A
`[Tool][GlobalClass]` `Resource` holding the thirteen shared water dials and the four water
texture paths, assigned to `TerrainWorldComponent.WaterLook` and pushed to the painted, tile,
block-isometric and isometric-autotile renderers by `Draw()` exactly as `TerrainPropSizing` is.
It is render configuration, not a world recipe: it is not saved with the recipe and changes
nothing about generation, cells or navigation.

Three renderers used to export the same thirteen dials and the same four texture paths, each
with its own defaults. The tile view's differed — `FoamStrength 1.0 / DeepTiles 6.0 /
ShallowTiles 6.0` against `0.50 / 4.5 / 1.8` in the painted and block views — so one map drawn
twice grew two seas, and the tile view's own comment called that a real divergence. The dials
live here now, once; no renderer exports one.

## Dials

The defaults are the shader author's own values, which are what the painted and block views
already drew. The tile view is the one that moved.

| Property | Range | Default | Uniform |
| --- | --- | --- | --- |
| `WaveIntensity` | 0–2 | 1.0 | `wave_intensity` |
| `FoamStrength` | 0–1 | 0.50 | `foam_strength` |
| `ShallowTiles` | 0–8 | 1.8 | `shallow_tiles` |
| `DeepTiles` | 0.5–12 | 4.5 | `deep_tiles` |
| `GroundTextureTiles` | 1–32 | 12.0 | `ground_texture_tiles` |
| `WaterTextureTiles` | 1–32 | 6.0 | `water_texture_tiles` |
| `FoamTilesAlong` | 1–48 | 11.0 | `foam_tiles_along` |
| `FoamTilesAcross` | 0.3–8 | 1.6 | `foam_tiles_across` |
| `FoamScroll` | 0–4 | 0.055 | `foam_scroll` |
| `FoamPulse` | 0–1 | 0.34 | `foam_pulse` |
| `FoamArrivalRate` | 0–4 | 0.9 | `foam_arrival_rate` |
| `SwellDirectionDegrees` | 0–360 | 210.0 | `swell_direction_degrees` |
| `SwellDirectionality` | 0–1 | 0.65 | `swell_directionality` |

`WaveIntensity` is one sea-state dial rather than several: bigger waves reach further out, their
crests broaden and the wash runs further up the sand, so those move together. `FoamTilesAcross`
is deliberately short — the surf band is under a tile deep, and a repeat spread over many tiles
parks the foam sheet's crest bands outside it. `SwellDirectionality` at zero puts surf on every
shore alike.

These are the **class** defaults. The shipped `terrain_water_look.tres` overrides two of them for
the demo scenes that assign it — see [Assignment](#assignment).

The ranges above are the shader's own `hint_range`, and `TerrainWaterMaterial.Apply` clamps to
them when it writes. An `[Export]` hint constrains the Inspector and nothing else, so a script
assigning `WaveIntensity = 40` used to reach the shader unchecked in two of the three views.
Two values pass through unclamped by design: `SwellDirectionDegrees`, because 370 degrees is 10
and not 360, and the coast range, which is the caller's and must agree with the range the coast
field was actually built with.

## Textures

`ShallowTexturePath`, `DeepTexturePath`, `SeabedSandTexturePath` and `FoamSheetPath` (`*.png`,
`*.webp`) bind `tex_shallow`, `tex_deep`, `tex_sand` and `foam_sheet` through
`TerrainWaterMaterial.ApplyTextures`, which loads them with `TerrainTextures.Load`. An empty
path leaves that sampler unbound and the shader falls back to flat colour; an empty
`FoamSheetPath` additionally leaves `use_foam_sheet` false so the sea draws generated crests.
A set-but-unloadable foam sheet is reported twice — once that the file did not load, once that
the map will draw generated crests instead.

**The seabed sand is `SeabedSandTexturePath`, not `SandTexturePath`.** The painted renderer
keeps its own `SandTexturePath` for the LAND material — `textures/terrain/sand.png`, the beach a
unit walks on, authored in four shipped scenes — while this one is the bottom seen through the
shallows. Both bind the same shader uniform from different views, so one name for both would
have quietly swapped a demo's beach for an ocean floor.

The foam sheet is a strip of equal frames of authored surf, sampled by distance from the
waterline rather than stamped per tile, so the coastline stays the smooth one the distance field
describes. It needs a soft fringe: a flat cutout silhouette collapses to one solid band.

## Assignment

Assign the look on the **world**, not on a renderer. `TerrainWorldComponent.Draw()` pushes
`WaterLook` to all four renderers on every build, restore and redraw, so a renderer-level
assignment would be overwritten on the next build.

A world that assigns nothing still draws one sea: each renderer falls back to
`TerrainWaterLook.Shared`, the class defaults, shared by every view. That fallback is the shipped
defaults, not the shipped `.tres`.

The shipped resource is `textures/terrain/terrain_water_look.tres`, a scene preference in the
same sense as `painted_ground_tiling.tres` — it carries what the demos authored: `FoamStrength`
0.4, `GroundTextureTiles` 6.0, the turquoise shallow, deep-blue ocean and golden-sand seabed
textures, and the `surf_foam_streaks.png` sheet. `terrain_tilemap_demo.tscn`,
`terrain_iso_demo.tscn`, `terrain_splat_demo.tscn` and `terrain_generator_lab.tscn` assign it on
their `TerrainWorldComponent`. A scene that wants its own sea authors its own `.tres` and assigns
that.

**The class default and the shipped profile are two different things, and both are correct.** The
class default is what a world draws when nobody has said anything — it is the shader author's
value, and the table above is the whole list of them. The shipped `.tres` is an authored
*preference*, tuned against the art the demos actually bind; where it names a property it
overrides the class default for the scenes that assign it, and it says nothing about the ones it
omits. Two of the thirteen are overridden today: `FoamStrength` 0.4 against the default 0.50, and
`GroundTextureTiles` 6.0 against the default 12.0 (FIX-14, 2026-09-16 — one repeat over twelve
tiles magnified the ground material past its own scale and read as blurred). Changing the `.tres`
changes those four demo scenes; changing the class default changes every world that assigns no
look at all, which is why the shipped art's tuning lives in the resource and not in the source.

## What is not here

What stays per view is what is genuinely per surface, not per look: the coast window each view
resolves for itself (`CoastRangeTiles`, `CoastDetail`), the transparent-sheet uniforms only a
floating water surface has (`MaxOpacity`, `ClarityTiles`, `LakeOpacity`, `ShoreOpacity`), and
the block view's `SeabedDepth`, `SeabedStep` and `WaterOverscan`. The painted view reads the
thirteen dials and `FoamSheetPath` and no more: it composites its seabed from its own LAND
materials, so the three water-bed textures are not bound there.

Per-texture ground repeat overrides remain [`TerrainMaterialTiling`](TerrainMaterialTiling.md)'s;
its unset slots inherit this resource's `GroundTextureTiles`.

## Tests

`tests/terrain_water_material_probe.gd` authors one look with thirteen non-default values,
pushes it to all four views and checks that every water material carries all thirteen, that the
four views agree, that the shared writer still clamps to the shader's `hint_range`, and that no
renderer exports a dial of its own. `tests/terrain_view_parity_probe.gd` checks eight of the
shared uniforms across the lab's four seas as one world-one-sea. Re-adding a private
`FoamStrength` export to the tile view fails both the probe and the contract scan.
