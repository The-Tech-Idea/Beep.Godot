# `gameui1.png` — parchment & wood adventure GUI sheet

**720 × 900** · asset sheet · **papercraft / torn parchment** family
**Relevance:** `rpg`, `survival`, `puzzle`, `strategy` — a crafted, hand-made feel without
the painted weight of `rpgui.png`.

An asset sheet shows **vocabulary**, not layout. Every element on the sheet is listed
below; the ones I could isolate cleanly carry scanline numbers.

---

## Family construction rule

Everything is **torn paper stacked on wood**. There are **no drawn borders anywhere**.
Edges are irregular — the silhouette is the decoration.

| property | measured |
|---|---|
| parchment fill | `#FADAC0` L=0.87 **S=0.85** — a highly saturated "white" |
| recess | `#CDAE92`–`#D5B599` L=0.69–0.72 → **0.80 × the panel** |
| recess border | **none** |

A depth cue in one flat tone. `citybuilder5` needed four layers and 11px for the same job;
`citybuilder3` independently landed on the same **0.79–0.80** ratio.

---

## Widget 1 — `ProgressTrack` (`LOADING...`, ×6 hues)

Scanned H at y=558.

```
   ◄chevron cap►  gap  ◄──────── fill ────────►◄─ track ─►
   │   33px    │ 18px │        73px            │   25px   │
     #544A01           #9E8E19 L=0.36 S=0.73    #9A8B80 L=0.55 S=0.11
```

| property | measured |
|---|---|
| fill | `#9E8E19` **L=0.36 S=0.73** |
| empty track | `#9A8B80` **L=0.55 S=0.11** |
| ends | **chevron / pointed**, not rounded |
| leading cap | a **detached** chevron, 18px clear of the bar |
| hues | olive, orange, teal, red, purple, cream |

**The empty track is LIGHTER than the fill** (0.55 vs 0.36) and desaturated. Progress here
reads as **saturation rising**, not brightness. This bar is nearly invisible in greyscale
— deliberately. The greyscale gate must not punish it.

## Widget 2 — `OnOffSwitch`

Scanned H at y=556.

| property | measured |
|---|---|
| total width | **42px** (x=636..677) |
| left segment | neutral/dark, light `ON` |
| right segment | **red** `#F9A198`–`#C45655`, `OFF` |
| divider | none — segments meet directly |

Two instances on the sheet, one **green**-lit and one **red**-lit. The lit half takes the
semantic colour; the dark half stays neutral. Confirms `ui5.png`: this is the game
checkbox.

## Widget 3 — `SwatchStrip` (colour reference, ×6)

Scanned H at y=300.

| property | measured |
|---|---|
| width | **46px** (x=416..461) |
| fill | `#D05B47` L=0.55 S=0.59 |
| bottom edge | 2px near-black `#2C0100` |
| edges | torn on all four sides |

Six hues laid out as a palette reference — the sheet's own role-colour set.

## Widget 4 — `ElementChip`

Scanned H at y=68.

| property | measured |
|---|---|
| label plate | **38px** wide, `#291C12` L=0.12 S=0.39 |
| meter | a row of small squares, **~8–9px each with 1–2px dark gaps** |
| square fill | `#C76E59`–`#CE5D5C` L≈0.56 |

**Progress drawn as discrete chunks**, confirming `ui5.png`'s `SegmentedBar` with
measurements: 8–9px cells, 1–2px gutters.

## Widget 5 — `BannerCard` (`EXAMPLE`, ×2)

Scanned V at x=58.

| region | measured |
|---|---|
| total height | **166px** (y=470..636) |
| wood header tab | **28px** = **0.17 of the card**, overhanging the top |
| parchment body | `#CEA486`–`#E4C0A1` L=0.67–0.77 |
| accent band | 15px of gold `#EDB32D` L=0.55 S=0.84 near the bottom |
| `LVL` plate | 17px, at the very bottom |

Anatomy top→bottom: wood tab (overhang) → hex icon → title → body text → gold band →
`LVL n` plate.

## Widget 6 — `InventoryGrid`

Parchment panel (142px tall, y=287..429) + **hexagonal** slot grid at the 0.80 recess +
a `1nventory` title tab overhanging the top-left + a `♥ 999` chip straddling the top-right.

## Widget 7 — `ToolsBoard`

Wood board with **3×2 recessed slots**, each carrying a value chip (`999`) **beneath** it,
green or red. The value sits outside the slot, not inside it.

## Widget 8 — `ZoneMap`

Wide wood frame + red ribbon title (`ZONES`) overhanging the top edge + a parchment strip
carrying a **dashed line threading pentagon nodes**.

Second appearance of a dashed stroke in the folder (after `citybuilder2`'s world label).
Here it means **path**. Nodes are **pentagons**, not circles.

## Widget 9 — `TitlePopup`

Torn sheet + a header title + an inner recessed area + a small `info:` tag hanging off the
bottom-left **on a red string with a visible knot**. The tag is an attachment with a
*flexible* connector — the first non-rigid attachment in the folder.

## Widget 10 — `ScrollPanel`

Parchment with a **rolled bottom edge** drawn as a cylinder, and a red ribbon title
(`TITLE EXAMPLE`) overhanging the top.

## Widget 11 — `PlankFrame` / `WoodBoard`

Horizontal boards with **nail heads** at the ends, used as headers and dividers; vertical
planks with visible grain and end caps.

## Widget 12 — `WaxSeal`

Round red wax medallion. Used two ways on the sheet: a corner ornament on a panel, and a
**lock indicator** (paired with a padlock glyph on the `ZONES` panel).

## Widget 13 — `StringTag`

Luggage-tag silhouette (rectangle with one cut corner and a punched hole) hung on a red
string. Appears alone and attached to panels.

## Widget 14 — `FloatingNumber`

`+10`, `-10`, `+20` with a small icon, **no plate at all**. Combat/economy feedback. Green
and red variants.

## Widget 15 — `PagerTriangle`

Chunky cream triangle with a dark keyline, left and right. Solid — no plate behind it.

## Widget 16 — `IconSquare`

Small dark rounded square with a single glyph (trophy, padlock). The sheet's smallest
control.

## Widget 17 — `StarRow`

★★★ above a mission card, gold on dark.

## Widget 18 — `MissionCard` / `ElementCard` (bottom row)

Parchment cards with a star row above, a title, and a body — the bottom strip of the sheet
shows them mid-composition against the wood `ZONES` frame.

## Widget 19 — `HealthStrip`

Dark rounded strip with a red inner bar and a heart icon — a compact health readout in the
sheet's top-right cluster.

---

## Cross-widget rules

1. **Depth = one flat tone at 0.80 ×.** No border, no shadow.
2. **Progress can read as saturation rather than brightness.** Exempt from the greyscale
   gate.
3. **Bar ends can be chevrons**, and a bar can have a **detached leading cap**.
4. **Irregular silhouettes replace every border** — torn edges, rolled ends, plank grain.
5. **Stacking is a device** — 2–3 sheets offset a few px reads as depth more cheaply than
   any shadow.
6. **Non-rectangular slots are normal** — hexagons for inventory, pentagons for map nodes.
7. **Attachments can be flexible** — the `info:` tag hangs on a string, not a rigid anchor.
8. **Segmented progress = 8–9px cells, 1–2px gutters.**

## Actions

- [ ] Add `KitShape.Torn` and `KitShape.Rolled` — seeded-noise offsets along the outline so
      the same widget is stable frame to frame.
- [ ] Add **stacking** (N offset copies behind the host) to `KitMaterial`.
- [ ] Allow progress fill/track to differ by **saturation**; exempt such skins from the
      greyscale gate.
- [ ] Add chevron end-caps and a detached leading cap to the progress widget.
- [ ] Add a **flexible attachment** (string/chain) to `KitAttach`.
- [ ] `WaxSeal`, `StringTag`, `PlankFrame`, `ScrollPanel`, `ElementChip`, `FloatingNumber`,
      `ZoneMap`, `SwatchStrip`, `BannerCard`, `ToolsBoard` → catalogue.
