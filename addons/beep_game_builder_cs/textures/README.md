# Placeholder texture directory

This directory holds the PNG/JPG assets referenced by `textures{}` blocks in
theme.json files. They are loaded at runtime as `StyleBoxTexture` 9-patches by
`ThemePresetComponent` (per slot).

Paths used by shipped themes:

```
textures/
├── backgrounds/
│   ├── sky_tile.png       ← platformer/geometry.json background_image
│   └── parchment.jpg      ← topdown/geometry.json background_image
├── cartoon/
│   ├── button_normal.png
│   ├── button_hover.png
│   ├── button_pressed.png
│   └── panel.png
├── scifi/
│   ├── button_normal.png
│   ├── button_hover.png
│   ├── panel.png
│   └── input_normal.png
└── sea/
    ├── button_normal.png
    ├── button_hover.png
    └── panel.png
```

## Authoring notes

- Each button-state texture is a 9-patch: corner radii match the JSON `margin_*`
  values. Center can be transparent if `draw_center: false`.
- The `panel.png` is the canonical "card" texture behind most UI surfaces.
- Backgrounds should tile seamlessly (the engine sets
  `StretchMode = Tile`/`Stretch` based on `background_mode` in geometry.json).

If any texture path is missing, the loader gracefully falls back to the
procedural `StyleBoxFlat` for that slot — themes still render correctly even
without these placeholder PNGs. Drop your own PNGs here to see the JSON-driven
texture pipeline in action.
