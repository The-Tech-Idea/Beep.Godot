# `survaivleandrpg1.png` — pixel-art open-book equipment UI

**960 × 540** · live menu screen · **pixel book, one-hue page** family
**Relevance:** **`rpg`**, **`survival`**, `puzzle`. The most **minimal** widget set in the
folder — everything is drawn from one page colour and two darker tints.

---

## The family's whole material, measured

Scanned H at y=300 and V at x=300.

| element | measured | ratio to page |
|---|---|---|
| page | `#EBBD7D` **L=0.71 S=0.73** | 1.00 |
| container/slot outline | `#D8A969` **L=0.63** | **0.89 ×** |
| bar fill | `#915D41` **L=0.41 S=0.38** | **0.58 ×** |
| end caps / keylines | `#623A2D` L=0.28 | 0.39 × |

**Four tones. That is the entire UI.** No white, no black, no accent colour anywhere on the
page — the coloured side tabs are the only saturated elements, and they sit *outside* the
book.

For the kit this is the extreme end of the "every layer switchable off" requirement: a skin
that draws containers as **2px lines at 0.89 ×** and fills at **0.58 ×**, with no frame,
bevel, gloss, shadow or keyline.

## Widget 1 — `StatBar` — the track is not drawn

```
   │▓▓▓▓▓▓▓▓▓▓▓│                                        │
   ^2px cap    ^2px             bare page               ^2px cap
   ◄── 54px fill ──►◄────────── 139px empty ───────────►
                total 200px
```

| part | measured |
|---|---|
| total width | **200px** (x=634..833) |
| fill | **54px** = 27 % |
| fill colour | `#915D41` L=0.41 S=0.38 — **0.58 × page**, and **less saturated** |
| **empty track** | **not drawn** — it is the bare page `#EBBD7D` |
| caps | **2px** dark, one at each end of the *whole* bar and one at the fill's edge |

**The empty portion has no track at all.** Two 2px marks define the bar's extent; the fill
does the rest. Compare `settings1`'s slider, which also omitted a fill; this omits the
track. Between them the folder shows both halves of a bar can be implied.

**Value placement:** the number (`35m`, `45hp`, `0.2s`) floats **directly above the fill's
right end**, moving with the value.

That is the **fourth** value placement in the folder:

| placement | source |
|---|---|
| centred on the fill | gameui8 |
| below the bar | rpgui1 |
| beside the bar | rpgui3 |
| **above the fill's end, tracking it** | **survaivleandrpg1** |

The tracking variant is the most informative — the number is spatially bound to the
magnitude — and the most awkward to lay out. Worth having as an option.

## Widget 2 — `EquipSlot`

Wide recessed slot showing the item's silhouette, with a **label above and outside** it.
Drawn entirely as **2px lines at 0.89 ×** the page — no fill change, no shadow.

Two of these (`Balanced Grail Sword`, `Radiant Sword`), plus an **accessory row** of four
small square slots (three ring silhouettes, one hammer).

**The item inside an empty slot is drawn as a ghost silhouette** in the slot's outline
tone — the slot shows what *belongs* there. That is a better empty state than `gameui4`'s
✕ or a blank recess, and it is free.

## Widget 3 — `BookPanel`

Red leather cover with a lighter inner border, two tan pages, and a **centre spine rendered
as a vertical gradient**. Page edges are visible at the outer margins.

The spine gradient is the only gradient in the UI and it is what sells the book. Everything
else is flat.

## Widget 4 — `SideTab` (×4 left, ×1 right)

Coloured tabs (red, orange, green, blue; purple on the right) **stacked vertically and
overhanging the book's outer edge**.

**Selection = the tab protrudes further.** The blue tab extends further left than the other
three. Same elevation-based mechanism as `gameui9`'s raised tab and `skilltree4`'s taller
nav tab, here expressed horizontally.

The tabs are the only saturated elements on screen, and they are deliberately **outside**
the content — colour is quarantined to navigation.

## Widget 5 — `ScreenTitle`

`Equipment` and `Grail Sword` in a **blackletter/gothic display face**, no plate. Two type
registers on one screen: blackletter for titles, a plain pixel face for body and labels.

## Widget 6 — `DetailPanel`

Framed panel (2px line) containing the item name, three lines of body text, an
**ornamental divider — a vine flourish with the item's icon centred in it** — and the stat
list.

Sixth reference to flank a divider with ornament, and the first to **put the item's own
icon inside the divider**, making the ornament content-specific rather than generic.

---

## Cross-widget rules

1. **A whole UI can be four tones of one hue**: page 1.00, outline 0.89, fill 0.58, keyline
   0.39.
2. **A bar's empty track may be omitted entirely** — two 2px caps define the extent.
3. **The value can track the fill's end.**
4. **An empty slot should show a ghost silhouette** of what belongs there.
5. **Colour can be quarantined to navigation**, leaving the content monochrome.
6. **Selection by protrusion** works horizontally as well as vertically.
7. **Two type registers** (display + body) is enough for a whole screen.

## Actions

- [ ] Add a **minimal material preset**: outline 0.89 ×, fill 0.58 ×, no frame/bevel/gloss.
- [ ] `ProgressBar` gains `ShowTrack: bool` (false = caps only) — pairs with
      `settings1`'s `ShowFill: bool`.
- [ ] Add `ValuePosition.TrackingFillEnd`.
- [ ] Add **ghost silhouette** as the default empty-slot renderer.
- [ ] Allow a **content-specific icon inside a divider ornament**.
