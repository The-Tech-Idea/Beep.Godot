# `skilltree3.png` — mobile talent grid

**564 × 1128** · live menu screen · **navy background, row-coloured tile grid** family
**Relevance:** **`rpg`**, `strategy`.

---

## Widget 1 — `TalentGrid` — colour by ROW, measured

Scanned V at x=95 through all four rows.

| row | fill | L | **S** |
|---|---|---|---|
| 1 | grey `#BEBEBE` | 0.75 | **0.00** |
| 2 | green `#539919` | 0.35 | **0.72** |
| 3 | blue `#266FBF` | 0.45 | **0.67** |
| 4 | orange `#F9BE2F` | 0.58 | **0.94** |

| property | measured |
|---|---|
| tile size | **~96–100px** square |
| row pitch | **~123px** → gutter ≈ 24px |
| keyline | thick, near-black |
| level number | small, **top-left corner** of the tile |
| glyph | white line art, centred |

**Row 1 is S = 0.00 — pure neutral grey.** Rows 2–4 are saturated. So the grid encodes tier
partly by hue and partly by *whether there is any hue at all*: tier 1 is the unowned/basic
tier and is literally colourless.

This is a variant of `skilltree.png`'s branch-hue system rotated 90°:

| reference | axis of colour |
|---|---|
| `skilltree.png` | **column** = branch |
| `skilltree3.png` | **row** = tier |
| `skilltree1.png` | **per-node** = state |

Three different meanings assigned to the same device. The kit's tree/grid container should
expose `HueAxis: Column | Row | Node`.

## Widget 2 — `UpgradeButton`

Scanned V at x=280.

| part | measured |
|---|---|
| height | **89px** (y=834..923) |
| fill | `#7FC847` L=0.53 S=0.55 |
| top rim | `#BAF587` L=**0.75** = **1.42 × fill** |
| keyline | 2–3px near-black |
| content | label `Upgrade` on the upper half, **cost row (coin + `2000`) on the lower half — inside the same plate** |

**Label above, cost below, one plate.** Distinct from `skilltree.png`, where the cost was a
*separate welded plate above the button*. Two ways to attach a price to an action:

| pattern | source |
|---|---|
| cost welded **outside** (above/below) | skilltree, rpg1, rpg2, store |
| cost **inside** the button's lower half | **skilltree3**, gameui5 |

The inside variant is better when the button is large; the welded variant when the cost
must be read before committing to the row.

Top rim at **1.42 ×** sits with `citybuilder1`'s 1.31–1.47 gloss measurements — the casual
gloss band is consistently **1.3–1.5 ×** the body across four references.

## Widget 3 — `Tooltip`

A **white rounded plate with a dark header bar** (`Devine strike`) and body text, with a
**tail pointing down-right** at the tile it describes.

| part | observed |
|---|---|
| header | dark bar, light text — a title *within* the tooltip |
| body | white plate, dark text |
| tail | points at the source tile |

Second tooltip in the folder (after `rpgui2`'s dark `Burn` plate) and the **first with a
header bar**. A tooltip with a titled header is a two-region widget, not a single text box.

## Widget 4 — `ResourceBar`

A **gold star level badge (`2`) overhanging an XP bar**, then three currency readouts, each
`icon + green + badge + value`. Same construction as `skilltree1`'s pills — the `+` badge
straddles the **icon**, again.

## Widget 5 — `TabStrip` (top and bottom)

Icon tabs; the selected one becomes a **light plate with a dark label** while the others
stay dark and icon-only. **Selection reveals the label** — unselected tabs have no text at
all.

That is a genuinely useful density trick and the **thirteenth** selection mechanism in the
folder: *selection adds content*, not just styling.

The bottom bar's selected tab (`Evolve`) additionally carries a **green up-arrow
overhanging its top-right corner** — an attention badge on the selected tab.

## Widget 6 — `ProgressText`

`Upgraded 20 times` with the number in **gold** and the surrounding words in grey. Colour
applied to a **run inside a sentence**, matching `rpgui2`'s combat log finding.

## Widget 7 — `ScreenTitle` / `DescriptionText`

`Talent` centred with no plate, then two lines of explanatory body text. Same plateless
title + help-text pairing as `skilltree.png`.

---

## Cross-widget rules

1. **`HueAxis: Column | Row | Node`** — the same colour device carries branch, tier or state
   depending on the tree.
2. **Tier 1 can be S = 0.00** — colourlessness is itself a tier.
3. **Casual gloss band = 1.3–1.5 × body** — fourth confirmation.
4. **Cost attaches inside a large button or welded outside a small one.**
5. **Selection can reveal content** (the label appears only on the active tab).
6. **A tooltip may have a header bar** — two regions, not one.
7. **Colour applies to text runs**, not only to controls.

## Actions

- [ ] Tree/grid container gains `HueAxis`.
- [ ] `CostPlacement: InsideButton | WeldedOutside`.
- [ ] `KitState.Selected` may **add a label**; account for the size change in layout.
- [ ] `Tooltip` gains an optional **header bar** region.
- [ ] Confirm gloss default **1.4 ×** (now four measurements: 1.31, 1.47, 1.42, 1.33).
