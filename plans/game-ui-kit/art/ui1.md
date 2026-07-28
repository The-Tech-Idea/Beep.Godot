# `ui1.png` — Layer Lab casual GUI sheet

**1200 × 800** · asset sheet · **flat casual, near-black keyline** family
**Relevance:** every genre. The folder's densest single sheet of *small* widgets.

---

## Widget 1 — `MissionRow` — measured

Scanned V at x=250.

```
   y  41 ─ 1px near-black keyline          #000005  L=0.01
   y  43 ─ 3px bright rim                  #FDFCD6  L=0.92   1.39 × plate
   y  46 ─ 51px plate                      #FFFC53  L=0.66 S=1.00
   y  98 ─ progress bar, 25px  ───────────────────────────── 0.22 × row
   y 124 ─ 23px plate
   y 148 ─ rim + keyline
   y 153 ─ end                              row height = 112px
```

| part | measured | ratio |
|---|---|---|
| row height | **112px** | — |
| plate | `#FFFC53` **L=0.66 S=1.00** | 1.00 |
| top rim | `#FDFCD6` L=0.92 | **1.39 ×** |
| keyline | `#000005` L=0.01, **1px** | 0.02 |
| inner progress bar | **25px** | **0.22 × row** |
| bar fill | `#22FB16` L=0.54 | — |
| bar fill gloss | `#D8FFC2` L=0.88 | **1.63 ×** |
| bar empty track | `#0A0A12` L=0.05 | **0.09 × fill** |

**Rim 1.39 ×** places this squarely in the flat family by the `INDEX.md` test (flat
1.3–1.5, carved 1.78–2.05). Confirms the discriminator on a fifth sheet.

**The empty track at 0.09 × the fill is the darkest in the folder.** Compare `gameui1`,
where the track was *lighter* than the fill (0.55 vs 0.36). The two extremes of the same
widget — this family maximises the contrast, the parchment family inverts it.

Row anatomy: **icon badge overhanging the left edge** · title · progress bar · **action
button at the right**. The inactive variant (pale grey-blue) keeps the geometry and drops
the saturation — the availability rule again.

## Widget 2 — `PlayerRow` (×2)

Rank chip left · avatar · name · trophy + score right. Blue and yellow variants of one
geometry. The rank chip is a **separate rounded plate welded to the row's left cap**.

## Widget 3 — `PlayerCard`

White card: avatar · name · trophy + score · **add-friend button at the right**. The only
white plate on the sheet — cards for *people* invert polarity from the coloured rows.

## Widget 4 — `RewardCard`

Tall lavender card: star art · `Text` · a **footer bar with a trophy + `1` and a magnifier
button**. The footer holds two different controls — a readout and an action — in one band.

## Widget 5 — `HexBadge` (×2)

**Hexagonal** tiles carrying a gem (`5`) and a chest (`3`). The chest badge has a **yellow
outer glow** marking it as highlighted.

Glow-as-highlight, third reference (`citybuilder5` selected tile, `rpgui1` active node,
here).

## Widget 6 — `StarburstBadge` (×4)

Scalloped discs: `x15 Value` (purple), `BEST` (red), `HOT` (orange), `FREE` (green). Four
hues, one silhouette. Confirms `store.md`'s finding that **starburst = attention** while a
circle = count.

## Widget 7 — `Tab` (×4)

Rounded-rect tabs; **blue = selected, dark = unselected**. One carries a **red `!` badge
straddling its top-right**; two carry icons.

## Widget 8 — `IconTabBar`

Dark strip with six glyph tabs and a **coloured underline beneath the selected one**.

**Selection #17: an underline indicator.** Cheapest strip selection in the folder — the tab
itself is untouched, and it works for icon-only tabs where a fill would fight the glyph.

## Widget 9 — `HintTooltip`

White rounded plate with a **tail pointing down**. Fourth tooltip in the folder; this is the
minimal form — no header, no icon, no border.

## Widget 10 — `NotificationDot` set

Six variants in a row: red `!`, red `7`, green `2`, plain red, plain white, plain grey.

So the sheet ships **glyph, count and plain** variants, in **alert / positive / neutral /
inactive** colours. That is the complete matrix for the badge widget and the kit should
implement it as such rather than as separate widgets.

## Widget 11 — `SearchField`

Dark pill with placeholder text, plus a **separate blue square search button beside it** —
detached, not welded. Same detached-action pattern as `store.png`'s `ADD` pill.

## Widget 12 — `ProgressBar` (×3)

Each with an **icon cap overhanging the left end**; fills in yellow, green and dark
(empty). Values (`5/10`, `100/1200`, `0/1`) centred on the bar.

## Widget 13 — `ChestProgress`

Chest icon overhanging a dark bar showing `1 / 10`, with a **red dot badge at the top-right**
of the chest. An icon that is simultaneously a cap and a badge host.

## Widget 14 — `TicketBar` / `StageBar`

Ticket-shaped plates with **notched ends**: one holds `3` in a notched left cap, a medal,
`13/50` and a crown; the other a fire icon, `0/5` and a **blue diamond chip at the right**.

Notched ticket ends are this sheet's distinctive silhouette — the same device as `rpgui1`'s
notched corners in a flat register.

## Widget 15 — `PillButton` (`FREE`, `GOLD`)

Pale-blue and yellow pills, text only. The two-tier offer button.

## Widget 16 — `PriceBubble` (×2)

Speech-bubble shapes with **tails pointing down**, carrying a gem and a value; one has a
**green ✓ badge** at its top-right (owned/confirmed).

A price attached to a *world or grid position* rather than a row — the tail points at what
it prices.

## Widget 17 — `LevelChip`, `RocketCard`, `TrophyRibbon`

- `LevelChip`: map icon + `Normal 2-1`, with a small `Text` label above
- `RocketCard`: blue card, rocket icon, `Text` over `84`
- `TrophyRibbon`: orange ribbon with **notched ends**, trophy + `15625`

## Widget 18 — `Slider` (×2)

Pink-filled track with a **white square knob**; and an empty track with the knob at the
left. Square knob, not round — matching `settings1` and `gameui7`'s bar knobs rather than
`ui1`'s own round style elsewhere.

---

## Cross-widget rules

1. **Rim 1.39 ×, keyline 1px near-black** — flat family, fifth confirmation of the
   discriminator.
2. **Empty track can be 0.09 × the fill** (this sheet) or *lighter* than it (`gameui1`).
   Track polarity is a skin choice with a huge range.
3. **Inner progress bar = 0.22 × its row height.**
4. **The badge matrix is glyph / count / plain × alert / positive / neutral / inactive** —
   one widget, not many.
5. **Selection #17: an underline** — best for icon-only strips.
6. **Cards for people invert polarity** (white) against coloured rows.
7. **Notched ends** are the flat family's equivalent of the premium family's notched
   corners.

## Actions

- [ ] `ProgressTrack` gains a **track polarity** parameter spanning 0.09 × → 1.5 × the fill.
- [ ] Implement `Badge` as **one widget with a content mode and a role**, per the matrix.
- [ ] Add **underline** as a `TabStrip` selection renderer.
- [ ] Record inner-bar ratio **0.22 × row**.
- [ ] `TicketBar` (notched ends), `PriceBubble` (tailed, positioned), `ChestProgress`
      → catalogue.
