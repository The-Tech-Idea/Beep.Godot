# `gameui4.png` — "GAME GUI #6", grey-plate casual kit

**800 × 800** · asset sheet · **neutral plate + coloured glyph** family
**Relevance:** every genre. This sheet is the **measured proof of the project's own
skin→theme→palette model**, and it shows the mechanism that makes it work.

---

## The headline: the plate never changes colour — only the content does

The same menu button is drawn three times, in three accent colours. Scanned H at y=400:

| variant | plate | text |
|---|---|---|
| green | `#FBFDF9` L=**0.98** | `#B6CE5E` L=0.59 S=0.53 |
| orange | `#FBFBFC` L=**0.99** | `#F89F2B` L=0.57 S=0.94 |
| teal | `#FDFDFD` L=**0.99** S=0.00 | `#22829C` L=0.37 S=0.64 |

**Three identical white plates. Only the glyph colour differs.** This is exactly
`genre (geometry) → theme → palette` — geometry and material fixed, palette applied to
content only. The art does it; the kit should too.

## And the mechanism that makes low-contrast palettes legal

Green text at L=0.59 on a plate at L=0.98 is roughly **2.2 : 1** — far below WCAG AA. It
is legible anyway because the scanline shows a **1px near-black outline hugging the
letterform** (`#1E2700` L=0.08 immediately before the green at x=524, `#0D1F00` and
`#1E3104` around the title text at y=176–177).

> **The art keeps the palette hue and adds a dark outline, instead of shifting the hue to
> reach contrast.**

That matters directly: this project currently enforces WCAG AA by **pushing colours**
(`UiSurface.ReadableOn` moves lightness in both directions and relieves saturation). That
changes the designer's hue. A **text outline** reaches legibility while preserving the hue
exactly — which is why every reference sheet with coloured text has one.

Both approaches are valid; they should be a per-skin choice, not a hardcoded policy.

---

## Widget 1 — `MenuButton`

| property | measured |
|---|---|
| plate | white L=0.98–0.99, **neutral** |
| keyline | 4–6px near-black — **very heavy** for the button size |
| text | palette hue + 1px dark outline |
| row pitch | **29px** (RESTART y=203, OPTIONS y=232, EXIT y=261) |

## Widget 2 — `Panel`

Scanned V at x=180 on `PAUSED`.

| property | measured |
|---|---|
| plate | `#D9D8D7` L=0.85 — light neutral grey |
| keyline | **4–6px** near-black, top and bottom |
| title banner | white L=0.97, **overhanging the panel's top edge** |
| banner text | palette hue with a dark outline |

Panel plate 0.85 vs button plate 0.98 → the button is **1.15 ×** the panel. Buttons are
raised by lightness alone; there is no bevel anywhere on the sheet.

## Widget 3 — `CloseButton`

A **red ✕** straddling the panel's **top-right corner**, roughly half outside the frame.
Present on `PAUSED`, `SHOP`, `UPGRADES`, `ACHIEVEMENT`, `OPTIONS`. Red is used for nothing
else on the sheet — close is the only destructive affordance.

## Widget 4 — `TierIcon` set

Coins, stars, gems, hearts, shields, magnets, tombstones — each drawn in **three tiers**
(gold / silver / bronze-grey) with **identical geometry**. Tier is carried entirely by
material colour.

Confirms the same principle as widget 1 one level down: one shape, N palettes.

## Widget 5 — `ProgressBar` set (top-right, ×8)

Each: an **icon cap at the left** + a track + a coloured fill. Variants seen: orange
continuous, red continuous, teal continuous, green **segmented**. So this family ships
both continuous and segmented fills for the same widget.

## Widget 6 — `LevelSelectGrid`

Numbered tiles, **★★★ above the number** (not below, unlike `gameui2`), plus a locked tile
with a padlock replacing the number. Star position is per-skin.

## Widget 7 — `ShopGrid`

Item tiles in a row inside a panel, each an icon on a plate, with pager arrows at both
ends and a bottom action row.

## Widget 8 — `AchievementGrid`

Earned entries show their icon; unearned show a **teal ✕ on a grey plate**. Third sheet to
draw absence explicitly rather than leaving a blank.

## Widget 9 — `OptionsRow`

`SOUND FX / MUSIC / QUALITY` — label at the left, **slider** at the right; and
`SUBTITLES` with a **checkbox** (✓ in a box).

Second sheet with a real checkbox, after `gameui2`. The `CATALOGUE-FROM-ART.md` §D claim
that game UI has no checkboxes is now contradicted twice.

## Widget 10 — `PrimaryAction`

The `PLAY` button on `LEVEL COMPLETE` is a **large green triangle on an oversized plate**,
noticeably bigger than the two buttons flanking it. Primary action = **bigger**, not
differently coloured — the same trick as `gameui2`'s larger centre star.

## Widget 11 — `IconButtonLibrary`

~40 glyphs × **3 colour families** (green, orange, teal) as rounded squares: grey plate,
heavy dark keyline, coloured glyph. Glyph set includes play, list, star, bulb, ✓, power,
pause, gear, home, person, ?, sound, cloud, share, exit, thumbs-up, `+`, trophy, chevrons,
trash, and more.

The library is the same 40 shapes recoloured three times — a palette swap shipped as art.

## Widget 12 — `UpgradeRow` (`UPGRADES` panel)

`(icon) ▬▬▬▬ (+)` — icon, a slider-style meter, and a round `+` at the right, three rows
stacked, with a coin total above. Matches `gameui2`'s UpgradeRow closely enough that this
is a genuine cross-sheet pattern, not one artist's idea.

## Widget 13 — `CoinTotal`

`(coin) 970` — icon + value, centred, no plate, appearing at the top of `SHOP`,
`UPGRADES` and `LEVEL COMPLETE`. A plateless readout inside a panel (contrast the HUD
capsules, which always have a plate — because they sit on the world, not on a panel).

## Widget 14 — `StarRow` (`LEVEL COMPLETE`)

Three gold stars in a shallow arc, centre one larger. Identical device to `gameui2`.

---

## Cross-widget rules

1. **Plates are neutral; the palette lives in glyphs and text.** Measured across three
   colour variants of one button.
2. **A dark text outline substitutes for contrast compliance** — the art keeps the hue and
   outlines it. Offer this as an alternative to `ReadableOn`'s hue shifting.
3. **Keylines are heavy** — 4–6px on ~30px controls, about 0.15–0.2 of the size.
4. **Raise by lightness alone** — button 0.98 vs panel 0.85 = 1.15 ×, no bevel anywhere.
5. **Primary action = larger**, not recoloured.
6. **Tier = colour of one shape.**
7. **Absence is drawn** (✕ tiles) — third sheet.
8. **Red is reserved for close/destroy.**
9. **Star position on a level tile is per-skin** (above here, below in `gameui2`).

## Actions

- [ ] Add a **text outline** option to the kit's label drawing, and make WCAG enforcement
      a per-skin choice: shift-hue (current) **or** keep-hue-and-outline.
- [ ] `KitMaterial` gains **neutral plate + palette-on-content** as an explicit mode.
- [ ] Keyline weight becomes a ratio of control size (**0.15–0.2** for this family).
- [ ] `PrimaryAction` sizing rule: **larger**, same colours.
- [ ] `TierIcon`, `AchievementGrid`, `CoinTotal`, `OptionsRow` → catalogue.
