# Puzzle HUD (Match-3 / Falling-block)

**Genre id:** `puzzle` · **Main scene:** `puzzle_main.tscn` · **Genre HUD script:** `PuzzleHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Candy Crush Saga** | The objective panel is the HUD: **icon + `collected / target`** for each goal, moves remaining top-centre, and a **three-star progress bar** on the score. Booster tray docked along the bottom. Everything else is board. |
| **Bejeweled** | Score with a continuously-filling level meter, plus a timer variant. Established cascade/combo popups as feedback. |
| **Tetris (modern guideline)** | **Next-piece queue** (up to 5) on the right, **hold slot** on the left, level / lines / score in a left column, and combo + back-to-back indicators near the well. |
| **Puyo Puyo** | Chain counter as huge centre-screen text — the chain *is* the score. |
| **Two Dots / Homescapes** | Objective-first HUD with animated goal icons that "collect" toward the panel on match. |

**The through-line:** in a puzzle game the board is the entire screen budget. The HUD answers exactly three questions: **what am I trying to do**, **how much do I have left**, **how am I doing**.

---

## 2. Canonical layout — match-3

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ┌───────────────────┐      ┌─────────┐      ┌──────────────────────────┐  │
│ │ 🍬 24/40  🍭 8/15 │      │  MOVES  │      │ 12,480                   │  │
│ │   objectives      │      │   17    │      │ ★───★────☆   star bar    │  │
│ └───────────────────┘      └─────────┘      └──────────────────────────┘  │
│                                                                           │
│                        ┌─────────────────┐                                │
│                        │                 │                                │
│                        │   match-3 board │      ✦ COMBO x4  (popup)       │
│                        │                 │                                │
│                        └─────────────────┘                                │
│                                                                           │
│              [ 🔨 ] [ 💣 ] [ 🔀 ]   booster tray                          │
└───────────────────────────────────────────────────────────────────────────┘
```

## Canonical layout — falling-block

```
┌───────────────────────────────────────────────────────────────────────────┐
│  ┌──────┐        ┌───────────────┐        ┌──────┐                        │
│  │ HOLD │        │               │        │ NEXT │                        │
│  │  ▣   │        │     well      │        │  ▤   │                        │
│  └──────┘        │               │        │  ▥   │                        │
│  SCORE 12,480    │               │        │  ▦   │                        │
│  LEVEL 7         │               │        └──────┘                        │
│  LINES 62        └───────────────┘                                        │
│                    B2B · COMBO x3                                         │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Objective panel** | top-left | icon + `have / need` per goal | ticks up with a fly-to animation on match; **checkmark when complete** | **P0** |
| 2 | **Moves or time** | top-centre | large number | turns amber ≤5, red ≤3, pulses at 1 | **P0** |
| 3 | **Score + star bar** | top-right | number + 3-star progress | stars pop as thresholds pass — the retention hook | **P0** |
| 4 | Combo / cascade | centre overlay | `ComboCounterComponent` | `x2 · x3 · x4` scaling text, fades | P1 |
| 5 | Booster tray | bottom | 3–5 slots | count badge; disabled at 0 | P1 |
| 6 | **Next-piece queue** | right of well | 3–5 previews | falling-block only | **P0** (block) |
| 7 | **Hold slot** | left of well | single preview | greyed once used this piece | **P0** (block) |
| 8 | Level / lines | left column | text | falling-block progression | P1 (block) |
| 9 | B2B / combo | near well | text tags | falling-block scoring state | P2 (block) |
| 10 | No-more-moves | centre | modal toast | shuffle prompt or auto-shuffle notice | P1 |
| 11 | Level complete / failed | centre | full panel | already exist as `level_complete.tscn` / `level_failed.tscn` | done |
| 12 | Pause | top-right corner | small button | must not sit near the board | P2 |

---

## 4. Genre best practices

1. **The objective is always visible and always first.** A player who forgets the goal stops playing. Icon + count, never text-only.
2. **Goal progress animates toward the panel.** Matched pieces flying to the objective icon is what makes progress feel earned.
3. **Moves remaining is the tension dial.** Escalate its treatment as it drops — colour, then scale, then pulse.
4. **The star bar is the retention loop**, not decoration. Show the *next* threshold, not just current score.
5. **Never cover the board.** Combo popups render over the board but fade fast (<1s) and never block a match.
6. **Boosters show counts and disable at zero.** A booster that looks available but does nothing is a bug report.
7. **Falling-block needs next AND hold.** Modern guideline play is impossible without both; they are not optional polish.
8. **The board is the screen budget.** At 720p, HUD chrome should occupy the top ~15% and bottom ~12%, no more.

---

## 5. Current state vs target

**Audited `puzzle_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Score / Target / Moves labels in a `VBoxContainer` | text only, **stacked in one corner** — the objective is not a panel |
| `ScoreLabel` carried a hardcoded `font_size = 40` | fixed in Stage 25 — now the `BeepDisplay` role |
| `Match3BoardComponent` | the board logic exists and emits scoring signals |
| `level_complete.tscn`, `level_failed.tscn`, `pre_level.tscn`, `level_map.tscn` | **already built** — the meta screens are the most complete of any genre |

**Missing:** objective panel with icons (P0), star progress bar (P0), moves emphasis (P0), combo popups, booster tray, next/hold (falling-block).

> Puzzle has the **best meta-game screens** of any genre here (pre-level, level map, complete, failed all exist) and the **weakest in-game HUD**. The gap is narrow and well-defined.

---

## 6. Data contract

`PuzzleHudComponent` currently binds:

```
score   target   moves
```

Stage 30.7 adds:

```
objectives    list of { icon, have, need, complete bool }
moves         { remaining, warn_at 5, critical_at 3 }   or time { remaining }
score         { value, stars [t1,t2,t3] }
combo         int   (0 = hide)
boosters      list of { icon, count, enabled }
next_queue    list of piece ids     (falling-block)
hold          piece id | null       (falling-block)
level / lines int                   (falling-block)
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`ComboCounterComponent` (exactly this) · `CounterComponent` (score) ·
`ProgressRingComponent` or a star bar · `MatchTimerComponent` (timed modes) ·
`ToastNotificationComponent` ("No more moves") · `BadgeComponent` (booster counts) ·
`ModalComponent` (shuffle prompt) · `SafeAreaComponent`

**Build new:**

| component | responsibility |
|---|---|
| `ObjectivePanelComponent` | icon + have/need rows, completion state, fly-to-target animation hook |
| `BoosterTrayComponent` | slots, counts, enable/disable, selection |
| `PiecePreviewComponent` | next queue + hold (falling-block only) |

---

## 8. Pitfalls

- The booster tray is **mouse-interactive**; the board is too. Everything else `mouse_filter = Ignore`.
- Combo popups must not intercept clicks mid-cascade — set `Ignore` explicitly on the popup.
- Star thresholds come from level data, not the HUD. Feed them in; do not hardcode `[1000, 2500, 5000]`.
- Match-3 and falling-block share `puzzle_main.tscn`. Branch on `genre.json#tuning`; do not fork the scene.
- `ScoreLabel` now uses `BeepDisplay`. Do not reintroduce a hardcoded font size.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `ObjectivePanel` | Goals | expanded | fold once the goal is known |
| `MoveCounter` | Moves | expanded | never fold by default — it is the fail condition |
| `PowerupBar` | Boosters | expanded |  |
| `HintPanel` | Hints | collapsed | on-demand |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../ObjectivePanel"]
script = ExtResource("collapsible")
Title = "Goals"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
