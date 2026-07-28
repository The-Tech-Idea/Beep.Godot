# `ui6.png` — spiral-notebook inventory

**1200 × 675** · live menu screen · **hand-drawn notebook** family
**Relevance:** `survival`, `puzzle`, `rpg`. The most radical construction in the folder:
**there are no plates, no frames and no fills anywhere.**

---

## The family: paper texture + variable-pressure pencil

Scanned H at y=400 across the page and grid.

| element | measured |
|---|---|
| paper | `#CFBEB3`–`#D2C1B6` **L=0.75–0.77 S=0.23–0.26** |
| paper grain | lightness wanders **±0.03** across the scan — the texture *is* the variation |
| pencil stroke (dark) | `#68574D` **L=0.35** = **0.46 × paper** |
| pencil stroke (light) | `#A18E80` **L=0.57** = **0.76 × paper** |
| stroke width | **1–3px**, varying |

**A hand-drawn stroke is not one darkness — it varies from 0.46 × to 0.76 × the surface
along its length, and its width varies 1–3px.** A constant-weight, constant-darkness line
reads as printed and kills the effect immediately.

This is the concrete recipe `rpgui2.md` asked for when it proposed `OutlineMode.HandDrawn`:

> **jitter the vertex positions, AND modulate the stroke's alpha between 0.46 × and
> 0.76 × of the surface, AND vary the width 1–3px** — all seeded from the control's
> identity so it is stable across frames.

Two of those three were already planned; the **alpha modulation** is the one I would have
missed, and it is what separates a wobbly line from a pencil line.

## Widget 1 — `SketchGrid` (4 × 5)

The item grid is **drawn, not built**. Cell boundaries are pencil strokes with **visible
overshoots at the intersections and gaps along the runs**. There are no cell fills, no slot
plates and no borders — items sit directly on the paper.

**Overshoot at corners is the tell.** A drawn grid crosses itself; a rendered grid meets
exactly. Any procedural version must extend each stroke 2–6px past the intersection.

## Widget 2 — `ItemEntry`

A painted item illustration on bare paper, with its **count handwritten beneath it**
(`10/10`, `8/10`, `1/2`, `1/4`) — not in a corner badge, not on a chip.

Contrast every other inventory in the folder (`gameui8`, `rpgui2`, `rpgui3`, `skilltree4`),
all of which put the count in the slot's bottom-right corner. Here it sits *below the
item, on the paper*, as if annotated afterwards.

## Widget 3 — `PaperTab` (×5)

Five tabs (`Items`, `Reminders`, `Notes`, `Memories`, `Alys`) **overhanging the page's top
edge**, drawn as the *same paper* as the page with only an outline and a slight offset to
distinguish them.

**No colour distinction whatsoever between selected and unselected tabs.** Selection is
carried purely by which page is showing. `Reminders` carries a **red `!`** — the only
saturated element on the entire screen.

That is the extreme end of `gameui4`'s "palette on one element": here the palette is a
single exclamation mark.

## Widget 4 — `DetailPage` (right)

Large item illustration · **name in red handwriting** · three lines of pencil description ·
a **`Take Out:` action with a small icon** at the bottom.

The action is a **handwritten label followed by an icon**, with no button plate at all. In
a family with no plates, an action is distinguished by being a verb and having an icon.

## Widget 5 — `NotebookPanel`

The container is a **physical spiral-bound notebook**: wire binding drawn down the left
edge, soft/torn page edges, and a **pencil resting on the right edge**, partly off the
page.

Second diegetic container in the folder after `gameui9`'s workbench, and it needs the same
two things: an **overlay art slot** (the pencil, drawn above the page and breaking its
bounds) and **furniture** (the spiral binding, structurally part of the container).

## Widget 6 — `Cursor`

A paper-plane/arrow cursor drawn in the same illustrated style. The cursor is part of the
art direction, not a system default — worth noting for any project committing to this
register.

---

## Cross-widget rules

1. **Hand-drawn strokes need three modulations**: position jitter, **alpha 0.46–0.76 ×**,
   and width 1–3px. All seeded per control.
2. **Drawn grids overshoot at intersections** by 2–6px; rendered grids do not.
3. **Counts can be annotations beneath an item**, not corner badges.
4. **Tabs can be materially identical** — selection carried by content alone.
5. **The palette can be a single element** (one red `!`).
6. **In a plateless family, an action is a verb plus an icon.**
7. **Paper grain = ±0.03 lightness variation**, which is the texture.

## Actions

- [ ] `OutlineMode.HandDrawn` ← jitter **+ alpha modulation 0.46–0.76 × + width 1–3px**;
      the alpha modulation is the part that makes it read as pencil.
- [ ] `SketchGrid` renderer with **intersection overshoot** (2–6px).
- [ ] `CountPlacement: CornerBadge | AnnotationBelow`.
- [ ] Confirm the **diegetic container** requirement from `gameui9` — overlay art slot plus
      furniture — now needed by two references.
