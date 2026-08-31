# Reference Mountain Prefab Creator

`MountainPrefabGeneratorComponent` builds a reference-style 2D mountain from shape-grid tiles.

Default source pack:

`res://addons/beep_game_builder_cs/generated/mountains/shape_based_mountain_prefab/prefab_manifest.json`

Source art:

`C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.Godot\addons\beep_game_builder_cs\generated\mountains\clean_source_atlases`

The prefab manifest points to:

- `prefab_chunk_manifest.json`: placed middle, surrounding, path, and route chunks.
- `tile_atlas.png`: reusable generated middle/edge/corner/cap/ramp tile atlas.
- `tile_atlas_preview.png`: labeled tile atlas for review.
- `prefab.png`: complete composed mountain prefab fallback.
- `prefab_preview.png`: visual preview.
- `prefab_path_debug.png`: level and path debug overlay.

## Chunk Contract

The default reference pack contains reusable tile roles:

- `middle`
- `edge_n`, `edge_e`, `edge_s`, `edge_w`
- `corner_ne`, `corner_es`, `corner_sw`, `corner_wn`
- `cap_nes`, `cap_new`, `cap_nsw`, `cap_esw`
- `path_middle`
- `ramp_ne`, `ramp_nw`

Every placed cell carries `height_level`, `tile_role`, and `missing_neighbor_sides`. Middle cells have no missing neighbors; surrounding cells become edge/corner/cap tiles. Route chunks also carry `from_level` and `to_level`, so gameplay can tell that a ramp sprite is a height transition.

## Godot Usage

Open:

`res://addons/beep_game_builder_cs/templates/scenes/reference_mountain_prefab_creator.tscn`

Or add `MountainPrefabGeneratorComponent` to any `Node2D`.

Recommended defaults:

- `SourceMode = Auto`
- `LayoutPreset = Reference`
- `CreateWalkableAreas = true`
- `CreateRouteConnectorAreas = true`
- `CreateAnchorNodes = true`

Useful runtime calls:

- `GeneratePrefab()`
- `GetMountainLevels()`
- `GetPrefabChunkAssets()`
- `GetHeightLevelAtLocalPosition(point)`
- `GetWalkableRegionAtLocalPosition(point)`
- `GetRouteRegionAtLocalPosition(point)`
- `GetAnchorPosition("player_spawn")`
- `GetAnchorPosition("castle_anchor")`
- `SaveGeneratedSceneToPath("res://generated/my_mountain.tscn")`
