# Mountain Prefab 1

`Mountain Prefab 1` is the front-facing 2.5D modular mountain style provided by
`ModularMountainPrefabComponent`.

## Material Variations

- Low Poly Sandstone
- Grass + Granite
- Grey Rock
- Volcanic Basalt
- Gentle Meadow Hill
- Red Rock Mesa
- Alpine Snow

All variations provide a three-level ramp-free mountain and a one-level
mountain. The three-level mountain is authored as separate base, middle, and
top plate sprites so the component can generate each height layer as its own
`Sprite2D`.

Every material variation also includes two manually placed ramp-replacement
plates:

- `step_quarter.png` / `plate_quarter_height.png`: quarter-height helper plate
- `step_half.png` / `plate_half_height.png`: half-height helper plate

## Godot Component

Use the template scene:

`res://addons/beep_game_builder_cs/templates/scenes/modular_front_2_5d_mountain_creator.tscn`

Inspector controls:

- `PrefabStyle`: `MountainPrefab1`
- `MaterialTheme`: selects one of the built-in material variations
- `BasePrefabId`: `three_level_wide_no_ramps` or `one_level_wide_no_ramps`
- `PrefabScale` and `PrefabOffset`: control placement

The component generates only the ramp-free mountain structure. Prefab 1 does
not include ramp modules; the quarter and half helper plates are exported in
the manifests for developer-side manual placement.

## Generated Catalog

The material catalog is:

`res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/modular_mountain_theme_catalog.json`

The combined visual comparison is:

`res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/mountain_prefab_1_all_variations_preview.png`

The manual helper plate comparison is:

`res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/mountain_prefab_1_all_step_plates_preview.png`
