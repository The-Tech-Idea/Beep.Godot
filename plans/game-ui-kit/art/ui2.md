# `ui2.png` — hero detail screen (Brawl-Stars register)

**736 × 414** · live menu screen · **saturated blue, darker-than-background plates** family
**Relevance:** **`rpg`**, `shooter`, `cardgame`.

---

## Widget 1 — `StatRow` (×5) — measured

Scanned H at y=210.

| part | measured |
|---|---|
| background | `#015B9F` **L=0.31 S=0.99** |
| row plate | `#013D77` **L=0.24 S=0.98** |
| **plate : background** | **0.77 ×** — the plate is *darker* than what it sits on |
| icon tile | ~18px, red `#FF3847`/`#F75611` L≈0.51, at the plate's **left, overhanging it** |
| label | small caps, white |
| value | large, white, left-aligned after the label |
| row width | ~180px |

**Third reference with a plate darker than its background** (`racing4` 0.67 ×, `rpgui1`,
here 0.77 ×). All three are dark, saturated screens. The pattern is now clear:

> On a **dark saturated** surface, panels **recede** (0.67–0.80 ×).
> On a **light or neutral** surface, panels **raise** (1.13–1.40 ×).

That is a rule the kit can apply automatically from the parent surface's lightness, rather
than requiring the skin author to pick a direction.

Row anatomy — **coloured icon tile overhanging a dark plate, then a small-caps label, then a
large value** — repeats five times with only the icon hue changing (red critical, blue
attack, green defense, red health, blue speed). One geometry, five palette entries.

## Widget 2 — `RarityChip`

`EPIC` in a small **purple** plate above the hero name. Rarity as a labelled chip, not just
a border colour — belt and braces, matching `gameui8`'s tooltip which also doubled hue with
a word.

## Widget 3 — `LevelRow`

A **hexagonal level badge (`8`)** at the left, then a progress bar (`3/50`), then a trophy +
`20`. Three different readouts welded into one line, each a different shape — hexagon, bar,
icon+value.

## Widget 4 — `DescriptionText`

Three lines with **inline coloured runs** (`10 damage` in orange against white body text).
Third reference needing **per-run text roles** (`rpgui2` combat log, `skilltree3` progress
text, here).

## Widget 5 — `RunePanel`

`RUNE` label above two rows of **pentagon slots**:

| slot | state |
|---|---|
| 3 filled | red / yellow / green runes |
| 1 empty | white outline, nothing inside |
| 2 locked | **padlock + the requirement level (`Lv15`, `Lv20`) printed on the slot** |

**The locked slot states its unlock level.** Fourth reference to explain the block in words
rather than just disabling (`citybuilder4`, `gameui9`, `gameui8`, here). This is clearly the
convention in shipped games, and the kit's `KitState.Locked` should carry a **requirement
string** as a first-class field.

Pentagon slots — third non-rectangular slot shape in the folder after `gameui1`'s hexagons
and `citybuilder5`'s hex buttons.

## Widget 6 — `SkillsPanel`

Two **circular skill icons**. The first carries a **`20` cost chip overhanging its bottom
edge**; the second is **locked with a padlock badge overhanging its bottom-right**.

Note both attachments hang off the *bottom* of a circular host, at different anchors —
confirming `gameui8`'s finding that circular hosts need several anchor points.

## Widget 7 — `UpgradeButton`

Green plate reading `⊙ 2,500`, with a **`Lv9 Upgrade` tag overhanging its top edge**.

Fifth cost/label placement in the folder:

| placement | source |
|---|---|
| welded above the button | skilltree |
| welded below the card | store |
| inside the button's lower half | skilltree3 |
| **overhanging the button's top as a tag** | **ui2** |
| in the tooltip's corner | Upgrades |

## Widget 8 — `SelectButton`

Large yellow plate, `Select`. Primary action = **larger + the only yellow element** — both
size *and* colour here, where earlier references used one or the other.

## Widget 9 — `TopBar`

Back chevron (dark rounded square) · title · three currency readouts · **round profile
button with a red `2` badge**. The badge straddles the profile button's top-right — the
folder's universal attention anchor, seventh sighting.

## Widget 10 — `PagerChevron`

Thin white `‹` and `›` flanking the hero art, with no plate at all. Contrast `store.png`'s
chunky green chevrons — same control, opposite weight, chosen to keep attention on the art.

---

## Cross-widget rules

1. **Panel direction follows the parent surface**: recede (0.67–0.80 ×) on dark saturated
   backgrounds, raise (1.13–1.40 ×) on light ones. Derivable automatically.
2. **`KitState.Locked` needs a requirement string** — fourth reference.
3. **Per-run text roles** — third reference.
4. **Circular hosts need multiple attachment anchors** — second reference.
5. **Rarity is doubled** (chip word + hue) — second reference.
6. **One row geometry, N palette entries** for a stat list.
7. **Pager weight is a deliberate choice** — chunky when paging is primary, hairline when
   the content is.

## Actions

- [ ] Derive panel lightness direction from the **parent surface** rather than the skin.
- [ ] `KitState.Locked` gains a **requirement text** field — promote to priority (4 refs).
- [ ] Add **per-run text roles** to the label widget — promote to priority (3 refs).
- [ ] `KitAttach` on circular hosts: support bottom-centre and bottom-right simultaneously.
- [ ] Record the five **cost placements** as a `CostPlacement` enum.
