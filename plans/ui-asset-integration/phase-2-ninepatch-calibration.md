# Phase 2 — Nine-patch margin calibration

## Why

A `StyleBoxTexture` stretches the *center* of a texture and keeps the *corners* fixed — the four
`margin_*` values define where the corners end. Get them wrong and buttons either smear their
rounded corners (margins too small) or show almost no stretch (margins too large). Each Kenney pack
has a different corner size, so margins are **per-pack**, set once and reused across every theme that
uses that pack. This is `TextureSlotDef.margin_*` → `StyleBoxTexture.TextureMargin*`
(`docs/FILE_FORMATS.md:255-258`).

## Measured reference (starting values — verify in-editor and adjust)

Kenney UI Pack rectangle buttons are **192×64**; sci-fi glassPanel **100×100**. Corner insets below
are conservative starting points; nudge ±4px while looking at a stretched button in the editor.

| Pack | Element | `margin_*` (L/T/R/B) | `axis_stretch_*` | Notes |
|---|---|---|---|---|
| **UI Pack** | button (flat/gloss/gradient) | 20 / 20 / 20 / 20 | 0 (Stretch) | ~20px rounded corners |
| **UI Pack** | button (**depth** variant, normal state) | 20 / 20 / 20 / **28** | 0 | extra bottom lip → larger bottom margin |
| **UI Pack** | panel | 16 / 16 / 16 / 16 | 0 | |
| **UI Pack** | input / progress bar | 12 / 12 / 12 / 12 | 0 | thinner elements |
| **UI Pack - Sci-fi** | button | 14 / 14 / 14 / 14 | 0 | tighter, angular corners |
| **UI Pack - Sci-fi** | glassPanel / metalPanel | 16 / 16 / 16 / 16 | 0 | |
| **UI Pack - Adventure** | button_brown/grey/red | 18 / 18 / 18 / 18 | 0 | wood bevel |
| **UI Pack - Adventure** | panel | 24 / 24 / 24 / 24 | 0 | thick frame |
| **Fantasy UI Borders** | panel-border-0xx | 28 / 28 / 28 / 28 | 1 (Tile) | ornate; tile keeps detail density; may need 32 |
| **UI Pixel / Pixel Adventure** | button/panel | **integer** 6 / 6 / 6 / 6 | 1 (Tile) | must be integers; NEVER fractional on pixel art |

### axis_stretch guidance
- `0 = Stretch` — smooth packs: the center pixels interpolate, clean for flat/gloss.
- `1 = Tile` — pixel packs and ornate borders: repeats the center so pattern/detail density is
  preserved instead of smeared. Pixel art **must** tile (stretch blurs it even with nearest filter).
- `2 = TileFit` — rarely needed; use if Tile shows a seam.

### content margins
Leave `content_margin_*` at `-1` (inherit → uses the texture margins as padding) unless a button's
text hugs the edge; then set `content_margin_left/right` to ~texture margin + 4 so the label clears
the rounded corner. (`docs/FILE_FORMATS.md:263-266`.)

## The work

1. For each pack, drop one button and one panel into a scratch scene, apply the texture as a
   `StyleBoxTexture`, and resize the host wide + tall. Tune `margin_*` until corners stay crisp at
   both small and large sizes. Record the final numbers here (replace the starting values).
2. Bake the per-pack numbers into the Phase 4 theme.json writes — every theme on the same pack uses
   the same margins, so this table is the single source.

## Gotchas

- **Depth vs flat bottom margin.** The Kenney "depth" buttons (used for the *normal/raised* state)
  have a taller bottom edge than the "flat" buttons (used for *pressed/sunken*). If normal and
  pressed use the same margins, the pressed state looks slightly off — give the depth normal a bigger
  bottom margin (28 vs 20) so its lip isn't stretched.
- **Fractional margins on pixel art** shimmer — always integers, and keep the source at its native
  size (don't pre-scale pixel PNGs).
- Margins are in **texture pixels**, independent of the button's on-screen size — they don't need to
  change per theme, only per pack.

## Verify

- A themed button at 2× and 0.5× its natural size keeps square, crisp corners.
- A pixel-theme button shows sharp, non-blurred pixels with the pattern tiling, not stretching.
