# Top-Down / Adventure HUD

**Genre id:** `topdown` · **Main scene:** `topdown_main.tscn` · **Genre HUD script:** `TopDownHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Zelda: A Link to the Past** | The template: **hearts row** top-left, rupees/bombs/arrows top-centre, and the **equipped-item boxes (A/B)** top-right. What you have equipped is always visible because the whole game is item-swapping. |
| **Stardew Valley** | **12-slot hotbar** across the bottom, energy bar bottom-right, and the **clock/date/season panel** top-right — the player plans the entire day around that clock. |
| **Breath of the Wild** | Hearts + stamina wheel top-left, minimap with compass bottom-left, temperature gauge, and **weapon durability** warnings. Chrome fades when unused. |
| **Hades** | Health bottom-left, boon icons in a row, cast/dash charges, and a room-clear objective indicator. |
| **Don't Starve (top-down survival)** | Overlaps with survival: ring gauges plus a slot-based inventory strip. |

**The through-line:** top-down adventure is defined by the **hotbar/equipped-item slot** and the **clock**. The player is constantly deciding *what to hold* and *how much day is left*.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│ ❤❤❤❤♡                                              ┌────────────────────┐ │
│ ⚡ stamina ████████░░                               │ ☀ Summer 14        │ │
│                                                     │ 6:40 pm            │ │
│                                                     └────────────────────┘ │
│                        (world viewport)             ┌────────┐             │
│                                                     │minimap │             │
│                                                     │   ▲ N  │             │
│                                                     └────────┘             │
│                          [E] Talk to Aldis                                 │
│  💰 1,240                                                                  │
│      ┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐                     │
│      │ 1 │ 2 │[3]│ 4 │ 5 │ 6 │ 7 │ 8 │ 9 │ 0 │ - │ = │  hotbar            │
│      └───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘                     │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Health** | top-left | hearts / pips | discrete, like platformer; empty pips visible | **P0** |
| 2 | Stamina / energy | top-left or bottom-right | bar or wheel | Stardew: bottom-right vertical; BotW: circular wheel near character | P1 |
| 3 | **Hotbar** | bottom-centre | 10–12 slots | icon + count + hotkey + **selection highlight**; scroll wheel cycles; the genre's core control | **P0** |
| 4 | **Clock / date / season** | top-right | panel | time of day, day number, season — Stardew's most-copied element | **P0** |
| 5 | **Minimap + compass** | top-right or bottom-left | `MinimapComponent` | north indicator, discovered-area fog, quest/POI markers | **P0** |
| 6 | **Interaction prompt** | above character or bottom-centre | `InteractionPromptComponent` | `[E] Talk`, `[E] Open` — contextual, appears only in range | **P0** |
| 7 | Currency | bottom-left | icon + number | gold/rupees | P1 |
| 8 | Equipped item (A/B) | top-right | 1–2 large slots | Zelda-style; alternative to a full hotbar | P1 |
| 9 | Dialog box | bottom, wide | `DialogUIComponent` | already present in this genre's toolkit | P1 |
| 10 | Quest objective | top-right, under clock | one line | current step only | P2 |
| 11 | Weather | top-right | icon | `WeatherHUDComponent` | P2 |
| 12 | Tool durability | on hotbar slot | small bar | warns before breaking | P2 |
| 13 | Damage feedback | screen edge | `ScreenFlash` + `Vignette` | on hit | P1 |

---

## 4. Genre best practices

1. **The hotbar is the primary verb.** Number keys *and* scroll wheel, always visible, with a clear selection highlight. This is how the player acts on the world.
2. **The clock drives planning.** Stardew players make every decision against the clock. If the game has a day cycle, the clock is P0, not decoration.
3. **Hearts, not a health bar.** Same reasoning as platformer — the player counts hits.
4. **Interaction prompts are contextual and appear near the target.** A permanent "press E" trains players to ignore it.
5. **The minimap needs a compass.** Top-down worlds rotate the camera rarely, but the player still needs cardinal orientation for directions ("go north").
6. **Show what is equipped, always.** Zelda's A/B boxes exist because swap-cost is the core decision.
7. **Chrome may fade when idle** (BotW) — but the hotbar and clock never fade.
8. **Dialog takes the bottom third and pauses the world.** It is a mode, not an overlay.

---

## 5. Current state vs target

**Audited `topdown_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Score / Level / Lives / Health labels in a `VBoxContainer` | text only; **generic platformer-style readouts, not adventure ones** |
| `MinimapComponent` | present; no compass, no markers, no fog |
| `TopDownController`, `HealthComponent`, `GameOverOnDeathComponent`, `HitSoundComponent` | gameplay side present |
| `pause_subscreen.tscn` (tabbed inventory/map/quests/status) | `PauseSubscreen.cs` (52 lines) **does** have real logic — tabs + save wiring. But its quest list is three hardcoded strings and the 8-col inventory grid holds 9 empty slots |
| `DialogUIComponent`, `WeatherForecastUI` | available |

**Missing:** hotbar (P0), clock/date (P0), hearts (P0), compass (P0), interaction prompt (P0), currency, equipped slots, durability.

> The readouts here are wrong for the genre, not merely unstyled: *Score* and *Lives* are arcade concepts. An adventure HUD wants **time, hearts, hotbar, currency**. Stage 30.6 should replace the stat set, not just restyle it.

---

## 6. Data contract

`TopDownHudComponent` currently binds:

```
score   level   lives   health
```

Stage 30.6 replaces/extends with:

```
health        { current, max }         rendered as hearts
stamina       { current, max }
hotbar        list of { icon, count, durability 0..1 }, selected_index
time_of_day   float 0..1
day / season  int / string
currency      int
equipped      { a: item, b: item }     (Zelda-style variant)
prompt        { key, verb, target } | null
quest         { title, step } | null
weather       string
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`InteractionPromptComponent` (exactly this) · `WeatherHUDComponent` ·
`VignetteComponent` + `ScreenFlashComponent` (damage) · `CounterComponent` (currency) ·
`TooltipComponent` (item info) · `SafeAreaComponent` · `ToastNotificationComponent` (pickups)
Plus `MinimapComponent` and `DialogUIComponent`, already in use.

**Build new:**

| component | responsibility |
|---|---|
| `HotbarComponent` | **shared with `survival`** — slots, counts, hotkeys, selection, scroll cycling, durability |
| `DayNightClockComponent` | **shared with `survival`** — time/day/season panel |
| `PipHealthComponent` | **shared with `platformer`** — hearts |

---

## 8. Pitfalls

- Every shared component here (`Hotbar`, `DayNightClock`, `PipHealth`) is also used by another genre — build them in Stage 30.2 **before** touching this scene, or they will be written twice.
- The hotbar is mouse-interactive; the rest must be `mouse_filter = Ignore`.
- The interaction prompt is world-anchored in most references (floats above the target). Decide screen-space vs world-space **before** building; retrofitting is expensive.
- Replacing Score/Lives with Time/Currency changes `TopDownHudComponent`'s bound keys — update the component and the scene together, or the HUD warns about missing data sources.
- `pause_subscreen.tscn` already covers inventory/map/quests. Do not duplicate that content in the HUD; the HUD only shows the *active* slice.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `QuestTracker` | Quests | expanded |  |
| `Minimap` | Map | expanded |  |
| `InventoryStrip` | Items | expanded |  |
| `DialogHistory` | History | collapsed | reference only |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../QuestTracker"]
script = ExtResource("collapsible")
Title = "Quests"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
