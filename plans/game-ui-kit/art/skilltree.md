# `skilltree.png` — flat dark mobile skill tree

**430 × 734** · live menu screen (Clicker-Heroes-like) · **flat dark, hue-per-branch**
family
**Relevance:** **`rpg`**, `strategy`, `citybuilder`. The clearest **branch-colour** system
in the folder.

---

## The organising idea: one hue per branch

Three columns, three hues — **orange**, **green**, **blue** — applied to **both the node
tiles and the connector lines**. A player identifies a branch by colour before reading any
icon.

This is cheap, greyscale-hostile but colour-blind-survivable if the branches are also
spatially separated (they are — three distinct columns). The kit should offer
`BranchHue` on a tree/graph container, propagating to nodes and edges.

## Widget 1 — `SkillNode`

Scanned H at y=232, V at x=71.

| property | measured |
|---|---|
| node size | **~50px** square (x=89..141) |
| horizontal gap | **7–14px** |
| vertical gap | **~10px** |
| panel background | `#292931`–`#272A35` **L=0.18 S=0.09** — dark desaturated navy |
| owned node | full-colour art, S=0.66–1.00 |
| **locked node** | **dark silhouette** — art rendered near-black, no colour, no number |
| level number | small, in the node's **bottom-right corner** |

**"Locked" here is a dark silhouette** — the art's shape survives, its colour does not. That
is the **ninth** distinct unavailable-rendering in the folder, and unlike the others it
keeps the icon legible while making it unmistakably unavailable.

Ranked by how well they preserve information:

| rendering | preserves shape | preserves identity |
|---|---|---|
| dark silhouette (here) | ✓ | ✓ |
| desaturate (gameui3, rpg1) | ✓ | partly |
| grey stars / dim (gameui2) | ✓ | ✓ |
| ✕ overlay (gameui4/5) | ✗ | ✗ |
| padlock replaces content (gameui7) | ✗ | ✗ |

For a skill tree the silhouette is the right choice, because the player is planning a route
and needs to see what is ahead.

## Widget 2 — `Connector`

**Thin orthogonal lines** in the branch hue, running at right angles between nodes. No
arrowheads, no dashes, no gradient. They pass *behind* the nodes.

Contrast `gameui1`'s `ZoneMap`, which used **dashed** connectors, and `rpgui1`, which drew
a **connector with an arrowhead** as part of a two-node widget. Three connector styles:

| style | meaning |
|---|---|
| solid orthogonal (here) | prerequisite structure |
| dashed (gameui1, rpgui2 map) | traversable path |
| arrowed (rpgui1) | directed upgrade |

## Widget 3 — `Header`

Dark bar with a **green up-arrow button at the left**, `Skill Tree` centred, and a **white ✕
at the right**. Both controls are inside the bar, not straddling it.

## Widget 4 — `DescriptionBlock`

Four lines of body text on a slightly lighter dark plate at the top. No title, no icon —
purely explanatory copy. The kit has no "help text block" role.

## Widget 5 — `PointsRow`

`0 Skill Points Available` in **yellow** at the left, and a `Reset Skills` button at the
right (dark plate + circular-arrow glyph + label).

Yellow is used only here and for the currency — **yellow = a spendable resource** on this
screen.

## Widget 6 — `DetailPanel`

```
 ┌──────┬───────────────────────────────┐
 │portr.│ Pet: Lightning Burst   Lv. 1  │
 │ well │ +20 Pet Focus Tap Damage      │ ← green: current effect
 │      │ description, 3 lines          │
 ├──────┴───────────────────────────────┤
 │ Next Upgrade                         │ ← blue: heading
 │ +40 Pet Focus Tap Damage             │
 │                    ┌ 2 Skill Points ┐│ ← cost plate, welded ABOVE
 │                    │    Upgrade     ││ ← button, disabled (grey)
 │                    └────────────────┘│
 └──────────────────────────────────────┘
```

| part | observed |
|---|---|
| portrait well | square, left |
| title row | name at the left, **`Lv. 1` right-aligned** |
| current effect | **green** text |
| next-upgrade heading | **blue** text |
| cost plate | **welded directly above** the button, same width |
| button state | **grey/disabled** because 0 points are available |

**Colour codes the tense**: green = what you have now, blue = what you would get. That is a
semantic use of hue the kit's `UiSurface.Role` can express (`Success` / `Info`) but which
nothing in the catalogue currently applies to *text runs inside a detail panel*.

The **cost-plate-welded-above-the-button** is the ninth appearance of the welded-footer
idiom, here inverted to a welded *header*.

---

## Cross-widget rules

1. **One hue per branch**, applied to nodes and connectors alike.
2. **Locked = dark silhouette** — the best unavailable-rendering for a planning screen.
3. **Three connector styles carry three meanings**: solid = prerequisite, dashed = path,
   arrowed = directed upgrade.
4. **Green = current, blue = next** in a detail panel.
5. **Yellow = spendable resource.**
6. **A cost plate can weld above a button**, not only below a card.
7. **Node grid: ~50px tiles, 7–14px gutters** on a 430px-wide screen — roughly 12 % of
   width per node.

## Actions

- [ ] Add `BranchHue` to a tree/graph container, propagating to nodes and edges.
- [ ] Add `KitState.Locked` variant **`Silhouette`** (render art at near-black, keep shape).
- [ ] Add `ConnectorStyle: Solid | Dashed | Arrowed` to the tree widget.
- [ ] Add a **help-text block** role.
- [ ] `DetailPanel` with green/blue tense-coding and a welded cost header → catalogue.
