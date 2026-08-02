# Platformer Demo

Run:

```text
godot --path . examples/platformer_demo/platformer_demo.tscn
```

Controls: A/D or arrow keys to move, Space/W/Up to jump.

This example instances the addon's own `platformer_main.tscn`, so it uses the same
platformer controller, level loader, weather/atmosphere scene, pickups, kit HUD and
theme binding that generated platformer projects use. The demo script only adds solid
terrain, a finish flag and input actions so the template is playable directly from the
repository.

Smoke test:

```text
godot --path . examples/platformer_demo/smoke.tscn
```
