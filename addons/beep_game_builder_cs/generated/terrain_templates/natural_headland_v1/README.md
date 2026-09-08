# Natural Headland, Version 1

A broad irregular single-level plateau inspired by the user's coastal
headland reference. The existing isometric cliff atlas supplies the painted
rock art direction. It has an open grass surface, weathered granite rims,
projecting headlands, and recessed front and side cliffs.

- `grass_granite_headland.png`: 1512x1040 RGBA terrain sprite, actual transparency.
- `natural_headland.tscn`: independent Godot Sprite2D scene at source scale.

Place the scene in a 2D level and adjust its Node2D scale as needed. Roads,
plants, structures, water and access pieces can be placed separately.
No collision, navigation or ramps are baked into this visual prefab.
It is one complete image, not a seamless tile set or automatically extensible
terrain mesh. Previously approved mountain and hill assets are untouched.

Shape reference: `codex-clipboard-d9190441-a913-4eb9-96db-e1839bafb730.png`.
Style reference: `Art/TileSets/isometric Cliff and Mountain Tileset Atlas.jfif`.
Selected generation: `exec-ddf8aa16-4164-4ee7-bb50-e1a42070db3d.png`.

Validated image mode, alpha range and visible bounds. Godot runtime import
and scene placement have not been tested in this pass.
