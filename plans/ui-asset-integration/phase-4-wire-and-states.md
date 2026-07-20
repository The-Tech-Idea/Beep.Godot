# Phase 4 — Wire theme.json slots + state derivation

## Why

With files imported (Phase 1), margins known (Phase 2), and assignments chosen (Phase 3), Phase 4
writes the actual `textures{}` blocks. The subtlety is **button states**: Kenney rarely ships
separate hover/pressed PNGs per button, so we derive the five states from a small number of source
files plus `modulate`.

## State derivation

Godot's themed Button needs up to five StyleBoxes: normal, hover, pressed, disabled, focus. Map them
from Kenney like this:

### UI Pack (has depth + flat + color variants)
| Slot | Source | modulate |
|---|---|---|
| `button_normal` | `button_rectangle_depth_<style>.png` (raised) in the theme color | `#FFFFFFFF` |
| `button_hover` | **same** depth texture | `#FFFFFFFF` lightened ~12% (e.g. `#FFFFFFFF`→brighten, or use the lighter color folder) |
| `button_pressed` | `button_rectangle_<style>.png` (**flat**, no lip → looks pushed in) same color | `#FFFFFFFF` |
| `button_disabled` | Grey `button_rectangle_flat.png` | `#FFFFFF80` (half alpha) |
| `button_focus` | normal texture | accent tint via `modulate`, or the `_border` variant |

### Adventure / Sci-fi / Fantasy (single button per color)
Only one PNG per button color, so derive states purely by `modulate` on the **same** texture:
| Slot | Source | modulate |
|---|---|---|
| `button_normal` | `button_brown.png` (or sci-fi/grey) | `#FFFFFFFF` |
| `button_hover` | same | `#FFFFFFFF` brightened (`#E6F0FFFF`-ish lift) |
| `button_pressed` | same | `#D0D0D0FF` (darken ~18%) |
| `button_disabled` | same | `#FFFFFF66` |
| `button_focus` | same | accent-tinted |

This is why the schema has per-slot `modulate` — one texture, five looks. It also means most themes
need only **1 button PNG + 1 panel PNG** copied, keeping the imported set small.

## The `textures{}` block to write (template)

Per theme, following `docs/FILE_FORMATS.md:230-279` and the Phase 2 margins for that pack:

```jsonc
"textures": {
  "button_normal":   { "texture_path": "res://addons/beep_game_builder_cs/textures/<g>/<t>/button_normal.png",
                       "margin_left":20,"margin_top":20,"margin_right":20,"margin_bottom":28,
                       "axis_stretch_horizontal":0,"axis_stretch_vertical":0,"draw_center":true,"modulate":"#FFFFFFFF" },
  "button_hover":    { "texture_path": ".../button_normal.png", "margin_left":20,"margin_top":20,"margin_right":20,"margin_bottom":28,
                       "modulate":"#FFFFFFFF" },   // same file, lifted; or a real _hover.png if provided
  "button_pressed":  { "texture_path": ".../button_pressed.png", "margin_left":20,"margin_top":20,"margin_right":20,"margin_bottom":20 },
  "button_disabled": { "texture_path": ".../button_disabled.png","margin_left":20,"margin_top":20,"margin_right":20,"margin_bottom":20,"modulate":"#FFFFFF80" },
  "panel":           { "texture_path": ".../panel.png","margin_left":24,"margin_top":24,"margin_right":24,"margin_bottom":24 }
}
```

Extend with `input_normal`/`input_focus`, `progress_bg`/`progress_fill`, `slider_grabber` where a
genre benefits (HUD-heavy genres: shooter/racing/survival get progress bars from Sci-fi/Adventure
bar textures; RPG/topdown get themed input fields).

## Do the 34 already-declared themes first

Those already have a `textures{}` block with the four core slots and margin `16`. For each:
1. Confirm the declared paths match the files Phase 1 placed.
2. Update the `margin_*` to the Phase 2 per-pack values (the current `16` is a placeholder).
3. Add `button_disabled` + a real `button_hover`/`button_pressed` distinction where the pack allows.

Then, if **Decision B = all 50**, add `textures{}` blocks to the 16 themes that lack one.

## C# changes — only if extending the slot set

The core outcome needs **zero C#** (JSON + assets only). C# is touched only if you add slots the
engine doesn't already map. It already maps all 13 slots in `docs/FILE_FORMATS.md:234-246`
(`FileThemePreset.cs:34-47`), so extension is unlikely. If a slot renders but a component ignores it,
that's a `ThemePresetComponent.NodeTheming` wiring gap — fix there, not in the JSON.

## Gotchas

- **Keep it idempotent.** `ThemePresetComponent.ApplyTheme()` is public and re-entrant (enhancement
  Phase 1) — texture styleboxes route through the same `SkinOr()` path, so re-applying a skin from
  the dock must not double-apply. Verified by the meta-guard already in place; don't add per-apply
  `AddChild` in the texture path.
- **Don't hardcode textures into `.tscn`.** Themes drive textures at runtime via
  `ThemePresetComponent`; a texture baked into a scene node bypasses the skin system and the dock's
  "apply skin" won't change it (same reason CLAUDE.md forbids Theme resources over per-node overrides).
- **A missing slot is fine** — omit it and that node stays procedural. Ship `panel`-only themes if a
  pack has a great panel but weak buttons.

## Verify

1. `dotnet build` → 0 errors; `validate_scenes.sh` → PASS (JSON changes don't affect scenes).
2. Dock → each genre → each theme → *Apply* → buttons show the texture; hover lightens, press sinks,
   disabled greys — all from the derivation above.
3. Toggling palette (warm/cool) still tints the textured theme (palettes modulate on top — confirm
   the texture `modulate` composes with the palette HSV shift, doesn't fight it).
