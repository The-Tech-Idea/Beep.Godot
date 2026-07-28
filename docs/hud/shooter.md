# Shooter HUD

**Genre id:** `shooter` · **Main scene:** `shooter_main.tscn` · **Genre HUD script:** `ShooterHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Doom Eternal** | Dense but readable: health/armour/ammo as three corner clusters, each a different colour so peripheral vision resolves them without focus. Chainsaw/flame/grenade cooldowns as discrete pips. |
| **Halo** | The **shield bar above health** convention, plus a motion tracker (radar) bottom-left and a grenade-type selector. Shield recharge has an audible + visual tell. |
| **Call of Duty** | Minimal centre, everything at the edges: ammo bottom-right, minimap top-left, **killfeed top-right**, and a **directional damage indicator** ringing the crosshair. |
| **Vampire Survivors** | The survivors-like template: **full-width XP bar pinned to the top**, run timer centre-top, kill count, and a passive/active item row. Almost no other chrome. |
| **Enter the Gungeon** | Hearts + blanks + keys + casings in a top-left cluster; active-item cooldown as a radial fill. |

**Two sub-families — pick per project:**
- **Arena/FPS** — crosshair-centred, corner clusters, ammo economy.
- **Survivors-like** — XP bar + timer dominate; the player reads *time survived* and *level*, not ammo.

---

## 2. Canonical layout — Arena/FPS

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ┌────────┐                                              ⚔ Player ▸ Grunt  │
│ │ radar  │                                              ⚔ Player ▸ Grunt  │ killfeed
│ │   ▲    │                                                                │
│ └────────┘              ╲   ╱                                             │
│                       ── ✛ ──   crosshair + hitmarker                     │
│                         ╱   ╲                                             │
│                     ◤ damage from this direction ◥                        │
│                                                                           │
│ ┌─────────────────┐                                   ┌──────────────────┐│
│ │ 🛡 ████████░░ 80│  shield                            │      24 / 120    ││ ammo
│ │ ❤ ██████░░░░ 62│  health                            │   ▓ RIFLE  [R]   ││
│ └─────────────────┘                                   └──────────────────┘│
└───────────────────────────────────────────────────────────────────────────┘
```

## Canonical layout — Survivors-like

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ████████████████████░░░░░░░░░░░░░░░░  LV 12          XP bar, full width   │
│                        12:47                          run timer            │
│ 💀 1,284                                                                  │
│                                                                           │
│                       (gameplay)                                          │
│                                                                           │
│ ⓪ⓐⓑⓒ  active items with cooldown sweeps                                  │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Crosshair** | centre | `CrosshairComponent` | spread widens with movement/fire; **hitmarker** flashes on damage dealt | **P0** |
| 2 | **Health** | bottom-left | bar + number | colour shifts green→amber→red; pulse under 25% | **P0** |
| 3 | Shield / armour | above health | second bar | recharges after delay; distinct colour | P1 |
| 4 | **Ammo `mag / reserve`** | bottom-right | large mag, small reserve | mag turns red at ≤25%; **reload ring** during reload | **P0** |
| 5 | Weapon name + icon | bottom-right | icon + label | shows fire mode | P2 |
| 6 | **Damage direction** | ring around crosshair | arc indicator | fades over ~1s; the difference between dying confused and repositioning | **P0** |
| 7 | Radar / minimap | top-left | `MinimapComponent` | enemy blips, facing cone | P1 |
| 8 | Killfeed | top-right | toast stack | `Killer ▸ Victim` | P2 |
| 9 | Wave / objective | top-centre | text + progress | "Wave 4 — 12 left" | P1 |
| 10 | **XP bar + level** | top, full width | bar | survivors-like: the primary progression readout | **P0** (survivors) |
| 11 | **Run timer** | top-centre | `MatchTimerComponent` | survivors-like: the score | **P0** (survivors) |
| 12 | Ability cooldowns | bottom-centre | icon + radial sweep | grenade, dash, ultimate | P1 |
| 13 | Boss health | top-centre | `BossHealthBarComponent` | named, segmented for phases | P1 |
| 14 | Low-health vignette | screen edge | `VignetteComponent` | red pulse under 25% | P1 |

---

## 4. Genre best practices

1. **The centre 40% of the screen stays clear.** Only the crosshair, hitmarker and damage arcs may live there. Everything else hugs corners.
2. **Colour-code the clusters.** Health red, shield blue, ammo white/amber. The player resolves them peripherally, never by focusing.
3. **Ammo is two numbers with different weights.** Magazine large, reserve small. `24/120` read as one number is useless mid-fight.
4. **Feedback must be immediate and multi-channel.** Hitmarker (visual) + tick (audio) for damage dealt; directional arc + screen flash for damage taken.
5. **Reload is a timed affordance**, not a state flag — a radial sweep on the ammo counter tells the player exactly when they can fire again.
6. **Never hide health behind a regenerating shield.** Show both bars stacked; Halo's convention exists because players must know if the next hit is lethal.
7. **Survivors-like inverts the hierarchy:** XP and time are P0; ammo and health are secondary. Do not ship one HUD for both — branch on `genre.json#tuning`.
8. **Damage direction is not optional in 3D/360° combat.** It is the single most-requested missing feature in shooter HUD feedback.

---

## 5. Current state vs target

**Audited `shooter_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Score / Level / Lives / Health / Ammo / Wave labels in one `VBoxContainer` | **all six stacked in one corner** — no clusters |
| `CrosshairComponent` | present; no spread, no hitmarker |
| `HealthComponent`, `LevelingComponent`, `GameOverOnDeathComponent`, `HitSoundComponent` | gameplay side exists — the data is already there |
| `WeatherForecastUI` | present |

**Missing:** health/shield bars, ammo formatting + reload ring, damage direction (P0), killfeed, radar, XP bar, ability cooldowns, boss bar, low-health vignette.

> `LevelingComponent` and `HealthComponent` already emit the signals an XP bar and health bar need. This is the genre where the wiring gap is most obvious: the data exists, the widgets exist, nothing connects them.

---

## 6. Data contract

`ShooterHudComponent` currently binds:

```
score   level   lives   health   ammo   wave
```

Stage 30.5 adds:

```
health        { current, max }
shield        { current, max, recharging bool }
ammo          { mag, reserve, reloading bool, reload_progress 0..1 }
weapon        { name, icon, fire_mode }
xp            { current, next_level }
run_time      float seconds
kills         int
abilities     list of { icon, hotkey, cooldown 0..1 }
damage_from   list of angles (radians, for the direction arcs)
boss          { name, hp 0..1, phases }
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`BossHealthBarComponent` · `BuffBarComponent` · `MatchTimerComponent` (run timer) ·
`ProgressRingComponent` (reload + cooldowns) · `ToastNotificationComponent` (killfeed) ·
`ScreenFlashComponent` + `VignetteComponent` (damage) · `ComboCounterComponent` (streaks) ·
`SafeAreaComponent` · `CounterComponent` (kills)

**Build new:**

| component | responsibility |
|---|---|
| `AmmoCounterComponent` | mag/reserve weighting, low-ammo colour, reload sweep |
| `DamageDirectionComponent` | arc indicators around the crosshair, angle + fade |
| `AbilityBarComponent` | **shared with `rpg`** — icon + hotkey + radial cooldown |
| `MeterBarComponent` | **shared with `rpg`/`survival`/`platformer`** — health/shield bars |

---

## 8. Pitfalls

- The crosshair must be **exactly centred** — anchor to centre, not a margin container, or it drifts at other aspect ratios.
- `mouse_filter = Ignore` on every HUD node; a shooter HUD that eats clicks eats *shots*.
- Damage-direction arcs are drawn, not laid out — they need a `_Draw` override, not `Control` children.
- Survivors-like and arena share one `shooter_main.tscn`. Branch on tuning and toggle whole region containers; do not build two scenes that drift apart.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `ObjectiveList` | Objectives | expanded | fold during firefights |
| `Scoreboard` | Score | collapsed | on-demand by nature |
| `WeaponWheel` | Loadout | collapsed |  |
| `Minimap` | Radar | expanded | never fold by default in a shooter |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../ObjectiveList"]
script = ExtResource("collapsible")
Title = "Objectives"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
