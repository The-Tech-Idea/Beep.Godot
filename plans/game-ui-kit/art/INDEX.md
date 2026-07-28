# Per-image art documentation

One document per file in `Example_Art/`. Each document records what is actually on the
screen, **measured in pixels**, not described in adjectives. A widget only enters the kit
catalogue with a document behind it.

## Method

Every number in these documents comes from a scanline through the real pixels
(`tmp/m.py segr|segc`), which segments a row or column wherever the colour genuinely
changes. A segment's length in px **is** the part's thickness. Where a number is an
estimate rather than a scan, it is written `~` and says so.

Recorded per widget: overall size, part thicknesses, corner treatment, the light/dark
band values, glyph cap-height, and the ratio of the widget's height to its text — because
ratios are what transfer to a renderer, and absolute px do not.

## Files (44)

| # | file | doc | genre relevance |
|---|---|---|---|
| 1 | `citybuilder1.png` | [citybuilder1.md](citybuilder1.md) | citybuilder — cartoon outline |
| 2 | `citybuilder2.png` | [citybuilder2.md](citybuilder2.md) | citybuilder — flat modern, translucent |
| 3 | `citybuilder3.png` | [citybuilder3.md](citybuilder3.md) | citybuilder — muted flat, dark-on-light |
| 4 | `citybuilder4.png` | [citybuilder4.md](citybuilder4.md) | citybuilder — soft dark drawer |
| 5 | `citybuilder5.png` | [citybuilder5.md](citybuilder5.md) | citybuilder — carved stone (**best numbers**) |
| 6 | `gameui1.png` | [gameui1.md](gameui1.md) | universal — papercraft/torn parchment |
| 7 | `gameui2.png` | [gameui2.md](gameui2.md) | universal — carved wood (**confirms frame formula**) |
| 8 | `gameui3.png` | [gameui3.md](gameui3.md) | universal — plank wood (**full 4-state matrix**) |
| 9 | `gameui4.png` | [gameui4.md](gameui4.md) | universal — neutral plate + coloured glyph (**palette model proof**) |
| 10 | `gameui5.png` | [gameui5.md](gameui5.md) | universal — flat angular (**inverts gameui4**) |
| 11 | `gameui6.png` | [gameui6.md](gameui6.md) | universal — gingerbread (**menu-button ratio 5.8**) |
| 12 | `gameui7.png` | [gameui7.md](gameui7.md) | universal — glossy blue (**5-band panel, true ellipse title**) |
| 13 | `gameui8.png` | [gameui8.md](gameui8.md) | **rpg / survival — richest RPG vocabulary in the folder** |
| 14 | `gameui9.png` | [gameui9.md](gameui9.md) | **survival** — pixel-art diegetic panel |
| 15 | `racing1.png` | [racing1.md](racing1.md) | **racing** — broadcast minimal, no plates |
| 16 | `racing2.png` | [racing2.md](racing2.md) | **racing** — angular slash (**genre vs theme split evidence**) |
| 17 | `racing3.png` | [racing3.md](racing3.md) | **racing** — light glass (**radar chart: missing primitive**) |
| 18 | `racing4.png` | [racing4.md](racing4.md) | **racing / shooter** — dark tech (**corner-bracket container**) |
| 19 | `rpg1.png` | [rpg1.md](rpg1.md) | **rpg** — parchment shop (**affordability = desaturate**) |
| 20 | `rpg2.png` | [rpg2.md](rpg2.md) | **rpg** — wood+gold shop (**neutral plates, saturated ornament**) |
| 21 | `rpg3.png` | [rpg3.md](rpg3.md) | **rpg** — equipment screen (**shape carries state**) |
| 22 | `rpgui.png` | [rpgui.md](rpgui.md) | **rpg** — painted fantasy (**supersedes PLAN §4.2a**) |
| 23 | `rpgui1.png` | [rpgui1.md](rpgui1.md) | **rpg** — premium gold hairline (**two frame regimes**) |
| 24 | `rpgui2.png` | [rpgui2.md](rpgui2.md) | **rpg / cardgame** — ink deckbuilder (**most complete screen vocabulary**) |
| 25 | `rpgui3.png` | [rpgui3.md](rpgui3.md) | **rpg** — dense JRPG (**LabelValuePair primitive**) |
| 26 | `settings1.png` | [settings1.md](settings1.md) | universal — settings screen (**corrects catalogue §D**) |
| 27 | `skilltree.png` | [skilltree.md](skilltree.md) | **rpg / strategy** — hue-per-branch tree (**locked = silhouette**) |
| 28 | `skilltree1.png` | [skilltree1.md](skilltree1.md) | **rpg / strategy** — talent tree (**welded footer = 0.19 × card**) |
| 29 | `skilltree3.png` | [skilltree3.md](skilltree3.md) | **rpg / strategy** — row-coloured grid (**HueAxis**) |
| 30 | `skilltree4.png` | [skilltree4.md](skilltree4.md) | **rpg** — merge/idle grid (**4-corner tile grammar**) |
| 31 | `store.png` | [store.md](store.md) | universal — canonical shop grid (**splits welded footer in two**) |
| 32 | `store1.png` | [store1.md](store1.md) | universal — banded card (**0.67/0.11/0.19**) |
| 33 | `survaivleandrpg.png` | [survaivleandrpg.md](survaivleandrpg.md) | **survival / rpg** — cosy journal (**selection may go calmer**) |
| 34 | `survaivleandrpg1.png` | [survaivleandrpg1.md](survaivleandrpg1.md) | **rpg / survival** — pixel book (**4 tones = whole UI**) |
| 35 | `survaivleandrpg2.png` | [survaivleandrpg2.md](survaivleandrpg2.md) | **survival** — pixel inventory (**surface ladder ×1.4**) |
| 36 | `Upgrades.png` | [Upgrades.md](Upgrades.md) | **strategy** — painted upgrades (**rim ratio test**) |
| 37 | `ui1.png` | [ui1.md](ui1.md) | casual sheet — **badge matrix, underline selection** |
| 38 | `ui2.png` | [ui2.md](ui2.md) | **rpg / shooter** — hero detail (**panel direction follows surface**) |
| 39 | `ui3.png` | [ui3.md](ui3.md) | **rpg** — items screen (**"empty" has three meanings**) |
| 40 | `ui5.png` | [ui5.md](ui5.md) | casual mega-sheet — **10 materials × 1 geometry** (families only; per-instance pass outstanding) |
| 41 | `ui6.png` | [ui6.md](ui6.md) | **survival / puzzle** — notebook (**pencil-stroke recipe**) |
| 42 | `ui7.png` | [ui7.md](ui7.md) | **duplicate of `gameui9.png`** (same MD5) |
| 43 | `ui8.png` | [ui8.md](ui8.md) | **citybuilder** — full chrome (**CollapsiblePanel spec**) |
| 44 | `skilltree1.png` dup check | — | — |

Progress: **44 / 44 files documented** (43 unique images — `ui7.png` is a byte-identical
duplicate of `gameui9.png`).

**One outstanding item, stated so it is not forgotten:** `ui5.png` (1200 × 3579, several
hundred elements across ~10 material families) is documented at the level of its
**organising principle and widget families**, not per instance. A per-instance measured
pass of that one sheet — specifically how each material changes frame thickness and rim
ratio — is a session's work on its own and has not been done. Every other file has a full
per-widget measured document.

## Two measurable tests for "painted" vs "flat"

Both derived from scanlines, both usable as automated material checks:

| test | painted | flat |
|---|---|---|
| **bottom : peak lightness** within a plate | **0.18–0.27** | **0.76–0.84** |
| **rim : body lightness** | **1.78–2.05 ×** | **1.3–1.5 ×** |

Sources: rpgui (PLAY 0.18, health bar 0.27), citybuilder5 (rim 2.05), Upgrades (rim 1.78),
citybuilder1 (rim 1.31/1.47), skilltree3 (rim 1.42), citybuilder2 (bottom 0.77).

This is what the failed greyscale gate should have been checking. Feeding painted
proportions to a flat renderer was the root error of the earlier sessions; these two ratios
detect it automatically.

## Settled rules (measured 3+ times, treat as decided)

| rule | measurements |
|---|---|
| **unavailable/disabled = drain saturation** (S → 0.01–0.05), lightness may rise | gameui3, citybuilder4, rpg1, rpg3, gameui9 — **5×** |
| **palette goes on ONE element**, the other stays neutral | gameui4, gameui5, rpg2, rpgui1, rpgui2 — **5×** |
| **empty/track = a dark tint of the surface's own hue**, never grey | rpg1, rpg2, rpgui2, gameui1 — **4×** |
| **segmented progress is the default**, continuous is the exception | gameui1–4, rpg1, rpg2, rpgui1 — **7×** |
| **HUD rail ≈ 3 % of screen height** (~30px at 1000px wide) | cb1, cb3, cb5, gameui8, racing1 — **5×** |
| **top-right corner straddle = the attention anchor** | rpg1, rpg3, gameui8, citybuilder5, gameui4 — **5×** |
| **primary action = bigger** (or saturated; expose as a choice) | gameui2, gameui4, gameui5, rpg1 vs rpg2, rpgui2 |
| **one element class flips polarity** (values/tooltips/actions) | gameui6, gameui7, gameui9, citybuilder4, rpgui2 — **5×** |
| **dashed stroke = path / provisional** | citybuilder2, gameui1, gameui8, rpgui2 — **4×** |
| **welded footer/price bar under a card** | store, skilltree1, Upgrades, ui5, gameui7, gameui8, rpg2, rpgui2 — **8×** |

## Selection mechanisms found so far (all measured)

The kit cannot have one `KitState.Selected` renderer — eleven distinct mechanisms appear
across the folder, and the choice depends on **widget class** as much as on skin.

| # | mechanism | source |
|---|---|---|
| 1 | none — position in the list only | citybuilder1 |
| 2 | value inversion (dark plate + light glyph) | citybuilder3 |
| 3 | lighten + border | citybuilder4 |
| 4 | hue shift + external glow | citybuilder5 |
| 5 | glyph colour only (body unchanged) | gameui3 |
| 6 | a filled pill appears behind the tab | gameui8 |
| 7 | 3px white outline **outside** the host | gameui9 |
| 8 | raise the selected tab (elevation) | gameui9 |
| 9 | fill the row with the only saturated colour | racing1 |
| 10 | fill with accent, keep the glyph dark (fill *darkens*) | racing3 |
| 11 | 3px accent border | racing4 |

**Convention by widget class:** card carousels use an outline (racing3, racing4, gameui9);
tab strips use fill/elevation; list rows use a fill.

## Standard for a finished document

Set after the first pass was rejected for measuring only one widget per image:

> **Every widget on the screen gets its own numbered entry with its own measurements.**
> A table row listing a widget's name is not a document. If a widget could not be
> isolated cleanly enough to scan, the entry says so rather than guessing.

## Findings that have already changed the kit design

| finding | source | effect |
|---|---|---|
| frame = **3.5px floor + 0.07 × height**, not a ratio | citybuilder5 | replaces `KitGeometry.FrameRatio` |
| inner plate : frame lightness = **0.12 recessed / 0.875 raised** | citybuilder5 | `PlateShade` → `PlateShadeFor(elevation)` |
| outer rim = **2.05 ×** plate | citybuilder5 | new `RimBrightness` |
| plates can be **translucent** (one plate measured two colours) | citybuilder2 | `KitMaterial` needs alpha |
| polarity can be **dark-on-light** | citybuilder3 | skin-level polarity flag |
| glyph tint has **three** modes: grey / lighter / darker | cb1, cb4, cb5 | `GlyphTint` enum |
| every material layer must be **switchable off** | citybuilder3 | zero-decoration skins exist |
| selection is **per-skin**: none / invert / lighten+border / hue+glow | cb1–cb5 | `KitState.Selected` renderer |
| locked = dim **0.79 ×** + desaturate to **S≈0.04** + reason label | citybuilder4 | `KitState.Locked` |
| subtle inset = **0.79–0.80 ×** host | cb3 + gameui1 independently | one constant |
| HUD rail height ≈ **30px** at ~1000px wide | cb1 (31), cb3 (29), cb5 (30) | `RailHeight` |
| rail height : text cap-height = **2.6** | citybuilder1 | `HeightRatio` |
| icon overhang is **per-skin**: 1.48 × or 1.0 × | cb1 vs cb2 | `IconOverhang` |
| glyph : button = **0.40** carved, **0.55** flat | cb1 vs cb2 | `GlyphRatio` |
| progress can read as **saturation**, not brightness | gameui1 | greyscale-gate exemption |
| **dashed vs solid stroke** is a state signal | cb2, gameui1 | `DashPattern` |

### Correction — the plate-shade row was over-general (2026-07-28)

This table originally read *"`PlateShade` 0.88 → 0.12"*, which turned one measurement into a
global constant. 0.12 is the **recessed `StoneCapsule` readout**; the **raised `ActionTile` on
the same screen, in the same material, measures 0.42/0.48 = 0.875** — 7× apart. Applying 0.12
everywhere, as the row asked, would have rendered every button's inner plate near-black.

The split tracks **elevation**, which `KitElevation` already models, so it is now
`PlateShadeFor(Recessed 0.12 / Flush 0.55 / Raised 0.88)`. Worth generalising: **two widgets in
one image disagreeing by 7× means the measurement is conditional on something, not a constant** —
find the condition before promoting either number.
