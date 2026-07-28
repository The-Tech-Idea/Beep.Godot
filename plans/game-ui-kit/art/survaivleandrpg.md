# `survaivleandrpg.png` — cosy storybook journal UI

**972 × 920** · live menu screen · **parchment, ink line art, botanical ornament** family
**Relevance:** **`survival`**, **`rpg`**, `puzzle`. A cosy register the folder otherwise
lacks, and the only **two-page spread** in it.

---

## Widget 1 — `QuestRow` (×5) and an inverted selection rule

Scanned V at x=250.

| element | measured |
|---|---|
| panel parchment | `#E1C49F` L=0.75 S=0.52 |
| **unselected row** | `#DABD98` **L=0.72 S=0.48** |
| **selected row** | `#A0A564` **L=0.53 S=0.26** — green |
| selected : unselected lightness | **0.74 ×** |
| selected : unselected saturation | **0.54 ×** |
| separator | 1–3px ink hairline `#130D00` L=0.04 |
| selected row height | ~113px |

**Selection here is darker AND less saturated.** Almost every other reference in the folder
makes the selected element brighter, more saturated, or both. This one shifts hue to green
and *reduces* both.

It works because the surrounding parchment is itself a warm, fairly saturated tan
(S=0.48) — so a calm, desaturated green reads as "resting on" the page rather than
shouting. **Selection contrast is relative to the surface, not absolute.** A skin with a
saturated surface should reach for a *calmer* selection, not a louder one.

That is selection mechanism **#15**, and the first that moves *away* from saturation.

Row anatomy: ink icon left · bold title · 2-line description · **gold diamond marker at the
right**; one row also carries a **progress fraction (`0 / 3`)** in that right slot. So the
right slot holds either a marker or a count.

## Widget 2 — `TabStrip` — two simultaneous selections

Five torn-paper tabs (`QUEST`, `SKILLS`, `ITEMS`, `EQUIP`, `STATUS`) **overhanging the
panel's top edge**.

| tab | fill |
|---|---|
| `QUEST` | **green** |
| `SKILLS` | **purple** |
| others | tan |

**Two tabs are lit at once**, because the panel is a two-page spread and each tab colours
the page it opens. Tab hue matches its page's accent (green quests, purple skills).

No other reference has a multi-select tab strip. It is a real requirement for book/journal
layouts and the kit's `TabStrip` assumes a single active index.

A small dark **`R1` chip** sits at the strip's far right — a controller-shoulder hint,
outside the tabs. Input hints as first-class chrome; `racing1`'s `KeyHint` was the only
other one.

## Widget 3 — `Panel` — a two-page spread

Torn edges all round, a visible **centre gutter/fold line** splitting it into two pages, and
**botanical vine ornament at the corners** growing inward.

Confirms `gameui9`'s `VineFrame` and `ui5`'s vine hanger in a third context. Vines are this
family's entire border treatment — there is no drawn frame at all.

## Widget 4 — `SectionHeader`

`QUESTS` / `SKILLS` / `AREA MAP` centred, **flanked by leafy vine flourishes on both
sides**. Fourth family to use flanking ornament instead of a rule (`rpg1`, `rpg2`,
`rpgui3`, here).

## Widget 5 — `SkillTile` (×4)

Rounded-square tiles with an ink border and a coloured fill (green, purple ×3), each with a
**label beneath and outside the tile**.

The selected tile (`Nature's Touch`) carries a **white pointer tab overhanging its left
edge** — a caret pointing at the selection, rather than a change to the tile itself.

**Selection #16: an external pointer.** The tile is untouched; a separate marker indicates
it. Useful when the tile's fill already encodes something else (here, element type).

## Widget 6 — `SkillDetailCard`

```
 ┌────────────────────────────────────────┐
 │ (icon)  Nature's Touch        MP 12    │  ← cost right-aligned, purple
 │         Restores a moderate amount of  │
 │         HP to one ally.                │
 │ · · · · · · · · · · · · · · · · · · ·  │  ← DOTTED divider
 │ Heal: 80 HP              [ Nature ]    │  ← stat left, element chip right
 └────────────────────────────────────────┘
```

| part | observed |
|---|---|
| cost | `MP 12`, **right-aligned, in the skill's accent hue** |
| divider | **dotted**, not solid |
| footer | numeric effect at the left, **element chip (green pill) at the right** |

Fifth dashed/dotted-stroke reference. Here it separates *description* from *hard numbers* —
a softer division than a rule, appropriate to the register.

## Widget 7 — `AreaMap`

Bordered illustration panel with an **irregular fog-of-war overlay** (a light shape
covering unexplored ground) and a **`!` marker in a circle** at the right edge.

The fog is a free-form silhouette laid over the art — same *overlay breaking the grid*
device as `gameui9`'s tools and `store1`'s mascot.

---

## Cross-widget rules

1. **Selection contrast is relative to the surface.** On a saturated parchment, the
   selected row is *calmer* (0.74 × lightness, 0.54 × saturation), not louder.
2. **A tab strip may have multiple simultaneous selections** in a spread layout.
3. **An external pointer can mark selection** without touching the item.
4. **Vines/ornament can be the entire border** — no frame drawn.
5. **Dotted dividers** separate prose from numbers.
6. **A row's right slot holds either a marker or a count.**
7. **Input hints (`R1`) are chrome** and need a home in the kit.

## Actions

- [ ] `TabStrip` must support **multi-select** for spread layouts.
- [ ] Add **external pointer** as a `KitState.Selected` renderer.
- [ ] Add `DividerStyle: Solid | Dotted`.
- [ ] Add an **InputHint** chip widget (`R1`, key glyphs) — second request after `racing1`.
- [ ] Record: **selection may reduce saturation** when the surface is already saturated;
      the greyscale gate must not assume selection increases contrast.
