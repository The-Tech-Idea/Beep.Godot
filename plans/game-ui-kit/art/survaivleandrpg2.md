# `survaivleandrpg2.png` — pixel life-sim inventory

**736 × 414** · live menu screen (Stardew-like) · **pixel cream & brown, three-surface**
family
**Relevance:** **`survival`**, `rpg`, `citybuilder`.

---

## Widget 1 — `InventoryPanel` — a three-surface lightness ladder

Scanned H at y=200.

| surface | measured | ratio to panel |
|---|---|---|
| outer border | `#A77639` **L=0.44 S=0.49** | **0.70 ×** |
| panel interior | `#C6A57C` **L=0.63 S=0.39** | 1.00 |
| **item-grid well** | `#F9E9C8` **L=0.88 S=0.80** | **1.40 ×** |

**The content well is LIGHTER than its panel**, not darker. Most of the folder recesses
content (0.59–0.80 ×); this family raises it to **1.40 ×**, using the panel as a dark mat
around a bright page.

Both directions are legitimate. What matters is the *step size*: 0.70 / 1.00 / 1.40 is
roughly a constant ratio of **1.4** between adjacent surfaces, which is what keeps three
surfaces distinguishable in a 736px pixel-art screen.

Recommend the kit express surfaces as a **ladder with a step ratio**, rather than absolute
lightnesses — it survives palette swaps.

## Widget 2 — `InputHintBar` (top)

Three hints across the top: a button glyph + `SELECT`, `USE / EQUIP`, and `L2 + ✛ SPLIT
STACK`. Dark rounded plates, white text, real controller glyphs.

**Third reference with input hints** (`racing1`'s `KeyHint`, `survaivleandrpg`'s `R1` chip,
here as a full bar). This is now clearly a widget class the kit is missing, and this
reference gives it its most complete form: **glyph + label, several per bar, docked to the
screen top**.

Note `L2 + ✛` composes two glyphs and a `+` — the hint must support **chords**, not just a
single key.

## Widget 3 — `ClockWidget` (top-right)

Dark plate with `SAT 20` over `10:20`, plus a **circular clock dial with an `AM` badge
overhanging its left edge**. Label-above-value again (`racing1`, `rpgui2`), here stacked
inside one plate with an attached dial.

## Widget 4 — `TabStrip` — split into two groups

~8 icon tabs at the **left** (content categories) and 3 at the **right** (`?`, wrench,
book — help/settings/log), separated by a gap.

**Content tabs and utility tabs are separated by whitespace within one strip.** Neither
group has a container; the gap alone does the grouping. That is a cleaner solution than a
second bar and the kit's `TabStrip` should support **groups with a gutter**.

The first tab is selected — lighter and slightly raised.

## Widget 5 — `CharacterPanel`

Circular portrait well containing the character sprite, with a **left chevron** at the
panel's edge for cycling. Below it a **stat grid**: 2 columns × 4 rows of `label + value`
(`END 78`, `CAT 40`, `COG 60`, `SOC 30` / `SPEED +0%`, `ENER.D +0%`, `DAM.R 0`,
`HEAT.R 0`).

Same `LabelValuePair` primitive measured precisely in `rpgui3`, here at pixel scale in a
2-column grid — confirming both the primitive and `rpg3`'s finding that `StatCluster`
needs a `Columns` parameter.

## Widget 6 — `EquipColumn`

A narrow vertical strip of 4–5 equipment slots between the character and the item grid.
Slots are the panel tone with a darker outline; empty ones show a **ghost silhouette** of
the equipment type — the same device measured in `survaivleandrpg1`.

## Widget 7 — `ItemGrid`

~10 columns × 4 rows of slots on the bright well. Slots have no visible border; the grid is
implied by even spacing of the item sprites.

**A grid can be implied by spacing alone** when the well is a flat, bright surface. Cheapest
grid in the folder.

## Widget 8 — `CurrencyRow`

Above the grid: a magnifier/search chip, a **long value plate (`3530`)**, and a second
smaller plate (`0`). The long plate's width is fixed regardless of the value's digit count,
so the number is right-aligned in a stable box.

## Widget 9 — `Hotbar` (bottom)

Dark strip carrying a heart icon + **red bar**, a lightning icon + **yellow bar**, then ~8
slots. The selected slot is filled **orange/gold**.

Selection = fill with the accent (mechanism #10, `racing3`). Vitals and hotbar share one
strip — a compact bottom-chrome pattern for survival games.

## Widget 10 — `FloatingButton`

A small pixel button with an up-arrow, **outside the panel** at the right. Screen chrome
rather than panel content.

---

## Cross-widget rules

1. **Surfaces form a ladder with a ~1.4 step ratio**: 0.70 / 1.00 / 1.40. Express as a
   ladder, not absolute values.
2. **A content well may be lighter than its panel** — the panel becomes a mat.
3. **Input hints are a real widget class** and must support **chords** (`L2 + ✛`).
4. **A tab strip can hold two groups separated by a gutter**, with no container.
5. **A grid can be implied by spacing** on a flat bright well.
6. **Vitals + hotbar share one bottom strip** in survival layouts.
7. **Ghost silhouettes in empty equipment slots** — second confirmation.

## Actions

- [ ] Express skin surfaces as a **ladder + step ratio (~1.4)**, direction configurable.
- [ ] Add an **InputHint** widget with chord support — third request; promote to priority.
- [ ] `TabStrip` gains **groups with a gutter**.
- [ ] `StatCluster.Columns` — second confirmation after `rpg3`.
- [ ] Record the **vitals+hotbar bottom strip** as a `survival` layout template.
