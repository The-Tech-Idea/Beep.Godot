# Mountain Prefab 2

`Mountain Prefab 2 - Natural Plateau` is a visual-only front-facing 2.5D
mountain pack. It is built from separate authored plate sprites so the Godot
component can place the base, middle, and top levels as independent layers.

The mountain structure does not contain attached ramps, paths, stairs,
collision, navigation, or walkability metadata. Two small manual step plates
are included as ramp replacements for every material theme:

- `step_quarter.png` / `plate_quarter_height.png`: quarter-height helper plate
- `step_half.png` / `plate_half_height.png`: half-height helper plate

Developers place those helper plates manually wherever their level design
needs a jump route.

## Godot Template

Use:

`res://addons/beep_game_builder_cs/templates/scenes/natural_plateau_mountain_creator.tscn`

The template uses:

- `PrefabStyle`: `MountainPrefab2NaturalPlateau`
- `MaterialTheme`: any built-in mountain material theme
- `BasePrefabId`: `natural_plateau_three_level_no_ramps`
- `PrefabScale` and `PrefabOffset`: visual placement controls

## Material Variations

Mountain Prefab 2 includes Low Poly Sandstone, Grass + Granite, Grey Rock,
Volcanic Basalt, Gentle Meadow Hill, Red Rock Mesa, and Alpine Snow.

Every theme contains:

- `plate_base.png`
- `plate_middle.png`
- `plate_top.png`
- `natural_plateau_three_level_no_ramps.png`
- `natural_plateau_one_level.png`
- `step_quarter.png`
- `step_half.png`
- `plate_quarter_height.png`
- `plate_half_height.png`
- `mountain_plate_sheet.png`
- `manual_step_plate_sheet.png`
- `one_level_mountain_sheet.png`
- `surface_fill_tile.png`
- `mountain_prefab_2_manifest.json`

Theme catalog:

`res://addons/beep_game_builder_cs/generated/mountains/natural_plateau/authored_prefabs/mountain_prefab_2/themes/mountain_prefab_2_theme_catalog.json`

Preview:

`res://addons/beep_game_builder_cs/generated/mountains/natural_plateau/authored_prefabs/mountain_prefab_2/themes/mountain_prefab_2_all_variations_preview.png`
