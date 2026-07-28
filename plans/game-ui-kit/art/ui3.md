# `ui3.png` — items / inventory screen

**736 × 414** · live menu screen · same **saturated blue** family as `ui2.png`
**Relevance:** **`rpg`**, `shooter`, `cardgame`.

Same game family as `ui2.md`, so this document records what is **new or different**, plus
its own measurements.

---

## Widget 1 — `ItemSlot` grid (6 × 3)

Scanned H at y=125.

| part | measured |
|---|---|
| background | `#013BC1` **L=0.38 S=0.99** |
| slot fill | `#0533A1` **L=0.33 S=0.94** |
| **slot : background** | **0.87 ×** — recedes |
| **selection outline** | `#97BBFF` **L=0.80**, ~2px = **2.4 × the slot** |
| empty slots | same fill, **no art** — no ✕, no padlock, no ghost |

`ui2` measured 0.77 ×, this measures 0.87 ×. Both recede on a dark saturated surface,
confirming the rule derived in `ui2.md`.

**Selection is a 2px light outline at 2.4 × the slot's lightness** — mechanism #7
(`gameui9`'s white outline) with a number attached. Note it is *blue-white*, not pure
white: the outline takes the family's hue at high lightness rather than going neutral.

That is worth copying — a hue-matched bright outline sits better in a saturated skin than a
neutral white one.

**Empty slots are simply empty.** No ✕ (`gameui4`), no padlock, no ghost silhouette
(`survaivleandrpg1`). For a *bag* this is right — an empty bag slot means "nothing here",
whereas an empty *equipment* slot means "something belongs here". The two need different
renderings, which the kit should distinguish.

## Widget 2 — `EquipSlot` columns — and they prove the point

Two columns flanking the character:

| slot | rendering |
|---|---|
| available-empty | a **`+` glyph** — an invitation |
| **locked** | **padlock + the required level** (`Lv.11`, `Lv.15`) |
| filled | the item art |

So on the same screen: **bag empty = blank, equip empty = `+`, equip locked = padlock +
level.** Three empty-ish states, three renderings, chosen by meaning.

Fifth reference where a locked slot **states its requirement in words**.

## Widget 3 — `TabStrip`

A rounded blue bar holding five tabs: `All` (text) plus four icon tabs. The selected tab is
a **lighter plate with a white underline beneath it** — combining fill and underline
(`ui1`'s mechanism #17).

Selection cues **stack** here, as they did in `skilltree4`'s nav bar. Stacking is common
enough that the kit should treat selection as a *set* of renderers, not a single choice.

## Widget 4 — `CurrencyReadout` (×3)

Each with an icon overhanging the left and a **green `+` badge welded at the right cap** —
the `citybuilder1` placement, not `skilltree1`'s icon-anchored one. Both placements appear
within one art family, so it is a per-screen decision.

## Widget 5 — `CharacterPanel`

Star rating above (**3 filled gold, 2 empty grey**), character art, name below, then a
`LevelRow`: hex badge `8` · progress bar `5/60` · trophy `37`.

Identical `LevelRow` construction to `ui2` — hexagon, bar, icon+value welded in a line.
Confirms it as a reusable compound.

## Widget 6 — `ActionRow`

A green `Smart Equip` button and a blue **trash button**. Primary action is green and
labelled; the destructive one is icon-only and a different hue — **destructive actions are
smaller and unlabelled** here, the opposite of the "primary = bigger" rule and consistent
with it (the biggest control is the safest).

## Widget 7 — `TopBar`

Back chevron · title · three currency readouts · **hamburger menu at the far right**. The
hamburger is the only non-game affordance in the folder — worth noting that it does occur,
against the catalogue's general finding that game UI avoids application idioms.

---

## Cross-widget rules

1. **Selection outline takes the skin's hue at high lightness** (2.4 ×), not neutral white.
2. **Empty has three meanings** — blank (bag), `+` (available equip), padlock + level
   (locked equip) — and needs three renderers.
3. **Selection cues stack** (fill + underline here, elevation + size + fill in
   `skilltree4`). Model selection as a set.
4. **Panels recede on dark saturated surfaces** — 0.87 ×, confirming `ui2`'s 0.77 ×.
5. **Destructive actions are small and icon-only**; the primary is large and labelled.
6. **`LevelRow` (hex badge · bar · icon+value)** is a reusable compound across screens.

## Actions

- [ ] `KitState.Empty` splits into **`Blank` | `Invite` | `Locked(requirement)`**.
- [ ] Selection outline colour ← **skin hue at 2.4 × lightness**, not white.
- [ ] `KitState.Selected` becomes a **set** of renderers, not one choice.
- [ ] `LevelRow` → catalogue as a compound widget.
