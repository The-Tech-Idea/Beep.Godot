# `citybuilder3.png` — settlement builder, HUD + info card + build palette

**1024 × 576** · low-poly settlement builder · **muted flat / editorial** family — a THIRD
family, distinct from `citybuilder1` and `citybuilder2`.
**Relevance:** `citybuilder`, `strategy`.

Every widget on screen is measured below.

---

## Family finding, measured first

| | `citybuilder1` | `citybuilder2` | `citybuilder3` |
|---|---|---|---|
| outline | 3–5px near-black | none | **none** |
| shadow | hard offset | 4px bottom edge | **none at all** |
| panel | `#3D4511` L=0.17 opaque | translucent, S=0.15 | **`#EEEEEE` L=0.93 S=0.00 opaque** |
| polarity | light-on-dark | light-on-dark | **dark-on-light** |
| accent | saturated per role | saturated per role | **one tan `#C9AE67`** |

The card body measures **S = 0.00** — pure neutral. Neither of the other families can
produce that: one is saturated, the other is alpha-blended over a coloured world. This
family is opaque, neutral and **inverted in polarity**.

---

## Widget 1 — `StatusBar` (top-left)

Scanned V at x=200, H at y=40.

| property | measured |
|---|---|
| height | **29px** (y=25..53) |
| fill | `#EDEBEA` L=0.92 S=0.08 |
| outline | none — world `#AFBBB5` → fill in 1px |
| day/night medallion | 41px near-black `#1D2206`, **overhanging the left cap** |
| transport glyphs | near-black `#010200`, **bare glyphs, no button plate** |
| progress fill | `#C9B474` L=0.62 S=0.44 — the tan accent |
| progress track | `#DADADA`–`#EDEDEB` L≈0.85 |
| progress length | 88px inside a ~280px capsule |

## Widget 2 — `StatCluster` (top-centre, ×2 pills)

Scanned V at x=330, H at y=40.

| property | measured |
|---|---|
| pill height | **29px** — identical to widget 1, so **one rail height** |
| pill fill | `#EFEEEB` L=0.93 |
| pairs per pill | **4** |
| pitch per pair | **~53px** |
| icon disc | **25px** = **0.86 × pill height** — inset, **not** overhanging |
| disc colours | green `#94A74D`, brown `#876240`, teal `#629C9C`, rose `#C17E7F` |
| disc saturation | **S = 0.19–0.37** — muted, never vivid |
| value | near-black digits, cap-height ~11px |

Four resources in one pill instead of four bars. Colour lives **only** in the 25px disc.

## Widget 3 — `EdgeChip` (right edge, ×2)

Scanned H at y=84.

| property | measured |
|---|---|
| width | **~57px** (x=940..997) |
| fill | `#DFE0DD` L=0.87 — **darker than the top pills' 0.93** |
| icon | teal glyph ~22px at the left |
| value | near-black digit |

A second, dimmer plate tone for secondary information. The family has **two plate
lightnesses**: 0.93 primary, 0.87 secondary.

## Widget 4 — `InfoCard` (left panel)

Scanned H at y=430, V at x=100.

| property | measured |
|---|---|
| width | **148px = 14.5 % of screen width** (x=29..176) |
| height | **~350px = 61 % of screen height** (y≈196..543) |
| body fill | `#EEEEEE` **S=0.00** |
| border / shadow | **neither** — 1px antialias straight to the world |
| left margin | 29px |

**Seven stacked regions, top to bottom:**

| # | region | measured |
|---|---|---|
| 1 | portrait medallion | ⌀~50px, sitting **above the card's top edge** |
| 2 | category band | tan `#C9AE67` L=0.60 S=0.48, ~60px tall |
| 3 | title | dark caps, centred |
| 4 | hairline rule | 1px |
| 5 | description | centred grey body text |
| 6 | effect row | widget 5 |
| 7 | footer band | tan `#D8CAA1` L=0.74, **38px** tall (y=506..543) |

Regions are separated by **band colour and letterspacing**, never by borders.

## Widget 5 — `EffectArrow` (`-1 → +6`)

Scanned H at y=402.

| property | measured |
|---|---|
| disc diameter | **32px** each |
| disc fill | blue `#7EABD6`, purple `#A780D7` — both L=0.67 **S=0.52** |
| glyph | near-black inside the disc |
| arrow | dark grey `#3E3E3E` L=0.24, **13px** wide, centred between |
| values | `-1` / `+6` beneath each disc |

**The effect discs are more saturated (S=0.52) and larger (32px) than the stat-cluster
discs (S≤0.37, 25px).** Importance is expressed as *size + saturation*, with hue held to
the same muted register.

## Widget 6 — `RatingRow`

Icon + ★★ beneath it, the stars tinted to that icon's hue (yellow, teal). Two entries side
by side. Confirms the catalogue's StarRating; new detail is that **the stars take the
icon's hue rather than a fixed gold**.

## Widget 7 — `LetterspacedLabel` (`LIFESTYLE`, `COST:`)

Small caps, wide tracking, tan. Used as a region header **in place of a divider line**.
`KitGeometry` has no letter-spacing; this family needs it.

## Widget 8 — `PalettePanel` (bottom-centre)

Scanned V at x=470.

| property | measured |
|---|---|
| panel top edge | y=471 |
| fill | `#EEEEEB` L=0.93 |
| item row | horizontally scrolling — **items clipped at both ends**, proving a viewport |

## Widget 9 — `TabStrip` (on the palette's top edge)

| property | measured |
|---|---|
| band height | **23px** (y=448..470) |
| position | entirely **above** the panel, touching its top edge |
| selected | dark `#565656` plate, **white** glyph |
| unselected | cream plate matching the panel, **dark** glyph |
| tab count | 7 |

**Selection is a full value inversion**, not a tint or a border — the strongest
greyscale-safe selection signal in the folder, and free.

## Widget 10 — `ItemCircle` (palette entries)

Scanned V at x=348.

| property | measured |
|---|---|
| diameter | **43px** (y=488..530) |
| ring | 1–2px, `#C3C3B5` L=0.74 = **0.79 × the panel** |
| content | isometric building art, no plate behind it |

Same 0.79–0.80 recess ratio seen in `gameui1`'s parchment slots. Two unrelated families
land on the same number for "a subtle inset".

## Widget 11 — `CloseButton` (palette top-right)

Scanned H at y=469.

| property | measured |
|---|---|
| diameter | **21px** (x=704..725) |
| fill | light `#F6F3F1` L=0.95 |
| glyph | near-black `#08080A`, stroke **3px** |
| position | **inside** the panel corner, not straddling it |

Contrast `store.png`/`ui2.png`, where the close button straddles the frame. Placement is a
per-skin choice.

## Widget 12 — `PortraitMedallion`

⌀~50px circle of character art crossing the card's top edge. Overhang appears in all three
citybuilder families so far — it is the most universal device in the folder.

---

## Cross-widget rules

1. **One rail height, 29px** — status bar and both stat clusters.
2. **Two plate lightnesses**: 0.93 primary, 0.87 secondary.
3. **Colour is quarantined into discs** of 25–32px. Chrome is always neutral.
4. **Importance = size + saturation**, never hue change (25px/S≤0.37 vs 32px/S=0.52).
5. **Selection = value inversion.**
6. **Subtle inset = 0.79–0.80 × the host** — matches `gameui1` independently.
7. **Type does the dividing** — letterspaced caps and band fills instead of rules.
8. **Nothing has an outline or a shadow.** Every `KitMaterial` layer must be switchable
   off, or this skin is inexpressible.

## Actions

- [ ] Add a **polarity** flag to the skin (dark-on-light vs light-on-dark).
- [ ] All `KitMaterial` layers default-off-able; prove with this skin.
- [ ] `KitState.Selected` gains a **value inversion** renderer.
- [ ] Add **letter-spacing** to `KitGeometry`.
- [ ] Two plate tones per skin (primary/secondary), measured 0.93 / 0.87.
- [ ] `StatCluster`, `EdgeChip`, `EffectArrow`, `ItemCircle`, `LetterspacedLabel` → catalogue.
