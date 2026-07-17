# Beep Skin System — File Formats Reference

> Source-of-truth schema reference for every JSON file loaded by
> `addons/beep_game_builder_cs/ecs/ui/SkinCatalog.cs`. Loaded at runtime by
> scanning the `addons/beep_game_builder_cs/catalogs/skins/` tree.

---

## Layout

```
catalogs/skins/
└── <genre_id>/                          # e.g. platformer, puzzle, shooter, topdown
    ├── genre.json                       # Genre metadata + theme shortlist
    ├── geometry.json                    # Per-genre geometry profile + background image + shapes
    └── themes/
        └── <theme_id>/                  # e.g. cartoon, modern, scifi
            ├── theme.json               # 22 colors + per-theme geometry + animation + textures
            ├── default.json             # Palette variant (optional)
            └── <other_palettes>.json
```

All paths are read by `BeepFileUtils.LoadJson` and `DirAccess` — nothing else
needs to compile when you add or edit a file. Drop the JSON, restart Godot,
the loader picks it up.

---

## `genre.json`

```jsonc
{
  "id":              "platformer",          // string — matches the folder name
  "display_name":    "Platformer",          // human label for UI pickers
  "icon":            "🎮",                  // emoji or 1-char icon
  "description":     "Side-scrolling jump & run",
  "default_theme":   "cartoon",             // fallback when no theme is selected
  "default_geometry": "platformer",         // matches the geometry.json id
  "main_scene":      "platformer_main.tscn",
  "scenes":          ["level_select.tscn", "level_results.tscn"],
  "nav_wiring": {                          // GameInfo property -> this genre's screen
    "LevelSelectPath":   "level_select.tscn",
    "LevelResultsPath":  "level_results.tscn",
    "LevelCompletePath": "level_results.tscn"
  },
  "tuning": {                              // free-form dictionary consumed by the genre code
    "gravity":       980,
    "jump_velocity": -400,
    "move_speed":    200
  }
}
```

| Field             | Type     | Required | Consumed by |
|-------------------|----------|----------|-------------|
| `id`              | string   | yes      | `SkinCatalog.LoadGenre` |
| `display_name`    | string   | no       | genre selector UI |
| `icon`            | string   | no       | genre selector UI |
| `description`     | string   | no       | genre selector UI |
| `default_theme`   | string   | yes      | `ThemePresetComponent` fallback |
| `default_geometry`| string   | no       | `GeometryProfile.ByName` lookup |
| `main_scene`      | string   | no       | `GameInfo` |
| `scenes`          | string[] | no       | `GameInfo` |
| `nav_wiring`      | object   | no       | `BeepGenreGenerator.ApplyNavWiring` → `GameInfo` |
| `tuning`          | object   | no       | `GameApp.GenreTuning` |

### `nav_wiring`

Points `GameInfo`'s **genre-specific** scene paths at this genre's own screens. Key = a
`GameInfo` property name (PascalCase, exactly as declared in `core/GameInfo.cs`); value = a
scene filename relative to this genre's UI folder (`res://scenes/ui/<genre>/`), or a full
`res://` path used verbatim. Applied at generation time and baked into `game_info.tres`.

Wireable properties:
`LevelSelectPath`, `LevelResultsPath`, `CharacterSelectPath`, `LevelUpPath`,
`RunResultsPath`, `CodexPath`, `LevelMapPath`, `PreLevelPath`, `LevelCompletePath`,
`LevelFailedPath`.

Shared paths (`MainMenuPath`, `GameScenePath`, `SettingsScenePath`, `GameOverScenePath`,
`PauseMenuPath`) are **not** wireable here — every genre uses the same ones.

> **Undeclared means "this genre has no such screen."** The generator clears all genre paths
> before applying `nav_wiring`, so anything you omit ends up empty and the flow falls through
> (e.g. level-complete → game over). This matters: those properties are *declared* with the
> four original genres' scenes as defaults, so before `nav_wiring` was applied, every genre —
> platformer included — finished a level on the **puzzle** end screen, because
> `LevelCompletePath` was never empty and shadowed the fallback.

An unknown property name is ignored with a warning rather than failing silently.

---

## `geometry.json`

Per-genre geometry profile — used by `GeometryProfile` (which `ThemePresetComponent`
consumes via the `GeometryProfileName` export).

```jsonc
{
  "id":               "platformer",
  "display_name":     "Platformer",
  "corner_radius":    6,                   // all-corner radius in px (-1 = leave preset's)
  "border_width":     2,                   // all-side border in px (-1 = leave preset's)
  "shadow_size":      10,                  // drop-shadow pixels (-1 = leave preset's)
  "shadow_offset_y":  3,                   // drop-shadow Y offset in px (-1 = leave preset's)
  "content_padding":  16,                  // content margin in px all sides (-1 = leave preset's)
  "font_size":        24,                  // themed font size (-1 = leave preset's)

  "background_image": "res://addons/beep_game_builder_cs/textures/backgrounds/sky_tile.png",
  "background_mode":  "tile",              // "tile" | "stretch" | "center"

  "shapes": {                              // per-node-type overrides (Phase B)
    "panel":      { "shadow_reduction": 2 },
    "input":      { "inset_x": 4, "inset_y": 3, "min_x": 4, "min_y": 2, "focus_border_min": 2 },
    "progress":   { "corner_inset": 4, "margin": 2 },
    "slider":     { "grabber_shadow": 3, "grabber_hover_shadow": 5, "shadow_scale": 0.5, "track_divisor": 2 },
    "scrollbar":  { "grabber_divisor": 3, "grabber_min": 3 },
    "selection":  { "corner_divisor": 2, "corner_min": 2, "margin_x": 4, "focus_border": 1 },
    "separator":  { "separation": 4 }
  }
}
```

### `shapes.*` per-node defaults

These defaults are baked into `ShapeOverrides` (see `ecs/ui/ShapeOverrides.cs`),
so a `shapes` block that omits a field uses the legacy literal exactly. To
customize a single genre, copy the platformer block and change the numbers.

| Sub-key     | Field             | Default | Consumed by (in `NodeTheming.cs`) |
|-------------|-------------------|---------|------------------------------------|
| `panel`     | `shadow_reduction`| `2`     | `PanelBox` (`_shadowSize - reduction`) |
| `input`     | `inset_x`         | `4`     | `InputBox` (subtracted from `_padL`/`_padR`) |
|             | `inset_y`         | `3`     | `InputBox` (subtracted from `_padT`/`_padB`) |
|             | `min_x`           | `4`     | `InputBox` floor for L/R margin |
|             | `min_y`           | `2`     | `InputBox` floor for T/B margin |
|             | `focus_border_min`| `2`     | `InputBox` floor for focused border width |
| `progress`  | `corner_inset`    | `4`     | `ThemeProgressBar` (subtracted from preset radius) |
|             | `margin`          | `2`     | `RoundBox` 4-side content margin for progress |
| `slider`    | `grabber_shadow`  | `3`     | `CircleBox` default shadow for slider grabber |
|             | `grabber_hover_shadow` | `5`| `CircleBox` hover shadow |
|             | `shadow_scale`    | `0.5`   | `BuildSliderGrabber` track shadow multiplier |
|             | `track_divisor`   | `2`     | slider track corner radius divisor |
| `scrollbar` | `grabber_divisor` | `3`     | `ThemeScrollBar` (radius / divisor) |
|             | `grabber_min`     | `3`     | `ThemeScrollBar` floor |
| `selection` | `corner_divisor`  | `2`     | `SelectedBox` (radius / divisor) |
|             | `corner_min`      | `2`     | `SelectedBox` floor |
|             | `margin_x`        | `4`     | `SelectedBox` L/R content margin |
|             | `focus_border`    | `1`     | `SelectedBox` focused border width |
| `separator` | `separation`      | `4`     | `ThemeSeparator` constant for H/V Separator |

### Background image

| Field             | Type     | Default | Notes |
|-------------------|----------|---------|-------|
| `background_image`| string   | —       | `res://...` PNG/JPG. Falls back to canvas color if missing. |
| `background_mode` | enum     | `"stretch"` | One of `"stretch"`, `"tile"`, `"center"`. |

---

## `theme.json`

Per-theme visual package. Each theme defines its own complete identity — colors,
geometry, animation, and optional textures.

```jsonc
{
  "id":            "cartoon",
  "display_name":  "Cartoon",
  "category":      "Playful",
  "description":   "Bright playful UI — large pill corners, solid black outline, hard drop shadow, bouncy hover",

  "colors":      { /* 22 hex strings — see Colors block below */ },
  "geometry":    { /* 12 numbers — see Geometry block below */ },
  "animation":   { /* 6 fields — see Animation block below */ },
  "textures":    { /* per-node StyleBoxTexture specs — see Textures block below */ }
}
```

### `colors` — 22 hex strings

Every field uses `#RRGGBBAA` format. Both `[A]RGB` HTML and `#AARRGGBB` (alpha-first)
are accepted by `Color.FromString`. Names are exact: parsing uses a direct
dictionary key lookup in `LoadTheme()`, so typos silently produce white.

| Group    | Fields |
|----------|--------|
| Surface  | `surface_primary`, `surface_hover`, `surface_pressed`, `surface_disabled` |
| Text     | `text_primary`, `text_hover`, `text_disabled`, `text_on_dark` |
| Accent   | `accent_primary`, `accent_secondary` |
| Border   | `border_normal`, `border_hover`, `border_focus`, `border_bevel_light`, `border_bevel_dark` |
| Shadow   | `shadow_color` |
| Background | `bg_panel`, `bg_canvas` |
| Semantic | `semantic_success`, `semantic_danger`, `semantic_warning`, `semantic_info` |

Consumed by `ColorSchema` (see `IThemePreset.cs`) and threaded through every
`Theme*()` method in `NodeTheming.cs`.

### `geometry` — 12 numbers

Same shape as the geometry block inside `geometry.json` but reduced to the
12 fields that vary per theme:

| Field             | Type | Consumed by |
|-------------------|------|-------------|
| `corner_radius`   | int  | All `NewBox()`-derived StyleBoxes |
| `border_left`/`border_top`/`border_right`/`border_bottom` | int each | All `NewBox()`-derived StyleBoxes |
| `shadow_size`     | int  | All `NewBox()`-derived StyleBoxes |
| `shadow_offset_x`/`shadow_offset_y` | int each | All `NewBox()`-derived StyleBoxes |
| `pad_left`/`pad_right`/`pad_top`/`pad_bottom` | int each | All `NewBox()`-derived StyleBoxes |
| `font_size`       | int  | `FontSz()` per-node themer |

This is the **theme's own** geometry — the one extracted in `ExtractGeometry`
and used as the basis for every derived StyleBox. The genre-wide `geometry.json`
overrides these when present (via `GeometryProfile.ApplyTo` + `StampGeometry`).

### `animation` — 6 fields

| Field               | Type   | Default        | Consumed by |
|---------------------|--------|----------------|-------------|
| `hover_scale`       | float  | `1.04`         | `SetupButtonAnimations` MouseEntered |
| `hover_duration`    | float  | `0.15`         | `SetupButtonAnimations` MouseEntered/MouseExited |
| `press_scale`       | float  | `0.96`         | `SetupButtonAnimations` ButtonDown |
| `press_duration`    | float  | `0.08`         | `SetupButtonAnimations` ButtonDown/ButtonUp |
| `shadow_lift`       | bool   | `true`         | `SetupButtonAnimations` MouseEntered/Exited Y offset |
| `focus_glow`        | bool   | `true`         | `SetupButtonAnimations` FocusEntered/Exited |

Zero-initialised when missing — disables animation.

### `textures` — per-slot `StyleBoxTexture` specs (Phase C)

```jsonc
"textures": {
  "button_normal":  { /* TextureSlotDef — see below */ },
  "button_hover":   { /* … */ },
  "button_pressed": { /* … */ },
  "button_disabled":{ /* … */ },
  "button_focus":   { /* … */ },
  "panel":          { /* … */ },
  "input_normal":   { /* … */ },
  "input_focus":    { /* … */ },
  "progress_bg":    { /* … */ },
  "progress_fill":  { /* … */ },
  "slider_grabber": { /* … */ },
  "scroll_grabber": { /* … */ },
  "separator":      { /* … */ }
}
```

Each slot is a `TextureSlotDef`:

| Field                   | Type             | Default        | Maps to Godot |
|-------------------------|------------------|----------------|---------------|
| `texture_path`          | string           | **required**   | `StyleBox.Texture` |
| `margin_left`           | float            | `0`            | `TextureMarginLeft` (9-patch) |
| `margin_top`            | float            | `0`            | `TextureMarginTop` |
| `margin_right`          | float            | `0`            | `TextureMarginRight` |
| `margin_bottom`         | float            | `0`            | `TextureMarginBottom` |
| `axis_stretch_horizontal` | int            | `1`            | `AxisStretchHorizontal` (0=Stretch, 1=Tile, 2=TileFit) |
| `axis_stretch_vertical` | int              | `1`            | `AxisStretchVertical` |
| `draw_center`           | bool             | `true`         | `DrawCenter` |
| `modulate`              | hex `#RRGGBBAA`  | `#FFFFFFFF`    | `ModulateColor` |
| `content_margin_left`   | float            | `-1` (inherit) | `ContentMarginLeft` (-1 → uses texture_margin_ fallback) |
| `content_margin_right`  | float            | `-1`           | `ContentMarginRight` |
| `content_margin_top`    | float            | `-1`           | `ContentMarginTop` |
| `content_margin_bottom` | float            | `-1`           | `ContentMarginBottom` |
| `expand_margin_left`    | float            | `0`            | `ExpandMarginLeft` |
| `expand_margin_top`     | float            | `0`            | `ExpandMarginTop` |
| `expand_margin_right`   | float            | `0`            | `ExpandMarginRight` |
| `expand_margin_bottom`  | float            | `0`            | `ExpandMarginBottom` |

A slot with a missing `texture_path` (or any slot key omitted from `textures{}`)
falls back to the procedural `StyleBoxFlat` from `colors+geometry`. So you can
ship a theme with `textures: {}` only declaring `panel`, and every other slot
remains procedural.

**Precedence (Phase C):** when a slot has a texture, JSON wins per-slot. Then
the inspector-set `UISkin` resource fills slots the JSON omits. Then
procedural `StyleBoxFlat`.

---

## Palette `<palette>.json`

```jsonc
{
  "display_name":   "Warm",
  "hue_shift":      15.0,             // degrees, applied via Color.ToHsv
  "saturation_mul": 1.1,
  "value_mul":      1.0
}
```

All numeric fields are optional. A palette applies an HSV-space retint to every
`ColorSchema` color via `ColorPalette.Tint()` — geometry, animation, and
textures are immune to palette tinting (the texture carries its own colors).
