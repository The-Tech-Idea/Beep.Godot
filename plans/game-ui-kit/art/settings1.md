# `settings1.png` — parchment settings screen

**1078 × 782** · live menu screen · **torn parchment in a wood frame** family
**Relevance:** every genre — this is the folder's only full **settings/options screen**,
and the source of the catalogue's §D form-control findings. This document supersedes §D
with measurements.

---

## Widget 1 — `ArrowSelector` (`Resolution`, `Display`, `Language`)

Scanned H at y=136.

```
   ◄        │  1024 x 640  │        ►
  16px  10px      298px      16px  18px
  gap between arrow and plate = 10–16px
```

| part | measured |
|---|---|
| arrow | **16–18px** wide, `#775C0E`/`#70570D` **L=0.25 S=0.79** — solid dark-gold triangle |
| gap arrow↔plate | **10–16px** |
| value plate | ~298px wide, `#C7B183` **L=0.64 S=0.38** |
| panel behind | parchment L≈0.80 |
| **plate : panel** | **0.80 ×** |

**0.80 again.** That is the third independent family landing on 0.79–0.80 for a gentle
recess (`gameui1` parchment slots, `citybuilder3` item ring, here). It is now the folder's
single most reproduced constant.

The arrows are **fully detached** from the plate — a 10–16px gap on each side. They are not
buttons on the plate's ends; they are separate controls flanking it. That matters for the
kit: `ArrowSelector` is a three-part layout with real gutters, not a spinner.

## Widget 2 — `Slider` (`Music`, `Sound Effects`)

Scanned V at x=700.

| part | measured |
|---|---|
| bar height | **~30px** |
| bar | `#7A6042`–`#795D41` **L=0.31–0.40 S≈0.30** — one leather-strap texture |
| **track / fill split** | **none** — the bar is a single texture end to end |
| knob | a **dark vertical bar** sitting on the strap |

**No fill indication at all.** The knob's position is the entire value display. Compare
`gameui2`'s bright gold track, `gameui1`'s saturation-based fill and `gameui6`'s
groove-and-fill: this family strips the widget to a strap and a marker.

30px matches the folder's ~30px rail height at this screen size.

## Widget 3 — `SegmentedIconGroup` (`Game Controls`, `Control Method`)

Scanned H at y=420.

| part | measured |
|---|---|
| button 1 (keyboard) | x=552..691, `#835F49` **L=0.40 S=0.28** |
| gap | **~26px** (6px dark edge + 20px parchment) |
| button 2 (gamepad) | x=726..864, `#805F48` **L=0.39 S=0.28** |
| glyph | light `#E7D9CF` L≈0.86 line art |

**Measured caveat, stated plainly:** at this scanline the two buttons are the **same
lightness** (0.40 vs 0.39). The first button *looks* lighter in the image, but that appears
to be the keyboard glyph's greater line density filling the plate, not a plate difference.
So either the selected state is carried somewhere I did not scan (a border, a top
highlight), or this pair is not showing a selection at all in this screenshot.

Recorded as **unresolved** rather than guessed. If the kit needs this widget's selected
state, it needs another reference or a second scanline near the button's top edge.

## Widget 4 — `LabelRow`

Right-aligned label, control at the right, consistent baseline across all seven rows.
`Game Controls:` wraps to two lines and **stays right-aligned**, so the wrap does not break
the column.

The label column and control column are both right-anchored to a shared axis — a two-column
form with a single alignment guide. That is the whole layout system on this screen.

## Widget 5 — `TornPanel`

Parchment with **irregular torn edges on all four sides**, set inside a dark brown wood
frame. The tear is the panel's silhouette; the wood frame is a separate rectangle behind it.

**Two nested containers of different shapes** — a rectangular frame holding an irregular
sheet. That is how the family gets a ragged look without losing a predictable layout rect.
Worth copying exactly: `KitPanel` should allow a **decorative inner silhouette** distinct
from its layout rect.

## Widget 6 — `ScreenTitle` (`Settings`)

Large serif display text **above the panel and clipped by the screen's top edge**. No
plate, no banner — the title floats over the frame and runs off-screen.

## Widget 7 — `CloseButton`

A small `X` at the top-right, **outside the panel entirely**, drawn as a thin outlined
square. Minimal and detached — contrast `gameui4`/`rpgui2`, which straddle the corner.

## Widget 8 — `PlainButton` (`Okay`)

Brown plate with a **lighter inner border** and cream text, centred at the panel's bottom.
No badge, no ornament, no icon. The plainest button in the folder.

---

## Corrections to `CATALOGUE-FROM-ART.md` §D

§D concluded from this screen that game UI has **no dropdown, checkbox or radio button**,
and that the theme system was wrong to style them.

That conclusion was correct **about this screen** and wrong as a generalisation —
`gameui2`, `gameui4` and `gameui5` all contain checkboxes, and `gameui2` contains a radio
group. The accurate statement:

> **Arrow selectors and segmented groups are the dominant game pattern for option
> selection. Checkboxes and radio buttons do occur, mostly in wood/cartoon kits. Dropdowns
> appear in none of the 26 references read so far.**

Keep styling checkboxes and radios; do not make them the default; treat a dropdown as a
genuine smell.

---

## Cross-widget rules

1. **Recess = 0.80 ×** — third family to land there; treat as the kit's default.
2. **`ArrowSelector` has real gutters** (10–16px) — arrows are detached controls, not caps.
3. **A slider may show no fill at all** — knob position alone.
4. **A panel's decorative silhouette can differ from its layout rect** (torn sheet inside a
   rectangular frame).
5. **Two-column form with one shared right-alignment axis**, wrapping labels included.
6. **A screen title may float outside and be clipped** — it is scene text, not a header.

## Actions

- [ ] `KitPanel` gains a **decorative silhouette** separate from the layout rect.
- [ ] `ArrowSelector` = arrow · gutter · plate · gutter · arrow, gutters ≈ 0.05 × plate width.
- [ ] Slider gains a `ShowFill: bool` (false = knob-only).
- [ ] Default `RecessRatio` ← **0.80**.
- [ ] Amend `CATALOGUE-FROM-ART.md` §D with the correction above.
- [ ] **Open question:** the selected state of `SegmentedIconGroup` is not visible in this
      capture — needs a second reference before implementing.
