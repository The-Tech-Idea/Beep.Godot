# `rpg1.png` — cartoon RPG upgrade shop

**564 × 424** · live menu screen (Kingdom-Rush-like) · **parchment on wood, heavy keyline**
family
**Relevance:** **`rpg`**, `strategy`, `platformer`. The clearest **affordability** model in
the folder.

---

## Widget 1 — `UpgradeRow` (×4)

Scanned H at y=228, V at x=260.

```
 ┌──────┬───────────────────────────────┬──────────────┐
 │ icon │ TITLE                         │  price plate │ (!)  ← badge overhangs
 │ tile │ description, two lines        │  ▪▪▪▪▪ pips  │
 └──────┴───────────────────────────────┴──────────────┘
   54px            ~143px                    65px
```

| part | measured |
|---|---|
| icon tile | **~54px** wide (x=27..80), coloured plate per item, glyph inside |
| text column | **~143px** (x=82..224) — title in bold caps, 2 description lines beneath |
| price plate | **65 × 30px** (x=227..291, y=211..240) |
| price fill (affordable) | `#AE5720` L=0.40 **S=0.69** — dark orange |
| price text | cream/white `#FEF3E3` |
| pip bar | **10px** tall, 4px below the plate |
| **pip : plate height** | **0.33** |
| unfilled pip | `#692C23`–`#61281B` L=0.24–0.27 |

## Widget 2 — Affordability, measured

The first row (`LUCK`, 8,150) is unaffordable; the other three are not.

| | affordable | unaffordable |
|---|---|---|
| price plate | dark orange `#AE5720` L=0.40 S=0.69 | **grey**, desaturated |
| price text | cream | dimmed |
| whole row | full contrast | dimmed |

**Affordability = desaturate the price plate and dim the row.** Same recipe as
`gameui3`'s disabled state (S → ~0.01) and `citybuilder4`'s locked card (S 0.22 → 0.04).
Three unrelated games, one rule: **cannot-act is a saturation drop, not a hue change.**

That is now measured on four references and should be the kit's `KitState.Disabled`
implementation, full stop.

## Widget 3 — `PipBar` (upgrade level)

A row of small squares beneath the price, showing how many levels are bought. Unfilled pips
are a **dark tint of the row's own hue** (`#692C23` against a `#BF966B` parchment), not
grey and not black.

Fifth reference to draw progress as discrete segments (`gameui1`, `gameui2`, `gameui3`,
`gameui4`, here). **Segmented is the default in game UI; continuous is the exception.**

## Widget 4 — `TabButton` (top bar and sub-bar)

| state | appearance |
|---|---|
| selected | **orange/tan** plate |
| unselected | **grey stone** plate |
| both | thick dark keyline, chunky rounded rect |

Two tab strips on one screen use the identical rule — top level (`SHOP` /
`ACHIEVEMENTS` / `QUESTS`) and sub level (`ARMORY` / `ACCESSORIES` / `POTIONS`).

**Selection #12: swap the plate between a neutral stone and the accent.** The simplest
possible two-state palette, and it is what `SkinCatalog`'s theme model already supports.

## Widget 5 — `AttentionBadge` (`!`)

Small **yellow circle with `!`**, straddling the **top-right corner** of tabs and price
plates. Present on `SHOP`, `ACCESSORIES` and every affordable price plate.

Semantically distinct from a count badge: it means *"something here changed / you can act"*.
The kit has `NotificationDot` for counts; this is the same anchor with a glyph instead of a
number, and it appears on **both containers and controls**.

## Widget 6 — `TornPanel` (×2)

Parchment sheets with **irregular torn edges** and a subtle inner border, laid on a wood
background. Confirms `settings1`'s `TornPanel` and `gameui1`'s torn family.

The two panels are **asymmetric** — a wide list panel at the left, a narrow preview panel at
the right, roughly 3:2. That split (list left, preview right) is the standard shop layout
and appears again in `racing3`, `store.png` and `gameui8`.

## Widget 7 — `CurrencyPlate`

Orange plate with a coin icon and `5,956`, **flanked by small decorative flourishes** at
both ends. The flourishes are attachments outside the plate's rect — the same overhang
device, used purely ornamentally.

## Widget 8 — `CharacterPreview`

Knight illustration centred in the right panel, no frame, no plate — art directly on the
parchment.

## Widget 9 — `PlayButton`

Large grey stone plate, `PLAY!` in dark caps, at the right panel's bottom. Note it is
**grey, not orange**, despite being the primary action — in this skin orange means
*selected/affordable*, not *primary*. Primary is conveyed by **size and position** instead.

Consistent with `gameui2`, `gameui4` and `gameui5`: **primary action = bigger, not a
different colour.**

---

## Cross-widget rules

1. **Cannot-act = desaturate.** Four references now agree; make it the kit's
   `KitState.Disabled`.
2. **Selection #12: neutral stone ↔ accent plate swap** — used at two levels of tab strip
   on one screen.
3. **Segmented progress is the default** — fifth reference.
4. **Unfilled segments are a dark tint of the host's own hue**, never grey.
5. **`!` badges straddle top-right corners** of both containers and controls.
6. **List-left / preview-right** is the standard shop layout, ~3:2.
7. **Primary action = size and position**, not colour — fourth confirmation.

## Actions

- [ ] `KitState.Disabled` ← **desaturate** (S → ~0.05) + dim; stop treating it as a tint.
- [ ] Add `AttentionBadge` (`!`) as a distinct attachment from `NotificationDot` (count).
- [ ] `PipBar` — unfilled segment = host hue at ~0.45 × lightness.
- [ ] Record the **list-left / preview-right 3:2** shop layout as a kit template.
- [ ] `UpgradeRow`, `TabButton` (stone/accent), `CurrencyPlate` with flourishes → catalogue.
