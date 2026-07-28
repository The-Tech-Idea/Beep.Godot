# `citybuilder2.png` — island base builder, in-game HUD

**1200 × 900** · Boom-Beach-style base builder · **flat modern casual** family
**Relevance:** `citybuilder`; the counter-example to `citybuilder1`.

Every widget on screen is measured below.

---

## Family finding, measured first

The same gem plate was scanned at two x positions with different worlds behind it:

| position | world behind | plate measures |
|---|---|---|
| x=1047..1090 | tan cliff | `#63574D` L=0.35 **S=0.13** |
| x=1114..1148 | green grass | `#414E37` L=0.26 **S=0.17** |

**One plate, two colours.** That is definitive: the plates in this family are
**translucent black, roughly 45–55% alpha**, not opaque fills. No amount of palette work
reproduces this; it needs an alpha channel on the plate.

| | `citybuilder1` | `citybuilder2` |
|---|---|---|
| outline | 3–5px near-black keyline | **none** — world→button is a 1–2px antialias |
| plate | opaque `#3D4511` S=0.60 | **translucent**, proven above |
| depth | keyline + hard shadow | 4px bottom edge at 0.77 × body |
| shape | pill / capsule | rounded square, small radius |

---

## Widget 1 — `ToolRailButton` (left column, 5 items)

Scanned H at y=160, V at x=48.

| property | measured |
|---|---|
| size | **72 × 63px** (x=12..83, y=128..190) |
| body | `#65B8E3` L=0.64 S=0.69 |
| bottom edge | 4px `#468EB5` L=0.49 = **0.77 × body** |
| outline | **none** |
| glyph | white `#ECEBEA` L=0.92, ~38px = **0.53 × width** |
| gap between buttons | **13px** = 0.20 × height |
| screen inset | starts at x=12 — near-flush to the edge |

## Widget 2 — `ChatButton` (right edge, single)

Scanned H at y=163.

| property | measured |
|---|---|
| width | **72px** (x=1114..1185) — **identical to the rail button** |
| body | `#5FB9E4` L=0.63 S=0.71 |
| glyph | `#EFEFEE` L=0.94, **41px = 0.57 × width** |

Two independent buttons, one size. **Glyph occupies 0.53–0.57 of the button** — call it
**0.55**. That is the flat-modern ratio; `citybuilder1`'s carved disc used 0.40.

## Widget 3 — `ResourceReadout` (×4, top-right)

Scanned H at y=32, V at x=560.

| property | measured |
|---|---|
| plate | **133 × 34px** (x=483..615, y=16..49) |
| plate colour | `#414A37` L=0.25 S=0.15 — translucent |
| icon | 41px (x=442..482), **left of** the plate, ≈ plate height, **no overhang** |
| value | white `#FDFCFA`, left-aligned inside the plate |
| outline | none |

Note the contrast with `citybuilder1`, where the icon was **1.48 ×** the plate and
overhung it. Here the icon matches the plate height and sits flush. **Icon overhang is a
per-skin decision, not a universal law.**

## Widget 4 — `GemBar` + `AddButton`

Scanned H at y=75.

| property | measured |
|---|---|
| gem icon | 35px (x=1012..1046), magenta `#FFB5FF` |
| plate | x=1047..1148 → **102px**, translucent (see the family finding) |
| value | white, cap-height ~10px |
| `+` button | x=1149..1188 → **40px**, green `#74B81D` L=0.42 S=0.73 |
| gap | **0px at the vertical midline** — the `+` is adjacent to the plate, not detached |
| `+` glyph | white, 22px = **0.55 × button width** — matches widgets 1–2 |

The `+` is **green**, not the plate's neutral, and not the resource's magenta. Add actions
take the success role.

## Widget 5 — `RankCapsule` (`103`)

Scanned H at y=77.

| property | measured |
|---|---|
| shield badge | 25px (x=107..131), green `#168C4D` — **left of and overhanging** the plate |
| medal icon | 40px (x=132..171), tan/orange |
| plate | x=172..289+, `#3D3B38` L=0.23 **S=0.04** — translucent |
| value | white `#FEFEFE`, cap-height ~11px |

Two icons before the value. The badge is the only part with a role colour.

## Widget 6 — `LevelRing` (`19`, top-left)

Scanned V at x=57.

| property | measured |
|---|---|
| outer diameter | **~95px** (y=13..108) |
| ring band | **13px** = 0.14 × diameter |
| ring colour | `#5FB4E1` |
| outer halo | 7px `#303F40` — a soft dark shadow, **not** a keyline |
| numeral | dark teal `#265B6E` on light |

**Polarity flips inside the ring**: dark-on-light in the badge, light-on-dark everywhere
else on the HUD.

## Widget 7 — `PlayerNamePlate` (`Hit The Lo`)

White text, soft drop shadow, **no plate at all**, sitting directly on the world. Cap
height ~20px — the largest text on screen. Proves a HUD label can go plateless if it
carries a shadow.

## Widget 8 — `WorldLabel` — dashed (`Full!`, ×3)

Scanned H at y=322, V at x=415.

| property | measured |
|---|---|
| size | **58 × 23px** (x=391..448, y=303..325) |
| border | **1–2px near-black** `#001100`, and **dashed** |
| interior | `#708A51` L=0.43 S=0.26 over grass → **white at ~35% alpha** |
| text | white, cap-height ~6px |
| height : cap-height | **3.8** — looser than the 2.6 HUD rail, because it floats |

## Widget 9 — `WorldLabel` — solid (`Upgrade`)

Same size class and construction as widget 8, but the border is **solid**. On the same
screen, at the same moment. **Stroke pattern is the state signal**: dashed = informational
(storage full), solid = actionable (upgrade available). Greyscale-safe and free.

## Widget 10 — `TimerPill` (`48m 49s`)

Scanned V at x=660.

| property | measured |
|---|---|
| height | **22px** (y=398..419) |
| plate | dark translucent, same family as widgets 3–5 |
| left cap | **orange**, holding the clock icon |
| text | white `#FCFFF4`, cap-height ~7px |

Orange appears **only** on this pill and on the storage fill. Colour is doing semantic
work: orange = time.

## Widget 11 — `ReadyMarker` (world, pale yellow)

Scanned V at x=489.

| property | measured |
|---|---|
| size | **28px** (y=549..576) |
| fill | `#F7EBA2` L=0.80 S=0.84 — pale saturated yellow |
| outline | none |

## Widget 12 — `BuildMarker` (green up-arrow tiles)

Small rounded-square plate carrying a green up-arrow, one per upgradeable building, in
world space. Same size class as widget 11 (~28–30px). Marks affordance, not state.

## Widget 13 — `CornerDisc` (hammer bottom-left, compass bottom-right)

Scanned V at x=60.

| property | measured |
|---|---|
| body | `#76C6ED` L=**0.70** S=0.77 — brighter than the rail's 0.64 |
| chord at x=60 | 54px; the disc is **cropped by the screen edge** (y=899) |
| content | an oversized tool illustration, part of it outside the disc |

Deliberately clipped by the viewport. Costs no layout space and reads as a physical object
resting on the screen edge.

## Widget 14 — `CountBubble` (red `4` on the hammer disc)

Small red filled circle carrying a numeral, straddling the disc's upper-left. ~30px.
Confirms the catalogue entry; here it is a corner **attachment on a circle**, so the
anchor must work on non-rectangular hosts.

---

## Cross-widget rules

1. **Plates are translucent black at ~45–55% alpha** — proven by one plate measuring two
   colours over two backgrounds.
2. **One button size (72px) for every icon button**, rail or docked.
3. **Glyph = 0.55 × button** (0.53 and 0.57 measured).
4. **No outline anywhere.** Depth is a 4px bottom edge at 0.77 × body.
5. **Icons do not overhang in this family** (contrast `citybuilder1`'s 1.48 ×).
6. **Stroke pattern is a state signal** — dashed vs solid, same widget, same screen.
7. **Orange = time, green = add/confirm, red = count.** Roles, not decoration.
8. **Floating labels run looser than HUD rails** — 3.8 vs 2.6 height : cap-height.

## Actions

- [ ] `KitMaterial` needs **plate alpha** (≈0.5) and **outline weight allowed to be 0**.
- [ ] Add `DashPattern` to the outline as a state signal.
- [ ] `IconOverhang` becomes a per-skin ratio: **1.48** (cartoon) or **1.0** (flat modern).
- [ ] `GlyphRatio` per skin: **0.40** carved, **0.55** flat.
- [ ] Corner attachments must anchor on **circular** hosts, not just rects.
- [ ] `WorldLabel`, `TimerPill`, `ReadyMarker`, `BuildMarker`, `CornerDisc` → catalogue.
