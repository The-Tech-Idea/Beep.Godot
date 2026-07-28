# RPG HUD

**Genre id:** `rpg` · **Main scene:** `rpg_main.tscn` · **Genre HUD script:** `RpgHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Diablo II / III** | The **two orbs** — health left, mana right — flanking a central **skill bar**. Orbs drain as liquid, readable from the corner of the eye during a fight. Belt/potion slots sit above the bar. |
| **World of Warcraft** | Player frame + **target frame** + target-of-target, cast bar, and a buff/debuff row with numeric durations. Established the convention that you watch the *target's* bar, not your own. |
| **The Witcher 3** | Minimal by default: small health bar, sign cooldowns as radial icons, quest tracker top-right with distance-to-objective, and a minimap with quest markers. |
| **Skyrim** | Three tapering arcs (health/magicka/stamina) that **fade out when full** — chrome only appears when it carries information. Compass strip along the top instead of a minimap. |
| **Final Fantasy XIV** | Party frames, cast bars, and a hotbar system with multiple pages; the reference for cooldown/GCD readability. |

**The through-line:** an RPG HUD is built around **resources you spend** and **cooldowns you wait for**. The player's attention loops: my resources → target's health → what's off cooldown.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│                     ┌──────────────────────┐         ┌───────────────────┐│
│                     │ Bandit Chief    Lv 14│ target  │ ◈ Slay the Chief  ││ quest
│                     │ ❤ ███████████░░░  78%│         │   ▸ 2 / 5 bandits ││ tracker
│                     └──────────────────────┘         │ ◈ Return to Aldis ││
│                                                       └───────────────────┘│
│                                                        ┌────────┐          │
│                        (world viewport)                │minimap │          │
│                                                        │  ◆  ▲  │          │
│                                                        └────────┘          │
│  🔥 ⚡ 🛡 ☠   buffs / debuffs with durations                               │
│                                                                           │
│         ███████████████░░░░░░  LV 14   XP                                 │
│  ╭───╮  ┌───┬───┬───┬───┬───┬───┐  ╭───╮                                  │
│  │ ❤ │  │ 1 │ 2 │ 3 │ 4 │ 5 │ 6 │  │ ✦ │   health orb · hotbar · mana orb │
│  ╰───╯  └───┴───┴───┴───┴───┴───┘  ╰───╯                                  │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Health** | bottom-left | orb or bar | drains visually; pulse + vignette under 25% | **P0** |
| 2 | **Mana / stamina** | bottom-right | orb or bar | separate colour; regen visible | **P0** |
| 3 | **Ability hotbar** | bottom-centre | 6–12 slots | icon + **hotkey label** + **radial cooldown sweep** + charge count | **P0** |
| 4 | **XP bar + level** | above hotbar | thin full-width bar | fills to next level; level-up flash | **P0** |
| 5 | **Target nameplate** | top-centre | name + level + HP bar | appears on target, hides on deselect; elite/boss framing | **P0** |
| 6 | Buff / debuff row | above hotbar or under player frame | `BuffBarComponent` | icon + stack count + **duration countdown** | P1 |
| 7 | Quest tracker | top-right | collapsible list | current objective bolded, sub-steps `2/5`, distance to marker | P1 |
| 8 | Minimap | top-right, below tracker | `MinimapComponent` | quest markers, NPC/enemy blips, compass ring | P1 |
| 9 | Cast bar | above hotbar | progress bar | interruptible tell; only while casting | P2 |
| 10 | Potion / belt | beside health orb | 2–4 quick slots | count + cooldown | P2 |
| 11 | Currency | top-left or menu | icon + number | gold, souls | P3 |
| 12 | Boss frame | top-centre | `BossHealthBarComponent` | replaces target frame; phase segments | P1 |
| 13 | Damage numbers | world-space | floating text | crits emphasised; **toggle in settings** (`DamageNumbersEnabled` already exists) | P2 |

---

## 4. Genre best practices

1. **Health and mana must be readable without focusing.** Orbs work because shape + fill + colour survive peripheral vision; a thin bar with a number does not.
2. **Every hotbar slot shows its hotkey.** The keyboard drives combat; the icon alone is not enough.
3. **Cooldowns are radial sweeps, not greyed icons.** The player needs *time remaining*, not a boolean.
4. **The target frame is as important as the player frame.** In combat the player watches the target's health bar more than their own.
5. **Buff durations are numeric under ~10s.** An icon that merely "is there" cannot be planned around.
6. **The quest tracker is collapsible and shows one objective bolded.** A wall of quests is ignored.
7. **Fade chrome that carries no information** (Skyrim's approach): a full health bar can fade to 40% opacity out of combat.
8. **Damage numbers are a setting, not a default.** `SettingsComponent.DamageNumbersEnabled` already exists — bind to it.

---

## 5. Current state vs target

**Audited `rpg_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Level / Health / Mana / Quest labels in a `VBoxContainer` | text only, no bars, no orbs |
| `MinimapComponent` | present; no quest markers, no compass |
| `GenreScreenComponent` -> `character.tscn`, `inventory.tscn`, `quests.tscn` | `Quests.cs` (38 lines) has real logic; **`Inventory.cs` and `Character.cs` are 17-line Close-only mockups** |
| `RpgHudComponent` | `level` binds for real; **`health`, `mana`, `quest` are all `Placeholder(...)`** |
| `LevelLoaderComponent`, `WeatherForecastUI` | present |

**Missing:** health/mana orbs (P0), ability hotbar (P0), cooldowns (P0), XP bar (P0), target nameplate (P0), buff row, quest tracker, cast bar, potion slots.

> **CORRECTION (audit 2026-07-26).** An earlier draft called these screens "correct". They
> are not. `Inventory.cs` and `Character.cs` are **17 lines each and only wire the Close
> button** — every value on them is a hardcoded scene literal (`1,250 g`, `Strength 15`,
> `Level 1 - Adventurer`). The *layout* is right (6x2 item grid, 3 equip slots, 8-stat grid);
> the data binding does not exist. `Quests.cs` (38 lines) does have real logic.
>
> Also missing versus real RPG inventories: **no carry weight and no slots-used counter** —
> an inventory without a capacity constraint is missing the screen's whole point.

---

## 6. Data contract

`RpgHudComponent` currently binds:

```
level   health   mana   quest
```

Stage 30.5 adds:

```
health / mana / stamina   { current, max, regen }
xp                        { current, next_level }
abilities                 list of { icon, hotkey, cooldown 0..1, charges }
target                    { name, level, hp 0..1, elite bool } | null
buffs                     list of { icon, stacks, remaining_seconds }
quests                    list of { title, objective, progress "2/5", distance }
casting                   { name, progress 0..1, interruptible } | null
potions                   list of { icon, count, cooldown }
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`BuffBarComponent` (buffs/debuffs — exactly this) · `BossHealthBarComponent` (boss/target frame) ·
`ProgressRingComponent` (cooldowns, cast bar) · `TooltipComponent` (ability tooltips) ·
`VignetteComponent` (low health) · `CounterComponent` (currency) · `SafeAreaComponent` ·
`ToastNotificationComponent` (level up, quest complete) · `AchievementToastComponent`

**Build new:**

| component | responsibility |
|---|---|
| `AbilityBarComponent` | **shared with `shooter`** — slots, hotkeys, radial cooldowns, charges |
| `OrbGaugeComponent` | circular liquid-fill gauge for health/mana |
| `QuestTrackerComponent` | collapsible objective list with progress + distance |
| `MeterBarComponent` | **shared** — bar fallback where orbs are not wanted |

---

## 8. Pitfalls

- The hotbar takes **mouse input** (clickable abilities); the rest of the HUD must be `mouse_filter = Ignore`.
- An orb is a shader/`_Draw` job, not a `ProgressBar` — a rectangular bar rotated or masked will not read as liquid.
- The target frame must **hide entirely** when nothing is targeted; an empty frame is worse than no frame.
- Buff durations tick every frame — update the label on a timer (4–10 Hz), not in `_Process`, or the text re-layout costs more than the combat.
- `DamageNumbersEnabled` and `ScreenShakeEnabled` already exist in `SettingsComponent`; wire to them rather than adding new toggles.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `QuestTracker` | Quests | expanded | the classic clutter offender |
| `Minimap` | Map | expanded |  |
| `PartyFrames` | Party | expanded | fold in solo play |
| `Hotbar` | Abilities | expanded | never fold by default — it is the combat loop |

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
