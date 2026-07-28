# `gameui3.png` — plank GUI kit with a full state set

**900 × 1200** · asset sheet (Game Art Partners) · **light plank wood** family
**Relevance:** `platformer`, `puzzle`, `citybuilder`.

**The most valuable sheet in the folder so far**, because it is the only one that shows the
**same control in all four interaction states** — for a label button and for ~40 icon
buttons. Everything else has had to guess what hover and press look like.

---

## Widget 1 — `IconButton` and its measured STATE MODEL

Scanned H at y=690 across the four states of the same `play` button.

| state | body | glyph |
|---|---|---|
| **Normal** | `#DBC56B` L=0.63 S=0.55 | pale cream `#F8EFCD` L=**0.89** S=0.75 |
| **Over** | `#ECC16D` L=0.65 S≈0.66 | **saturated yellow** `#FFED47` L=0.62 **S=1.00** |
| **Click** | `#E7C968` L=0.64 S≈0.70 | deeper yellow `#FFF40B` L=**0.50** S≈0.99 |
| **Disabled** | `#B3B3B1` L=**0.70** **S=0.01** | `#D7D7D7` L=0.82 **S=0.00** |

**Three findings that overturn common assumptions:**

1. **Hover does not lighten the button.** Body lightness moves 0.63 → 0.65 — inside noise.
   What changes is the **glyph**: pale cream (L=0.89, S=0.75) becomes saturated yellow
   (L=0.62, S=1.00). The *content* reacts, not the plate.
2. **Press darkens the glyph further** (0.62 → 0.50) while the body again barely moves.
   Press is "the glyph is pushed in", not "the button is pushed in".
3. **Disabled is desaturation, not dimming.** S collapses 0.55 → **0.01**, while lightness
   *rises* 0.63 → 0.70. A disabled button here is **lighter** than a normal one.

This matches `citybuilder4`'s locked card (S 0.22 → 0.04) from a completely different
sheet. **Disabled = drain saturation** is now measured twice.

| property | measured |
|---|---|
| button size | **~30 × 30px** (x=66..95) |
| keyline | 1–2px `#312817` L=0.14 |
| glyph : button | ~18/30 = **0.60** |
| gap between states | ~6px |

## Widget 2 — `LabelButton` states

`Normal / Over / Click / Disabled` shown as labelled plates, plus a blank pair below
(wood + grey). Confirms the same model applies to a text button: the **Disabled variant is
rendered in grey wood grain**, not a tinted copy — the material itself changes.

## Widget 3 — `Panel`

Scanned H at y=420 on the large panel.

```
   bg │ keyline │  frame  │ keyline │      plate
      │   2px   │   8px   │   2px   │
       #322D21   #DACFA3→#C9AC3D  #593717   #ECDB9F
                 L=0.75 → 0.51              L=0.77
```

| property | measured |
|---|---|
| panel | ~340 × 230px |
| total frame | **12px** (2 + 8 + 2) |
| frame gradient | L=0.75 at the outer edge → 0.51 at the inner |
| inner plate | `#ECDB9F` L=**0.77** |
| **plate : frame** | **~1.40** — the plate is *lighter* than the frame |

**The frame formula does not hold here.** `3.5 + 0.07 × 230` predicts 19.6px; measured
**12px**. It held on `gameui2` (19 vs 20.7 predicted) and `citybuilder5` (its source).

Honest revision: **frame ≈ floor + slope, with floor 2.5–3.5px and slope 0.04–0.07,
per family.** It is a shape of law, not one constant. Record the pair per skin.

**Plate shade now spans three sheets:**

| sheet | plate : frame |
|---|---|
| `citybuilder5` | **0.12** (near-black inset) |
| `gameui2` | **0.70** (darker inset) |
| `gameui3` | **1.40** (lighter, raised) |

A plate can be lighter than its frame. `KitGeometry.PlateShade` must allow values above 1.

## Widget 4 — `PanelHeader`

A separate plank **overhanging the panel's top edge**, narrower than the panel, centred.
Appears on every panel variant on the sheet. Same device as `gameui2`'s TitleBanner but
here it is the **same lightness as the frame**, not darker.

## Widget 5 — `ListPanel`

Panel + **three recessed row plates** stacked with even gaps. The rows are darker than the
plate — the only recess on the sheet.

## Widget 6 — `IconRowPanel`

Panel + **three circular icon wells** in a row above three list rows. Circles are recessed
into the plate.

## Widget 7 — `BannerBar`

A wide horizontal plank with a **small tab plank above it** — a header for a full-width
strip rather than a panel.

## Widget 8 — `DividerPlank`

Thin horizontal plank used as a rule between sections; a vertical variant also appears.

## Widget 9 — `SegmentedProgressBar`

A plank-framed track filled with **six discrete chunks**. Third sheet to draw progress as
segments (`gameui1` ElementChip, `gameui2` ToggleSwitch/UpgradeRow). Segmented progress is
the norm in this vocabulary, not the exception.

## Widget 10 — `ColourBar` (×3)

Brown, pink and white/cyan horizontal strips, each with a dark cap at the left — compact
health/mana/stamina meters.

## Widget 11 — `IconSet`

`+` (pink cross), shield, star, `$`, lightning, clock. Flat, saturated, dark keyline,
white inner highlight — the same construction as `gameui2`'s icon set.

## Widget 12 — `PageIcon`

A small panel with a **folded corner**, used as a document/page glyph.

## Widget 13 — `StarRating` tiers

★★★ / ★★ / ★ stacked as three separate rows — the sheet ships the *tiers* as distinct
assets rather than one widget with a fill count.

## Widget 14 — `LockButton`

Padlock icon button in **normal and disabled** only — no hover or press. Locked controls
are not interactive, so the artist did not draw the states. Worth copying: the kit should
not render hover on a locked control.

## Widget 15 — `IconLibrary` (~40 glyphs × 4 states)

play, home, list, info, trophy, medal, pause, trash, back, cart, forward, grid, down,
person, up, plus, gamepad, menu, upload, refresh, download, question, ✕, ✓, gear, left,
undo, facebook, twitter, save, music, share, sound, arrows, thumbs-up, expand.

Every one drawn four times. The sheet's real product is the **state matrix**, not the
icons.

---

## Cross-widget rules

1. **Hover and press change the GLYPH, not the plate.** Measured: body moves ≤0.02 L,
   glyph moves 0.89 → 0.62 → 0.50 and S 0.75 → 1.00.
2. **Disabled = desaturate to S≈0.01, lightness may RISE.** Measured twice across two
   unrelated sheets.
3. **A locked control has no hover state** — do not render one.
4. **Plate can be lighter than frame** (1.40 here vs 0.12 elsewhere).
5. **The frame formula is a shape, not a constant** — floor 2.5–3.5px, slope 0.04–0.07,
   recorded per family.
6. **Segmented progress is the norm** — third sheet in a row.
7. **Glyph : button ≈ 0.60** in this family (vs 0.40 carved, 0.55 flat).

## Actions

- [ ] Implement `KitState` from these numbers: Hover = glyph → S 1.0; Pressed = glyph
      L × 0.8; Disabled = **S → 0.01, L × 1.1**.
- [ ] Suppress hover/press rendering when `KitState.Locked`.
- [ ] `PlateShade` range must include values **> 1**.
- [ ] Store `FrameFloor` + `FrameSlope` per skin instead of one global pair.
- [ ] `SegmentedProgressBar`, `ColourBar`, `ListPanel`, `IconRowPanel`, `BannerBar`,
      `PageIcon` → catalogue.
