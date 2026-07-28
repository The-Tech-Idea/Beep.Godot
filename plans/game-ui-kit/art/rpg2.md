# `rpg2.png` — ornate wood & gold shop screen

**600 × 600** · live menu screen · **wood + parchment + gold ribbon** family
**Relevance:** **`rpg`**, `strategy`, `cardgame`.

---

## Widget 1 — `ShopRow` (×6) and its three-part bottom bar

Scanned H at y=280, V at x=200.

```
 ┌──────────────────────────────────────────────┐
 │ ┌────┐   description text,                   │
 │ │icon│   two lines                           │
 │ └────┘   ▪▪▪▪ pips (upgrade rows only)       │
 ├──[3/10]────[◉ 150]────[  Buy  ]──────────────┤ ← bar straddles the bottom edge
 └──────────────────────────────────────────────┘
```

| part | measured |
|---|---|
| counter plate (`3/10`, `10s`) | x=47..90, `#2C2F2F` **L=0.18 S=0.03** — **neutral dark grey** |
| price ribbon (`◉ 150`) | x=110..190, `#FCC843`–`#FFC028` **L≈0.60 S=0.97** — saturated gold |
| action button (`Buy`) | x=191..248, `#2D2E2B` **L=0.17 S=0.03** — neutral dark, **gold rim** below |
| button height | **~20px** (y=269..288) |
| card keyline | 2px near-black |
| card interior | `#71492A` L=0.30 S=0.46 — brown wood |
| text on plates | white |

**The measured rule: plates are neutral (S ≤ 0.03), ornament is saturated (S ≈ 0.97).**

That is the third independent confirmation of the `gameui4` finding — a saturated,
ornate-looking family is actually built from **neutral dark plates plus a gold accent
applied only to rims and ribbons**. It is why these kits reskin so easily: swap the accent
hue and nothing else changes.

Note the contrast with the card interior at S=0.46: the *container* is allowed hue, the
*plates on it* are not.

## Widget 2 — `RibbonPlate` (top bar: `Shop`, `More Gold`; currency `9999`)

Tan/gold plate with **notched, folded ends**, and an icon **overhanging the left end**
(chest, gold bag, coin). Same silhouette used for navigation, currency and price.

`More Gold` is **disabled** — its label is grey while `Shop`'s is dark. Consistent with the
folder's rule: unavailable = desaturate/dim the content, keep the plate.

## Widget 3 — `TitleBanner` (`SHOP`)

Ornate dark-wood plaque with a carved decorative frame and a serif display face, centred at
the top and **overhanging the panels below it**. Heaviest title treatment in the folder.

## Widget 4 — `TornPanel` ×2

Parchment sheets with **ragged edges** and dark wood borders. Two-column layout,
`CONSUMABLES` left and `UPGRADES` right — here **equal width**, unlike `rpg1`'s 3:2
list/preview split, because both columns are lists.

## Widget 5 — `SectionHeader`

Serif display text (`CONSUMABLES`, `UPGRADES`) **flanked by small decorative flourishes on
both sides**. The flourishes are separate ornaments, not part of a plate — the header has
no plate at all.

Same device as `rpg1`'s currency flourishes. In this family **ornament substitutes for a
container**.

## Widget 6 — `ItemWell`

Square gold-rimmed recess holding the item icon, at the card's left. Upgrade rows add a
**small segmented pip bar directly beneath the well** (green/yellow chunks) — sixth
reference with segmented progress.

## Widget 7 — `ScrollChevron`

A gold **▲** above the right list and a gold **▼** below it. No scrollbar, no track, no
thumb — just two arrows outside the content.

**Third scrolling idiom in the folder:** `citybuilder3` clipped items at the viewport edge,
`gameui2`/`gameui7` drew a track-and-knob scrollbar, and this one uses bare chevrons. For a
game kit the chevrons are the cheapest and the most legible at small sizes.

## Widget 8 — `IconButton` (settings, sound, music)

Square dark plates with gold rims, in the top bar. Same neutral-plate/gold-rim construction
as the `Buy` button — the family has **one button material at two sizes**.

## Widget 9 — `PrimaryAction` (`RUN!`)

The only **saturated orange fill** in the top bar, with a running-figure icon and larger
text. Here the primary action *is* colour-differentiated, unlike `rpg1` where it was grey
and differentiated by size.

Recorded as a genuine variation: **primary = bigger (rpg1, gameui2/4/5) or primary =
saturated (here)**. Both occur; it is a skin choice, and the kit should expose it rather
than hardcode one.

---

## Cross-widget rules

1. **Plates neutral (S ≤ 0.03), ornament saturated (S ≈ 0.97)** — third confirmation.
2. **The container may carry hue; the plates on it may not.**
3. **Ornament can substitute for a container** — flourishes flanking a plateless header.
4. **Bottom bars straddle the card edge**, carrying counter + price + action.
5. **Bare chevrons above/below a list** are a valid scroll affordance — no track needed.
6. **Primary action: bigger OR saturated** — expose as a skin choice.
7. **One button material at two sizes** covers both toolbar and in-card actions.

## Actions

- [ ] Add `PrimaryStyle` to the skin: `Larger` | `Saturated`.
- [ ] Add **bare chevron** scroll affordance as an alternative to a scrollbar.
- [ ] `RibbonPlate` (notched folded ends, icon overhanging left) → catalogue; it is the
      single most reused silhouette on this screen.
- [ ] Enforce **S ≤ 0.05 on plates** for ornate skins, with the accent restricted to rims,
      ribbons and glyphs.
- [ ] `SectionHeader` with flanking flourishes, `ItemWell`, three-part `CardBottomBar`
      → catalogue.
