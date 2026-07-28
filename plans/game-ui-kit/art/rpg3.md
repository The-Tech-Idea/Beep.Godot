# `rpg3.png` — cartoon RPG equipment screen

**512 × 287** · live menu screen · **thick dark outline, saturated** family (same
construction as `citybuilder1`)
**Relevance:** **`rpg`**. Small source image, so measurements are coarser than elsewhere;
where a number could not be isolated reliably it is marked as observed rather than
measured.

---

## Widget 1 — `ItemSlot` grid (4 × 3, right panel)

Scanned H at y=85, V at x=325.

| state | measured |
|---|---|
| **available** slot | `#BAE76E`–`#88E6A1` **L≈0.67–0.72 S=0.65–0.72** — saturated green |
| **empty** slot | `#404147` **L=0.26 S=0.05** — desaturated dark grey |
| keyline | 1–2px near-black `#000D00`, `#0C0200` |
| slot pitch | **~55–58px** |

**Saturation again is the whole signal**: available S=0.72, empty S=0.05. That is now the
**fifth** reference measuring the same rule (gameui3 disabled S→0.01, citybuilder4 locked
S 0.22→0.04, rpg1 unaffordable, gameui9 unavailable text, here).

I consider this settled: **`KitState` availability is a saturation axis, not a lightness or
hue axis.**

## Widget 2 — `EquipSlot` (×3, left)

Three large square slots below the character, each with a thick dark keyline and a
**per-item coloured background** (blue-purple weapon, purple monster, green shield). A
**green ▼ arrow overhangs the first slot's top edge**, marking an available upgrade.

The arrow is an attachment, not part of the slot — the same overhang device, used as a
*state indicator* rather than as decoration or a badge.

## Widget 3 — `StatGrid` (character block)

A 2 × 2 block of `icon + value` pairs (sword 14, shield 19, lightning 16, gem 8) on a light
plate. Compact alternative to `gameui8`'s row-per-stat list, and closer to
`citybuilder3`'s `StatCluster` — but arranged as a **grid rather than a strip**.

Three layouts for the same content now exist in the folder:

| layout | source |
|---|---|
| one bar per stat | citybuilder1, gameui8 |
| N pairs in one pill (strip) | citybuilder3 |
| **2 × 2 grid** | **rpg3** |

A `StatCluster` widget should take an `Orientation`/`Columns` parameter rather than being
three widgets.

## Widget 4 — `LevelChip` / `HpChip`

`Lv.20` and `HP:152/25` in small dark plates beneath the stat grid. White text with a dark
outline.

## Widget 5 — `ItemDetailCard`

| part | observed |
|---|---|
| title | `⚡ Energy Blades` on a **blue ribbon** across the card's top |
| level | `Lv.14` plate at the ribbon's right end |
| stats | a row of four chips — `0`, `0`, `8`, `4` — each with an icon above |
| **buffed stats** | the last two chips are drawn as **green pentagons**; the unbuffed two are plain |

**Shape carries the buff, not just colour.** A modified stat becomes a green pentagon while
an unmodified one stays a plain numeral. That is greyscale-safe and is the first time in
the folder that a *value* changes silhouette to signal state.

Worth stealing: `KitShape` per state, not just per skin.

## Widget 6 — `CategoryTab` (×4, top-right)

Square tabs (backpack, lightning, potion, skull); the first is selected and lighter. Each
carries a **small green `+` badge straddling its top-right corner**, meaning "new items
here".

Same anchor as `rpg1`'s yellow `!` badge and `gameui8`'s red dot — **top-right corner
straddle is the universal "attention" anchor** across the folder.

## Widget 7 — `CurrencyReadout`

Coin icon + `4497` in white with a dark outline, **no plate**, top-left over the panel.
Confirms the plateless-with-outline treatment from `citybuilder5` and `gameui9`.

## Widget 8 — `BottomBar`

A tan strip carrying:
- a red **◄** pager button at the left
- a `Weapons` plate with a sword icon and a **refresh glyph** (sort/cycle)
- a large tan **`OK`** button at the right

Primary action is at the **right end of the bottom bar** — the same position as
`citybuilder3`'s and `racing4`'s confirm actions.

---

## Cross-widget rules

1. **Availability is a saturation axis** — fifth measurement (S 0.72 available vs 0.05
   empty).
2. **Shape can carry state** — buffed stats become pentagons.
3. **Top-right corner straddle is the universal attention anchor** (`!`, `+`, red dot).
4. **`StatCluster` needs a `Columns` parameter** — bar-per-stat, strip and 2 × 2 grid are
   one widget.
5. **Overhanging arrows mark actionable slots**, distinct from badges.
6. **Confirm sits at the bottom bar's right end.**

## Actions

- [ ] Make **saturation** the implementation of availability in `KitState` — settled.
- [ ] Allow `KitShape` to vary **per state** (plain → pentagon for a buffed value).
- [ ] `StatCluster` gains `Columns`.
- [ ] `EquipSlot` with an overhanging state arrow, `ItemDetailCard` with a title ribbon
      → catalogue.
