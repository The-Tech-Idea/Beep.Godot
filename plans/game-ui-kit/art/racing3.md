# `racing3.png` — car tuning / garage screen

**734 × 413** · live menu screen (Chinese racing title) · **light glass on a bright
scene** family
**Relevance:** **`racing`**, and the **radar chart** is directly useful to `rpg` and
`strategy` too.

---

## Correction to my first read

I first described this as "dark glassmorphism". The scanlines say otherwise: the left
category panel and its tiles measure **L = 0.90–0.95** — they are **near-white translucent
glass**, not dark. Only the top bar and the bottom carousel are dark.

So this screen is a **mixed-polarity layout**: light panels over a bright showroom, dark
chrome at the top and bottom edges. That is a real pattern and not a mistake — the dark
strips frame the screen, the light panels sit on the bright subject.

---

## Widget 1 — `CategoryGrid` (left panel)

Scanned H at y=110, V at x=70.

| property | measured |
|---|---|
| unselected tile | `#F1F3F2` **L=0.95** S=0.08 — near-white |
| **selected tile** | `#FDC267` **L=0.70 S=0.97** — saturated orange |
| glyph | **dark in both states** |
| tile height | **43–45px** |
| row pitch | **~53px** → gutter ≈ 8–10px |
| grid | 4 columns × 3 rows |

**Selection mechanism #10: fill the tile with the accent, keep the glyph dark.** Note the
selected tile is *darker* (0.70) than the unselected (0.95) — selection here **reduces**
lightness while adding saturation. Every other reference that fills on selection raises
lightness.

This is only legible because the glyph stays dark in both states — the contrast direction
never flips, so the icon never needs re-drawing.

## Widget 2 — `PanelHeader`

A row above the grid holding a small icon and a label (`改装选项`), on the same near-white
glass. No divider line — the header is separated by spacing alone.

## Widget 3 — `PartInfoCard` (bottom-left)

| region | observed |
|---|---|
| header | small icon + part name (`Geed-FB03`), left; **price chip** right (wrench icon + `50000 + 2000`) |
| body | 3–4 lines of description text |
| action | a **full-width primary button** (`选择`) at the bottom, with a light border |

The price is expressed as **`base + surcharge`** rather than a single figure — worth noting
for any shop widget: the value slot may need two parts.

## Widget 4 — `RadarChart` (right) — NEW, and the most valuable widget here

| part | observed |
|---|---|
| shape | **pentagon** spider chart, 5 axes |
| axis labels | outside each vertex (torque, RPM, weight, grip, braking) |
| grid | concentric pentagon rings |
| data | a **filled translucent yellow polygon** |
| legend | two dots beneath — **before / after** (`改装前` / `改装后`) |

This is the first radar chart in the folder. It solves a problem the kit has no answer
for: **comparing many stats at once, and showing a before/after delta on the same figure.**

Directly useful beyond racing — an RPG equipment comparison and a strategy unit comparison
are the same widget.

Construction is entirely derivable: N axes at equal angles, M grid rings, one polygon per
series, labels at the vertices. No art needed, which makes it a good early kit widget.

## Widget 5 — `ItemCarousel` (bottom)

| property | observed |
|---|---|
| cards | 4 visible, dark rounded rects holding a rendered part |
| **selection** | a **light border** around the active card |
| badge | one card carries an **orange badge at its top-left corner** (ownership/equipped) |
| pagers | `‹` and `›` chevrons at each end; the right one is **larger and highlighted** |

**Two different selection mechanisms on one screen**: the grid fills with accent (widget 1),
the carousel draws a border (here). Split by widget type, not by skin.

That matters: `KitState.Selected` cannot be one global renderer. It needs a per-widget-class
default that a skin may override.

## Widget 6 — `TopBar`

Thin **dark** strip spanning the full width: a circular icon button at the far left, a title
chip, a currency readout at the right (icon + `1,238,680`), and a **red circular ✕** at the
far right.

Red reserved for close — fourth sheet to do so.

## Widget 7 — `RoundIconButton` pair (right)

Two round dark buttons (car view, list view) stacked at the right edge — view toggles,
placed away from the content they affect.

## Widget 8 — `Disclaimer`

Small grey text at the bottom-left. Recorded for completeness: a legal/footnote slot is a
real requirement in shipped games and the kit has no low-emphasis text role.

---

## Cross-widget rules

1. **Mixed polarity is legitimate** — light glass panels with dark chrome strips at the
   screen edges.
2. **Selection #10: fill with accent, keep the glyph dark.** Selection may *darken* the
   tile as long as the glyph's contrast direction is unchanged.
3. **`KitState.Selected` must default per widget class** — grids fill, carousels border.
4. **A price may be a two-part value** (`base + surcharge`).
5. **The radar chart is a missing primitive** and is fully procedural.
6. **A low-emphasis text role is needed** for disclaimers and footnotes.

## Actions

- [ ] Add `RadarChart` to the kit — N axes, M rings, multiple series, vertex labels.
      Procedural, no art dependency; useful for `racing`, `rpg`, `strategy`.
- [ ] `KitState.Selected` ← per-widget-class default renderer, skin-overridable.
- [ ] Add a **low-emphasis text role** to `UiSurface`.
- [ ] Support a **two-part value** in shop/price widgets.
- [ ] `ItemCarousel` (border selection + corner badge + asymmetric pagers) → catalogue.
