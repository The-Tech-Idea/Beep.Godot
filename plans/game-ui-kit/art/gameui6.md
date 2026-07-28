# `gameui6.png` — gingerbread / cookie casual kit

**1080 × 1080** · asset sheet · **warm baked / thick-rim** family
**Relevance:** `puzzle`, `platformer`, `cardgame`. The heaviest rim treatment in the
folder, and the source of the **menu-button proportion** number.

---

## Widget 1 — `PillButton` (`RESUME/RESTART/ABOUT/SETTINGS/QUIT`)

Scanned V at x=330.

```
     y   76..84   9px  tan rim         #CE8C3E→#DC9D4B  L=0.53→0.58
     y   85..86   2px  bright highlight #FFF7D4          L=0.92
     y   87..111 25px  plate            #E9CB9B          L=0.76
     y  112..129 18px  TEXT             #6C1F1B          L=0.26
     y  164..171  8px  bottom rim       #DA9E51          L=0.59
     y  172..179  8px  dark outline     #732917          L=0.27
```

| property | measured | ratio to plate |
|---|---|---|
| button height | **104px** (y=76..179) | — |
| plate | `#E9CB9B` L=0.76 | 1.00 |
| top highlight | 2px L=0.92 | **1.21 ×** |
| rim | 7–9px L=0.58 | **0.76 ×** |
| outer outline | 8px L=0.27 | **0.36 ×** |
| text cap-height | **18px** | — |
| **height : cap-height** | **5.8** | |
| silhouette | full **stadium/pill** — radius = half the height | |

**5.8 is the number I have been missing.** The HUD rail measured **2.6** (`citybuilder1`).
A menu button is **more than twice as roomy** as a HUD chip for the same text. Sizing every
control from one ratio is why some of the kit's controls looked cramped and others looked
empty.

Record two ratios, not one:

| context | height : cap-height |
|---|---|
| HUD rail / chip | **2.6** |
| menu / dialog button | **5.8** |

## Widget 2 — `Panel` (`COMPLATE`)

Scanned H at y=380.

```
   bg │ dark frame │ tan frame │ hi │      plate
      │    14px    │   15px    │2px │
       #6A2418      #DD9D4A     #FFFADC   #EACB9A
       L=0.25       L=0.58      L=0.93    L=0.76
```

| property | measured |
|---|---|
| panel | ~500 × 500px |
| total frame | **31px** (14 + 15 + 2) |
| frame formula predicts | 3.5 + 0.07 × 500 = 38.5 — in the ballpark, again slightly over |
| structure | **dark outer → tan middle → bright inner highlight → plate** |

Three frame layers, ordered dark→light inward. `citybuilder5` ordered them
light→dark inward (bright rim outside, dark shadow inside). **Frame layer order is a
per-skin choice** and flips the read between "carved into" and "sitting on top of".

## Widget 3 — `RibbonBanner` (`COMPLATE`, `YOU WIN`)

Green ribbon with **folded/notched ends** hanging below the main bar, white caps text,
brown outline. Sits **overhanging the panel's top edge**, and also appears standalone.

Green is the only cool hue on the sheet and is reserved for **success/positive** states —
the banner and the `ON` toggle knob. Pink marks `OFF`.

## Widget 4 — `IconSquare` (left column, ×6)

Rounded square, tan plate, brown outline, **cream glyph** (play, pause, trophy, ✕, ✓,
chart). Glyph is the *lightest* element — the inverse of `citybuilder1`, where the glyph
was the darkest.

## Widget 5 — `PillLabel` (`LEVEL 99`)

Small tan pill, brown outline, brown text. Same construction as widget 1 at ~1/3 the
height — proving the family scales the same shape rather than switching shapes.

## Widget 6 — `ScorePlate` (`SCORE : 987654321`)

**Dark brown pill with white text** — the only dark-plate widget on the sheet. Values that
must be read exactly get an inverted plate; labels and actions do not.

## Widget 7 — `StarRow`

Three stars, **centre larger**, cream fill with a brown outline and a soft inner glow.
Fourth sheet with the centre-star-larger arrangement.

## Widget 8 — `BottomActionRow`

Three icon buttons (play, replay, home) **straddling the panel's bottom edge**, half in and
half out. Same device as `gameui2`'s OKAY and `gameui3`'s nav rows.

## Widget 9 — `ToggleSwitch` (`ON` / `OFF`)

| property | observed |
|---|---|
| construction | rounded plate + a **square knob at the left** + label to the knob's right |
| `ON` knob | green |
| `OFF` knob | pink |
| label | white, inside the plate |

Note the knob does **not** move between states in the artwork — both are drawn knob-left,
with only the knob colour and the word changing. That is a **colour+label toggle**, not a
sliding one. Cheaper to implement and clearer at small sizes.

## Widget 10 — `ProgressBar` (×2)

Scanned V at x=600.

```
     y  816..820   rim
     y  821..828   8px  outer plate  #EECE98  L=0.76
     y  830..836   7px  groove edge  #6B231A  L=0.26
     y  839..841   2px  fill highlight #FFEFEB L=0.96
     y  842..849   8px  FILL          #E0ACB3  L=0.78
     y  850..854   5px  fill shade    #BA4950  L=0.51
     y  855..861   7px  groove edge  #72201A  L=0.27
     y  863..871   9px  outer plate
     y  872..890   rim + dark outline
```

| property | measured |
|---|---|
| bar height | **74px** (y=816..890) |
| groove | **32px = 0.43 of the bar** |
| fill | pink `#E0ACB3` with a 2px highlight above and a 5px shade below |
| end icon | heart / lightning bolt, **at the RIGHT end**, overhanging the bar |

**Two things worth taking:**

1. The bar is **mostly frame** — the groove is only 0.43 of its height. Every other
   reference makes the track most of the bar.
2. The icon is at the **right** end. `citybuilder1`, `gameui2`, `gameui3` and `gameui4`
   all put it at the left. Icon end is a per-skin choice, so the widget needs a
   `CapSide`.

---

## Cross-widget rules

1. **height : cap-height = 5.8** for a menu button vs **2.6** for a HUD chip. Two
   ratios, not one.
2. **Frame layer order is per-skin** — dark→light inward here, light→dark inward in
   `citybuilder5`.
3. **Glyphs can be the lightest element** on the control, not the darkest.
4. **Values get an inverted plate**; labels and actions do not.
5. **A toggle can be colour + label only**, with no knob travel.
6. **Progress bars can be mostly frame** (groove 0.43) and can cap their icon at either
   end.
7. **Green = success, pink = off** — a two-hue semantic set on an otherwise monochrome
   sheet.

## Actions

- [ ] Add a **second height ratio** to `KitGeometry`: `ChipRatio` 2.6, `ButtonRatio` 5.8.
- [ ] Add `FrameOrder`: `DarkOutside` | `LightOutside`.
- [ ] Add `CapSide` (`Left` | `Right` | `None`) to the progress widget, plus a
      `GrooveRatio` (measured **0.43** here).
- [ ] Add a `ColourLabel` toggle variant with no knob travel.
- [ ] `PillLabel`, `ScorePlate`, `RibbonBanner`, `BottomActionRow` → catalogue.
