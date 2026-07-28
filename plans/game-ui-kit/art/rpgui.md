# `rpgui.png` — painted fantasy UI sheet

**1000 × 2000** · asset sheet · **hand-painted fantasy** family
**Relevance:** `rpg`, `strategy`. Previously the source of §4.2a in `PLAN.md` — this
document supersedes those numbers with a full per-widget pass.

---

## Correction to the earlier §4.2a numbers

`PLAN.md` §4.2a recorded "frame ≈ 12 % of height" from this sheet. Re-measured on the
`PLAY` button (V at x=220):

| part | measured |
|---|---|
| button height | **102px** (y=520..622) |
| wood frame | **16px** |
| inner dark keyline | **5px** |
| **frame : height** | wood alone **0.157**; wood + keyline **0.21** |

So 0.12 was low. The correct figure depends on whether the inner keyline counts, and both
should be recorded separately.

## The painted signature: a strong internal gradient

| element | top / peak | bottom | ratio |
|---|---|---|---|
| `PLAY` green plate | `#87B320` L≈0.40 | `#122203` L=**0.07** | **0.18** |
| health bar red fill | `#E43A33` L=**0.55** | `#4A0603` L=0.15 | **0.27** |

**A painted plate falls to 0.18–0.27 of its peak lightness by its bottom edge.** Flat
families sit at 0.76–0.84 (`citybuilder1`'s bottom shade, `citybuilder2`'s bottom edge).

That single number is what separates "painted" from "flat", and it is why feeding this
sheet's proportions to a flat procedural renderer produced the wrong look — the earlier
session's core error, now quantified.

---

## Widget 1 — `HealthBar` set (×6)

Long horizontal bars, measured **34px** tall (V at x=300).

| variant | end caps |
|---|---|
| 1 | wrapped rope / bandage caps |
| 2 | spiked star caps |
| 3 | arrow caps |
| 4–6 | simple metal caps, in red / gold / blue |
| green | **segmented** fill, thin |

**The end cap is the entire variation.** Six bars, one body, six cap designs. That is how
this family expresses hierarchy — cap ornament, not colour or size.

For the kit: `ProgressBar` needs a **cap slot** that can take arbitrary art, independent of
the track and fill.

## Widget 2 — `PortraitFrame` (×2)

- circular metal-rimmed portrait
- rectangular portrait with a **name plate above** (`Johnny`), a **level badge `35`
  overhanging the bottom-left**, and a thin red bar beneath

Three attachments on one host at three anchors.

## Widget 3 — `IconButton` grid

Square buttons with a metal frame and glyphs (expand, gear, contract, `MED`, music, sound,
sun). Plus a row of **tan/gold plates** with glyphs (armour, mail, speech, potion) — a
second, lighter button material on the same sheet.

## Widget 4 — `Bookmark` ribbons

Small vertical tab ribbons (red; green with an up-arrow) that hang off a panel's edge.
`KitAttach` with a large overhang.

## Widget 5 — `PotionIcon` row

Coloured vials — the sheet's consumable set.

## Widget 6 — `HangingBanner` (×3)

- small red banner on a pole
- **large purple banner with metal end rollers** (a wide title bar)
- purple cloth banner hanging from a rod with a **torn bottom edge**
- blue **shield-shaped pennant**

Four ways to hang a title. Confirms `ui5.png`'s hanger family with painted execution.

## Widget 7 — `TitleBar` (`Knight - Level 8`)

Ornate brown plate with a **pouch medallion overhanging its left end** and a **red ✕ inside
the frame at the right end**. Gold serif text, centred.

## Widget 8 — `WoodPlank`

Horizontal plank with nail heads — a divider/shelf.

## Widget 9 — `ScrollBar`

Vertical track with a knob at the sheet's right edge. Painted, with metal end caps.

## Widget 10 — `PrimaryButton` (`PLAY`)

Measured in the header above. Heavy wood frame with **corner screws**, a green plate with a
strong vertical gradient, gold serif text.

## Widget 11 — `RaritySwatch` row

Five small square plates: grey, green, blue, purple, orange. The **rarity ladder** as a
material set — the same five hues used by almost every RPG.

Directly usable: this is the `rpg` project's rarity palette, taken from art rather than
invented.

## Widget 12 — `RankMedal` (1st / 2nd / 3rd)

Ornate badges: gold winged with laurel, silver, bronze — each with a **ribbon banner
carrying the rank text**. Tier by both material and ornament complexity (1st has wings and
laurel; 3rd has neither).

**Tier expressed as ornament count**, not just colour — a device the kit could express as
an attachment count per tier.

## Widget 13 — `OrnatePlate`

Blue plate with a gold border and **cut corners** — same notched silhouette as `rpgui1`.

## Widget 14 — `RedRibbon`

Long red ribbon with **notched swallowtail ends** — a title/label holder.

## Widget 15 — `ColourPlate` set (×4)

Plain rounded plates (grey, blue, gold, green) — the button bases before ornament. The
sheet ships the **unornamented base** alongside the finished controls, confirming the
family is built as base + ornament.

## Widget 16 — `CircleMedallion` set (×9)

Round metal-rimmed medallions with glyphs (torch, cross, shield, skull, boot, gauntlet,
sword, helm, heart). The skill/ability icon set.

## Widget 17 — `SmallGlyph` set

Hourglass, hammer, gem, speech bubble, ✓, ✕, ▼, ●, ↑ — flat UI glyphs, distinct from the
painted medallions. Same **glyph/painted split** as `rpgui1`.

## Widget 18 — `GoldPile` set

Four sizes of coin pile — a *quantity expressed as art volume* rather than a number.

## Widget 19 — Decorative set

Blood splatter, blue crescent glow, red swoosh, glowing forge, hooded character in a stone
arch. Scene dressing rather than controls.

## Widget 20 — `Texture` set

A brick wall and a **stitched leather panel** — background materials for panels, with the
stitching drawn as the border. A texture *is* the frame in this family.

## Widget 21 — `TextStyle` declaration

The sheet literally labels its text colours: **Silver Text**, **Gold Text**, **Gem Cost**,
and a value sample (`6,432`). Three named text roles, declared as art.

Worth copying into the kit's theme: an RPG skin needs at least `Silver` (normal),
`Gold` (emphasis/values) and `Gem` (premium currency) text roles — which is more specific
than `UiSurface`'s current generic roles.

---

## Cross-widget rules

1. **Painted plates fall to 0.18–0.27 of peak lightness at their bottom edge**; flat
   families sit at 0.76–0.84. This is the measurable definition of "painted".
2. **Variation lives in the end caps**, not the body — six bars, one track.
3. **Tier = ornament count** (wings, laurel) as well as material.
4. **The base plate ships unornamented** — this family is base + attachments, which is
   exactly `KitControl`'s model.
5. **Rarity ladder: grey / green / blue / purple / orange** — take it verbatim.
6. **Named text roles**: silver, gold, gem.
7. **A texture can be the frame** (stitched leather).

## Actions

- [ ] Record **painted vs flat** as a measurable material property: bottom : peak lightness
      ≤ 0.30 = painted, ≥ 0.70 = flat. Use it as the greyscale gate's material check.
- [ ] `ProgressBar` gains an **independent cap-art slot**.
- [ ] Add the **rarity ladder** to the `rpg` palette from this sheet.
- [ ] Add `Silver` / `Gold` / `Gem` text roles to the RPG theme.
- [ ] Supersede `PLAN.md` §4.2a's frame figure with **0.157 wood / 0.21 including keyline**.
- [ ] `RankMedal`, `HangingBanner` (4 variants), `RaritySwatch`, `GoldPile` → catalogue.
