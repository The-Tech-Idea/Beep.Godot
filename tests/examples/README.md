# Beep 2D/Isometric Addon Examples

These scenes are small, loadable examples for inspecting the current addon work
inside Godot.

- `grid_world_kit_hud_example.tscn` is a curated design-time gameplay slice:
  a base depot, trucks, cleared land, prepared plots, road work, resource HUD,
  tool palette, and base command panel. It uses the addon components on authored
  nodes instead of showing the raw starter scene as a component dump.
- `grid_world_painterly_demo.tscn` is the focused terrain/grid demo. It uses
  `GridTerrainGeneratorComponent`, `GridPainterlyTerrainBridgeComponent`,
  `PainterlyTerrainComponent`, roads, navigation, placement reservations,
  worker spawning, and `GridWorldStateComponent` together from design-time
  scene nodes.
- `base_worker_templates_example.tscn` shows the reusable base/depot and
  worker/truck template scenes side by side.

Run the normal smoke check to validate them:

```powershell
powershell -ExecutionPolicy Bypass -File tests\runtime_smoke.ps1 -GodotCommand 'H:\dev\Godot\Godot_v4.7-stable_mono_win64.exe' -TimeoutSeconds 90
```
