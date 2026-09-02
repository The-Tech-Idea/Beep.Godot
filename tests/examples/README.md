# Beep Terrain And Grid Guards

The example **scenes** are not here. They ship with the addon, in
`addons/beep_game_builder_cs/templates/scenes/terrain/`, so a project that copies
`addons/beep_game_builder_cs` gets working wiring rather than scenes left behind in a test
folder:

- `terrain_generator_lab.tscn` — the generator inspector: world axes, seed, projection
  switching, and the diagnostics line the generator reports.
- `terrain_splat_demo.tscn`, `terrain_tilemap_demo.tscn`, `terrain_iso_demo.tscn` — the same
  world drawn as a shader surface, as tiles, and isometrically.
- `terrain_generation_layers_demo.tscn`, `terrain_15_piece_layers_demo.tscn` — the layer stack
  and the 15-piece autotile path.
- `grid_world_kit_hud_example.tscn` — a curated design-time gameplay slice: base depot, trucks,
  cleared land, prepared plots, road work, resource HUD, tool palette, and dispatch panel, all
  on authored nodes rather than a component dump.
- `base_worker_templates_example.tscn` — the reusable base/depot and worker/truck templates
  side by side.

What lives in this folder is the **guards** that drive those scenes. Each one builds a real
world and asserts something about the result, then exits non-zero if any check failed:

| Guard | Asserts |
|---|---|
| `addon_selfcontained.gd` | no absolute paths, no addon reference into `tests/`, no C# here, scenes shipped |
| `demo_scenes.gd` | every shipped scene loads and builds something |
| `landmass.gd` | asking for N landmasses gives N, and they stay separated |
| `beach.gd` | `BeachWidth` is enforced, and a wider beach is a deeper one |
| `erosion.gd` | erosion strength actually changes the terrain, in both directions |
| `relief.gd` | peak materials land on peaks, not on flat ground |
| `vegetation.gd` | woods are ranked per landmass and appear on ground that can carry them |
| `views.gd` | the projections agree about where the water is |
| `tile_layers.gd` | re-layering keeps the cells it had |
| `iso_layers.gd` | sea → ground → hills → peaks → props draw in level order |
| `stack_order.gd` | scenes obey the `TerrainLayers` stack rules |
| `cell_data.gd` | a cell answers the same in every view, and is solid only on its own ground's layer |
| `perf.gd` | each world axis produces a distinguishable world, within budget |

`capture.gd`, `worldmap.gd` and `vegprobe.gd` are deliberately not in that table. The first two
render PNGs to a directory named by an environment variable and the third prints noise
distributions; they are tools for looking at the generator, not checks that pass or fail.

## Running them

All of them:

```powershell
powershell -ExecutionPolicy Bypass -File tests\terrain_guards.ps1 -GodotCommand 'H:\dev\Godot\godot.cmd'
```

One of them:

```powershell
& 'H:\dev\Godot\godot.cmd' --headless --audio-driver Dummy --path . --script res://tests/examples/cell_data.gd
```

`tests\run_addon_checks.ps1` runs `terrain_guards.ps1` as part of the full suite.
