# `rpgui3.png` — handheld JRPG dual-screen UI

**512 × 768** · live game screens (Rune-Factory-like, DS dual screen) · **pixel cream &
brown, welded plates** family
**Relevance:** **`rpg`**. The **densest** reference in the folder, and the source of the
`LabelValuePair` primitive.

---

## Widget 1 — `LabelValuePair` — the primitive this family is built from

Scanned H at y=160 on the `ATTACK  7` row.

```
 ┌────────────────────────────┬──┬──────────────┐
 │  ATTACK                    │▓▓│      7       │
 └────────────────────────────┴──┴──────────────┘
   dark brown  L=0.19            2px   PURE WHITE L=1.00
   cream text                  keyline  dark text
        92px                              46px
```

| part | measured |
|---|---|
| label plate | `#4B2F17` **L=0.19 S=0.53**, cream letterforms |
| joint | **2px** near-black `#1F1001` L=0.06 |
| value plate | `#FEFEFE` **L=1.00 S=0.00** — pure white |
| outer keyline | 4px `#3E3425` |
| **label : value width** | **92 : 46 = 2 : 1** |

**Two plates of opposite polarity welded by a 2px keyline.** The label is dark-on-light
inverted; the value is maximum contrast. Nothing else is needed — no icon, no divider, no
alignment guide.

This is the single most reusable widget in the folder for dense information, and the kit
does not have it. `ATTACK/DEFENSE/COMBO/TYPE` all use it; so do `ATK/DEF/STR/INT/DEX/VIT`
in the inventory screen at half the size.

**Proportion to take: 2 : 1 label to value, 2px weld, value plate at maximum lightness.**

## Widget 2 — `TitledPanel` (`STATUS`, `EFFECT`, `INVENTORY`)

A **header bar with decorative end caps** across the panel's top, title centred in caps.
`STATUS`/`EFFECT` use brown bars; `INVENTORY` uses a blue/teal bar on the second screen.

The end caps are small ornamental flares, not part of the bar's rectangle — ornament again
standing in for a border.

## Widget 3 — `ItemHeader`

A cream plate with a **square icon well at the left** and the item name in large pixel
caps. The well is welded to the plate, same construction as widget 1.

## Widget 4 — `DescriptionPanel`

Cream plate with **ornate corner flourishes** and three lines of text. The flourishes sit
*inside* the plate's corners — the only inward-facing ornament in the folder (everything
else overhangs outward).

## Widget 5 — `ClockCluster` (top-right)

A compact stack: weather/portrait icon, a blue plate with day number + season (`07 HOL`),
a time readout (`AM 06:00`), and a **circular sun/moon dial** at the right.

Four unrelated readouts fused into one cluster with no gaps. This is the density technique:
**weld, don't space**.

## Widget 6 — `ElementChipGrid`

A 2 × 2 (status panel) and 1 × 4 (inventory) grid of chips, each a **coloured element icon
plus two stacked values** (`0` over `0`). Resistance/affinity display.

Two values in one chip, stacked vertically — a compact form the kit has no equivalent for.

## Widget 7 — `StatBar` (HP, RP, NEXT)

Scanned H at y=443.

| part | measured |
|---|---|
| fill (HP) | `#699962` **L=0.49 S=0.22** green; RP blue; NEXT red |
| fill length | 74px |
| value | **outside the bar, to its right**, on a white plate |
| track | cream with a dark keyline |

Third value placement in the folder: `gameui8` centred it **on** the fill, `rpgui1` put it
**below**, this one puts it **beside**. All three are valid; the widget needs a
`ValuePosition`.

## Widget 8 — `CategoryRail` (inventory, left)

Six or seven square icon buttons stacked vertically; the selected one is **cream/bright**
while the rest are **dimmed grey**. Selection by lightness + saturation, consistent with
the folder's availability rule.

## Widget 9 — `CharacterBlock`

Portrait well at the left, then `LV 10`, a segmented `NEXT` XP bar, and `HP`/`RP` bars with
values — four readouts in a block about 60px tall. Density again.

## Widget 10 — `StatGrid`

3 columns × 2 rows of `LabelValuePair` chips (ATK/STR/DEX over DEF/INT/VIT). Widget 1 at
half scale, tiled. Confirms the primitive scales.

## Widget 11 — `ItemGrid`

3 rows × 5 columns of slots, each with item art and a **count in the bottom-right corner**.
Same construction as `gameui8` and `rpgui2` — corner count is universal.

## Widget 12 — `SideRail` (inventory, right)

Three tall decorative buttons at the right edge — page/scroll affordances rendered as
ornamental bars rather than arrows or a scrollbar. Fourth scrolling idiom in the folder.

---

## Cross-widget rules

1. **`LabelValuePair`: two welded plates of opposite polarity, 2 : 1 width, 2px joint.**
   The most reusable dense-information widget in the folder.
2. **Weld, don't space.** This family achieves density by butting plates together with a
   keyline instead of separating them with gutters.
3. **Value position is a parameter** — on the fill, below it, or beside it.
4. **Ornament can face inward** (corner flourishes inside a plate) as well as outward.
5. **Header bars get decorative end caps** instead of a border.
6. **Two stacked values in one chip** is a valid compact form.
7. **Selection = bright vs dimmed** — consistent with the saturation rule.

## Actions

- [ ] Add **`LabelValuePair`** to the kit — 2 : 1, 2px weld, inverted label, max-contrast
      value. High priority; it unlocks every dense stat screen.
- [ ] Add `ValuePosition: OnFill | Below | Beside` to the progress widget.
- [ ] Add `ElementChip` (icon + two stacked values).
- [ ] Add inward corner ornament as a `KitAttach` anchor.
- [ ] Add `HeaderBar` with decorative end caps.
- [ ] Record the **weld-don't-space** density technique as a layout mode for dense panels.
