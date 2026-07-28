# `citybuilder5.png` — farm/city builder, full ornate HUD

**1000 × 750** · dense Russian-market casual builder · **carved stone-and-wood** family
**Relevance:** `citybuilder`, `strategy`. The reference **closest to what `KitControl`
already tries to render** — frame + inner plate + content — so its numbers are the most
directly usable in the folder.

Every widget on screen is measured below.

---

## The headline finding: the frame is NOT a constant ratio

| control | height | frame | ratio |
|---|---|---|---|
| resource capsule (gem) | **35px** (y=27..61) | **6px** | 0.17 |
| action tile (`ОТМЕНИТЬ`) | **107px** (y=500..607) | **11px** | 0.10 |

A single ratio cannot produce both. Linear fit:

> **frame ≈ 3.5px + 0.07 × height**

The frame has a **floor of ~3.5px**. This is why `KitGeometry.FrameRatio = 0.10` looked
right on a big button and vanished on a chip: at 30px tall it yields 3px, under the floor,
and reads as a hairline border instead of carving.

---

## Widget 1 — `ActionTile` (bottom command grid, 10 tiles)

Scanned H at y=515 and y=545, V at x=255.

**Edge stack, outer → inner:**

```
   world │ rim  │ bezel │ shadow │      plate
         │ 2px  │  4px  │  5px   │
  #A7C62B│#EBFABF│#90995B│#636D37 │    #75864F
   L=0.47│L=0.86 │L=0.48 │ L=0.32 │    L=0.42
```

| layer | measured | ratio to plate |
|---|---|---|
| outer bright rim | 1–2px `#EBFABF` L=0.86 | **2.05 ×** |
| stone bezel | 4px `#90995B` L=0.48 | 1.14 × |
| inner dark shadow | 5px `#636D37` L=0.32 | **0.76 ×** |
| plate | `#75864F` L=0.42 | 1.00 |

| property | measured |
|---|---|
| size | ~98 × **107px** — near-square |
| icon | large, centred in the upper ~60 % |
| caption | white with a **dark outline stroke**, lower ~25 % |
| optional cost pill | **above** the icon (`40` + gem) |
| optional two-line label | `Осталось:` / `21:48` |

**Selected tile** (`ЗАВЕРШИТЬ`): plate shifts olive `#75864F` → cyan `#5AAA95`, plus a
**bright blue outer glow** (`#337CFF`, `#8FC9FF` at x=368–396). Selection = **hue shift +
external glow**, not a border.

## Widget 2 — `StoneCapsule` (resource readouts, ×6)

Scanned V at x=730 (gem).

| layer | measured |
|---|---|
| capsule height | **35px** (y=27..61) |
| stone frame | **7px top, 5px bottom**, `#CCCBAC`–`#DED3C1` L=0.74–0.81 |
| inner plate | `#00122C` **L=0.09** |
| gloss band | ~8px of teal `#77C7DD` L=0.67 at the top of the plate |
| **plate : frame lightness** | **0.09 / 0.77 = 0.12** |

`KitGeometry.PlateShade = 0.88` is far too timid — measured here it is **0.12**. The inner
plate is nearly black against a pale frame.

## Widget 3 — `StackedMeter` (XP + coin, top-left)

Scanned V at x=180.

| property | measured |
|---|---|
| bar 1 (XP) | y=48..79 → **32px** |
| bar 2 (coin) | y=80..108 → **29px** |
| frame | 3–4px light stone `#E0D6C2` L=0.82 at the top of each |
| fill 1 | orange/brown, `#F9C981` highlight |
| fill 2 | teal `#6ECFC6` |
| value | white with a dark outline, centred |

Two bars, ~30px each — the same rail height as `citybuilder1` (31px) and `citybuilder3`
(29px). **Three unrelated skins converge on ~30px for a HUD bar at 1000-ish px wide.**

## Widget 4 — `StarLevelBadge` (`35`)

Scanned H at y=45.

| property | measured |
|---|---|
| width | **71px** (x=19..90) |
| bright facet | `#F6D345` L=0.62 S=0.91 |
| dark facet | `#CD6C0A` L=0.42 S=0.92 = **0.68 × bright** |
| outline | dark keyline |
| position | **overhanging the meter stack's left end**, and the screen's top-left corner |

A **star silhouette used as a button/badge shape** — the first non-round, non-rect level
badge in the folder. Two facet tones at a 0.68 ratio give it its faceted read.

## Widget 5 — `HexStoneButton` (left edge, ×2)

Scanned H at y=182.

| property | measured |
|---|---|
| stone frame | **28px** at the left — 0.31 of the ~90px width |
| stone tone | `#D4CEC7` L=0.81 S=0.13 |
| inner plate | dark green `#3F602C` L=0.27 |
| glyph | brown/tan tool art, not a flat icon |

The "frame" here **is** the silhouette — an irregular rock. Proves button shapes beyond
rounded rectangles, alongside widget 4's star.

## Widget 6 — `ConfirmPair` (world, ✓ / ✗)

Scanned H at y=258.

| property | measured |
|---|---|
| button width | **~45px** each |
| gap | **7px** |
| green body | `#A3EC17` L=0.51 S=0.85 |
| green glyph | `#369C17` L=0.35 = **0.69 × body** |
| orange body | `#FEA321` L=0.56 S=0.99 |
| orange glyph | `#C8591A` L=0.44 = **0.79 × body** |

**A third glyph rule.** The glyph is a *darker* tint of the body hue. Across the folder so
far:

| skin | glyph treatment | measured |
|---|---|---|
| `citybuilder1` | desaturated grey | S=0.00–0.05 |
| `citybuilder4` world badge | **lighter** tint | L 0.89 vs body 0.50 |
| `citybuilder5` confirm | **darker** tint | L 0.35 vs body 0.51 |

Three options, all in the same folder. `GlyphTint` needs three values, not two.

## Widget 7 — `PromoRibbon` (`АКЦИЯ` / `4 дн. 3:45ч.`)

Scanned H at y=465.

| property | measured |
|---|---|
| width | **154px** (x=845..998) |
| body | `#FEB911` L=0.53 **S=0.99** |
| fold/shadow | `#CF5600` L=0.41 S=1.00 = **0.77 × body** |
| shape | banner with a **notched swallowtail** end |
| content | two lines — a title and a countdown |

Fully saturated orange, used nowhere else. Time-limited offers get their own hue, matching
`citybuilder2`'s "orange = time" finding.

## Widget 8 — `RadarPanel` (bottom-left)

Scanned V at x=85.

| property | measured |
|---|---|
| display | y=623..688 → **66px** tall, a green gradient `#BEC49E` L=0.69 → `#BBD224` L=0.48 |
| frame above | ~23px of grey/brown stone |
| caption | white `РАДАР` with a dark outline, **below** the display, inside the frame |

A **round display inset into a square frame** with the caption in the frame's lower band.

## Widget 9 — `BadgeGrid` (2×2 small buttons)

Four small rounded-square buttons (chart, trophy, mail, stars). Two carry **red count
badges straddling their top-right corners** (`i`, `4`). Confirms `NotificationDot` as a
corner attachment, here on a square host.

## Widget 10 — `ShopButton` (bottom-right)

A button that **is an illustration** — a striped red/white awning over a shop front — with
`МАГАЗИН` captioned beneath. Not an icon in a plate: the whole control is themed art.
Recorded because it sets an upper bound on what "button" means in this family.

## Widget 11 — `WorldArrows`

Four chunky yellow arrows arranged around the selected building (left, right, up-left,
down-right), in world space. Move/placement affordance. Same saturated yellow as the
`citybuilder4` ready badge.

## Widget 12 — `TagBubble`

Resource icon in a rounded bubble with a **downward tail**, floating over a building.
Confirms the catalogue's `CountBubble` tail on a different skin.

## Widget 13 — `PlayerNamePlate`

`АНАСТАСИЯУПАКЫ` — white text with a **dark outline stroke** and no plate, over the world.
Same plateless treatment as `citybuilder2`, but achieved with an outline rather than a
shadow.

---

## Cross-widget rules

1. **Frame = 3.5px + 0.07 × height** — an absolute floor plus a slope.
2. **Inner plate : frame lightness = 0.12** — nearly black, not "slightly darker".
3. **Outer rim = 2.05 × plate.** Carving reads through the bright rim, not the shadow.
4. **Rail height ≈ 30px** — third independent skin to land there.
5. **Glyph tint has three modes**: grey, lighter, darker.
6. **Silhouettes include stars, hexagons, irregular rocks and swallowtails.**
7. **Text carries its own dark outline stroke**, which is what lets a plateless label sit
   on a saturated world.
8. **Selection is per-skin** — five images, four mechanisms (none / invert / lighten+border
   / hue+glow).

## Actions

- [ ] Replace `KitGeometry.FrameRatio` with `FrameMin = 3.5px` + `FrameSlope = 0.07`.
- [ ] `PlateShade` default 0.88 → **0.12** for this family (per-genre).
- [ ] Add `RimBrightness ≈ 2.0 ×` plate to `KitMaterial`.
- [ ] Add a **text outline stroke** to the kit's label drawing.
- [ ] `GlyphTint` ← `Grey` | `Lighter` | `Darker`.
- [ ] `KitState.Selected` becomes a per-skin renderer.
- [ ] `StarLevelBadge`, `StackedMeter`, `HexStoneButton`, `RadarPanel`, `PromoRibbon`,
      `ShopButton`, `WorldArrows`, `ConfirmPair`, `BadgeGrid` → catalogue.
