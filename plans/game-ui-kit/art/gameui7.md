# `gameui7.png` — blue ice/water casual GUI kit

**869 × 1600** · asset sheet (Envato watermarked) · **glossy blue, black keyline** family
**Relevance:** `puzzle`, `platformer`, `racing`. The clearest example of a **panel built
from five concentric bands**, and the only sheet using a **true ellipse** as a title plate.

---

## Widget 1 — `Panel`, five concentric bands

Scanned H at y=200 on `LEVEL SELECT`.

```
 bg │keyline│ gloss rim │ mid band │  plate  │keyline│  well
    │  4px  │    8px    │   7px    │  15px   │  4px  │
     #000302  #72E8FA     #0283B1    #01A9EA  #003A5D  #026087
     L=0.01   L=0.71      L=0.35     L=0.46   L=0.18   L=0.27
```

| band | measured | ratio to plate |
|---|---|---|
| outer keyline | 4px `#000302` L=0.01 | 0.02 |
| gloss rim | 8px `#72E8FA` L=0.71 | **1.54 ×** |
| mid band | 7px `#0283B1` L=0.35 | 0.76 × |
| plate | 15px `#01A9EA` L=0.46 | 1.00 |
| inner keyline | 4px `#003A5D` L=0.18 | 0.39 |
| content well | `#026087` L=0.27 | **0.59 ×** |

**Two keylines, not one** — the frame is bounded on both sides by near-black. That is what
lets a saturated blue frame sit on a saturated blue well without the two merging.

The **well at 0.59 ×** is the family's recess depth — compare `gameui1`/`citybuilder3` at
0.79–0.80. This family recesses roughly twice as deep.

## Widget 2 — `TitleOval`

Scanned V at x=240.

| layer | measured |
|---|---|
| black keyline | 3px |
| **white ring** | **6px** `#F9F5FA` L=0.97 |
| fill | `#0370BA` L=**0.37** |
| height | ~65px (y=19..84) |
| shape | a **true ellipse**, not a rounded rectangle |
| text | white caps, centred |
| position | **overhanging the panel's top edge**, centred |

| relationship | measured |
|---|---|
| oval fill : panel plate | 0.37 / 0.46 = **0.80 ×** — the title plate is *darker* than the panel |
| separation | a 6px white ring + a 3px black keyline |

`gameui2` also made its title banner darker than the frame (0.44 ×). `gameui3` made it the
same. So **title plates are usually darker than their panel**, and this family adds a
white ring to keep it legible.

Several ovals carry an **icon overhanging their left end** (star, trophy, gear) — an
attachment on an attachment.

## Widget 3 — `LevelTile`

Yellow rounded square (`#FCD41D` L=0.55 S=0.97, measured at x=83..115) with a dark number,
**★★★ beneath the tile** and outside it. Locked tiles are **grey with a padlock**, and the
stars disappear entirely rather than greying.

Third variant of star placement across the folder: below-inside (`gameui2`),
above-inside (`gameui4`), **below-outside** (here).

## Widget 4 — `Button`

Rounded rectangle, blue plate with a top gloss, black keyline, white caps text
(`RESTART`, `PLAY`). Same band construction as the panel at smaller scale.

## Widget 5 — `CircleButton`

Round, blue, black keyline, white glyph — home, ✕, back. Two are **brand buttons**
(Twitter, Facebook) rendered in their own brand colours, breaking the palette deliberately.

## Widget 6 — `AchievementRow`

Icon (star/rosette/trophy) **overhanging the left end** of a **light plate** carrying dark
text. Unearned rows are the same light plate with no text.

Note the polarity flip: the panel is dark-blue-on-blue, but these rows are
**dark-on-light**. Rows that must be read carefully get inverted — the same rule
`gameui6` used for its `ScorePlate`.

## Widget 7 — `StoreCard`

Near-white card holding item art, with a **`BUY $1.99` plate welded to the bottom edge**.
Eighth picture in the folder with the welded-footer construction.

## Widget 8 — `PowerupSlot`

Light card with a **small green label tab at the top** (`POWERUP 1`) and a **folded
bottom-right corner**. The fold is drawn as a triangle of the darker plate — a cheap
"card" affordance.

## Widget 9 — `UnlockRow`

Icon overhanging the left of a light plate carrying a **large dark value** (`100`, `50`,
`20`, `5`). Same construction as widget 6 with different content weight.

## Widget 10 — `DifficultySelector`

`EASY` (green) / `NORMAL` (white) / `HARD` (orange) — three plates in a row, **each with
its own colour**, not one highlighted out of three neutrals.

This is a **semantic segmented control**: the options are coloured by meaning
(safe/neutral/dangerous), so the selected state must be shown some other way. Distinct
from `citybuilder3`'s value-inversion selection and `citybuilder5`'s glow.

## Widget 11 — `Slider`

Round speaker icon at the left (**overhanging**), a thin light track, and a **vertical bar
knob** — the same bar-knob as `settings1.png`, not a round knob.

## Widget 12 — `Rosette` (×3)

Circular medal with **ribbon tails below**, in blue / green / gold tiers. Confirms
`ui5.png`'s `MedalRosette` and `gameui4`'s tier-by-colour rule.

## Widget 13 — `ScoreField`

Label at the left (`SCORE:`, `HIGH SCORE:`) and a **dark recessed plate** at the right for
the value. The value plate is darker than the well it sits in — a recess inside a recess.

## Widget 14 — `StarRow` (`LEVEL CLEARD!`)

Three stars where the **unearned star is drawn as a dark silhouette** in the well's colour,
not removed and not greyed. Fifth distinct treatment of "unearned" in the folder.

## Widget 15 — `BottomTab` (×3)

Plates each with a **green bar across the top edge** — a tab whose selected indicator is a
coloured strip rather than a fill change.

---

## Cross-widget rules

1. **A frame can need two keylines** — one outside, one inside — when frame and content
   share a hue.
2. **Recess depth is per-skin**: 0.59 here, 0.79–0.80 in the flat families.
3. **Title plates are darker than their panel** (0.80 × here, 0.44 × in `gameui2`), and
   are separated by a light ring when hues collide.
4. **Rows that carry values flip polarity** to dark-on-light. Third sheet to do this.
5. **Segmented controls can be semantically coloured**, which forces selection to be shown
   by something other than colour.
6. **Brand buttons legitimately break the palette.**
7. **"Unearned" has at least five renderings** across the folder: grey stars, ✕ tile, dark
   silhouette, padlock, absent. It must be a `KitState`, not per-widget art.

## Actions

- [ ] `KitMaterial` gains an **inner keyline** distinct from the outer one.
- [ ] `RecessRatio` per skin — measured 0.59 (this), 0.79–0.80 (flat families).
- [ ] Title plate: add `PlateShade` for attachments + an optional **contrast ring**.
- [ ] Add `KitState.Unearned` with a per-skin renderer (grey / ✕ / silhouette / padlock).
- [ ] `PowerupSlot` (folded corner), `DifficultySelector`, `ScoreField`, `BottomTab`
      → catalogue.
