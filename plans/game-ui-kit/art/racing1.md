# `racing1.png` — arcade racing HUD (Forza-Horizon-like)

**736 × 414** · live gameplay screen · **broadcast minimal** family
**Relevance:** **`racing`** — the first dedicated racing reference in the folder.

---

## The family finding: racing has no plates

Every other reference builds its HUD from framed plates. This one has **none**. The whole
HUD is:

- text with a soft shadow
- thin hairline strokes
- translucent dark bars behind rows only
- arcs and rings

No frame, no bevel, no keyline, no corner radius anywhere. A racing HUD is closer to a
television sports overlay than to a game panel, because it must sit on a fast-moving,
high-contrast scene without stealing attention.

**Consequence for the kit:** the `racing` skin should set frame, bevel, gloss and keyline
to **zero** and rely on shadow + alpha only. That is exactly the "every layer switchable
off" requirement `citybuilder3` produced from the opposite direction.

---

## Widget 1 — `LeaderboardRow` (×4)

Scanned V at x=90 and H at y=89.

| property | measured |
|---|---|
| row size | **115 × 13px** |
| normal fill | `#1C2336`–`#2B3141` L=0.16–0.21 S=0.20–0.32 — dark translucent |
| **player row fill** | `#E1BB1B`–`#EBB721` **L=0.50 S=0.80** — saturated gold |
| row content | delta chip · position number · driver name |
| delta chip | a separate cell at the **left**, holding `-00:00.0` / `+00:00.0` |

**Selection mechanism #9: fill the row with a saturated accent.** The player's row is the
only saturated element in the HUD, which is why it is findable at a glance while driving.

Row height 13px on a 414px screen = **3.1 % of screen height**. Scaled to 1080p that is
**34px** — the same ~30px rail height that `citybuilder1` (31), `citybuilder3` (29),
`citybuilder5` (30) and `gameui8` (32) all landed on. Six independent references now agree
that **a HUD row is ~3 % of screen height**.

That is a better rule than a pixel constant, and it is what the kit should compute from.

## Widget 2 — `PositionReadout` (top-left)

`POSITION` in small letterspaced caps above `3/48` in a large value face. **No plate**, no
icon, left-aligned to the screen edge.

## Widget 3 — `LapReadout` (top-right)

The exact mirror: `LAP` above `1/4`, right-aligned. Label-above-value, small-caps label,
large value — the same construction, mirrored to the opposite corner.

**The label-above-value pair is this family's atom.** Both corners use it, and nothing else
on the screen needs explaining.

## Widget 4 — `TimingTable` (top-right, under widget 3)

Three rows, each a **two-cell strip**: a dark cell holding a right-aligned label
(`TIME`, `LAP`, `BEST TIME`) and a second dark cell holding a monospaced value
(`00:00.000`).

A real two-column table, with the label column narrower than the value column and a 1px
gap between cells. The only table in the entire folder — everything else is a list.

## Widget 5 — `CountdownRing` (centre)

Scanned H at y=143.

| property | measured |
|---|---|
| diameter | **~85px** (x=325..410) |
| stroke | **4px** white, `#F8FAFE` L=0.98 |
| **stroke : diameter** | **0.047** |
| fill | **none** — the scene shows through |
| gap | the ring is **not closed** — it has a break at the top, and the arc sweeps as the count runs |

A **hollow ring with a ~5 % stroke**, sweeping to show time. This is the racing signature
gauge and the kit has nothing like it — `RadialMeter` in the catalogue was recorded from
`rpgui` but never measured. **0.047 is the number.**

## Widget 6 — `Speedometer` (bottom-right)

| part | observed |
|---|---|
| arc | a semicircular tick scale, numbers `1`–`7` around the outside |
| needle | thin, pivoting from the arc's centre |
| digital readout | large `00` inside the arc |
| status row | a row of small square icons beneath the arc |
| gear bar | a segmented strip below the icons |

An **analogue gauge and a digital readout in one widget** — the arc gives rate of change,
the numerals give precision. Worth taking as a pattern for any fast-changing value.

## Widget 7 — `VerticalScale` (bottom-left)

A thin vertical hairline with a small travelling marker and a **rotated caption** along it.
The only rotated text in the folder.

## Widget 8 — `KeyHint` (bottom-left)

Small square glyphs showing keyboard keys — input prompts, drawn as outlined squares with a
letter inside. No plate, hairline stroke only.

---

## Cross-widget rules

1. **A racing skin sets every decoration layer to zero** — no frame, bevel, gloss or
   keyline. Shadow and alpha only.
2. **A HUD row is ~3 % of screen height.** Six references now agree; compute from screen
   height, not from a pixel constant.
3. **Label-above-value in small caps** is the corner-cluster atom.
4. **Selection #9: fill the row with the only saturated colour on screen.**
5. **Ring gauge stroke = 0.047 × diameter**, open arc, no fill.
6. **Pair an arc with a digital readout** for fast-changing values.
7. **Racing is the only genre with a real table** (label column + value column).

## Actions

- [ ] `racing` skin: frame/bevel/gloss/keyline = **0**, plate alpha ≈ 0.5, shadow on.
- [ ] `KitGeometry.RailHeight` ← **0.031 × screen height**, not a constant.
- [ ] Add `RadialMeter` with a measured **stroke ratio 0.047** and an open-arc option.
- [ ] Add `GaugeWithReadout` (arc + numerals) to the catalogue.
- [ ] Add `LabelAboveValue` as a first-class readout layout.
- [ ] `LeaderboardRow`, `TimingTable`, `KeyHint`, `VerticalScale` → catalogue,
      **priority for a racing project**.
