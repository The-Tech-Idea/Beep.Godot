# Racing HUD

**Genre id:** `racing` · **Main scene:** `racing_main.tscn` · **Genre HUD script:** `RacingHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Forza Motorsport / Horizon** | Analog **tachometer arc with a shift light**, large gear number, digital speed beneath. Lap delta (`-0.42`) shown against the best lap, colour-coded green/red — the number that makes a lap feel fast. |
| **Gran Turismo** | Sector splits and a full lap-time table; a track map with live car dots and sector colouring. |
| **Mario Kart** | Arcade inversion: **item box top-left** is the most important element, position `3/8` huge, lap `2/3`, coin count. No tachometer at all. |
| **Need for Speed** | Big digital speed, **nitrous bar**, heat/pursuit meter, and a minimal track line rather than a full map. |
| **F1 20xx** | DRS indicator, ERS deployment bar, tyre wear and fuel delta — the "systems" end of the genre. |

**Two sub-families:**
- **Sim/circuit** — tachometer, gear, delta, sector splits, tyre/fuel.
- **Arcade/kart** — position and item box dominate; speed is decorative.

---

## 2. Canonical layout — sim

```
┌───────────────────────────────────────────────────────────────────────────┐
│ LAP 2/3          ┌──────────────────────────┐                             │
│ POS 3/8          │  1:24.źź1   BEST 1:23.99 │  lap time + best            │
│                  │  ▼ -0.42                 │  delta (green = faster)     │
│                  └──────────────────────────┘                             │
│                                                                           │
│                        (track viewport)                                   │
│                                                                           │
│ ┌──────────┐                                        ╭──────────────────╮  │
│ │ track    │                                        │   ╭─────────╮    │  │
│ │  ●   ○   │                                        │  ╱  9,400   ╲  6 │  │
│ │    ○     │                                        │ │   RPM      │gear│ │
│ └──────────┘                                        │  ╲ 214 km/h ╱    │  │
│  BOOST ███████░░░                                   │   ╰─────────╯    │  │
│                                                     ╰──────────────────╯  │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Speed** | bottom-right | large digital + unit | the single biggest number on screen | **P0** |
| 2 | **Tachometer** | bottom-right, around speed | arc gauge | **shift light** flashes near redline | **P0** (sim) |
| 3 | **Gear** | bottom-right | large single character | `1..6`, `N`, `R` | **P0** (sim) |
| 4 | **Lap `2/3`** | top-left | text | flashes on final lap | **P0** |
| 5 | **Position `P3/8`** | top-left | large text | changes flash green (gained) / red (lost) | **P0** |
| 6 | **Lap delta** | top-centre | signed time, coloured | `-0.42` green faster, `+0.18` red slower — **the defining sim readout** | **P0** (sim) |
| 7 | Current + best lap | top-centre | two times | current ticks live | P1 |
| 8 | Sector splits | top-centre | 3 coloured blocks | purple = best ever, green = personal best | P2 |
| 9 | **Track map** | bottom-left | `MinimapComponent` | live car dots, player highlighted | P1 |
| 10 | Boost / nitrous | bottom-left | bar or arc | fills with drift/draft | P1 (arcade) |
| 11 | **Item box** | top-left | slot + icon | roulette animation on pickup — the arcade P0 | **P0** (kart) |
| 12 | Coins / score | top-left | counter | arcade | P2 |
| 13 | Countdown | centre | `3 · 2 · 1 · GO` | race start | P1 |
| 14 | Race finished panel | centre | results | position, total time, best lap | P1 |
| 15 | Tyre / fuel | bottom-left | small bars | sim-heavy only | P3 |

---

## 4. Genre best practices

1. **Speed is the largest element on screen.** It is the one number read continuously at 200 km/h.
2. **The delta is the sim's score.** Without `+/-` against the best lap, the player cannot tell whether a corner was good. Colour it, and place it where it is readable without leaving the racing line.
3. **The tachometer needs a shift light.** An arc alone does not convey "shift now" at speed — a colour break at redline does.
4. **Position changes must announce themselves.** A flash on gain/loss; the number alone is missed mid-corner.
5. **Track map orientation is a decision, not a default.** Either always north-up (learnable) or rotate with the car (intuitive). Pick one and keep it.
6. **Arcade inverts the hierarchy.** In kart racers the item box outranks the speedometer; ship a tuning flag, not a second scene.
7. **The bottom-centre is where the car is.** Keep chrome in the corners — the driver's eyeline is the vanishing point.
8. **Final-lap and last-corner states deserve emphasis.** Flashing the lap counter on the final lap is a cheap, high-value cue.

---

## 5. Current state vs target

**Audited `racing_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Lap / Position / LapTime / Speed / SpeedUnit labels in a `VBoxContainer` | text only, **stacked in one corner** |
| `SpeedLabel` carried a hardcoded `font_size = 56` | fixed in Stage 25 — now the `BeepDisplay` role |
| `WeatherForecastUI`, `LevelLoaderComponent` | present |

**Missing:** tachometer (P0), gear (P0), lap delta (P0), track map, boost meter, sector splits, item box, countdown, results panel.

> Racing is the genre where the current HUD is furthest from its references — four stacked labels versus an instrument cluster. It is also the genre with the **fewest reusable components**, so expect more new code here than elsewhere.

---

## 6. Data contract

`RacingHudComponent` currently binds:

```
lap   position   lap_time   speed
```

Stage 30.7 adds:

```
speed        { value, unit }
rpm          { current, redline }
gear         int | "N" | "R"
lap          { current, total }
position     { current, total }
lap_time     { current, best, delta }      delta signed seconds
sectors      list of { time, rating }      rating: purple/green/yellow
boost        0..1
item         { icon, rolling bool } | null
racers       list of { track_progress 0..1, is_player }
countdown    int | null
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`ProgressRingComponent` (tachometer arc, boost) · `MinimapComponent` (track map) ·
`CounterComponent` (coins) · `MatchTimerComponent` (race clock) ·
`ToastNotificationComponent` (position changes, final lap) · `SafeAreaComponent` ·
`ScreenFlashComponent` (boost/collision)

**Build new:**

| component | responsibility |
|---|---|
| `SpeedometerComponent` | tachometer arc + needle + redline/shift light + gear + digital speed |
| `LapDeltaComponent` | signed delta vs best, colour-coded, sector split blocks |
| `MeterBarComponent` | **shared** — boost/nitrous |

---

## 8. Pitfalls

- A tachometer is a `_Draw` job (arc, needle, tick marks) — a `ProgressBar` bent into a circle will not read as an instrument.
- Lap delta must be **monospaced or fixed-width**, or the number jitters horizontally as digits change and becomes unreadable at speed.
- The track map needs the track *shape*, which `MinimapComponent` does not know — either feed it a spline or accept a simplified progress bar for v1.
- Sim vs arcade share one scene: toggle whole region containers from `genre.json#tuning`, do not fork `racing_main.tscn`.
- `SpeedLabel` now uses the `BeepDisplay` type variation (2.5× base). Do not reintroduce a hardcoded font size.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `Leaderboard` | Positions | expanded | fold on a hot lap |
| `LapTimes` | Splits | expanded |  |
| `Minimap` | Track | expanded | never fold by default |
| `TuningPanel` | Tuning | collapsed | pit/garage only |

### Rules

- **The header always survives the fold.** It is inserted as a *sibling above* the panel, not
  as a child — a child would be folded away with everything else, leaving no way back.
- **Collapsed means zero space, not just hidden.** The component drops
  `custom_minimum_size.y` to 0 and clears `visible`, so the container reflows and neighbours
  take the freed room. A merely-transparent panel still eats clicks.
- **State persists.** The component is `ISaveable` and joins the `saveables` group, keyed on the
  panel name (`hud.collapsed.<panel>`). A player who folded a panel expects it folded after a
  reload; a HUD that silently reopens everything on load is exactly the annoyance this removes.
- **Never default the genre's core loop to collapsed.** The panels marked *never fold by
  default* above are the ones a player reads continuously — hiding them behind a click makes
  the game worse, not cleaner.
- **Bind a key where the panel is toggled often** via `ToggleAction`; click-only is fine for
  the rest.

### Wiring

```
[node name="Collapse" type="Node" parent="HUD/Root/.../Leaderboard"]
script = ExtResource("collapsible")
Title = "Positions"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
