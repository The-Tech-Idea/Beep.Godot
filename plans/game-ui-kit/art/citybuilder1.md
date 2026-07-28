# `citybuilder1.png` — village builder, in-game HUD

**800 × 600** · isometric village builder ("Варвары") · **casual cartoon, dark keyline**
family
**Relevance:** `citybuilder` — first dedicated citybuilder reference.

Live gameplay screen, so it shows both the parts **and** where they sit. Every widget on
the screen is measured below; nothing here is described without a scanline behind it.

---

## Widget 1 — `ChromeStrip` (top bar)

Scanned V at x=680.

| property | measured |
|---|---|
| extent | y=0..50 → **50px tall**, full 800px width |
| fill | vertical gradient: `#FCFFFF` L=0.99 at the top → `#A699AB` L=0.64 at the bottom |
| saturation | S=0.00–0.10 — **neutral mauve-grey**, the only unsaturated surface on screen |
| bottom edge | 1–2px darker, no keyline |
| content | logo at the left (overhanging), two buttons docked right |

The strip is deliberately desaturated so the saturated world beneath never competes with
it. It is **not** a panel — it is a full-bleed band with no corners and no margins.

## Widget 2 — `ChromeButton` (`Играть`, `Справка`)

Scanned H at y=33.

| property | measured |
|---|---|
| size | **84 × 28px** (x=633..716, y=22..49) |
| fill | `#493730` L=0.24 S=0.21 — dark brown |
| text | gold `#FFCF3A` L=0.61 **S=1.00** |
| gap between buttons | **30px** |
| alignment | bottom-aligned to the strip, overhanging its lower edge by ~0px |
| corner | small radius, ~4px |

**height : strip height = 28 : 50 = 0.56.** The button occupies just over half the chrome
band, which is what leaves room for the strip to read as a surface rather than a toolbar.

## Widget 3 — `LogoBanner`

Ornate plate at the top-left carrying the game title, **overhanging the chrome strip both
above and below** — it breaks the strip's top edge (y<0 clipped) and its bottom. Not
measured precisely: it is art, not a reusable control. Recorded because the *overhang* is
the reusable part.

## Widget 4 — `CurrencyBar` (gem, `9`)

Scanned H at y=90 and y=79, V at x=130 / x=44 / x=152.

```
   ◄──49px──►◄──────── 66px plate ────────►◄─34px─►
  ┌─────────┬────────────────────────────┬────────┐
  │  GEM    │        9                   │   +    │  plate 31px
  └─────────┴────────────────────────────┴────────┘
   icon 46px tall = 1.48 × plate, overhangs 8px above / 7px below
```

| property | measured |
|---|---|
| plate | **31px** tall (body 28px + 3px dark bottom keyline) |
| plate fill | `#3D4511` L=0.17 S=0.60 |
| plate left edge | x=70 at **both** y=79 and y=90 → the plate does **not** run under the icon |
| icon | 49 × 46px = **1.48 × plate height** |
| `+` button | 34 × 31px — **exactly** plate height, square, welded to the right cap |
| `+` fill | `#00587F` → `#5FCAF6` gloss; white glyph 15px |
| value text | `#FDFAF9`, cap-height **12px** |
| **plate height : cap-height** | **2.6** |

## Widget 5 — `CurrencyBar` (helmet, `1/1`)

Scanned V at x=120.

| property | measured |
|---|---|
| plate | y=133..161 → **29px** — within 2px of widget 4, so **one rail height rules** |
| fill | **two-tone vertical**: upper `#DF9046` L=0.57, lower `#B7661F` L=0.42 |
| tone ratio | lower / upper = **0.74** |
| keyline | 1px `#2F3300` L=0.10 at the bottom |

The second bar is a **different material** (tan leather) from the first (dark olive) at the
same size. Material varies per resource; geometry does not.

## Widget 6 — `CapacityBar` (wood `4 750`, gold `4 850`)

Scanned H at y=95, V at x=420.

| property | measured |
|---|---|
| plate height | **31px** (y=76..106) — identical to widget 4 |
| icon | 76px wide (x=310..385) — **wider than the gem's 49px** |
| value | white, left-aligned after the icon |
| `+` button | 32px (x=458..489) ≈ plate height |

**Icon width is per-icon; icon height and plate height are fixed.** That is the rule the
kit needs: a rail element sizes on height, and lets width float.

## Widget 7 — `CapacityRibbon` (`max: 8 000`) — NEW

Scanned V at x=420.

| property | measured |
|---|---|
| height | **11px** = **0.35 × the bar height** |
| position | bottom edge y=75, bar top edge y=76 → **flush, touching, centred** |
| text cap-height | ~5px = **0.42 ×** the bar's own text |
| fill | dark, matching the bar |

A limit/capacity plate docked **entirely above** its host. `KitAttach` at `TopCentre`
with `Overhang = 1.0`.

## Widget 8 — `RoundSlotButton` (build queue, ×3)

Scanned H at y=258 (purple), V at x=47 (purple), V at x=47 y=310..400 (green).

| property | purple | green |
|---|---|---|
| diameter | **72–74px** (H 74, V 71) | ~72px |
| body | `#A464D4` L=0.61 S=0.57 | `#77B532` L=0.45 S=0.57 |
| top gloss | `#D7AAF0` L=0.80 | `#A7DA75` L=0.66 |
| **gloss : body** | **1.31 ×** | **1.47 ×** |
| bottom shade | — | L=0.38 = **0.84 ×** body |
| keyline | 1–2px `#421F40` | 2–3px `#3A5100` |
| glyph | 29px = **0.40 × diameter**, `#EDEAEA`–`#747474` **S=0.00–0.05** | `#979398`–`#B9B2B6` **S=0.02–0.05** |

**Two independent measurements agree:** the glyph inside a coloured disc is a
**desaturated grey**, never white and never tinted. Gloss sits at **1.3–1.5 ×** the body
and covers the upper ~15% of the disc. Keyline is the body hue darkened to ~0.3 L, never
black.

## Widget 9 — `SlotCountBadge` (`0/1`)

White text with a dark outline, centred on the disc's **lower rim**, overlapping it. No
plate of its own. Cap-height ~9px against a 72px disc = 0.13.

## Widget 10 — `WorkerChip` (`2/8`)

Scanned H at y=378.

| property | measured |
|---|---|
| plate fill | `#382F1D`–`#283013` L=0.13–0.17 — same dark family as widget 4 |
| icon | a blue+tan glyph at x=698..722, ~25px, **left of** the plate |
| value | white `#FFFDFE`, cap-height ~10px |
| plate start | x=723, i.e. the icon again sits **outside** the plate |

Confirms the CurrencyBar construction at a smaller size: icon outside the left cap, value
inside, dark plate.

## Widget 11 — `WorldPin` (collect marker) — NEW

Scanned V at x=560.

| property | measured |
|---|---|
| height | **51px** (y=130..180) |
| body | `#E6B87C` L=0.69 S=0.68 — warm tan |
| held icon | coin ⌀ **24px** = **0.47 × pin height**, `#FECC03` |
| keyline | 2px transition only, no heavy outline |
| shape | rounded body tapering to a **downward point** — a teardrop |

Floats in world space above a building. The only widget on screen with a **pointed tail**.

## Widget 12 — `BuildingFlag`

Cloth banner on a pole planted on a building. Diegetic art, not a control. Recorded so the
list is complete.

---

## Cross-widget rules this screen establishes

1. **One rail height.** 31, 31, 31, 29px across four different readouts. Derive one
   `RailHeight` from font size; never size a rail element independently.
2. **height : cap-height = 2.6** on the rail.
3. **Icons live outside the plate.** Measured on the gem (plate starts x=70, icon ends
   x=68) and the worker chip (plate starts x=723, icon ends x=722). The plate is never
   drawn under the icon.
4. **Icons are 1.48 × plate height** so they overhang top and bottom.
5. **`+` buttons are square, exactly plate height, welded flush** to the right cap.
6. **Glyph in a coloured disc = desaturated grey** (two measurements, S≤0.05).
7. **Gloss = 1.3–1.5 × body**, top ~15%.
8. **Keylines are the body hue darkened**, never black.
9. **The chrome band is the only desaturated surface** (S≤0.10) — it is what stops the HUD
   competing with a saturated world.

## Actions

- [ ] `CapacityRibbon`, `RoundSlotButton`, `WorldPin`, `ChromeStrip`, `WorkerChip` → catalogue.
- [ ] `KitGeometry.HeightRatio` for `citybuilder` ← **2.6 × cap-height**.
- [ ] `KitMaterial.Gloss` ← **1.4 × body**, upper 15%.
- [ ] `KitMaterial` glyph rule ← **desaturate to S≤0.05** inside coloured discs.
- [ ] Icon slot must render **outside** the plate rect, at **1.48 ×** its height.
