# Survival HUD

**Genre id:** `survival` · **Main scene:** `survival_main.tscn` · **Genre HUD script:** `SurvivalHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Minecraft** | The reference **hotbar**: 9 slots, selection highlight, item counts, hearts + hunger drumsticks above it as discrete pips. Everything else is hidden until relevant (armour row, air bubbles when submerged). |
| **Don't Starve** | Three ring gauges — **health, hunger, sanity** — clustered top-right, plus a day/night/season clock ring. Sanity is the genre's most famous "invisible" stat made visible. |
| **Valheim** | Health/stamina/eitr bars bottom-left, **three food slots** with remaining duration, status-effect row, and a weight indicator that warns before over-encumbrance. |
| **Subnautica** | Health/food/water/**oxygen**, with oxygen becoming the dominant readout underwater — a HUD that reprioritises by context. |
| **The Forest / Rust** | Minimal in-world HUD, heavy on **contextual prompts** and a radial crafting menu. |

**The through-line:** survival HUDs track **multiple decaying meters** the player must keep topped up, and a **hotbar** for the tools that top them up. The critical design job is *warning before a meter empties*, not reporting after.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│                                                    ┌────────┐  ☀ Day 12   │
│                                                    │ ◐ clock│  🌡 -4°C    │
│                                                    └────────┘  🌧 Rain    │
│                                                                           │
│                       (world viewport)                     ┌────────┐     │
│                                                            │minimap │     │
│                                                            └────────┘     │
│  ❄ 🥩 💧   status effects with durations                                  │
│  ❤ ████████████░░░░  84                                                   │
│  🍖 ███████░░░░░░░░░  52   ← amber, warned at 30                          │
│  💧 ████░░░░░░░░░░░░  28   ← red + pulse, critical                        │
│  ⚡ ██████████████░░  91                                                  │
│           ┌───┬───┬───┬───┬───┬───┬───┬───┬───┐                           │
│           │ 1 │ 2 │[3]│ 4 │ 5 │ 6 │ 7 │ 8 │ 9 │  hotbar, slot 3 selected  │
│           └───┴───┴───┴───┴───┴───┴───┴───┴───┘                           │
│                    [E] Gather Wood        contextual prompt               │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Health** | bottom-left | bar or pips | red pulse + vignette when critical | **P0** |
| 2 | **Hunger** | bottom-left | bar | amber at 30%, red at 15%, **toast warning on crossing** | **P0** |
| 3 | **Thirst** | bottom-left | bar | same thresholds; usually decays faster than hunger | **P0** |
| 4 | **Stamina** | bottom-left | bar | depletes on sprint; **flashes when an action is blocked** by low stamina | **P0** |
| 5 | **Hotbar** | bottom-centre | 9 slots | icon + count + hotkey + selection highlight; scroll-wheel cycles | **P0** |
| 6 | Day/night clock | top-right | ring or dial | day number + time of day; night is the danger signal | P1 |
| 7 | Temperature | top-right | number + icon | `TemperatureComponent` already exists in the scene | P1 |
| 8 | Weather | top-right | icon | `WeatherHUDComponent` (built, unused) | P2 |
| 9 | Status effects | above meters | `BuffBarComponent` | cold / wet / poisoned / well-fed, with durations | P1 |
| 10 | **Critical warning** | screen edge | `VignetteComponent` + toast | fires *before* the meter empties, not after | **P0** |
| 11 | Interaction prompt | bottom-centre | `InteractionPromptComponent` | `[E] Gather Wood` | P1 |
| 12 | Tool durability | near hotbar | small bar on the slot | warns before breaking | P2 |
| 13 | Minimap / compass | top-right | `MinimapComponent` | often compass-only in this genre | P2 |
| 14 | Oxygen | above health | bar, **contextual** | appears only underwater | P2 |
| 15 | Weight / encumbrance | near hotbar | `current/max` | turns red when over | P3 |

---

## 4. Genre best practices

1. **Warn before empty, not at empty.** Every meter needs threshold events (30% amber, 15% red + toast). A meter that silently hits zero is the genre's cardinal sin.
2. **Meters are bars, never text.** `Thirst: 28` cannot be read while being chased. Four stacked bars with distinct colours can.
3. **Distinct colour AND distinct icon per meter.** Colour-blind players need the icon; peripheral vision needs the colour.
4. **The hotbar is the primary verb.** It must never be covered, must show counts, and must respond to number keys *and* scroll wheel.
5. **Contextual readouts appear only in context.** Oxygen underwater, temperature when extreme. Permanent chrome for occasional state trains the player to ignore it.
6. **The clock is a danger signal, not a decoration.** Approaching night should be legible at a glance — that is what the ring shape conveys.
7. **Durability warns before breaking.** Losing a tool without warning is the second most-cited survival HUD failure.
8. **Stamina must explain refusal.** If an action is blocked by stamina, flash the stamina bar — otherwise the input reads as broken.

---

## 5. Current state vs target

**Audited `survival_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Health / Hunger / Thirst / Stamina labels in a `VBoxContainer` | **text only** — the four meters that define the genre are plain text |
| `TemperatureComponent` | present in the scene |
| `MinimapComponent`, `WeatherForecastUI`, `LevelLoaderComponent` | present |
| `GenreScreenComponent` -> `backpack.tscn`, `crafting.tscn`, `world_map.tscn` | `Crafting.cs` (56 lines) has real logic; **`Backpack.cs` and `WorldMap.cs` are 17-line Close-only mockups** |

**Missing:** all four meters as bars (P0), hotbar (P0), critical warnings (P0), status effects, day/night clock, interaction prompt, durability.

> This genre has the **highest reuse ratio** of any: `BuffBar`, `WeatherHUD`, `Vignette`,
> `ToastNotification` and `InteractionPrompt` are all built and unused, and
> `TemperatureComponent` is already in the scene. Most of Stage 30.6 here is wiring.
>
> **But note:** `SurvivalHudComponent` binds all four meters through `Placeholder(...)` —
> i.e. **none of health/hunger/thirst/stamina has a real data source**. The scene text is
> fabricated. `backpack.tscn`'s `24.8 / 40 kg` carry weight is likewise a literal.
>
> **Research note.** Survival-design guidance is explicit that health/stamina/hunger/thirst
> should be specified *first*, that vitals belong bottom- or top-left, and that colour
> convention matters (health red/green, stamina green, mana blue). It also argues for
> **themed meters over plain bars** — a stamina bubble that flickers reads better than a
> rectangle. `MeterBarComponent` should therefore support a themed fill mode, not only a bar.
> Valheim's three **food slots with countdown timers** are the pattern worth copying for the
> "well-fed" buff, rather than a generic buff row.

---

## 6. Data contract

`SurvivalHudComponent` currently binds:

```
health   hunger   thirst   stamina
```

Stage 30.6 adds:

```
health / hunger / thirst / stamina   { current, max, warn_at, critical_at }
hotbar          list of { icon, count, durability 0..1 }, selected_index
time_of_day     float 0..1
day             int
temperature     float  (TemperatureComponent already provides this)
weather         string
status_effects  list of { icon, remaining_seconds }
oxygen          { current, max } | null   (null = hide)
weight          { current, max }
prompt          { key, verb } | null
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`BuffBarComponent` (status effects) · `WeatherHUDComponent` · `VignetteComponent` (critical) ·
`ToastNotificationComponent` (threshold warnings) · `InteractionPromptComponent` ·
`ProgressRingComponent` (clock ring) · `SafeAreaComponent` · `TooltipComponent` (item info)

**Build new:**

| component | responsibility |
|---|---|
| `MeterBarComponent` | **shared with `rpg`/`shooter`/`platformer`** — bar + icon + threshold colours + warn events |
| `HotbarComponent` | **shared with `topdown`** — n slots, counts, hotkeys, selection, scroll cycling |
| `DayNightClockComponent` | **shared with `topdown`** — ring/dial, day counter |

---

## 8. Pitfalls

- Four meters stacked at 44px each is 176px of screen — use a compact variant (~22px bars with inline icon) or they dominate a 720p screen.
- Threshold warnings must **fire once on crossing**, not every frame. Latch the state per meter.
- The hotbar is the only mouse-interactive HUD element here; everything else `mouse_filter = Ignore`.
- Contextual elements (oxygen, temperature) should be **hidden**, not zero-alpha — a hidden `Control` costs no layout.
- `TemperatureComponent` already exists in the scene but is not bound to any label; check its signal before adding a second source of truth.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `VitalsPanel` | Vitals | expanded | never fold by default — the genre IS the vitals |
| `Hotbar` | Hotbar | expanded |  |
| `CraftingPanel` | Crafting | collapsed | modal-ish, opened deliberately |
| `StatusEffects` | Effects | expanded | fold when clean |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../VitalsPanel"]
script = ExtResource("collapsible")
Title = "Vitals"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
