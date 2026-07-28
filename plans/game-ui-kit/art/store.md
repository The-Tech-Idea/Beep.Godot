# `store.png` — farm-game store screen

**758 × 624** · live menu screen · **wood frame + cream card** family
**Relevance:** every genre — the folder's canonical **shop grid**.

---

## Widget 1 — `ItemCard` (×8)

Scanned V at x=160.

```
        y 177 ┌──────────────────┐
              │  TITLE IN CAPS   │
              │                  │
              │    item art      │
              │                  │
              │  description,    │
              │  three lines     │
              │  ◉ 20            │  ← price row: coin icon + value
        y 348 ├══════════════════┤
              │       BUY        │  ← welded action, 20px
        y 368 └──────────────────┘
```

| property | measured |
|---|---|
| card height | **191px** (y=177..368) |
| card fill | `#FCFEEB` **L=0.96 S=0.91** — very light warm cream |
| `BUY` button | **20px** tall, `#FCC911`–`#FCB006` **L=0.52 S=0.98** gold |
| **footer : card height** | **0.10** |

**This corrects a conflation in the catalogue.** `skilltree1` measured its welded footer at
**0.19**; this one is **0.10**. They are not the same element:

| kind | ratio | purpose |
|---|---|---|
| **status band** (skilltree1: `MAX`, `2/3`) | **0.19** | reports state; fills the card's width and reads as part of the card |
| **action button** (here: `BUY`) | **0.10** | is pressed; smaller, saturated, reads as a control |

The kit needs both as distinct slots, not one "footer". A card may carry either or both.

## Widget 2 — `Panel`

Wood-framed parchment with a **`STORE` title banner overhanging the top edge** and a **red
✕ straddling the top-right corner**. The panel's parchment has **torn edges** inside the
wood frame — same nested-silhouette construction as `settings1`.

## Widget 3 — `TabStrip` (7 tabs)

| state | appearance |
|---|---|
| selected (`SPECIALS`) | **blue plate** + a **gold star badge straddling its top-right** |
| unselected | tan plate |
| two tabs | carry a **`NEW!` starburst badge straddling the bottom-left** |

Two badge anchors on one strip — attention (`NEW!`) bottom-left, selection marker
top-right. The folder's usual attention anchor is top-right, so this screen deliberately
separates the two by using opposite corners.

Seven tabs at 758px wide means ~95px per tab — tight, which is why unselected tabs are
label-only with no icon.

## Widget 4 — `CurrencyRow`

Coin icon + `2750` + an **`ADD` pill button**, repeated for a second currency. The `ADD`
button is a **separate detached pill**, not welded to the plate — contrast `citybuilder1`'s
welded square `+` and `skilltree1`'s icon-anchored `+`. Three placements of the same
control across the folder.

## Widget 5 — `PagerArrow`

Large **green chevrons overhanging the panel's left and right frames**, half outside the
panel. Larger than any other control on screen — paging is the primary interaction in a
grid this size.

## Widget 6 — `NewBadge`

Yellow **starburst** with `NEW`/`NEW!`, straddling the top-left of two cards and the
bottom-left of two tabs. The starburst silhouette (not a circle, not a rect) is what makes
it read as "attention" rather than "count".

## Widget 7 — `TopChrome`

Wood/leaf strip above the panel carrying a coin readout, the game's energy bar, and a star.
Diegetic decoration (leaves, food) overlaps the strip's edges — the same *decorative
overlay breaking bounds* device measured in `gameui9`'s workbench.

## Widget 8 — `ToolRail` (right edge)

A column of round and square buttons **outside the panel** (expand, `+`, `−`, save). Screen
chrome, not panel content — anchored to the viewport edge.

---

## Cross-widget rules

1. **Two distinct welded footers**: a **status band at 0.19** and an **action button at
   0.10**. Do not model them as one thing.
2. **Attention and selection badges can use opposite corners** when both appear on one
   control class.
3. **`ADD`/`+` has three placements** across the folder — welded to the plate, anchored to
   the icon, or a detached pill. Expose as a parameter.
4. **Pager arrows may be the largest controls on screen** and overhang the panel frame.
5. **A starburst silhouette means attention**; a circle means count.
6. **Nested silhouettes** (torn parchment inside a wood rectangle) — second confirmation
   after `settings1`.

## Actions

- [ ] Split the catalogue's "welded footer" into **`StatusBand` (0.19)** and
      **`ActionFooter` (0.10)**.
- [ ] `AddButtonPlacement: Welded | IconAnchored | Detached`.
- [ ] `BadgeAnchor` must allow **two simultaneous badges at different corners**.
- [ ] Add `KitShape.Starburst` for attention badges.
