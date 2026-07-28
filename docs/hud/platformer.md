# Platformer HUD

**Genre id:** `platformer` · **Main scene:** `platformer_main.tscn` · **Genre HUD script:** `PlatformerHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Celeste** | Almost **no HUD at all**. Strawberry count appears only on collection; the speedrun timer is opt-in. The screen belongs to the level, and dash state is communicated by the *character's hair colour*, not chrome. |
| **Hollow Knight** | Top-left cluster: **masks** (health as discrete pips), the soul orb that fills as you hit enemies, geo counter, and charm notches. Everything diegetic and hand-drawn. |
| **Super Mario World** | The classic top strip: score, coins, world/level, time, lives, and the reserve-item box centre-top. |
| **Ori and the Blind Forest** | Health and energy as small orb clusters top-left, ability wheel on hold, and almost nothing else during play. |
| **Shovel Knight** | Deliberate retro strip: health bar, magic bar, gold, and item slot in a fixed top band. |

**The through-line:** platformer HUDs are the **sparsest in games**. The player's eyes are on the character and the next platform. Anything that is not health, lives, or a collectible count is a distraction.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ❤❤❤♡♡     ×3                                        🍓 12/20    01:24.31 │
│ health pips  lives                                  collectibles   timer  │
│                                                                           │
│                                                                           │
│                        (level viewport)                                   │
│                                                                           │
│                              ◆                                            │
│                           character                                       │
│                                                                           │
│                          ⟳ dash ready                                     │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘
   red vignette pulses at the screen edge when health is low
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Health pips** | top-left | discrete units (hearts/masks) | **never a percentage** — the player counts remaining hits; empty pips stay visible | **P0** |
| 2 | Lives | top-left | icon + `×3` | flash on loss | P1 |
| 3 | **Collectibles** | top-right | icon + `12/20` | pops/scales on pickup, then settles | **P0** |
| 4 | Score | top-right | number | arcade-style platformers only | P2 |
| 5 | Level / world | top-centre | text | shown briefly on entry, then fades | P2 |
| 6 | **Run timer** | top-right | `MatchTimerComponent` | `mm:ss.cc`; speedrun mode; **off by default** | P1 |
| 7 | Ability recharge | near character or bottom-centre | radial or pip | dash/double-jump availability | P1 |
| 8 | **Damage feedback** | screen edge | `ScreenFlash` + `Vignette` | red flash on hit; sustained vignette at 1 pip | **P0** |
| 9 | Checkpoint toast | centre | toast | "Checkpoint" — brief | P2 |
| 10 | Boss health | top-centre | `BossHealthBarComponent` | only during boss fights | P1 |
| 11 | Death / retry | centre | fade + counter | Celeste-style death count | P2 |

---

## 4. Genre best practices

1. **Discrete health, not a bar.** A platformer player asks "how many more hits can I take" — that is a countable question. A percentage bar answers the wrong one.
2. **Empty pips remain visible.** Showing `❤❤♡♡♡` tells the player their maximum; hiding lost health hides progression.
3. **Less is more.** Celeste ships with essentially no HUD and is the genre's critical high point. Default to hiding, not showing.
4. **Collectible pickups need a pop.** Scale-up + settle on the counter is the reward feedback; a silent increment feels unrewarding.
5. **Damage must be felt at the screen edge.** The character is small and the camera moves — a red vignette communicates faster than a shrinking pip.
6. **Ability state belongs near the character where possible.** Celeste's hair-colour dash tell is the gold standard; a HUD pip is the fallback.
7. **The timer is opt-in.** Speedrunners want it; everyone else finds it stressful. Bind it to a setting.
8. **Never obscure the lower two-thirds.** That is where the player is looking and where the platforms are.

---

## 5. Current state vs target

**Audited `platformer_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Score / Level / Lives / Health labels in a `VBoxContainer` | text only; **health is a number, not pips** |
| `HealthComponent`, `GameOverOnDeathComponent`, `HitSoundComponent` | gameplay side exists and emits the signals a pip display needs |
| `PlatformerController`, `LevelLoaderComponent`, `WeatherForecastUI` | present |

**Missing:** pip health (P0), collectible counter with pop (P0), damage feedback (P0), run timer, ability recharge, boss bar.

> Platformer needs the **smallest** amount of new work of any genre — one new component (`PipHealth`) plus four reuses. It is a good candidate to ship first in Stage 30.7 and validate the shared-component approach.

---

## 6. Data contract

`PlatformerHudComponent` currently binds:

```
score   level   lives   health
```

Stage 30.7 adds:

```
health         { current, max }        rendered as pips
lives          int
collectibles   { collected, total, icon }
run_time       float seconds   (hidden unless the setting is on)
abilities      list of { icon, ready bool, cooldown 0..1 }
boss           { name, hp 0..1 } | null
deaths         int
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`CounterComponent` (collectibles) · `MatchTimerComponent` (run timer) ·
`ScreenFlashComponent` + `VignetteComponent` (damage) · `ProgressRingComponent` (ability recharge) ·
`BossHealthBarComponent` · `ToastNotificationComponent` (checkpoints) · `SafeAreaComponent`

**Build new:**

| component | responsibility |
|---|---|
| `PipHealthComponent` | **shared with `topdown`** — n discrete units, filled/empty/temporary states, animated loss |

---

## 8. Pitfalls

- Pips must handle **partial units** (half-hearts) and **temporary units** (Hollow Knight's lifeblood) or the component will be rebuilt later.
- The entire HUD is non-interactive — set `mouse_filter = Ignore` on all of it.
- Collectible pop animations must not shift layout; animate `scale`/`offset_transform`, never `custom_minimum_size`, or the whole row jitters.
- The run timer updating every frame re-lays-out the label. Update at 10 Hz and use a fixed-width font, or it jitters horizontally.
- `HealthComponent` already emits a health-changed signal — bind to it rather than polling in `_Process`.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `StatsPanel` | Stats | expanded | score/lives/level — small, fold for a clean shot |
| `CollectiblesPanel` | Collectibles | collapsed |  |
| `AbilityBar` | Abilities | expanded |  |
| `TimerPanel` | Timer | expanded | never fold in a timed mode |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../StatsPanel"]
script = ExtResource("collapsible")
Title = "Stats"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
