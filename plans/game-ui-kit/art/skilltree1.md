# `skilltree1.png` — modern casual talent tree

**1200 × 1920** · live menu screen · **navy background, framed cards with welded footers**
family
**Relevance:** **`rpg`**, `strategy`. The clearest measurement of the folder's most
repeated compound element — the **card with a welded footer bar**.

---

## Widget 1 — `TalentNode` — the welded-footer card, measured

Scanned V at x=290 on the top-left node.

```
        y 256 ┌──────────────────┐
              │  ▓▓ frame 20px ▓▓│   #ECA451 L=0.62 S=0.80
              │ ┌──────────────┐ │
              │ │              │ │
              │ │   ability    │ │   recessed well, art inside
              │ │     art      │ │
              │ └──────────────┘ │
        y 466 ├══════════════════┤   ← welded footer, 51px
              │      MAX         │   #FCDE49 L=0.64 S=0.97, white text
        y 518 └──────────────────┘
```

| property | measured |
|---|---|
| node height | **262px** (y=256..518) |
| frame | **~20px** `#ECA451` L=0.62 S=0.80 |
| footer band | **~51px** `#FCDE49` L=0.64 **S=0.97** |
| **footer : node height** | **0.19** |
| footer text | white, centred |
| background | `#0C172F` L=0.12 S=0.59 |

**0.19 is the number the catalogue has been missing.** `CATALOGUE-FROM-ART.md` records the
welded footer as "the most repeated compound element in the whole folder" (now seen in ten
pictures) but never measured it. A footer is **one fifth of the card's height**.

**The footer is more saturated than the frame** (S=0.97 vs 0.80) at nearly the same
lightness (0.64 vs 0.62). So it separates by *saturation*, not by lightness — which is
exactly why it still reads when the card is small.

## Widget 2 — State by frame + footer colour

| state | frame | footer | text |
|---|---|---|---|
| **maxed** | gold/orange | gold | `MAX` |
| **in progress** | **blue** | blue | `2/3`, `1/3` |
| **locked** | dimmed almost to the background | dimmed | none |

Frame and footer always match. **Two elements, one hue, one state** — the cheapest possible
state encoding on a card, and it works at any size because the frame is visible even when
the art is not.

Locked nodes at the bottom are dimmed to near-background, which is the **silhouette**
treatment again (`skilltree.md`) — tenth unavailable-rendering in the folder, and the
second in a skill tree specifically. For planning screens, silhouette is clearly the
convention.

## Widget 3 — `Connector`

Thick **light-blue** lines, diagonal and vertical, running **behind** the nodes. Unlike
`skilltree.png`, the connectors are **not** branch-hued — they are one neutral blue for the
whole tree, because here the *node frames* carry the state colour instead.

That is a real trade-off worth recording:

| system | branch identity | node state |
|---|---|---|
| `skilltree.png` | connector + node hue | dark silhouette |
| `skilltree1.png` | **neutral connectors** | **frame + footer hue** |

You can spend colour on the branch **or** on the state, not both. `skilltree1` chose state,
which suits a tree where every node is on one path.

## Widget 4 — `PlayerCard` (top-left)

Portrait in a frame, name plate, an `LV` row with a right-aligned value, and an `EXP` bar
beneath. Four readouts welded into a block ~110px tall — the same **weld-don't-space**
density technique measured in `rpgui3`.

## Widget 5 — `CurrencyPill` (×3)

| part | observed |
|---|---|
| icon | large, **overhanging the pill's left cap** |
| value | right-aligned in a dark plate |
| `+` badge | **green, straddling the icon's bottom-right** — not the plate's |

**The `+` badge attaches to the icon, not to the plate.** Every other currency bar in the
folder (`citybuilder1`, `citybuilder2`, `gameui8`, `racing4`) welds `+` to the plate's
right cap. This one anchors it to the icon, which keeps the value's right edge clean for
alignment across three pills of different value widths.

Worth adopting when several currency readouts must right-align.

## Widget 6 — `EnergyReadout`

`25/30` with a lightning icon — a **capacity** value rather than a total, in the same pill
shape as the currencies. Form follows the container, not the semantics.

## Widget 7 — `PromoBanner` (bottom)

Magenta angular banner with **circuit-trace line art**, large display text over two lines,
and a **mascot character overhanging its right end and its top edge**.

Same angular-cut construction as `gameui5`/`racing4`: the banner's ends are cut at an
angle, the body stays axis-aligned.

## Widget 8 — `SettingsButton`

Grey gear at the top-right of the bar, larger than the currency pills' height — the only
control not in a pill.

---

## Cross-widget rules

1. **Welded footer = 0.19 × card height** — the folder's most repeated element, finally
   measured.
2. **The footer separates by saturation (0.97 vs 0.80), not lightness.**
3. **Frame + footer share one hue to encode state**; locked = dimmed to background.
4. **Spend colour on branch identity OR on node state, not both.**
5. **A `+` badge can anchor to the icon rather than the plate** when values must
   right-align across several readouts.
6. **Weld-don't-space** appears again in the player card.

## Actions

- [ ] `KitCard.FooterRatio` ← **0.19**; footer hue = frame hue at **+0.17 saturation**.
- [ ] `KitState` on a card ← frame + footer hue swap (gold/blue/dimmed).
- [ ] Add `BadgeAnchor: Plate | Icon` to currency readouts.
- [ ] Record the **branch-hue vs state-hue** trade-off in the tree widget's docs.
