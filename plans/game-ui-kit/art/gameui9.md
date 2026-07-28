# `gameui9.png` — pixel-art survival crafting screen

**1200 × 834** (letterboxed mobile capture) · live gameplay screen · **pixel-art
diegetic** family
**Relevance:** **`survival` above all**, plus `citybuilder`. The only reference in the
folder where the UI container is a **rendered object in the world** rather than an
abstract plate.

---

## The family finding: a diegetic panel

The crafting screen is a **wooden workbench with legs**, and a **saw and hammer lie across
it** at an angle, overlapping the content. The tabs sit on the bench's top edge like real
tabs on a real board.

This is a different construction principle from every other reference:

| approach | examples |
|---|---|
| abstract plate + frame | citybuilder1–5, gameui1–8 |
| **the panel IS an object** | **gameui9** |

For the kit this means a panel must be able to accept **decorative overlays that break its
own bounds** (the saw runs off the bench's left edge) and **structural extras** (legs
below). Practically: a `KitPanel` needs an art-overlay slot drawn *above* its content, and
a "furniture" slot drawn *below* its rect.

---

## Widget 1 — `HotbarSlot` (×9) and the selection rule

Scanned H at y=675.

| part | measured |
|---|---|
| slot interior | `#845E55` **L=0.42** |
| dark border | `#2C1610` L=0.12 |
| light bevel | `#B08E6F` L=0.56 = **1.33 × interior** |
| interior width | **49px**; slot pitch **~85px** |
| **selection** | a **3px pure-white rectangle** `#FFF7EA` L=0.96, drawn **outside** the slot |
| slot number | small pale numeral in the top-left corner |

**Selection mechanism #7** in the folder, and by far the cheapest: a white outline around
the host, no fill change, no glow, no size change. It survives greyscale, works on any
plate colour, and costs one rectangle.

For a pixel-art or high-density skin this is the right default. Recommend it as
`KitState.Selected`'s fallback renderer.

## Widget 2 — `RecipeBoard`

Scanned V at x=500.

| part | measured |
|---|---|
| keyline | **3px** `#2E2A23` L=0.16 |
| fill | `#4C875A` **L=0.41 S=0.28** — a muted green board |
| grid lines | `#5C9D6A` L=0.49 = **1.20 × fill** |
| grid pitch | **66–67px** |

A **visible grid at 1.20 × the fill** is the board's texture. It is not decoration — the
recipe rows land on it, so the grid doubles as an alignment guide the player can see.

## Widget 3 — `RecipeList`

Item names in a pixel font on the board's left half. **Unavailable recipes are drawn in a
darker green** (`Makeshift Stone Saw`, `Simple Fishing Pole`, `Stone Hammer`,
`Stone Pickaxe`) against the available `Iron Axe` and `Stone Axe`.

Availability is carried by **text lightness alone** — no icon, no strike-through, no
padlock. The eighth distinct "unavailable" rendering in the folder.

## Widget 4 — `RecipeDetail` pane

| row | content |
|---|---|
| header | item icon + `Iron Axe` + **`x1` right-aligned** |
| hint | small anvil icon + `Use Anvil to craft!` — a **requirement line** |
| rule | 1px horizontal divider |
| ingredients | icon + name, each with a **quantity as a small subscript at the icon's lower-right** |

**The quantity subscript is new.** Every other reference puts a count in a corner badge or
a separate column; here it is a tiny numeral tucked under the icon, which is what lets four
ingredient rows fit in a narrow pane.

The `Use Anvil to craft!` line is a **requirement stated in words**, matching
`citybuilder4`'s `2 Units Max` and `gameui8`'s `???` quest. Three unrelated games all
explain *why* rather than just disabling.

## Widget 5 — `CraftButton`

Scanned H at y=522.

| part | measured |
|---|---|
| width | **123px** (x=642..765) |
| fill | `#C0A97B` L=0.62 |
| text | dark brown L=0.37–0.44 — **dark-on-light**, inverted from the screen |
| keyline | **4px** dark `#27262B` L=0.16 |
| position | bottom-right **of the board**, inside it |

The only dark-on-light element on the screen. Confirms the rule from `gameui6`/`gameui7`:
the element that must be acted on or read exactly flips polarity.

## Widget 6 — `CategoryTabStrip`

Nine tan square tabs with tool icons, sitting **on the bench's top edge**. Two details
worth taking:

- the **selected tab is raised higher** than the others — selection by *position*, not
  colour (mechanism #8)
- the selected tab carries a **tooltip label above it** (`Tools`), i.e. the name is only
  shown for the active tab

Selection-by-elevation is a natural fit for a physical/diegetic skin and is greyscale-safe.

## Widget 7 — `VineHudPanel` (top-left)

A stat cluster framed with **plant/vine art growing around it**:

- heart icon + red bar
- lightning icon + tan bar
- a sun/moon dial
- a `02:41 pm` time plate
- a globe icon at the right, overhanging the frame

Confirms `ui5.png`'s `VineFrame` hanger with a real in-game use. The frame is organic and
**asymmetric** — the vines are heavier on one corner.

## Widget 8 — `Minimap` (top-right)

A wood-framed rectangle **hanging** on the screen edge, showing terrain. Simpler than
`gameui8`'s circular minimap — no compass, no zoom buttons.

## Widget 9 — `StatPanel` (bottom-left)

A second vine-framed vertical panel holding two bars (blue, white). Splitting the stat
cluster across two corners rather than one HUD block.

## Widget 10 — `BackpackButton`

A framed square with a bag icon, bottom-left, in the same vine/wood frame vocabulary.

## Widget 11 — `XPBar`

A thin green bar spanning the hotbar's full width, immediately below it. No frame, no
label, no value — the least decorated element on the screen.

## Widget 12 — `DamageNumber`

`113` in large red pixel numerals with a black outline, floating in world space with no
plate. Confirms `gameui1`'s FloatingNumber; here it is much larger relative to the screen.

## Widget 13 — `ScreenTitle`

`CRAFTING` in a display pixel font, **above the bench, with no plate** — it reads as a
caption on the scene rather than a header on a panel.

---

## Cross-widget rules

1. **A panel can be a world object.** It needs an art-overlay slot (tools laid on top,
   breaking bounds) and a furniture slot (legs below the rect).
2. **Selection #7: a 3px white outline outside the host.** Cheapest and most portable
   mechanism seen; recommend as the kit's fallback.
3. **Selection #8: raise the selected tab.** Position instead of colour.
4. **Availability by text lightness alone** is sufficient in a dense list.
5. **Quantity as a subscript under the icon** fits more rows than a badge or a column.
6. **State the requirement in words** — third unrelated game to do it.
7. **A visible grid at 1.20 × the fill** doubles as texture and alignment guide.
8. **The actionable element flips polarity** — fourth confirmation.

## Actions

- [ ] `KitPanel` gains an **overlay art slot** (drawn above content, may exceed bounds) and
      a **furniture slot** (drawn below the rect).
- [ ] `KitState.Selected` fallback ← **3px outline outside the host**.
- [ ] Add **selection by elevation** for tab strips.
- [ ] Add a **quantity subscript** anchor to `KitAttach` (icon lower-right, inside).
- [ ] Add a **requirement line** slot to card/detail widgets — text, not just a lock.
- [ ] `RecipeBoard` (grid at 1.20 ×), `RecipeDetail`, `HotbarSlot`, `VineHudPanel`
      → catalogue, **priority for the `survival` project**.
