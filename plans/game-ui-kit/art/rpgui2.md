# `rpgui2.png` — hand-drawn ink roguelike deckbuilder kit

**735 × 490** · asset sheet · **hand-drawn ink on parchment** family (Slay-the-Spire
register)
**Relevance:** **`rpg`**, **`cardgame`**, `puzzle`. The most *complete screen vocabulary*
in the folder — this one sheet covers HUD, cards, dialogs, shop, inventory, map, menu,
tooltip and log.

---

## Family construction

| property | measured |
|---|---|
| parchment plate | `#DBC096`–`#DBBE93` **L=0.72 S=0.49** |
| ink outline | **1px**, `#291806`, `#120C00` — near-black, hand-wobbled |
| background | `#434343` L=0.26 neutral grey |
| tooltip plate | **dark** — the only dark plate on the sheet |

**A 1px ink line and a flat cream fill is the entire material.** No bevel, no gloss, no
gradient, no shadow. This is the cheapest family in the folder to render and one of the
most characterful — the identity is entirely in the **wobble of the stroke** and the
silhouette.

For the kit that means a `HandDrawn` outline mode: constant 1–2px, with a small
per-vertex jitter seeded from the control's identity so it is stable frame to frame.

---

## Widget 1 — `Card` (×4: Strike, Defend, Fireball, Cleave)

Scanned V at x=208.

```
 ┌──────────────┐  ← 1px ink
 │▓▓ TITLE ▓▓▓▓▓│  ← coloured banner, 18px = 0.14 of the card
 │              │
 │     ICON     │
 │  ──────────  │
 │ description  │
 │(1)           │  ← cost pip, ~14px, overhangs the bottom-left corner
 └──────────────┘
```

| part | measured |
|---|---|
| card height | **130px** (y=64..194) |
| title banner | **18px = 0.14 × card**, red `#A65449` L=0.47 S=0.39 (green for Defend) |
| body | parchment L=0.72 |
| cost pip | **~14px** circle, coloured, at the **bottom-left, straddling the edge** |
| ink | 1px |

**Banner hue encodes card type** — red attack, green defence. Same geometry, palette on
the banner only. Sixth reference confirming neutral-geometry / palette-on-one-element.

## Widget 2 — `StatBar` (×2, top-left)

Scanned H at y=25.

| part | measured |
|---|---|
| icon plate | separate small parchment square at the left |
| bar fill | `#B14938` **L=0.46 S=0.52** (red) / green for the second bar |
| empty track | `#403629` **L=0.21 S=0.22** = **0.46 × the fill** |
| bar length | ~113px |
| value | `36 / 50` centred **on** the bar |

Track at 0.46 × fill, and both are the parchment family's own browns — the empty portion
is a dark tint of the surface, not grey. Same rule as `rpg1`'s unfilled pips.

## Widget 3 — `InfoChip` (`Floor 7`, `Gold 125`, `Turns 23`)

Rounded parchment plates carrying a **label above and a value below**, with an optional
icon beside the value. Same label-above-value atom as `racing1`, in a completely different
family.

## Widget 4 — `CharacterPanel`

Title, portrait at the left, and a **stat list at the right** — six `icon + value` rows
(HP, energy, block, gold, and two percentages). No bars, no plates per row — just aligned
icon/value pairs on the panel.

## Widget 5 — `EffectsPanel`

Title plus a row: status icon + name + stack count + a duration icon. **Buff/debuff display
is a distinct widget** from the stat list, and the kit has nothing for it.

## Widget 6 — `CombatLog`

Title plus four lines of **colour-coded text** — red for damage taken, blue for gains. No
icons, no plates: the log is pure coloured text.

A **text-role set beyond good/bad** is implied: damage, gain, neutral, and a subject
highlight. `UiSurface.Role` covers the semantics; the log needs them applied to *runs of
text*, not to whole controls.

## Widget 7 — `Tooltip` (`Burn`)

The **only dark plate on the sheet**: dark fill, light text, an icon beside the title.
Polarity inverts specifically for the transient overlay, so it reads as floating above
everything.

Fifth reference to flip polarity for one element class (gameui6 score, gameui7 rows,
gameui9 button, citybuilder4 lock, here).

## Widget 8 — `DialogPanel` (`Treasure Chest`)

Title, **✕ straddling the top-right corner**, an icon, body text, and **two action buttons
side by side** — green `Open`, red `Leave`. Confirm/deny as a coloured pair.

## Widget 9 — `InventoryPanel`

Title, ✕ close, and a **grid of slots**; filled slots show art, some with a **count in the
bottom-right corner** (`125`). Empty slots are the plain recess.

## Widget 10 — `ShopPanel`

Title, ✕ close, four items each with **name above / icon / price with coin icon below**,
and a **`Refresh 20` button** carrying a refresh glyph at the panel's bottom.

The refresh-for-a-price control is a genuinely game-specific widget the catalogue lacks.

## Widget 11 — `MenuButtonList`

`Continue` (green), `New Run`, `Options`, `Quit` — stacked parchment plates with the
primary one **filled green**. Here primary is colour-differentiated, matching `rpg2` rather
than `rpg1`.

## Widget 12 — `LevelUpPanel`

Title + subtitle (`Choose a card`) + three **mini-cards**. The card widget at a second
scale — same construction, ~0.7 × size, proving the card is scale-independent.

## Widget 13 — `MapPanel` (`The Forgotten Depths`)

Nodes (`?`, chest, combat) joined by **dashed lines** in a branching layout.

**Fourth dashed-stroke reference** (citybuilder2, gameui1, gameui8's leader dots, here).
Dashed consistently means *path* or *provisional*.

## Widget 14 — `CompactStatRow`

Heart `36/50`, energy `3/3`, shield — small chips in a row, a condensed version of widget 2
for tight spaces. The sheet ships **both a full and a compact form of the same readout**,
which is a good pattern for the kit: one widget, two densities.

## Widget 15 — `ShapeSwatchRow`

A row of **shape swatches: square, rounded, hexagon, circle, star**, plus a vertical bar.
The kit's author is explicitly declaring the supported silhouettes.

Directly relevant: `KitShape` should be able to render **any of these in the ink material**
without new art, because the material is a 1px stroke.

## Widget 16 — `BottomIconBar`

A long row of ~16 small square icon buttons — the sheet's icon library at button size.

---

## Cross-widget rules

1. **A 1px hand-wobbled ink line plus a flat fill is a complete material.** Add
   `OutlineMode.HandDrawn` with seeded jitter.
2. **Empty/track segments are a dark tint of the surface** (0.46 ×), never grey.
3. **Banner hue encodes type**; geometry stays fixed.
4. **Tooltips invert polarity** — fifth confirmation of "one element class flips".
5. **Colour-coded text runs** are needed inside a log, not just per-control roles.
6. **Ship a full and a compact form** of a readout.
7. **Dashed = path** — fourth confirmation.
8. **Buff/debuff display is a distinct widget** from a stat list.

## Actions

- [ ] Add `OutlineMode.HandDrawn` — 1–2px, seeded per-vertex jitter, stable across frames.
- [ ] Add `EffectsRow` (status icon + name + stacks + duration) to the catalogue.
- [ ] Add `LogView` with per-run text roles.
- [ ] Add `RefreshForPrice` control.
- [ ] Every readout gets a `Density: Full | Compact`.
- [ ] Verify `KitShape` renders square/rounded/hex/circle/star in the ink material — this
      sheet is the acceptance test for shape coverage.
