# Mountain Access Ramps

This pack contains standalone narrow ramp sprites for Mountain Prefab 1 and Mountain Prefab 2. The ramps are not embedded into the mountain bases, so designers can place them manually in Godot.

## Output

- Catalog: `addons/beep_game_builder_cs/generated/mountain_access/ramps/mountain_access_ramps_catalog.json`
- Preview: `addons/beep_game_builder_cs/generated/mountain_access/ramps/mountain_access_ramps_preview.png`
- Generator: `tools/prepare_mountain_access_ramps.py`

Each prefab style has seven matching material themes:

- `sandstone`
- `grass_granite`
- `grey_rock`
- `volcanic_basalt`
- `meadow_hill`
- `red_rock_mesa`
- `alpine_snow`

## Ramp Modules

Every theme contains nine manual ramp sprites:

- `ramp_quarter_height_front`
- `ramp_quarter_height_left`
- `ramp_quarter_height_right`
- `ramp_half_height_front`
- `ramp_half_height_left`
- `ramp_half_height_right`
- `ramp_full_height_front`
- `ramp_full_height_left`
- `ramp_full_height_right`

The ramps are intentionally compact wedge pieces, not long strips. Their visible width is kept under 160 px so they can fit smaller level designs without forcing the whole mountain to be wider.

## Usage

Use the mountain prefab manifests for the base structures, then place ramp sprites from this catalog as separate manual access pieces. The `relative_level_height` value tells the designer whether the ramp is quarter, half, or full mountain-level height.
