# Phase 3 — Genre × theme → Kenney pack mapping

## Why

This is the heart of the initiative: **which Kenney pack + color + button style each of the 50
themes uses.** It drives Phase 1 (which files to copy) and Phase 4 (what to write into each
theme.json). The aesthetic goal is that a theme's *name* matches its texture — `royal` looks royal,
`neon` looks neon, `parchment` looks like paper.

Legend for **Button style** (UI Pack): the style suffix on `button_rectangle_<style>` — `flat`,
`gloss`, `gradient`, `border`, `line`, and the `depth_` prefix for a raised look.

## The full map (50 themes)

### cardgame — ornate / luxurious
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| arcane | Fantasy UI Borders + UI Pack | border 0xx (purple-ish) / Blue | depth_gloss |
| casino | UI Pack | Red + Green | depth_gloss |
| paper | UI Pack - Adventure | button_grey / parchment panel | flat |
| royal | Fantasy UI Borders | ornate gold border | (panel-driven) depth_gradient |
| velvet | Fantasy UI Borders + UI Pack | border 0xx / Red | depth_gloss |

### citybuilder — clean / civic
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| blueprint | UI Pack | Blue | line |
| eco | UI Pack | Green | flat |
| future | UI Pack - Sci-fi | blue glass/metal | (sci-fi button) |
| industrial | UI Pack - Adventure | button_grey | flat |
| urban | UI Pack | Grey | depth_gloss |

### platformer — playful
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| cartoon | UI Pack | Green + Yellow | depth_gloss |
| modern | UI Pack | Grey/Blue | flat |
| nature | UI Pack - Adventure | button_brown | flat |
| pixel8bit | UI Pack - Pixel Adventure | pixel tiles | (pixel, nearest+tile) |
| retro80s | UI Pixel Pack | pixel sheet / neon | (pixel) |

### puzzle — bright / friendly
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| candy | UI Pack | Red + Yellow | depth_gloss |
| cartoon | UI Pack | Green | depth_gloss |
| japan | UI Pack - Adventure | button_red / paper panel | flat |
| modern | UI Pack | Grey | flat |
| sea | UI Pack | Blue | depth_gloss |

### racing — sleek / tech
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| arcade | UI Pack - Sci-fi | yellow/green bars | (sci-fi) |
| carbon | UI Pack - Sci-fi | dark metal | (sci-fi) |
| motorsport | UI Pack | Red | line |
| neon | UI Pack - Sci-fi | blue/green glow | (sci-fi) |
| street | UI Pack | Grey | flat |

### rpg — fantasy
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| arcane | Fantasy UI Borders + UI Pack - Adventure | border + button_grey | flat |
| darkfantasy | Fantasy UI Borders | dark ornate border | (panel-driven) |
| fantasy | UI Pack - Adventure | button_brown + wood panel | flat |
| parchment | UI Pack - Adventure | button_grey / parchment | flat |
| royal | Fantasy UI Borders | gold ornate border | (panel-driven) |

### shooter — sci-fi / military
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| cyberpunk | UI Pack - Sci-fi | neon glass | (sci-fi) |
| military | UI Pack - Adventure | button_grey | flat |
| scifi | UI Pack - Sci-fi | metalPanel blue | (sci-fi) |
| space | UI Pack - Sci-fi | dark metal | (sci-fi) |
| toxic | UI Pack - Sci-fi | green bars | (sci-fi) |

### strategy — command
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| blueprint | UI Pack | Blue | line |
| command | UI Pack - Sci-fi | metalPanel | (sci-fi) |
| military | UI Pack - Adventure | button_grey | flat |
| royal | Fantasy UI Borders | ornate border | (panel-driven) |
| scifi | UI Pack - Sci-fi | blue glass | (sci-fi) |

### survival — rugged
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| apocalypse | UI Pack - Adventure | button_grey (weathered) | flat |
| desert | UI Pack - Adventure | button_brown | flat |
| frozen | UI Pack | Blue | flat |
| industrial | UI Pack - Adventure | button_grey | flat |
| wilderness | UI Pack - Adventure | button_brown + wood panel | flat |

### topdown — adventure
| Theme | Pack | Color / element | Button style |
|---|---|---|---|
| classic | UI Pack | Grey | depth_flat |
| fantasy | UI Pack - Adventure | button_brown | flat |
| japan | UI Pack - Adventure | button_red | flat |
| military | UI Pack - Adventure | button_grey | flat |
| nature | UI Pack - Adventure | button_brown + wood panel | flat |

## Notes

- **Pixel themes** (`platformer/pixel8bit`, `platformer/retro80s`) depend on **Decision D** — if
  pixel-textured, they use the Pixel packs with nearest filter + tile stretch (Phase 2). If not,
  leave them procedural (they still look intentionally 8-bit via the flat color box).
- **"(panel-driven)"** themes lean on an ornate Fantasy-border **panel** as the dominant look; their
  buttons can reuse a neutral UI Pack button so the border is the star.
- **Color availability:** UI Pack ships Blue/Green/Grey/Red/Yellow only. A theme wanting orange/purple
  uses the nearest color + the theme's existing `modulate`/palette HSV shift (palettes already tint
  at runtime — `PaletteTintedPreset`), so we don't need a PNG per hue.
- The 16 themes whose `theme.json` has **no `textures{}` block yet** (per the audit: several
  platformer/puzzle/topdown themes) are covered only if **Decision B = all 50**; otherwise they stay
  procedural and this table's rows for them are aspirational.

## Verify

Eyeball test per genre: the themed main menu "reads" as the theme name — parchment looks like paper,
neon glows, royal is ornate. If a theme looks generic, it's using the wrong pack/color — fix here
before Phase 4 bakes it in.
