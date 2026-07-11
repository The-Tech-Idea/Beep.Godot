# Beep Skin System — Architecture & Cookbook

> File-based theming for `beep_game_builder_cs`. Every genre ships with a
> `geometry.json`; every theme under a genre ships with `theme.json` + optional
> palette `.json` + optional textures via 9-patch PNG paths. Zero C# changes
> required to add a theme, palette, texture set, or even a whole new genre.

For per-field JSON schemas see **[FILE_FORMATS.md](FILE_FORMATS.md)**.

---

## 1. Overview

Before the skin-system refactor, there were 22 hardcoded `ThemePreset*.cs`
classes — one per visual style — each writing the same box/border/margin code
over and over with different numbers. Theming was a `Color` hex in code, not a
fileset you could swap.

The skin system replaces all of it with JSON. The same `ThemePresetComponent`
node reads any theme.json at runtime; palettes retint themes in HSV space;
texture 9-patches override individual StyleBox slots per author spec;
genre-wide geometry + per-node shapes + canvas backgrounds round out the four
independent dimensions a skin can vary on.

### Why file-based?

- **One source of truth.** No drift between "the theme the artist picked" and
  "the colors that compiled into the engine".
- **Instant iteration.** Drop a new PNG, edit one margin in JSON, scene
  reload — no `Build → Build Project` needed.
- **Modular themes.** 4 genres × 5 themes × 7 palettes × N texture packs =
  hundreds of combinations, all from files.

---

## 2. Directory layout

```
addons/beep_game_builder_cs/catalogs/skins/
├── platformer/
│   ├── genre.json          ← tuning + default theme + scene list
│   ├── geometry.json       ← per-genre geometry + background_image + shapes
│   └── themes/
│       ├── cartoon/
│       │   ├── theme.json  ← 22 colors + geometry + animation + textures
│       │   ├── default.json
│       │   ├── warm.json
│       │   └── cool.json
│       ├── modern/   …
│       ├── nature/   …
│       └── retro80s/ …
├── topdown/    (same structure)
├── shooter/    (same structure)
└── puzzle/     (same structure)
```

Everything starts at `res://addons/beep_game_builder_cs/catalogs/skins/`. The
loader (`SkinCatalog.LoadAllGenres`) scans this tree on first access; no
pre-registration needed.

---

## 3. Loader walk (what happens at startup)

```
SkinCatalog.LoadAllGenres()                  // scans catalogs/skins/*/ directories
    └─ LoadGenre(id, path)                   // parses genre.json + geometry.json
        ├─ ParseGeometry(...)                // reads corner_radius, border_width, …
        │   └─ ParseShapes(...)              // reads per-node overrides into ShapeOverrides
        └─ LoadTheme(...)                    // for each themes/<theme>/ folder
            ├─ parse colors{}                 // ColorSchema
            ├─ parse geometry{}               // ThemeGeometry
            ├─ parse animation{}              // AnimationConfig
            └─ ParseTextures(...)             // ThemeTextureSlots → TextureSlotDef{}
                                                    + palettes/*.json loaded by scan
```

After the first call to `AllGenres` (or any `GetXxx(...)`), the result is
cached in a static dictionary. `SkinCatalog.Reload()` clears the cache and
re-scans — useful in the editor after editing JSON.

---

## 4. Component consumption pipeline

```
GameInfoBinder._Ready                       (or ThemePresetComponent._Ready on its own)
    ├─ SkinCatalog.GetTheme(genreId, themeId)        ← resolves the JSON
    ├─ new FileThemePreset(themeDef)                 ← wraps it as IThemePreset
    ├─ if palette set: new PaletteTintedPreset(...) ← HSV-tint wrapper
    └─ ThemePresetComponent.ApplyTheme
        ├─ ExtractGeometry(preset.GetButtonNormal()) ← seeds _gTL/_bL/_padL/…
        ├─ ApplyBackground()                        ← spawns TextureRect behind root
        ├─ ApplyToSubtree()
        │   ├─ ThemeButton() / ThemeOptionButton() / …  ← one method per node type
        │   │   ├─ SkinOr(jsonTex, skinPath, procedural)  ← three-way texture resolution
        │   │   ├─ Box() / InputBox() / PanelBox() / RoundBox() / CircleBox() / SelectedBox()
        │   │   │     └─ ActiveShapes.* reads  (data-driven geometry)
        │   │   └─ StampGeometry()                      ← restamps genre geometry on the box
        │   ├─ ThemeSeparator() / ThemeWindow() / ThemeTree() / …
        │   ├─ root.Theme = _generatedTheme
        │   ├─ InjectIntoButtons() (animations + ripple, if enabled)
        │   └─ ApplyButtonOverrides(root, preset) (per-node for [Tool] editor visibility)
```

**The four dimensions stay independent:**
- Genre suggests geometry, shapes, and background image.
- Theme provides colors, per-theme geometry, animation, and optional textures.
- Palette retints colors in HSV space (no effect on textures).
- Texture (per-slot JSON path or inspector UISkin) overrides procedural boxes.

---

## 5. Adding a new genre

1. Create `addons/beep_game_builder_cs/catalogs/skins/<new_genre>/`.
2. Drop `genre.json` (copy from platformer, change `id`/`display_name`/`tuning`).
3. Drop `geometry.json` (copy from platformer — every field set, including
   `shapes` with the platformer defaults; tweak as desired).
4. Drop `themes/<new_theme>/` with `theme.json` (see schema in `FILE_FORMATS.md`).
5. Optional: drop PNGs under `addons/beep_game_builder_cs/textures/<new_genre>/`
   and reference them in your theme's `textures` block.
6. Restart the editor (or call `SkinCatalog.Reload()`).

---

## 6. Adding a new theme

1. Inside an existing genre's `themes/` directory, create `<theme_id>/`.
2. Drop `theme.json` with all 4 mandatory blocks (`colors{}`, `geometry{}`,
   `animation{}`; `textures{}` is optional).
3. Optional palette files alongside (`default.json`, `warm.json`, etc.).
4. Restart editor.

---

## 7. Adding a new palette

Palettes are dead simple — a single JSON with three floats:

```json
{ "display_name": "Sunset", "hue_shift": 30.0, "saturation_mul": 1.2, "value_mul": 0.9 }
```

Drop it in any theme directory; `SkinCatalog.LoadTheme()` picks it up alongside
`theme.json`. The HUD picker uses `display_name`.

---

## 8. Adding a new geometry profile (per-genre)

Edit `geometry.json`. Everything is optional with sensible defaults, so a
minimal entry is:

```json
{ "id": "platformer", "corner_radius": 6, "border_width": 2, "shadow_size": 10 }
```

Plus any of: `shadow_offset_y`, `content_padding`, `font_size`,
`background_image` + `background_mode`, and the `shapes` block.

---

## 9. Adding a new texture set to a theme

1. Drop PNGs under `addons/beep_game_builder_cs/textures/<genre>/<theme>/`.
2. In the theme's `theme.json`, add a `textures{}` block:

   ```json
   "textures": {
     "button_normal": {
       "texture_path": "res://addons/beep_game_builder_cs/textures/cartoon/button_normal.png",
       "margin_left": 16, "margin_top": 16, "margin_right": 16, "margin_bottom": 16,
       "axis_stretch_horizontal": 1, "axis_stretch_vertical": 1
     }
   }
   ```

3. Restart editor. The `TextureSlotDef.BuildStyleBox()` helper builds a
   complete `StyleBoxTexture` from your spec — corners stay fixed at the
   margin widths, the center tiles to fill the button bounds.
4. Any slot key you omit (or that has no `texture_path`) falls back to the
   procedural `StyleBoxFlat`. So you can ship a 1-texture theme and watch
   the panel/buttons mix and match.

---

## 10. C# extensibility hooks

If you need to extend the theming pipeline from C#, the public hooks are:

- **`SkinCatalog.Reload()`** — clear the cache and re-scan the JSON tree.
- **`GeometryProfile.AsAuthored`** — no-op fallback profile (use the theme's
  own geometry; don't override anything).
- **`GeometryProfile.ByName(name)`** — search every genre's geometry.json for a
  profile by display name; falls back to `AsAuthored`.
- **`GeometryProfile.ForGenre(genre)`** — the genre's default geometry profile.
- **`ShapeOverrides`** (`addons/beep_game_builder_cs/ecs/ui/ShapeOverrides.cs`)
  — per-node-type shape defaults; the dynamic instance lives on
  `GeometryProfile.Shapes` (set when `GeometryDef.ToProfile()` is called).
- **`GeometryDef.ToProfile()`** — convert the loaded data into the runtime
  profile that `ThemePresetComponent` consumes.
- **`TextureSlotDef.BuildStyleBox()`** — convert a JSON slot entry into a live
  `StyleBoxTexture`. Returns null on missing texture.

---

## 11. StyleBoxTexture semantics (the 9-patch mental model)

Godot's `StyleBoxTexture` works exactly like Android's NinePatch:

```
+---+-----------+---+
| 1 |     2     | 3 |    1 = top-left corner   (fixed size)
+---+-----------+---+    2 = top edge          (stretched horizontally)
| 4 |     5     | 6 |    3 = top-right corner  (fixed)
|   |           |   |    4 = left edge         (stretched vertically)
+---+-----------+---+    5 = center            (stretched in both axes if draw_center)
| 7 |     8     | 9 |    6 = right edge
+---+-----------+---+    7 = bottom-left corner
                           8 = bottom edge
                           9 = bottom-right corner
```

The `margin_*` fields tell Godot where the fixed-size corners begin:

- `margin_top = 16` ⇒ the top 16 px of the texture are the top edge zone.
- The corners above (corners 1 + 3) are themselves bounded by both the top
  margin and the left/right margins.

`axis_stretch_horizontal = 1` (Tile) means the center repeats horizontally
instead of stretching — useful for repeating patterns like wood grain or
circuit-board traces. `Stretch` (0) scales one texture copy to fill.

When in doubt, set every margin to 16, the stretch mode to Tile, and you get
a clean 9-patch that scales to any button size.

---

## See also

- **[FILE_FORMATS.md](FILE_FORMATS.md)** — every field, every default, every consumer.
- `addons/beep_game_builder_cs/ecs/ui/SkinCatalog.cs` — loader source.
- `addons/beep_game_builder_cs/ecs/ui/ShapeOverrides.cs` — per-node defaults.
- `addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.cs` — runtime consumer.
- `addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.NodeTheming.cs` —
  per-node-type themers + primitive helpers.
