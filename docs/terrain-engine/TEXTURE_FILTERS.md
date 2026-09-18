# Texture filters, per rendering type

Every view states how **its own** art is sampled. The answers differ because what the views draw
differs, and a single engine-wide setting would be wrong for most of them. This page is the one
place the whole set is written down; each renderer states its own answer in its own `Rebuild`.

## The two questions a filter answers

Godot's canvas filters vary along two independent axes, and only one of them is a matter of taste.

**Mipmaps, or not.** Every terrain view minifies: a tile, a tree or an icon is drawn smaller than
its art as soon as the camera pulls back. Without a mip chain that is aliasing, which reads as a
shimmering grid on a moving map. So every art-bearing node in this engine is on a `*_WITH_MIPMAPS`
filter. Plain `Nearest` and plain `Linear` cannot sample a mip level at all — Godot's class
reference calls both "grainy from a distance (due to mipmaps not being sampled)".

**What happens when the camera MAGNIFIES.** This is the only real choice, and the Godot docs state
it plainly: `NEAREST_WITH_MIPMAPS` is "pixelated from up close, and smooth from a distance";
`LINEAR_WITH_MIPMAPS` is "smooth from up close, and smooth from a distance". Both handle distance
identically. Up close, linear blends the four nearest texels and nearest keeps the artist's own.

- **Sprite stamps take nearest.** `TerrainPropSizing.DrawnPixels` already refuses to draw a stamp
  larger than the art it comes from, so the renderer never magnifies — the only magnification left
  is the player's zoom. These sheets are hard-edged cartoon art, and interpolating a hard edge
  produces a smear, not detail.
- **Ground and icons take linear.** Tile and block art is a continuous surface drawn one-to-one,
  and the resource icons were drawn with soft anti-aliased edges: keeping their texels would only
  keep the jaggies the artist smoothed away.

## The set

| Rendering type | Node it is stated on | Filter | Why this one |
| --- | --- | --- | --- |
| `TerrainPaintedRendererComponent` | `SplatSurface` | Linear | Samples no art. Every `sampler2D` in `terrain_splat.gdshader` declares its own filter (ids nearest so they never interpolate, shade linear, materials linear over their own mip chains), so this covers only the blank tile the surface is built from. Ground sharpness belongs to the shader and to `TerrainMapArt`'s grain. |
| `TerrainTileRendererComponent` → `TerrainTransitionLayerComponent` | each biome `TileMapLayer` | Linear + mipmaps | Stated where the 15-piece atlas is built **with** a mip chain, so both halves of one decision sit together. Tiles are drawn one-to-one and minified as the camera pulls back. |
| Either view under a `TerrainLibraryPack` | the published layer, written by `TerrainLibraryPainter.Build` | pack's `PixelArt`: nearest + mipmaps, else linear + mipmaps | The pack's art knows what it is, and the painter is the one place that asks — the same answer whether the pack is drawn flat or isometrically. |
| `TerrainIsometricRendererComponent` | `IsoLevel*`, `IsoSeabed` | Linear + mipmaps | Detailed block art on 462x308 cells, minified several times over at map zoom. |
| `TerrainIsometricAutotileRendererComponent` | `IsoTerrain` | Linear + mipmaps | Authored isometric tiles, minified hardest of any view. This is the answer for the view's **own** art; under a pack the painter writes the pack's on the same layer. |
| `TerrainFeatureRendererComponent` | the renderer node | **Nearest + mipmaps** | Authored sprite frames, capped at their own art by `TerrainPropSizing`. |
| `TerrainReliefRendererComponent` | the renderer node | **Nearest + mipmaps** | Painted rock silhouettes with hard edges, same cap. |
| `TerrainIsometricFeatureRendererComponent` | the renderer and each `Props<level>` child | **Nearest + mipmaps** | The per-level children draw the stamps, so each states it; the parent's is what an unlisted child would inherit. |
| `TerrainResourceRendererComponent` | the renderer node | Linear + mipmaps | Soft-edged icons drawn at about half a tile — always minified. |
| `SeededTerrainPropScatterComponent` | each `GeneratedTerrainStamp` `Sprite2D` | Linear + mipmaps | The exception that proves the stamp rule. Its scatter draws at `MinScale` 0.32 to `MaxScale` 0.52, so these stamps are **always** reduced — there is no magnification to preserve texels for, and at minification linear blends four texels within the mip where nearest picks one. Nearest is the answer for stamps a player can zoom INTO, which these are not. |
| `MountainTileMapLayerGeneratorComponent` | the generated `TileMapLayer` | Linear + mipmaps | A tile layer like the other tile views, drawn one-to-one and minified at map zoom. |
| `MountainPrefabGeneratorComponent` | each generated part `Sprite2D`, via its `PartTextureFilter` export | Linear + mipmaps | Big painted plates, up to 550x379 in the shipped manifests, drawn one-to-one — so minified from the moment the camera pulls back. Soft-edged painted art, not a capped sprite stamp, so linear rather than nearest. |
| `ModularMountainPrefabComponent` | same, its own `PartTextureFilter` | Linear + mipmaps | The same art and the same argument — authored plates up to 550x379 drawn one-to-one. |
| `TerrainMapOverlayComponent` | — | none | Draws rings and survey patches with the canvas primitives and binds no texture, so it has no filter to state. |
| Every shader sea (`TileWater`, `IsoWater`, `IsoRivers`) | — | canvas filter unused | `water_common.gdshaderinc` declares a filter on each sampler it uses (coast linear, sand/shallow/deep/foam linear over mip chains). |

One gap is deliberate and worth knowing about: `TerrainTransitionLayerComponent` states the filter
inside `EnsureDisplayTileSet`, which runs only when it **builds** the TileSet from an atlas path. A
scene that authors its own TileSet and uses Godot terrain sets keeps the filter its author set on
the layer — which is how the 15-piece demo scene once had every layer on `Nearest` and discarded the
mip chain the component had just built. If you author a terrain TileSet by hand, set the layer's
filter to linear with mipmaps yourself.

## What changed on 2026-09-18, and why

Trees and rocks blurred as soon as the camera zoomed in, in both the Original and Cartoon looks.
`TerrainPropSizing.DrawnPixels` had already stopped the renderers magnifying art (see
`TerrainPropSizing.md`), so what remained was the filter: at zoom 4 a linear filter interpolated
between the artist's pixels and turned a 58x120 tree into a smear. Rendered side by side at zoom 4,
linear smeared and nearest showed the art's own pixels.

Three things were wrong, and they were wrong in different ways:

1. **The flat and isometric prop renderers hardcoded linear + mipmaps.** They now state nearest.
2. **The relief renderer asked `TerrainMapArt.PixelArt`.** That flag is the **ground's** art style —
   it picks `art_style` for the splat shader — and asking it here answered "linear" for the cartoon
   profile, which is how painted rock silhouettes came to blur at zoom. The relief renderer now
   states its own answer, like its two siblings, and `PixelArt` keeps its one real job.
3. **A library pack's tiles were sampled without mipmaps, and two places claimed to decide it.**
   `TerrainLibraryPainter.Build` writes the pack's answer on the layer it publishes, for both views
   that can draw a pack — but it wrote `Nearest` / `Linear`, the two filters that cannot sample a
   mip level at all, so a pack shimmered at map zoom where the engine's own atlases did not. Both
   answers are now mip-aware, and `PixelArt` still decides what magnification does.

   Beside it, `TerrainIsometricAutotileRendererComponent.EnsureLayer` stated its own copy of the
   pack's answer, which the painter then overwrote — two owners agreeing today and free to disagree
   tomorrow, with nothing to report it. It now states only the answer for the view's own art. (A
   rival copy was briefly added to the flat tile view as well, on the mistaken reading that the flat
   view enforced nothing; the guard's mutation run is what exposed the painter as the real owner.)

4. **The mountain prefab generators built a mip chain and threw it away.** Found on the same sweep,
   fixed later the same day. Both `MountainPrefabGeneratorComponent` and
   `ModularMountainPrefabComponent` set every `Sprite2D` they generate to plain `Linear`, which
   cannot sample a mip level — while their loaders (`TerrainTextures.Load` and an inline copy) call
   `GenerateMipmaps()` on every part. Nothing in the repo overrode either export, so every mountain
   part shipped mipless and aliased whenever a map camera pulled back. Both now default to
   `LinearWithMipmaps`.

   `ModularMountainPrefabComponent` carried a second fault found in the same read, and it is the
   more instructive one: its inline `LoadTexture` was the last surviving copy of the bug
   `TerrainTextures.Load` was written to fix. `Image.LoadFromFile` returns **null**, not an empty
   image, for a file that is missing or undecodable — so testing `IsEmpty()` first dereferenced that
   null and threw `NullReferenceException`, making the `InvalidDataException` that names the path
   unreachable on exactly the failure it was written for. It now delegates to the shared loader and
   throws on null, which fixes the bug and removes the duplicate in one move. Its sibling had been
   migrated off the same copy earlier; this is what rule 3 means by a second implementation being
   somewhere for a bug to survive on its own.

The cost of nearest is honest: at play zoom a prop is slightly minified, and nearest picks one texel
within the mip where linear blends four, so props shimmer a little more when the camera pans. The
real fix for that is art with enough pixels to be drawn at the size it is asked for — `PROP_ART_BRIEF.md`
states the sizes the sheets need.

## Verification

`tests/terrain_texture_filter_probe.gd` builds the lab, switches through all four projections and
pins every row of the table above **that the lab draws**, plus both library-pack answers. A node is
only pinned once it has actually drawn — the probe counts stamps, icons or used cells first — so a
renderer that never rebuilt fails instead of passing on Godot's inherited default.

**Its blind spot, stated because a guard's limits matter as much as its checks:** the last four rows
are not pinned. `SeededTerrainPropScatterComponent`, `MountainTileMapLayerGeneratorComponent` and
the two mountain prefab components are authoring components placed in their own creator scenes, not
renderers the terrain lab instantiates, so the probe never sees them — which is exactly how the two
mountain components sat on a mipless filter through a sweep that was looking for mipless filters.
Anything added to those four is unguarded until a probe drives their scenes.

**Mutation** (run 2026-09-18): restoring `LinearWithMipmaps` in
`TerrainFeatureRendererComponent.Rebuild` fails it with
`flat props (trees, bushes): linear+mips, wanted nearest+mips`, and restoring the painter's mipless
pair fails both pack pins with `library pack drawn flat, PixelArt false: linear, wanted linear+mips`
and `... PixelArt true: nearest, wanted nearest+mips`. That second mutation is what showed the
painter, not the renderers, owns a pack's answer.
