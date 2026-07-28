# `racing4.png` — racing garage / car select

**1200 × 675** · live menu screen · **dark tech, corner-bracket** family
**Relevance:** **`racing`**, `shooter`, `strategy` — any genre wanting a technical HUD.

---

## Widget 1 — `StatPanel` and the corner-bracket device

Scanned H at y=200.

| property | measured |
|---|---|
| panel fill | `#1B212D`–`#1B222B` **L=0.14** S=0.23 |
| background | `#2A3340` **L=0.21** S=0.21 |
| **relationship** | the panel is **darker than the background** — 0.14 vs 0.21 = **0.67 ×** |
| border | **none** — no continuous stroke on any edge |
| delimiter | **four small square marks, one at each corner** (crop-mark style) |

**Two things worth taking.**

1. **A panel can be darker than what it sits on.** Every framed family in this folder makes
   the panel lighter or adds a frame. Here the panel recedes, and the content floats.
2. **Corner brackets replace a border.** Four ~8px squares at the corners is enough to read
   as a bounded region. It is the cheapest container in the folder — cheaper even than
   `citybuilder4`'s 1px highlight — and it is the signature of technical UI.

The kit has no corner-bracket container. It is trivial to draw and instantly reads as
"sci-fi / technical", which is what `shooter` and `racing` both want.

## Widget 2 — `StatRow` (`ENGINE`, `HANDLING`, `SUSPENSION`, `NITRO`)

Scanned V at x=150.

| part | measured |
|---|---|
| row pitch | **~60px** |
| progress bar | **7px** tall, `#0189EC` **L=0.46 S=0.99** — a saturated blue line |
| bar : row | **0.12** |
| layout | icon · label · **chevron rank at the right** · bar underneath, full row width |

The bar is a **hairline under the whole row**, not a widget beside the label. That is a
denser layout than any other reference and it is why four stats fit in 240px.

## Widget 3 — `ChevronRank` (`»»`)

A stack of chevrons at each row's right end showing tier. Grey on three rows; **lime on
`SUSPENSION`**, marking the one that can be upgraded now.

**Actionability is marked on the indicator, not the row.** Cheap, and it scales to a long
list without adding buttons.

## Widget 4 — `TitleChip` (`OHIO RAMPAGE W33`)

A small plate with a thin light border at the panel's top-left, **overlapping the panel's
top edge**. Overhang again, in a family with no frames at all.

## Widget 5 — `GhostButton` (`UPGRADE`, `CUSTOMIZE`)

Icon + label, **thin light border, transparent fill**. No plate. Two side by side at the
panel's bottom.

First reference in the folder with a genuinely **outline-only button**. Every other family
fills. Worth adding as a `KitMaterial` mode — it is the natural button for a dark technical
skin where a filled plate would be too heavy.

## Widget 6 — `CarCard` (carousel, ×7)

Scanned H at y=600.

| property | measured |
|---|---|
| card width | **~145px** (x=34..178) |
| **owned/selected border** | **3px lime** `#DEFB6F` L=0.71 S=0.95 |
| other cards | thin grey/white border |
| content | car render above, **two-line name plate** below (small caps line + bold line) |
| baseline | all cards sit on a horizontal **blue rule** spanning the screen |

**Selection #11: a 3px accent border.** With `racing3`'s carousel border and `gameui9`'s
white outline, that is three references choosing an outline for a card carousel. For cards,
an outline is the convention — a fill would hide the artwork.

## Widget 7 — `TopBar`

Dark full-width strip carrying:

- a **white angled wedge** cut into the bar's left end, holding a magenta `‹` back chevron
- `GARAGE` in white caps
- two currency plates (right)
- a lime **notification button** with a red dot
- a home button

The wedge is the same trick as `gameui5`: **an angled shape cut into a rectangular
container** rather than a rotated control.

## Widget 8 — `CurrencyPlate` (×2)

Dark plate, coloured icon at the left (blue coin, gold bars), value, and a **lime `+`** at
the right. Same construction as every currency bar in the folder — icon left, value,
add-button right — now in a fifth visual family.

## Widget 9 — `NotificationButton`

Lime plate, clipboard glyph, **red dot at the top-right**. The only saturated fill in the
top bar; the one thing demanding attention.

---

## Cross-widget rules

1. **A container can be darker than its background** (0.67 ×) and still read as a panel.
2. **Corner brackets are a container.** Four small squares, no stroke.
3. **Outline-only buttons** belong in dark technical skins.
4. **Selection #11: a 3px accent border** — and for **card carousels specifically**, an
   outline is the cross-reference convention (racing3, racing4, gameui9).
5. **Mark actionability on the indicator**, not on the row.
6. **Angled wedges are cut into rectangles**, never rotated — second confirmation after
   `gameui5`.
7. **Accent hue does double duty**: lime marks both *ownership* (card border) and
   *availability* (chevrons, `+`, notification). One hue, one meaning — "you can act here".

## Actions

- [ ] Add a **corner-bracket container** to the kit (4 marks, no stroke) — high value for
      `racing` and `shooter`.
- [ ] Add an **outline-only** button material.
- [ ] `KitState.Selected` for card/carousel classes ← **3px accent border**, confirmed by
      three references.
- [ ] Add a **chevron rank** indicator widget with an actionable tint.
- [ ] Allow panel fill **darker** than the parent surface.
- [ ] `StatRow` (bar as a hairline under the row), `TitleChip`, `GhostButton`,
      `NotificationButton` → catalogue.
