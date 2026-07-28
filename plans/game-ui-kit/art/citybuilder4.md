# `citybuilder4.png` — tribal builder, build drawer over the world

**1024 × 768** · isometric tribal builder (Tribez-like) · **soft dark drawer** family
**Relevance:** `citybuilder`; first reference with a **build drawer** and a
**locked-with-reason** state.

Every widget on screen is measured below.

---

## Widget 1 — `Drawer` (the container)

Scanned H at y=200.

| property | measured |
|---|---|
| total width | **340px = 33 % of screen width** |
| fill | `#4C547D` L=0.39 S=0.24 |
| opacity | **opaque** — 64px of uniform `#4C537B` with a bright green world behind |
| card column | 275px (x=65..339) |
| inner divider | 1–3px lighter `#6B85B6` at x=62–64 |

Third plate treatment in four citybuilder images: opaque dark cartoon, translucent dark
flat, opaque near-white flat, and now opaque dark soft.

## Widget 2 — `DrawerHeader` (`Residential`)

Scanned V at x=200.

| property | measured |
|---|---|
| height | **49px** (y=0..48) |
| fill | `#393D5D` L=0.29 = **0.74 × the drawer body** |
| title | white, centred, cap-height ~11px |

## Widget 3 — `CategoryRail` (left column, 7 items)

Scanned H at y=200, V at x=30.

| property | measured |
|---|---|
| rail width | **62px** (x=0..61) |
| button height | **~97px** (y=58..155 for the first) |
| button plate | `#454B6C` L=0.35 = **0.90 × the drawer** — the buttons are *recessed*, not raised |
| icon | pale near-white, **31px wide × 56px tall** |
| icon aspect | tall — building silhouettes, not square glyphs |

## Widget 4 — `BuildingCard`

```
   ┌──────────────────────────────┐
   │          Bungalow            │  centred title
   │  0m 10s                      │  build time, small, left
   │  ────────────                │  hairline
   │  🪵  0            ╭────────╮ │  cost rows, left column
   │  💎  30           │  art   │ │  artwork on a grass plinth
   │                   ╰─────(◉)╯ │  output badge OVERHANGS the art corner
   └──────────────────────────────┘
```

| property | measured |
|---|---|
| card fill | `#4C547D` — **identical to the drawer**; only the border separates them |
| top highlight | 1–2px `#5C6285` L=0.44 = **1.13 × the fill** |
| gap between cards | ~20px `#404466` L=0.33 = **0.85 × the fill** |
| cost value colour | white for one resource, **magenta `#F573F7`** for the premium one |

**Separation costs 1px at 1.13× plus a gap at 0.85×.** No border, no shadow, and it
survives greyscale.

## Widget 5 — `LockedState` — measured, and it corrects my first reading

Scanned V at x=100 and H at y=435.

| zone | measured |
|---|---|
| normal card | `#4C547D` L=0.39 **S=0.24** |
| locked card, title zone | `#3D4460` L=0.31 S=0.22 → **0.79 × lightness**, hue kept |
| locked card, plate zone | `#403B3F` L=0.24 **S=0.04** → darker **and desaturated** |
| reason text | white `#FFFEFF`, cap-height ~13px |

**Correction to my first pass:** I described the reason plate as a *light plate with dark
text* — a polarity flip. The scanline says otherwise. It is a **neutral dark plate**
(S=0.04 against the drawer's S=0.22) carrying **white** text. The lock is a
**desaturation**, not an inversion.

So the measured lock recipe is:

> **dim to 0.79 × lightness, drain saturation to ~0.04, overlay a white reason label**

Still better than a bare padlock, because it *states the reason* — but it is the classic
desaturation lock the catalogue already recorded, not a new polarity trick.

## Widget 6 — `OutputBadge`

Small ring holding a resource icon, placed on the artwork's bottom-right corner and
**straddling it**. Overhang again — fourth family in a row.

## Widget 7 — `CostRow`

Small resource icon + value, stacked vertically, left-aligned in the card's left column.
Confirms `StatList`. The value colour, not a badge, marks the premium resource.

## Widget 8 — `WorldBadge` (floating status discs)

Scanned H at y=262, V at x=488 on the yellow "ready" badge.

| property | measured |
|---|---|
| diameter | **43px** — a true circle (H 466..508, V 249..291) |
| body | `#FCDF05` L=0.50 **S=0.98** |
| glyph | `#FEF8C7` L=**0.89 S=0.96** — a **lightened tint of the body hue** |
| glyph size | ~19px = **0.44 × diameter** |
| bottom shading | last ~8px shift to `#FBBF07` — warmer, slightly darker |
| outline | **none** — grass to yellow in a 2px blend |

Hues seen: yellow (ready), green (food), orange (resource), grey (`Zzz`, asleep). **Hue is
the entire message; the shape never changes.**

**Compare `citybuilder1`'s round button**, whose glyph measured **S=0.00–0.05** — a
desaturated grey. Here the glyph is a tint at **S=0.96**. Same construction, opposite
glyph rule:

- **tinted glyph** → reads as glowing, diegetic, part of the world
- **grey glyph** → reads as an interface control sitting on the world

## Widget 9 — `BuildSpot`

Green up-arrow on a rounded-square plate, one per upgradeable tile, in world space.
Confirms `citybuilder2`'s BuildMarker at a different scale and skin.

---

## Cross-widget rules

1. **Locked = 0.79 × lightness + saturation to ~0.04 + a white reason label.**
2. **Cards need no tint or border** — 1px at 1.13× plus a 0.85× gap is enough.
3. **Glyph treatment inside a coloured disc is a per-skin rule**, tinted or grey.
4. **The container header is 0.74 × the body**, and the rail buttons **0.90 ×** — this
   family builds hierarchy entirely from lightness ratios of one hue.
5. **Four citybuilder images, four plate treatments.** Genre does not determine skin;
   `SkinCatalog` is right to keep genre, theme and palette separate.

## Actions

- [ ] `KitState.Locked` ← dim 0.79 + desaturate to 0.04 + reason label slot.
- [ ] Add `GlyphTint` to `KitMaterial`: `Grey` | `TintOfBody`.
- [ ] Add a **container lightness ladder** to the skin: header 0.74, recessed 0.90,
      highlight 1.13, gap 0.85 — all relative to the body.
- [ ] `BuildingCard`, `DrawerHeader`, `WorldBadge`, `CategoryRail`, `OutputBadge` → catalogue.
