# VIEW-14 — Terrain shaders honour the item modulate and the canvas modulate

**Type:** fix (accepted-then-ignored: a tint or fade on a terrain renderer node never reached the ground or the sea, and the scene's ambient tint never reached the tile view's ground or the natural-terrain art) · **Area:** `shaders/terrain_splat.gdshader`, `iso_water.gdshader`, `terrain_tile_detail.gdshader`, `natural_terrain_green.gdshader`, `terrain_roads.gdshader`, `shoreline_contour_debug.gdshader`, `tools/build_cartoon_terrain_pack.gd`, `TerrainShaderSurface` (contract), `tests/terrain_item_modulate_probe.gd`, `tests/addon_contract_scan.ps1` · **Status:** Implemented 2026-09-15 · **Effort:** XS–S · **Risk:** low (under a white modulate and no CanvasModulate every surface draws exactly what it drew before; no 2D lights exist in the repo)

## Correction made on implementation (2026-09-15)

As proposed, this item said `terrain_splat` and `iso_water` discard the scene's **`CanvasModulate`**
(the `AmbientController` tint), "so at night the tile view's ground darkens while its sea stays
daylight". **Both halves of that were wrong, and experiments showed it before any code changed.**

Godot's canvas shader (`drivers/gles3/shaders/canvas.glsl` and
`servers/rendering/renderer_rd/shaders/canvas.glsl`, fragment `main()`) runs the user's
`#CODE : FRAGMENT` first. Afterwards it multiplies `color *= canvas_modulation`, but only
`#elif !defined(MODE_UNSHADED)`. Two rendered experiments under `CanvasModulate(1, 0.25, 0.25)`
confirmed this on Compatibility and Forward+:

| Item | Result | Meaning |
|---|---|---|
| shader writes `COLOR = vec4(1.0)` | `(1.00, 0.25, 0.25)` | the CanvasModulate survives overwriting COLOR |
| same shader, item `modulate (0.25, 1, 1)` | `(1.00, 0.25, 0.25)` | the **item** modulate is lost |
| `render_mode unshaded`, COLOR untouched | `(1.00, 1.00, 1.00)` | an unshaded item **skips** the CanvasModulate |
| production `terrain_tile_detail` / `natural_terrain_green` | `(1.00, 1.00, 1.00)` | both are unshaded |

So the painted ground and the sea always darkened at night. The sea did not stay daylight, but the
tile view's **ground** did: its detail shader is unshaded. The proposed guard (a CanvasModulate probe
over `terrain_splat` and `iso_water`) could not have failed. It was replaced by the probe below,
which failed first on each of the four defects.

## Gap

A canvas item is given two tints, and terrain dropped each one somewhere:

1. **The item modulate** (`Modulate`, `SelfModulate`, every parent's) arrives in the fragment's
   `COLOR` as texture × vertex colour × modulate; `construction_reveal.gdshader` already records the
   contract. `terrain_splat` wrote `COLOR = vec4(col, 1.0);` (and its contour-debug branch likewise),
   so the painted ground and composited sea ignored the renderer node's tint **and** opacity.
   `iso_water` kept only `COLOR.a` (`* surface_alpha`), so the tile view's `TileWater` and the
   block view's `IsoWater`/`IsoRivers` honoured a fade and the diamond cutout but dropped a tint.
   `terrain_roads` and `shoreline_contour_debug` also replaced `COLOR`.
2. **The canvas modulate**: one `CanvasModulate`, into which `AmbientController` composes
   day/night, weather and seasons, and the only lighting input in the repo (it has no 2D lights).
   `terrain_tile_detail.gdshader` draws the lab's Tiles-view ground layers (loaded by
   `TerrainTileRendererComponent.ApplyGroundDetail`), and `natural_terrain_green.gdshader` draws
   `NaturalTerrainPrefabComponent`'s plateau art. Both declared `render_mode unshaded` for no stated
   reason, so they stayed noon-bright under every ambient tint while every sprite on them darkened.
   `terrain_roads`, `shoreline_contour_debug` and the inline green-key shader in
   `tools/build_cartoon_terrain_pack.gd` were unshaded too.

## Design (as landed)

1. **Capture, then multiply.** Every shader that replaces `COLOR` starts `fragment()` with
   `vec4 modulate = COLOR;` and multiplies its result by it:
   - `terrain_splat` ends `COLOR = vec4(col, 1.0) * modulate;`, and the debug branch does the same.
   - `iso_water` ends `COLOR = vec4(water_col, max(depth_alpha, sea.foam) * on_water) * modulate;`.
     The captured alpha still carries the diamond cutout, so `terrain_water_alpha_probe` is unchanged.
   - `terrain_roads` and `shoreline_contour_debug` follow the same pattern.

   Shaders that already captured the input (`vertex()` varyings, in-place `COLOR.rgb *=`) are unchanged.
2. **No unshaded terrain shader.** `render_mode unshaded` is removed from `terrain_tile_detail`,
   `natural_terrain_green`, `terrain_roads`, `shoreline_contour_debug` and the tool's inline shader,
   each with a one-line comment saying why.
3. **The contract, written where the surfaces are made.** `TerrainShaderSurface`'s summary and
   `docs/terrain-engine/TerrainShaderSurface.md` say: replaced in colour, never in modulate. They also
   say the CanvasModulate is applied after the fragment, and only for a shaded item.
4. **Consumers.** `AmbientController` (day/night, weather, seasons) now reaches the Tiles-view
   ground and natural terrain. Any tint or fade on a terrain renderer or its parents reaches the
   painted ground and the sea. Nothing new is exported.

## Guards (failed first)

- `tests/terrain_item_modulate_probe.gd` (rendered; registered as `item_modulate` in
  `run_terrain_integration.ps1`). Every comparison is two copies of one surface in one frame.
  - **Item modulate:** the real `TerrainPaintedRendererComponent` and the production `iso_water`
    under a white parent and under a parent with `modulate (1, 0.25, 0.25)`. The tinted pixel must
    equal the reference × tint per channel within 3/255. Copies under `modulate.a = 0.5` must come
    out at alpha 0.5.
  - **Canvas modulate:** two viewports, the second with `CanvasModulate(1, 0.25, 0.25)`, each drawing
    the painted renderer, `iso_water`, and sprites with the production `terrain_tile_detail` and
    `natural_terrain_green`. Same tolerance.
  - **Controls:** a shader-less item under each tint proves the capture measures that tint at all.

  **Mutations, run against the final probe:**

  | Defect restored | Probe result |
  |---|---|
  | `terrain_splat` → `COLOR = vec4(col, 1.0);` | fails "painted ground under a node tint", `tinted=(0.780, 0.937, 0.659)` = reference |
  | `iso_water` → alpha-only `* modulate.a` | fails "sea surface under a node tint" |
  | `render_mode unshaded` on `terrain_tile_detail` | fails "tile ground detail under the CanvasModulate" |
  | `render_mode unshaded` on `natural_terrain_green` | fails "natural terrain under the CanvasModulate" |

  With everything fixed: ground `(0.780, 0.235, 0.165)`, sea `(0.459, 0.153, 0.204)`, tile detail
  `(0.667, 0.149, 0.129)`, natural `(0.620, 0.137, 0.118)`. OK on both renderers.
- Pin in `tests/addon_contract_scan.ps1`, beside the one-sea pins. It covers every `.gdshader` and
  `.gdshaderinc` under `addons/beep_game_builder_cs/shaders/`, read with comments stripped:
  - no `render_mode` may name `unshaded` or `light_only`;
  - every `COLOR =` assignment must use a variable the file captured from `COLOR` (or `COLOR`
    itself) other than only its `.a`;
  - it fails if it reads fewer than 10 files or 10 assignments.

  **Mutations:** all four defects above at once → the scan reported exactly those four new failures
  (20 = 16 pre-existing + 4), including the alpha-only one. Its limits are in its own comment:
  line-based, in-place writes not judged, shaders built from strings and `templates/shaders/*.template` not covered.
- Existing rendered probes that draw these shaders pass unchanged: `shader_alignment`,
  `painted_blend`, `painted_shading`, `material_scale`, `material_origin`, `lava_material`,
  `bedrock_repeat`, `coast_filter`, `shoreline_contours`, `water_alpha`, `iso_river`, `iso_cliff`,
  `lake_banks`, `coast_centres`, `lab_tile_views`.

## Dependencies / collisions

FEAT-07 (seasons) can use an `AmbientController` contribution for a whole-scene seasonal tint, and
it now reaches every terrain view. Independent of VIEW-04. VIEW-07 edits `terrain_splat.gdshader`
elsewhere (the beach band). `terrain_roads.gdshader` is the grid session's style asset. Its change
is the same two lines as the others, and it is bound by nothing but the offline bake tool.

## Out of scope

Per-view tint dials; 2D lights (none exist); who owns the day/night cycle (atmosphere session); HDR;
the untracked copy of `natural_terrain_green.gdshader` under `tests/NaturalTerrainPrefabProbe/`,
which that probe's `run.ps1` refreshes from the addon on every run.
