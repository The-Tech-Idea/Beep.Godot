# HUD Design — Index

Design reference for the genre HUD rebuild (**Stage 30**). One detailed document per genre in
[`docs/hud/`](hud/), each covering: reference games, canonical layout wireframe, a numbered
element specification with priorities, genre best practices, audited current-vs-target,
the `SetStat` data contract, component reuse/build lists, and implementation pitfalls.

| genre | document | biggest gap |
|---|---|---|
| City Builder | [`hud/citybuilder.md`](hud/citybuilder.md) | **build toolbar + RCI demand** — the two pieces that define the genre |
| Strategy (RTS/4X) | [`hud/strategy.md`](hud/strategy.md) | command card + selection panel |
| Shooter | [`hud/shooter.md`](hud/shooter.md) | health/ammo clusters, damage direction, reload ring |
| RPG | [`hud/rpg.md`](hud/rpg.md) | orbs, ability hotbar with cooldowns, target frame |
| Survival | [`hud/survival.md`](hud/survival.md) | four meters as bars + hotbar + threshold warnings |
| Card Game | [`hud/cardgame.md`](hud/cardgame.md) | **there is no hand** — only a label counting cards |
| Racing | [`hud/racing.md`](hud/racing.md) | tachometer, gear, lap delta |
| Puzzle | [`hud/puzzle.md`](hud/puzzle.md) | objective panel + star progress |
| Top-Down | [`hud/topdown.md`](hud/topdown.md) | hotbar + clock; and the stat set is wrong for the genre |
| Platformer | [`hud/platformer.md`](hud/platformer.md) | pip health + damage feedback (smallest gap) |

---

## The problem, in one table

Audited 2026-07-26. Every shipped HUD is a **stack of text Labels in one corner**:

| genre | readouts | widgets beyond Label |
|---|---|---|
| platformer | Score, Level, Lives, Health | — |
| topdown | Score, Level, Lives, Health | Minimap |
| shooter | Score, Level, Lives, Health, Ammo, Wave | Crosshair |
| puzzle | Score, Target, Moves | — |
| racing | Lap, Position, LapTime, Speed | — |
| rpg | Level, Health, Mana, Quest | Minimap |
| cardgame | Health, Gold, Energy, Hand, Deck, Discard | — |
| citybuilder | Population, Budget, Power, Happiness, Date | Minimap |
| strategy | Gold, Food, Wood, Units, Turn | Minimap |
| survival | Health, Hunger, Thirst, Stamina | — |

No bars, no meters, no hotbar, no build toolbar, no alerts, no cooldowns, no timers.
A player cannot read health at a glance from `"Health: 72"`.

---

## Key finding: this is mostly a WIRING gap

**45 of the addon's 70 UI components have never been placed in any scene.** 21 are directly
reusable for HUD work with no new code:

`SafeArea` · `InteractionPrompt` · `BuffBar` · `BossHealthBar` · `ComboCounter` ·
`MatchTimer` · `ToastNotification` · `AchievementToast` · `ProgressRing` · `Counter` ·
`WeatherHUD` · `Vignette` · `ScreenFlash` · `Tooltip` · `Table` · `TabGroup` ·
`ContextMenu` · `Badge` · `FlipCard` · `Carousel` · `Marquee`

---

## Second audit finding: the data is fake too

Re-audited the genre HUD components themselves, not just the scenes. `GenreHudComponent`
offers real binders (`BindScore`, `BindLives`, `BindLevel`, `BindHealth`) and a
`Placeholder(...)` fallback that only warns and shows scene text.

**31 of 44 stat bindings across the ten genres are `Placeholder`.** Five genres have
**zero** real data sources:

| genre | real bindings | placeholders |
|---|---|---|
| platformer / topdown | 4 | 0 |
| shooter | 4 | 2 |
| rpg | 1 (`level`) | 3 |
| puzzle | 1 (`score`) | 2 |
| **cardgame** | **0** | 5 |
| **citybuilder** | **0** | 5 |
| **racing** | **0** | 4 |
| **strategy** | **0** | 5 |
| **survival** | **0** | 4 |

And **6 of 29 genre screens are Close-only mockups** whose every figure is a scene literal:
`citybuilder/districts`, `citybuilder/economy`, `rpg/character`, `rpg/inventory`,
`survival/backpack`, `survival/world_map` — each a 17-line script that wires one button.

> An earlier draft of these docs claimed rpg/survival/topdown "already have correct modal
> screens". That was wrong — it was based on the files existing, not on their contents.
> Corrected in each genre doc.

---

## Research-backed rules (external sources)

Findings from current game-UI literature, applied on top of the genre specifics:

1. **The 80/20 attention split.** Players spend ~80% of visual attention on the gameplay
   area and only ~20% on the HUD. This is the argument for **progressive disclosure**: show
   only what is needed now, hide secondary information until requested, and surface
   contextual prompts only when the action is available.
2. **Hierarchy by stability, not just size.** Health and immediate threats get the strongest
   contrast *and the most stable screen position*; cosmetic or low-frequency information
   (currency, season XP) gets quieter treatment. A readout that moves is a readout that gets
   missed.
3. **Hybrid diegetic/non-diegetic is the norm**, not full diegesis. God of War pairs minimal
   non-diegetic meters with diegetic world-telling; Dead Space puts health on the suit;
   Destiny 2 switches to diegetic holograms only outside combat. Treat full diegesis as a
   per-project option, never the framework default.
4. **RTS: do not spread information across many zones.** Splitting data across multiple
   areas splits attention — the golden-age layouts each committed to one or two dense
   regions (Blizzard's left bar, AoE's top+bottom, StarCraft's four-piece bar). Age of
   Empires III additionally uses *degrees of information reveal* to avoid overload.
   Reinforces the "command card + selection panel as one bottom block" plan.
5. **RTS: hotkeys for every command**, shown on the button. Competitive play is keyboard-first.
6. **Survival: specify vitals first.** Health / stamina / hunger / thirst are designed before
   anything else; vitals belong bottom-left or top-left; colour convention matters (health
   red or green, stamina green, mana blue).
7. **Survival: theme the meter, don't ship a rectangle.** A stamina bubble that flickers as
   fatigue sets in reads better than a progress bar. `MeterBarComponent` should therefore
   support a themed fill mode, not only a linear bar.
8. **Valheim's food slots** (three slots, each with a countdown) are a better pattern for
   "well-fed" than a generic buff row.

**Reference libraries worth using during implementation** — both index real screenshots of
shipped game HUDs and are the fastest way to check a layout against the genre:
[Game UI Database](https://www.gameuidatabase.com/) · [Interface In Game](https://interfaceingame.com/)

---

## Shared rules (apply to all ten before genre work starts)

1. **Anchor by screen region, not one corner.** A HUD is a frame: top-left / top-centre /
   top-right / bottom-left / bottom-centre / bottom-right / centre. Today everything is
   stacked in a single `VBoxContainer`.
2. **`SafeAreaComponent` wraps every HUD root.** Nothing within ~4% of the screen edge —
   TV overscan and phone notches. Built, never used.
3. **A number read under pressure is a BAR, not text.** Health, stamina, mana, hunger,
   thirst, fuel, XP. Keep the number as a small overlay on the bar.
4. **State changes must announce themselves** — `ToastNotification` for events,
   `ScreenFlash`/`Vignette` for damage. Silent state change is the most common complaint
   across all ten.
5. **`mouse_filter = Ignore` on every non-interactive HUD node**, or the HUD eats gameplay
   clicks. Only toolbars, command cards, hotbars and hands take input.
6. **Every readout binds through the genre's `*HudComponent`** — no scene literals. The
   existing `SetStat(key, value)` contract already covers this.

---

## Component build plan

**Build the 6 SHARED components first** — they cover four genres before any genre-specific work:

| component | genres |
|---|---|
| `MeterBarComponent` | survival, rpg, shooter, platformer |
| `HotbarComponent` | topdown, survival |
| `AbilityBarComponent` | rpg, shooter |
| `SelectionPanelComponent` | strategy, citybuilder |
| `DayNightClockComponent` | survival, topdown |
| `PipHealthComponent` | platformer, topdown |

**Then genre-specific (14):**

| genre | components |
|---|---|
| citybuilder | `BuildToolbar`, `DemandMeter`, `GameSpeed`, `InfoView` |
| strategy | `CommandCard`, `ProductionQueue` |
| racing | `Speedometer`, `LapDelta` |
| puzzle | `ObjectivePanel`, `BoosterTray`, `PiecePreview` |
| cardgame | `HandLayout`, `PileCounter`, `EndTurnButton`, `IntentDisplay` |
| shooter | `AmmoCounter`, `DamageDirection` |
| rpg | `QuestTracker`, `OrbGauge` |

---

## Non-negotiable: production-ready only

**No placeholders. No mockups. No legacy fallbacks.** Standing project rule.

That settles what was previously written here as an open question ("real signals or demo
values?"). The answer is **real signals**. A HUD element is not delivered until the component
that owns and emits its data is delivered with it.

Concretely, for this rebuild:

1. **`Placeholder(...)` is banned as an end state.** Every stat a genre HUD shows must have a
   real source. Five genres — cardgame, citybuilder, racing, strategy, survival — currently
   have zero, so each needs a state component built as part of its stage.
2. **No hardcoded figures in any scene.** `1,250 g`, `24.8 / 40 kg`, `Strength 15`,
   `Happiness 78%` are all scene literals today. Scene text may only be a static label
   ("Health", "Moves"); every value comes from a binding.
3. **No Close-only screens.** A screen ships bound or it does not ship.
4. **No parallel legacy path.** When a readout moves from a Label to a widget, the Label goes.

**Scope consequence, stated plainly:** this makes Stage 30 larger than a UI pass. It adds a
per-genre state layer (Stage 30.0 below). That is the cost of the rule and is accepted — the
alternative is ten screens that look finished and show invented numbers, which is what the
audit found.

---

## Sources

- [Game UI Design: Complete Interface Guide 2026 — Generalist Programmer](https://generalistprogrammer.com/tutorials/game-ui-design-complete-interface-guide-2025)
- [Game HUD Essentials: Designs for 2024 — Page Flows](https://pageflows.com/resources/game-hud/)
- [Game UI/UX Design Principles: HUD, Menus, and Feedback Systems That Work — StraySpark](https://www.strayspark.studio/blog/game-ui-ux-design-principles)
- [UI Strategy Game Design Dos and Don'ts — Game Developer](https://www.gamedeveloper.com/design/ui-strategy-game-design-dos-and-don-ts)
- [A look at the HUD & UI Changes of Age of Empires III: Definitive Edition — World's Edge](https://www.ageofempires.com/news/aoe3de-hud-and-ui/)
- [Strategy Game Battle UI — treeform, Medium](https://medium.com/@treeform/strategy-game-battle-ui-3b313ffd3769)
- [Survival Game Design (Principles, Examples, Template) — Game Design Skills](https://gamedesignskills.com/game-design/survival/)
- [HUD — Valheim Wiki](https://valheim.fandom.com/wiki/HUD)
- [Cities: Skylines — Interface In Game](https://interfaceingame.com/games/cities-skylines/)
- [Game UI Database — Cities: Skylines](https://www.gameuidatabase.com/gameData.php?id=526)
- [Game UI Database — Age of Empires II: Definitive Edition](https://www.gameuidatabase.com/gameData.php?id=722)
- [Game UI Database — Don't Starve](https://www.gameuidatabase.com/gameData.php?id=197)

---

## Cross-genre rule: every HUD panel is collapsible

Added 2026-07-26 at the user's direction, and specified per genre in section 9 of each
per-genre document.

A HUD competes with the game for the same pixels. Every panel is useful *some* of the time, and
occupying the screen for the rest is how a HUD becomes clutter. `CollapsiblePanelComponent`
(`ecs/ui/CollapsiblePanelComponent.cs`) attaches as a child of a panel that already exists,
inserts a header bar above it, and folds the panel away leaving only that header.

Non-negotiables, each of which is a bug if broken:

1. **The header survives the fold** — inserted as a *sibling above* the panel, never a child. A
   child is folded away with everything else and the panel can never be reopened.
2. **Collapsed means zero space** — `custom_minimum_size.y = 0` and `visible = false`, so the
   container reflows and neighbours take the room. A transparent-but-present panel still eats
   clicks.
3. **State persists** — `ISaveable`, joins the `saveables` group, keyed `hud.collapsed.<panel>`.
   A HUD that reopens every panel on load is the annoyance this exists to remove.
4. **The genre's core loop never defaults to collapsed** — a survival vitals panel, a shooter
   radar, a card game's hand. Each per-genre table marks these *never fold by default*.

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the toolbar drops out
of the layout with the minimap and RCI meter reflowing into the freed space, the header stays
clickable while collapsed, and the component is in the `saveables` group alongside
`CityEconomyComponent`.

### Host constraint (learned the hard way)

`CollapsiblePanelComponent` adds its header as a **sibling above the panel**, which only lands
correctly when the host is a `VBoxContainer`. In an `HBoxContainer` the header appears *beside*
the panel; under a bare `Control` it goes wherever the panel's own anchors put it.

A runtime fix — wrap the panel in a `VBoxContainer` and reparent it — was implemented and then
**reverted**: reparenting changes the panel's node path, which silently broke
`CityBuilderHudComponent.DemandMeterPath` and produced *"no DemandMeterComponent at
'DemandMeter'"*. **A HUD component must never rearrange a scene tree that other components hold
NodePaths into.**

So: to give a panel a clickable header, put it in a `VBoxContainer` **in the scene**. Collapsing
itself (`ToggleAction`, `SetCollapsed()`) works from any host; only the header needs the VBox.
Where the host is unsuitable the component warns and adds no header rather than adding a
misplaced one.

---

## Resolution model — the design canvas is fixed

Corrected 2026-07-26. `BeepProjectDefaults` used to write the developer's chosen resolution into
`display/window/size/viewport_width|height`. That is the **design canvas**, not the window: it
redefined the coordinate space every HUD pixel is authored in, so choosing 1920×1080 rendered a
UI laid out for 1280×720 at two-thirds the intended proportion — panels and fonts came out too
small at exactly the resolutions meant to look better.

The model now matches Godot's own guidance (*Multiple resolutions*, which calls the base size
"the design size, i.e. the size of the area that you work with in the editor"):

| Setting | Value | Meaning |
|---|---|---|
| `viewport_width` / `viewport_height` | **1280 × 720, fixed** | the design canvas everything is authored in |
| `window_width_override` / `window_height_override` | the developer's choice | the actual window |
| `stretch/mode` | `canvas_items` | scale the canvas to the window |
| `stretch/aspect` | **`expand`** (was `keep`) | grow the viewport on the wider axis |

`aspect = keep` letterboxes anything that is not the design aspect, so on a 21:9 monitor a HUD
anchored to the screen edge anchors to the edge of a *black bar*. `expand` puts Control anchors
on the real screen corners at any aspect ratio — which is the point of anchoring the HUD inside
a CanvasLayer rather than positioning it.

**Consequence:** the ~58 hardcoded pixel sizes across the HUD components are correct as written.
They are design-space coordinates, and the canvas scales them. They only looked wrong because
the canvas was being redefined underneath them.

*Verified:* OS window 1600×900, design viewport 1280×720, `TopBar` reporting a 1280×56 rect —
window and design canvas genuinely decoupled. Varying the OS window further was not possible in
this environment (Godot clamped it to 1600×900 regardless of `--resolution`), so the decoupling
is confirmed structurally rather than by comparing two rendered resolutions.

## Collapse affordance — a floating toggle, not a header bar

The first implementation added a full-width header row above each panel. That reads as a desktop
accordion, not a game HUD, and it only lands correctly inside a `VBoxContainer`.

Replaced with a **22×22 floating chevron pinned to the panel's top-right corner**, positioned
per-frame from the panel's own rect and parented to the CanvasLayer's top-level Control:

- Works over **any** host — HBox, bare Control, VBox — because no container lays it out. This
  removed the VBox-host constraint entirely.
- Never disturbs the panel's node path (the reparenting attempt that broke `DemandMeterPath`).
- Survives the fold: while collapsed the panel may be hidden, so the toggle holds the last known
  panel rect rather than jumping to the origin and stripping the player of the way back.
- Needs its own compact styling: inheriting the HUD Button theme's content margins (14px sides,
  plus the sci-fi art's baked header offset) left no room inside a 22px square and the chevron
  rendered invisible while the button drew fine.
