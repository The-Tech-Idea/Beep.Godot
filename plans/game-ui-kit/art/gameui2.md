# `gameui2.png` — wood-framed cartoon GUI sheet

**1200 × 815** · asset sheet (Dreamstime comp, watermarked) · **carved wood** family
**Relevance:** `platformer`, `puzzle`, `citybuilder`, `strategy`. Closest sheet to
`citybuilder5`'s construction, and it independently **confirms the frame formula**.

---

## It confirms the frame formula from a different artist

`citybuilder5` gave **frame ≈ 3.5px + 0.07 × height**. Measured here on the
`LEVEL COMPLETE` panel (H at y=150):

| property | measured | formula predicts |
|---|---|---|
| panel | **234 × ~245px** (x=25..258) | — |
| frame | **19px** (x=25..43, of which 3px is a dark keyline) | 3.5 + 0.07 × 245 = **20.7** |

19 measured vs 20.7 predicted, from a different sheet by a different artist. The formula
holds.

**But the plate shade does not transfer:**

| | frame | plate | ratio |
|---|---|---|---|
| `citybuilder5` capsule | `#CCCBAC` L=0.77 | `#00122C` L=0.09 | **0.12** |
| `gameui2` panel | `#CF9655` L=0.57 | `#9C4C30` L=0.40 | **0.70** |

So **frame *thickness* is a near-universal formula; plate *shade* is per-skin** and spans
at least 0.12 → 0.70. That is exactly the split the kit needs: geometry shared, material
per-genre.

---

## Widget 1 — `FramedPanel` (×9 on the sheet)

`LEVEL COMPLETE`, `OPTIONS`, `INFORMATION`, `WARNING`, `PAUSED`, `UPGRADES`, `SKILLS`,
`MAP`, `LEVEL SELECT` — all one construction.

| layer | measured |
|---|---|
| wood frame | **19px**, `#CF9655`–`#CC8F64` L=0.57–0.60, visible plank grain |
| inner keyline | **3px** `#712C1C` L=0.28 |
| inner plate | `#9C4C30` L=0.40 = **0.70 × frame** |
| corner treatment | plank ends overhang the corners, irregular |

## Widget 2 — `TitleBanner`

Scanned V at x=140.

| property | measured |
|---|---|
| height | **31px** (y=27..57) |
| position | centred on the panel's top edge, **overhanging above it** |
| fill | darker wood `#763F0C` L=0.25 than the frame's 0.57 → **0.44 ×** |
| text | white `#FEF7E8` L=0.95, cap-height ~11px |
| ends | a bolt/rope detail at each end |

The banner is **0.44 × the frame's lightness** — darker, not lighter. A title plate reads
as *recessed into* the frame here, the opposite of a raised badge.

## Widget 3 — `LevelTile` (grid of 15)

Scanned H at y=110.

| property | measured |
|---|---|
| tile | **63px** wide (x=869..932) |
| face | `#BF8650`–`#C78B53` L=0.53–0.55 |
| separator | 5px `#74290E` L=0.24–0.29 = **0.50 × face** |
| number | white, centred, cap-height ~14px |
| rating | **★★★ below the number**, gold earned / grey unearned |
| locked variant | a **padlock replaces the number**, stars all grey |

Rating **inside** the tile, under the number — not a separate row. Locked = padlock +
all-grey stars, and the tile face itself is *not* dimmed.

## Widget 4 — `Slider`

Scanned H at y=92.

| property | measured |
|---|---|
| track | **83px** (x=422..504), `#F5EF3E` L=0.60 **S=0.90** — bright gold |
| knob | a **square wood block** carrying a **◄► double-arrow glyph** |
| groove | none — the track is the bright element |

**Inverted from the usual.** Most sliders draw a dark groove with a bright fill; this one
makes the whole track bright gold and lets the wooden knob be the dark element. Matches
`gameui1`'s finding that a light track is legitimate.

## Widget 5 — `Checkbox` — corrects an earlier catalogue claim

`SUBTITLES ✓`, `TUTORIALS ✓` — a **check glyph in a square inset**.

## Widget 6 — `RadioGroup` — same correction

`ANTI ALIAS: (●) 2x  (○) 4x` — two small **circular** options, one red, one green.

> **Correction.** `CATALOGUE-FROM-ART.md` §D concluded from `settings1.png` that *"no
> reference picture in this folder uses a dropdown, checkbox or radio button"* and that
> the theme system was wrong to style them. This sheet contains **both a checkbox and a
> radio group**. The conclusion was drawn from one settings screen and is wrong as a
> general claim.
>
> The accurate statement: **arrow selectors and segmented groups are the dominant game
> pattern, but checkboxes and radio buttons do occur** in the wood/cartoon family. Keep
> styling them; do not make them the default.

## Widget 7 — `ToggleSwitch`

`ON` label plus a **green segmented bar** — the lit state is drawn as discrete chunks, not
a solid fill. Same segmented idiom as `gameui1`'s ElementChip.

## Widget 8 — `StarRow` (Level Complete)

Three large gold stars, **the centre one noticeably larger**, arranged in a shallow arc
across the panel's upper area. Size, not colour, marks the centre star.

## Widget 9 — `StatRow`

`Time [clock] 00.12.30`, `Score [star] 300` — label at the left, icon, then a **recessed
value field** at the right. The value sits in its own inset plate, the label does not.

## Widget 10 — `IconButtonRow` / `NavBar`

A row of square wood buttons docked to a panel's **bottom edge**, straddling it (grid,
cart, gamepad, play / home, gamepad, upload, cart, play). Buttons are the frame's
lightness, not the plate's.

## Widget 11 — `MenuButtonList` (`PAUSED`)

`RESUME / RESTART / OPTIONS / HELP / EXIT` — full-width **recessed plates** stacked with
even gaps, white centred labels. The button is an inset in the panel, not a raised block.

## Widget 12 — `ActionButtonRow` (`OPTIONS`)

✓ / trash / ✕ in separate wood squares **below and outside** the panel, straddling its
bottom edge. Confirm and cancel are not inside the dialog.

## Widget 13 — `UpgradeRow`

`[icon] ▮▮▮▯ (+)` — an icon, a **segmented meter of 3–4 chunks**, and a **round `+`
button** at the right. Three rows stacked.

## Widget 14 — `ScrollBar`

Vertical gold track with a wood knob carrying **up/down arrow glyphs**. Same inverted
polarity as widget 4 — bright track, dark knob.

## Widget 15 — `PageDots`

Three small circles at a panel's bottom, one lit. The only paging indicator on the sheet
that is not an arrow.

## Widget 16 — `PagerArrow`

Round wood button with a white triangle, placed **flanking the `LEVEL SELECT` title
banner**, overhanging the panel's top-left and top-right corners. Paging controls sit on
the *title*, not the content.

## Widget 17 — `SkillGrid`

3×3 grid of icon tiles inside a panel. Empty/locked slots carry a **✕ on a darker plate**
— absence is drawn explicitly, not left blank.

## Widget 18 — `ShopCard` (×4)

Wood card + crest/weapon icon + a **price plate welded to the bottom edge** (`$200`,
`$300`, `$400`). The fourth card is **dark with a ✕** — the disabled variant is a
different plate colour, not a dimmed copy.

## Widget 19 — `StatusBar` (×3, small)

Compact horizontal meters: an **icon in a round cap at the left**, then a coloured fill
(grey/shield, blue/lightning, pink/heart) in a wood-framed track.

## Widget 20 — `ItemRow` (×5)

Wide wood plates each with an **icon in a round cap at the left** and empty space for a
label. The row and the status bar share the same left-cap construction.

## Widget 21 — `IconSet`

Loose glyphs: `$`, `+`, lightning, skull, gem, cup, star, clock, heart, shield. All flat,
saturated, with a dark keyline and a white inner highlight.

## Widget 22 — `MapPanel`

Wood frame + a **light map image** inside (the only light-plate content on the sheet) +
a vertical scrollbar at the right + a horizontal slider along the bottom frame.

## Widget 23 — `WarningPanel`

Wood frame, dark inner plate, and an `OKAY` button **straddling the panel's bottom edge**
— centred, half in and half out.

## Widget 24 — `InfoField`

`LEVEL [12]  PRICE [1500]` — label + a small recessed value box, inline. The information
panel's header row.

---

## Cross-widget rules

1. **frame = 3.5px + 0.07 × height** — confirmed on a second, unrelated sheet.
2. **plate shade is per-skin**, 0.12 (citybuilder5) to 0.70 (here).
3. **Title banners are darker than the frame** (0.44 ×) — recessed, not raised.
4. **Bright track, dark knob** for both sliders and scrollbars.
5. **Buttons straddle panel edges** — bottom for confirm/cancel, top for paging.
6. **Absence is drawn** — ✕ tiles, grey stars, dark disabled cards.
7. **Checkboxes and radio buttons do exist** in game art; the earlier blanket claim is
   corrected above.
8. **Left round cap holding an icon** is this family's row idiom, used for both meters and
   list rows.

## Actions

- [ ] Correct `CATALOGUE-FROM-ART.md` §D — checkbox/radio are not absent from game art.
- [ ] `PlateShade` must be per-genre with a range of at least 0.12–0.70.
- [ ] Add **slider polarity** (bright track / dark knob) as a skin option.
- [ ] `LevelTile`, `MenuButtonList`, `UpgradeRow`, `ShopCard`, `PageDots`, `InfoField`,
      `StatusBar`, `ItemRow`, `WarningPanel` → catalogue.
- [ ] Record: title banner **0.44 ×** frame, tile separator **0.50 ×** face.
