# `skilltree4.png` — merge/idle RPG equipment screen

**1200 × ~1743** · live menu screen · **purple-navy, saturated rarity tiles** family
**Relevance:** **`rpg`**, `cardgame`.

---

## Widget 1 — `ItemTile` — rarity by fill, measured

Scanned H at y=880.

| property | measured |
|---|---|
| tile size | **~139px** square |
| pitch | **149px** → gutter **10px** |
| rarity fill (epic) | `#E272FD` **L=0.72 S=0.97** magenta |
| rarity fill (rare) | blue, rows 3–4 |
| gutter colour | `#433A54` L=0.28 S=0.19 |

**Corner grammar, consistent across every tile:**

| corner | content |
|---|---|
| top-left | small **category icon** (weapon / armour / helm / boot / ring) on a darker chip |
| top-right | **`Lv.n` plate** |
| centre | item art |
| bottom-left (equipped tiles only) | **rank letter** (`S`) |

Four independent metadata slots on one 139px tile, and none of them collide with the art.
That is the densest corner usage in the folder and it is worth copying verbatim: the kit's
`ItemTile` should expose **four corner slots** plus a centre.

Rarity is carried **entirely by the tile's fill hue** at a constant lightness (~0.72) and a
constant saturation (~0.97). So the ladder is a **hue rotation at fixed L and S** — which
is exactly how a palette should be authored, and directly usable for the `rpg` project's
rarity set alongside `rpgui.png`'s grey/green/blue/purple/orange swatches.

## Widget 2 — `EquipSlot` (3 per side of the stage)

Gold/orange tiles flanking the character view, each with the same corner grammar
(category icon top-left, `Lv.70` top-right) plus a **sparkle overlay** marking equipped
status.

**Equipped = gold fill + sparkle**, versus inventory tiles which take the rarity hue. So
the fill encodes *rarity* in the grid and *state* in the slots — the same property doing
different jobs in two containers on one screen.

## Widget 3 — `TabStrip` (`Equipment` / `Skin`)

Selected = a **filled gold plate with dark text**; unselected = **no plate at all**, white
text directly on the bar.

Selection mechanism #14: **the plate itself appears**. Related to `gameui8`'s pill and
`skilltree3`'s label reveal — three variants of "selection materialises something that was
not there".

## Widget 4 — `StatChip` (under the character)

Light plate with an **icon overhanging the left cap** and a value (`68,26K`, `353,92K`).
Same construction as every currency readout in the folder, at stat scale.

Note the values use a **comma decimal and a K suffix** — abbreviated large numbers are the
norm in idle games, and the kit has no number-formatting concept.

## Widget 5 — `PlayerBadge` (top-left)

Avatar in a **green rounded frame overhanging the top bar's bottom edge**, with a name plate
beside it. The avatar breaks the bar's boundary — overhang again, now on a full-width bar.

## Widget 6 — `CurrencyReadout` (×2)

Gem `315` and coin `582,63M`, icon overhanging the left cap, no `+` badge on this screen.

## Widget 7 — `ActionPill` (`By Quality`, `Merge`)

**Green rounded pills** on the panel's bottom bar. Green = actionable, consistent with the
folder-wide role usage.

## Widget 8 — `NavBar` (5 tabs)

| property | observed |
|---|---|
| tabs | Store · Facility · Battle · **King** · Manor |
| unselected | dark plate, icon above a label |
| **selected (`King`)** | **gold plate, taller than the bar, overhanging it upward** |
| badge | `Battle` carries a **red `!` straddling its top-right corner** |

**Selection by elevation + size + fill**, all three at once — the strongest selection
treatment in the folder, appropriate because this is the primary navigation.

Compare `gameui9`, where the selected tab was merely raised. Here the tab is raised **and**
enlarged **and** recoloured. The kit should let a skin stack these rather than pick one.

---

## Cross-widget rules

1. **`ItemTile` needs four corner slots** (category, level, rank, count) plus a centre.
2. **Rarity = hue rotation at fixed L≈0.72, S≈0.97.** Author palettes this way.
3. **One property can encode different things in different containers** — fill = rarity in
   the grid, state in the equip slots.
4. **Selection #14: the plate materialises.** Three folder variants of "selection adds
   something".
5. **Primary navigation stacks selection cues** — elevation + size + fill together.
6. **Abbreviated numbers (`582,63M`, `68,26K`) are the norm** in idle/merge games; the kit
   needs a number-formatting policy.
7. **Gutter : tile = 10 : 139 ≈ 0.07** for a dense inventory grid.

## Actions

- [ ] `ItemTile` ← four corner slots + centre; document the corner grammar.
- [ ] Rarity palette ← hue rotation at fixed L/S; reconcile with `rpgui.png`'s five swatches.
- [ ] `KitState.Selected` ← allow **stacking** elevation + size + fill.
- [ ] Add a **number-formatting policy** (K/M/B abbreviation, locale separator) to the kit.
- [ ] Record dense-grid gutter ratio **0.07**.
