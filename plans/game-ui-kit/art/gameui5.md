# `gameui5.png` — "GAME GUI #7", flat angular paper kit

**626 × 626** · asset sheet · **flat angular / cut-paper** family
**Relevance:** every genre. Same designer as `gameui4`, and it is the **exact inversion**
of that sheet — which makes the pair the clearest statement of what a "skin" is.

---

## The headline: same designer, inverted rule

Scanned H at y=400 (`gameui4`) and y=258 (here), on the same `PLAY / RESUME / RESTART`
button in three colour variants.

| | `gameui4` | `gameui5` |
|---|---|---|
| plate | **white** L=0.98–0.99, identical across variants | **coloured**: maroon `#5A203E` L=0.24, orange `#FA9B03` L=0.50, red `#CD0C0E` L=0.43 |
| text | **palette hue** + dark outline | **white**, no outline |
| keyline | **4–6px near-black** | **none** — background to fill in 1px |
| corners | rounded | **cut / angular** |
| shadow | none | hard offset |

Both sheets apply the palette to exactly **one** element and hold the other neutral. Which
one flips is the skin's decision:

- **`gameui4`**: neutral plate, palette on content → needs a **text outline** for legibility
- **`gameui5`**: palette on plate, neutral content → needs **no outline**, because white on
  a saturated fill is already high contrast

That is a genuinely useful rule for the kit: **whichever element carries the palette, the
other must be near-white or near-black**, and only the content-carrying variant needs an
outline.

---

## Widget 1 — `MenuButton`

| property | measured |
|---|---|
| width | ~46–50px each |
| maroon fill | `#5A203E` L=0.24 S=0.48 |
| orange fill | `#FA9B03` L=0.50 S=0.98 |
| red fill | `#CD0C0E` L=0.43 S=0.89 |
| text | white, no outline |
| keyline | **none** |
| silhouette | rectangle with **angled cut corners** — a rhomboid read |

## Widget 2 — `Panel`

Scanned H at y=150 and y=210 on `PAUSED`.

| property | measured |
|---|---|
| fill | `#FEFDFE` L=0.99 — flat white |
| left edge x | **80 at y=150 and 80 at y=210** → the edge is **vertical** |
| skew source | **not** a rotation — the panels read as tilted because of **diagonal corner cuts** and a skewed header banner |
| keyline | none |
| shadow | hard offset, no blur |

Worth recording precisely, because "rotate the control" is the wrong implementation. The
tilt is produced by **cutting corners at an angle and skewing the attached banner**, while
the body stays axis-aligned. That is cheap to render and keeps layout sane.

## Widget 3 — `HeaderBanner`

Dark maroon **skewed parallelogram** overhanging the panel's top edge, white caps text. The
skew is real here — the banner's top and bottom edges are not parallel to the panel's.

## Widget 4 — `CloseButton`

Red ✕ on a small skewed plate straddling the panel's **top-right corner**. Same placement
as `gameui4`, same reserved red.

## Widget 5 — `IconButton` library

~40 glyphs × **3 colour families** (maroon, orange, red) as **skewed squares**. White
glyphs, no keyline, hard shadow. The glyph set matches `gameui4`'s almost exactly — the
same library re-skinned, which is the clearest possible demonstration that geometry and
palette are separable.

## Widget 6 — `Slider` (top-right, ×6)

A **thin line** track with a **round knob**, in several colours. Notably minimal: the track
is 2–3px, with no groove, no frame and no fill distinction. Compare `gameui2`'s chunky gold
track and wooden knob — the same control, two extremes of weight.

## Widget 7 — `LevelSelectGrid`

Trophy icons with ★ ratings, pager arrows at the sides, and a bottom action row. Tiles are
skewed squares.

## Widget 8 — `ShopRow` / `UpgradeRow`

Item cards each carrying a **price plate at the bottom** (`400`, `950`, `3250`). Same
welded-footer construction as every other sheet in the folder — now seen in seven pictures.

## Widget 9 — `LevelComplete`

Three stars in an arc (centre larger) above a **coin total plate** (`3250`), then a bottom
row of three buttons with the centre one larger — `PrimaryAction = bigger`, third sheet.

## Widget 10 — `AchievementGrid`

Earned icons; unearned drawn as a **grey ✕**. Fourth sheet to draw absence explicitly.

## Widget 11 — `OptionsRow`

`SOUND FX / MUSIC / QUALITY / SUBTITLES` — labels at the left, sliders at the right, and a
checkbox on the last row. **Third sheet with a checkbox.**

## Widget 12 — `IconSet`

Money notes, coins, stars, flags, gems, flames, and a distinctive **drip/melt motif** used
as a decorative edge on several icons. The drip is this kit's equivalent of `gameui1`'s
torn edge — an irregular silhouette used as identity.

## Widget 13 — `PromoCard`

White card with a large play triangle and list lines — a video/featured card. The only
widget on the sheet with no palette colour at all.

---

## Cross-widget rules

1. **The palette goes on the plate or on the content, never both.** The other element is
   near-white or near-black.
2. **Only the content-carrying variant needs a text outline.**
3. **Tilt = corner cuts + a skewed attachment**, not a rotated body.
4. **A slider can be a 2–3px line with a knob** — the control has no minimum weight.
5. **Welded price footers**, **absence drawn as ✕**, and **primary = larger** all recur
   here, confirming them as cross-family patterns rather than one artist's habit.

## Actions

- [ ] Add `PaletteTarget` to the skin: `Plate` | `Content`. Derive the neutral for the
      other element automatically, and enable the text outline only for `Content`.
- [ ] Implement tilt as **corner cuts + skewed attachment**, never a rotated control.
- [ ] Allow a **hairline slider** variant (2–3px track, no frame).
- [ ] Add the **drip/melt** irregular edge alongside `KitShape.Torn`.
- [ ] `PromoCard`, `HeaderBanner` (skewed) → catalogue.
