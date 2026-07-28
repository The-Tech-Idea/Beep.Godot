# `ui8.png` — village builder, **full-chrome** capture

**1004 × 788** · live gameplay screen · same game as `citybuilder1.png` ("Варвары"),
**wider crop**
**Relevance:** `citybuilder`. Not a duplicate — this capture includes the chrome that
`citybuilder1`'s 800 × 600 crop cut off.

Read [citybuilder1.md](citybuilder1.md) for the shared widgets (chrome strip, currency
bars, capacity ribbons, round slot buttons, world pin). This document covers **only what is
new here**.

---

## Widget 1 — `CollapsiblePanel` (bottom friend bar) — the widget this whole effort started for

Scanned V at x=300.

| part | measured |
|---|---|
| handle | **33px** tall (y=651..683), dark plate `#4D4136` **L=0.26** |
| handle glyph | **`▼` in pure white `#FFFFFF` L=1.00** — maximum contrast |
| position | **overhanging the panel's top edge**, centred |
| panel interior | `#F0D5B2` **L=0.82 S=0.67** warm cream |

**A collapsible panel is a panel plus a handle that straddles its leading edge.** The
handle is small (33px), dark, and carries the highest-contrast glyph on the screen —
because it is the only control whose *state* the player must read at a glance.

This is the "sliding and collapsing panel" the kit was originally asked for, and the
reference is unambiguous: **the affordance lives outside the panel, on the edge it moves
along, and it is a chevron.**

## Widget 2 — `FriendCard` (×6 in the bar)

| part | observed |
|---|---|
| name plate | above the avatar, cream `#F0D5B2` |
| avatar | photo or art, filling the card |
| **level badge** | a **star at the card's bottom-right, straddling the corner** |
| variants | real cards, plus `Добавить` (Add) and `Добавь меня` (Add me) prompt cards |

The **prompt cards sit in the same row as real entries**, using the same geometry — an
empty-slot invitation inline with content, rather than a separate button. Same principle as
`ui3`'s `+` equip slot.

## Widget 3 — `CarouselPagers`

Two pager levels at each end of the bar: `◄ ►` (step one) and `|◄ ►|` (jump to end). Four
controls for one axis.

First reference in the folder with **step and jump paging as separate controls** — worth
having when a list can be long.

## Widget 4 — `PromoButton` (×2, right edge)

Round buttons (pink gems, red hammer) each with an **`Акция!` ribbon banner overhanging its
top edge**, and a soft outer glow.

A **round host with a banner attachment across its top** — the same construction as
`gameui8`'s minimap title and `rpgui1`'s gem-ornamented banner, here used to mark a
time-limited offer. Glow marks urgency, matching `citybuilder5`'s `PromoRibbon` finding
that **offers get their own visual channel**.

## Widget 5 — `ProgressHeader` (top-right)

A **purple star level badge (`1`)** overhanging a progress bar reading `505`, and beneath
it a **trophy + `0`** bar. Same `StackedMeter` construction measured in `citybuilder5`
(two framed bars, ~30px each, badge overhanging the left).

## Widget 6 — `EdgeButtonRail` (left and right)

Vertical rails of square buttons pinned to the screen edges:

- **left**: gear · target/coin · mail with a **red `5` badge**
- **right**: clan shield (`Клан`) · trophy · shield with a **red `1` badge**

Badges straddle the top-right corner — eighth sighting of the folder's universal attention
anchor.

## Widget 7 — `ShopButton`

`Магазин` with a coin-bag illustration and a **green `6` badge overhanging its top-right**.
An illustrated button, matching `citybuilder5`'s awning shop button — **the shop entry is
consistently a picture, not an icon**, across two references in this genre.

Note the badge is **green** here and **red** on the mail and shield buttons: green = new
content available, red = action required. Two badge roles on one screen.

## Widget 8 — `PrimaryAction` (`В атаку!`)

Large brown plate with crossed swords, bottom-right. Largest control on screen — **primary
= bigger**, sixth confirmation.

---

## Cross-widget rules

1. **A collapsible panel = panel + a chevron handle straddling its leading edge**, dark
   plate, maximum-contrast glyph, ~33px.
2. **Prompt cards sit inline with content** using the same geometry.
3. **Step and jump paging** can be separate control pairs.
4. **Offers get their own channel** — ribbon + glow on a round host.
5. **Badge colour carries a role**: green = new content, red = action required.
6. **The shop entry is an illustration**, not an icon — two references in this genre.

## Actions

- [ ] Implement `CollapsiblePanel` from this reference: handle **outside** the panel on the
      moving edge, ~33px, dark plate, white chevron. **This is the widget the kit was
      originally asked for and now has a measured spec.**
- [ ] Add **jump-to-end** pagers alongside step pagers.
- [ ] `Badge` role: green = new, red = action required (extends `ui1`'s badge matrix).
- [ ] `PromoButton` (round host + top ribbon + glow) → catalogue.
