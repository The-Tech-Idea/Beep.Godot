# `Upgrades.png` — Kingdom-Rush-style upgrade screen

**686 × 563** · live menu screen · **painted fantasy, wood + gold** family
**Relevance:** **`strategy`**, `rpg`, `citybuilder`.

---

## Widget 1 — `UpgradeTile` grid (6 columns × 5 rows)

Scanned V at x=105 through a full column.

| property | measured |
|---|---|
| tile pitch | **~64px** |
| gold frame band | **~18px** (shared between adjacent tiles) |
| icon area | **~42px** |
| frame | `#C69B1F` **L=0.45 S=0.73** |
| frame highlight | `#FFF093` L=0.79–0.81 = **1.78 × frame** |

**Frame highlight at 1.78 ×** is the brightest rim ratio measured so far except
`citybuilder5`'s 2.05 ×. Both are carved/painted families; the flat families sit at
1.3–1.5 ×. So the rim ratio itself distinguishes carved from flat:

| family | rim : body |
|---|---|
| flat casual (citybuilder1, skilltree3) | **1.3–1.5 ×** |
| carved / painted (Upgrades, citybuilder5) | **1.78–2.05 ×** |

That is a second measurable painted/flat discriminator alongside `rpgui.md`'s
bottom : peak gradient ratio (0.18–0.27 painted vs 0.76–0.84 flat).

## Widget 2 — Tile states

| state | appearance |
|---|---|
| **purchased / available** | **gold frame**, full-colour icon, no chip |
| **locked** | **grey stone frame**, desaturated icon, plus a **cost chip (`★ 2`, `★ 3`) welded to the tile's bottom** |

Locked tiles get an **extra element** (the cost chip) rather than losing one — the player is
told the price at the point of blocking. Same principle as `citybuilder4`'s reason plate and
`gameui9`'s requirement line, now on a grid tile.

And again: **locked = desaturated** (grey frame, grey icon). Sixth measurement of the
saturation rule.

## Widget 3 — `ColumnRail`

A **gold vertical line runs behind each column**, connecting its five tiles into a chain.
The rail is continuous and passes under the tiles.

Fourth connector style in the folder:

| style | meaning | source |
|---|---|---|
| solid orthogonal | prerequisite | skilltree |
| dashed | traversable path | gameui1, rpgui2 |
| arrowed | directed upgrade | rpgui1 |
| **continuous rail behind a column** | **a fixed linear chain** | **Upgrades** |

The rail says "this column is one ordered track" more clearly than per-pair connectors.

## Widget 4 — `CategoryCrest`

A stone/metal shield at each column's foot carrying a weapon glyph and a **blue gem at its
base**. Anchors the column and identifies its category.

Column footer as an *emblem* rather than a label — a device the kit has no equivalent for,
and a good fit for a strategy skin.

## Widget 5 — `Tooltip`

| part | observed |
|---|---|
| plate | **black with a gold border** |
| title | **italic serif, gold** |
| body | white/yellow, two lines |
| **cost** | `★ 3` at the **top-right corner** |
| position | overlaps the tiles, floating |

Third tooltip in the folder (`rpgui2` dark plate, `skilltree3` header bar, here). This one
puts the **cost in the tooltip's corner** — so the price appears twice, on the tile and in
the tooltip, which is correct for a screen where hovering is how you plan.

## Widget 6 — `TitleBanner` (`UPGRADES`)

Gold ribbon with **folded ends and a scalloped bottom edge**, overhanging the panel's top.
Same painted banner vocabulary as `rpgui.png`.

## Widget 7 — `BottomBar`

| control | state |
|---|---|
| `★ 0` star chip | currency readout |
| `RESET` | enabled — full contrast wood plate |
| `UNDO` | **disabled — desaturated and dimmed** |
| `DONE` | enabled |

`UNDO` vs `DONE` side by side is a clean enabled/disabled pair in one family: same
geometry, same size, **saturation and lightness dropped**. Seventh measurement of the rule.

---

## Cross-widget rules

1. **Rim : body ratio discriminates carved (1.78–2.05 ×) from flat (1.3–1.5 ×)** — a second
   measurable painted/flat test.
2. **Locked tiles gain a cost chip** rather than losing information.
3. **A continuous rail behind a column** expresses a fixed linear chain — fourth connector
   style.
4. **A column can be footed by an emblem** instead of a label.
5. **Cost appears in both the tile and the tooltip** on a planning screen.
6. **Disabled = desaturate + dim** — seventh confirmation, now on a button pair.

## Actions

- [ ] Record `RimRatio` per family: **1.4 flat / 1.9 carved**; use with `rpgui.md`'s
      gradient test as the greyscale gate's material check.
- [ ] Add `ConnectorStyle.Rail` (continuous, behind the nodes).
- [ ] Add a **column-footer emblem** slot to grid/tree containers.
- [ ] Locked tiles should **add** a cost chip, not just desaturate.
