# Phase 7 — Item dependencies & the time model

**Goal:** an item can require other items, and anything with a duration works in a **turn-based**
genre as well as a real-time one.

**Depends on:** Phase 1 (`GameItem`). **Blocks Phase 2** — `StatModifier.Duration` cannot be
specified until the time model is decided.

> **This phase exists because the plan missed both.** Phases 1–6 model what an item *is* and what
> it *does*, and never asked what it **needs** or **how long anything takes**. Two of the ten
> genres are turn-based. The `Duration : float` in Phase 2 quietly assumed seconds.

---

## Part A — Dependencies

### What already exists, and is right

**`CraftingRecipe` (`ecs/CraftingComponent.cs:53-60`) already models item-depends-on-items:**

```csharp
[Export] public CraftingIngredient[] InputItems  // { ItemId, Count }
[Export] public string OutputItem;  [Export] public int OutputCount;
[Export] public float CraftTime = 0f;            // ← never read (Part B)
```

**And it is recipe-owned, not item-owned — which is the correct call.** The same sword may be
craftable three ways (forge, salvage, quest reward). `RequiredItems[]` on `GameItem` would hardcode
one path and make the item know about its own production. **Adopt this; do not reinvent it.**

**Phase 1 change:** `CraftingIngredient.ItemId` (string) → `GameItem`; `OutputItem` → `GameItem`.
Same retirement as `PickupComponent.ItemId`.

### The three kinds it does NOT cover

`CraftingRecipe` is a **construction** dependency. Three others exist, and each is a different
shape — they are not one feature:

| Kind | Example | Owner | Lifetime |
|---|---|---|---|
| **Construction** | sword ← 2 iron + 1 leather | the **recipe** ✅ exists | one-shot, at craft |
| **Consumption** | gun ← ammo; lamp ← oil; wand ← charges | the **item** ❌ missing | recurring, at use |
| **Composition** | sword ← 3 gems socketed | the **instance** ❌ missing | persistent, per-instance |
| **Unlock** | barracks ← "Bronze Working" | a **tech node** ❌ missing | one-shot, permanent |

**Consumption is intrinsic to the item and must live on it** — a gun *is* a thing that eats ammo,
regardless of how it was made:

```
GameWeapon : GameEquipment
    AmmoItem     : GameItem?     — null = no ammo needed (a sword)
    AmmoPerUse   : int = 1
```

The consumer is the entity's `InventoryComponent`: on attack, `RemoveItem(AmmoItem, AmmoPerUse)` or
refuse. **Warn** when `AmmoItem` is set and the wielder has no inventory — otherwise a gun that
silently never fires (`CLAUDE.md` § *Never fail silently*).

**Composition collides with the resource-sharing trap, and this is the crux.** Sockets are
**per-instance**: two swords pointing at `sword_iron.tres` must not share their gems. Phase 1
already establishes that per-instance state lives on the instance or the slot, **never on the
definition**, and that `resource_local_to_scene` is not a way out
([godot#45350](https://github.com/godotengine/godot/issues/45350)). So:

```
GameEquipment.SocketCount : int         — the DEFINITION says how many holes
InventorySlot.Socketed    : GameItem[]  — the INSTANCE says what is in them
```

**Never `GameEquipment.Socketed : GameItem[]`.** That is the same defect as writing durability onto
the `.tres` — every iron sword in the world would share one set of gems.

**Unlock is a tech node, not an item.** Strategy's research tree is `ResearchNode : Resource` with
`Prerequisites : ResearchNode[]` — a graph over *nodes*, not over `GameItem`s. Do not overload
`GameItem` with it; a technology is not a thing you can hold. `BuildingSpec.RequiredResearch`
points at one.

### Cycles are authorable today and nothing catches them

`InputItems` is `[Export]`, so a developer can author A → B → A **in the inspector**, and
`CraftingComponent` would recurse or deadlock with no diagnostic. **The same is true of sockets**
(a gem whose recipe requires the sword it sits in) and research prerequisites.

**Fix:** a cycle check. Cheap and it belongs in the editor, not at runtime — extend
`validate_scenes.sh`, or a `[Tool]`-time check in `CraftingRecipe._ValidateProperty`. **Prove it
fails before trusting it** — `validate_scenes.sh`'s own header is the rule: *every check exists
because it caught a real bug*, and a check you've only seen pass is not evidence.

---

## Part B — Time, and why `Duration : float` is wrong

### The framework has three clocks and no abstraction

| Clock | Where | Unit | State |
|---|---|---|---|
| Real seconds | `StatusEffectComponent.cs:182` — `e.Duration -= (float)delta` | seconds | live |
| In-game hours | `DayNightCycleComponent.cs:72` — `TimeOfDay + delta * (24f / DayLengthSeconds)` | hours | **live and correct** |
| Turns | `CardBattle.cs:10` — `TurnChanged(bool playerTurn, int turnNumber)` | turns | **a local signal on a screen script** |

**`DayNightCycleComponent` is already a working clock** — `:72` converts real delta into in-game
hours with an `[Export] DayLengthSeconds` (`:31`). That is exactly the right shape. **Nothing else
reads it.**

### The bug that proves the gap is already shipped

**`SeasonalComponent.cs:74-75`:**

```csharp
[Export] public double DaysPerSeason { get; set; } = 7.0;  // in-game days   ← :27
...
_seasonTimer += delta;                    // ← real seconds
if (_seasonTimer >= DaysPerSeason)        // ← compared against "days"
```

**The comment says days. The code counts real seconds. Seasons rotate every 7 seconds.** A working
in-game-day clock sits in the same folder, in the same scene, and this component doesn't use it.
**This is the time-unit confusion, already costing a shipped feature.**

### Why this blocks Phase 2

Phase 2 specifies `StatModifier.Duration : float`, `duration 0` = permanent, decremented per frame.
**That is a real-time-only design.** In cardgame and strategy — **2 of 10 genres** — a buff lasts
*3 turns*, and there is no frame count that means "3 turns". It cannot be expressed.

I designed a modifier system for real-time games and called it generic. `StatusEffectComponent`
has the same limit today; it is simply never noticed because nothing turn-based uses it.

### `WorkComponent.Tick(double delta)` is the evidence for the fix

`components/items.md` flags it as odd: a component with **no `_Process`** whose `Tick` takes an
external `delta`. **It is not odd — it is the only component in the addon built for an injectable
clock.** A turn-based factory ticks once per turn; a real-time one ticks per frame; `Tick(delta)`
serves both. The original author had this insight and nothing else adopted it.

**Generalize it rather than "fixing" it into a `_Process`.**

### The design: the **genre** picks the axis. There is no `GameClock`.

> **An earlier draft of this phase specified `GameClock { Mode, Ticked(delta), AdvanceTurn(), Now }`
> — a single injectable clock with a mode switch. It was researched against practice and it is the
> wrong abstraction. Three reasons, any one of which is disqualifying.**
>
> **1. It is a union interface.** `Delta`/`Scale`/`Paused` are meaningless for turns;
> `AdvanceTurn`/`CurrentTurn` are meaningless for real time. **Every implementer would no-op on
> half its interface** — the definitive symptom of a wrong abstraction.
>
> **2. It silently desyncs from Godot itself.** `Timer`, `SceneTreeTimer`, `AnimationPlayer` and
> tweens are **all hardwired to `Engine.time_scale`**
> ([Engine class ref](https://docs.godotengine.org/en/stable/classes/class_engine.html)). Any
> component we put on a custom clock diverges from every built-in node a developer drops beside
> it — **compiles clean, passes `validate_scenes.sh`, and surfaces months later as "the animation
> and the cooldown disagree."** That is precisely the shape of the 67 dead `[Export]` assignments.
> Godot also **rejected** per-node `time_scale`
> ([proposal #2507](https://github.com/godotengine/godot-proposals/issues/2507), closed *not
> planned*), and its documented workaround is *"provide a global value for your scale, and remember
> to multiply delta in relevant nodes."*
>
> **3. Two cases is where you do NOT abstract.** [Rule of
> Three](https://en.wikipedia.org/wiki/Rule_of_three_(computer_programming)) — a generic system
> built to unify two known cases is the textbook wrong abstraction. Nystrom, *[Game Programming
> Patterns](https://gameprogrammingpatterns.com/architecture-performance-and-games.html)*:
> *"whenever you add a layer of abstraction … you're speculating that you will need that
> flexibility later … when that modularity doesn't end up being helpful, it quickly becomes
> actively harmful."*

**The distinction is real — and it is physical vs. logical clocks, not two clocks.**

| | **Derived** (day/night, seasons) | **Independent** (turns) |
|---|---|---|
| Advances by | `delta * scale` | an explicit event |
| Source of truth | the one real clock | its own counter |
| Recomputable from root? | **yes** — a pure function of elapsed | **no** |
| Pausing the game | free, automatic | meaningless |
| Analogue | a **physical** clock | a **Lamport logical** clock |

A turn counter *is* a [Lamport clock](https://mwhittaker.github.io/blog/lamports_logical_clocks/):
no wall time, only causal ordering. A day/night cycle *is* a physical clock plus a unit conversion.
Once separated, the case for a clock abstraction collapses — **every derived clock is a multiply,
not a clock**, and the one genuinely independent clock needs *none* of the machinery a clock type
provides. It is an `int` and a signal.

### So: three things, and the genre selects between two of them

**1. Real-time genres use `delta`. No wrapper.** Godot's `Engine.time_scale` already gives pause
and slow-motion for free, **and keeps us in sync with `Timer`/tweens/`AnimationPlayer`.**
`Engine.time_scale` is the one clock. (Trap to document: it does **not** auto-adjust
`physics_ticks_per_second`.)

**2. Derived views are a multiply, not a type.** `DayNightCycleComponent.cs:72` **already does this
correctly** — `TimeOfDay + delta * (24f / DayLengthSeconds)`. It is the model. `SeasonalComponent`
should read it and does not. Unreal's entire per-object time story is one multiplication
(`DeltaTime × GlobalTimeDilation × CustomTimeDilation`); even Bevy's `Time<Virtual>` is **derived
from `Time<Real>`** — a hierarchy over one root, not peers.

**3. Turn-based genres get a `TurnManager`, and it is deliberately tiny:**

```
TurnManager : Node                (autoload — turn-based genres only)
    int CurrentTurn { get; }
    [Signal] TurnEnded(int turn)
    void EndTurn()                — increments, emits. That is the whole type.
```

No delta, no scale, no pause, no `Now`. It is a Lamport clock and it looks like one.
`CardBattle.OnEndTurn` (`:32`) calls it; `CardBattle.TurnChanged` (`:10`) stays as the UI signal.

### The genre picks the axis — `genre.json`, not code

**This is the part you called, and it is the load-bearing decision.** `GenreDef` already carries a
`tuning{}` block applied at runtime by `BeepGenreScene`:

```json
"tuning": { "time_axis": "turns" }     // cardgame, strategy.  Default: "realtime"
```

**One axis per genre, and it holds** — check it against every duration this framework needs:

| | real-time genre | turn-based genre |
|---|---|---|
| status buff | seconds | turns |
| cooldown | seconds | turns |
| craft / work | seconds | turns |
| crop growth | in-game days (**derived**) | turns |
| day/night, seasons | **derived** | turns, or absent |

**Nothing mixes.** There is no scenario in a platformer where crop growth continues while cooldowns
freeze, and none in a card game where a buff counts seconds. So **`StatModifier.Duration` needs no
unit tag** — my earlier objection to `{Amount, Unit}` was right, and *this* is why: the genre
answers it once, so no consumer ever branches and no stat list can hold mixed units.

**Duration semantics, unchanged:** `Duration` is in the genre's axis units. **`< 0` = permanent** —
unit-independent, and the 2-line `StatusEffectComponent` guard in `combat.md` is untouched by any
of this.

### Turn-based durations: pick an edge, or ship D&D's bug

**The off-by-one here is not hypothetical — it has shipped in three well-known products:**
- **D&D 5e `True Strike`** — 1-round duration, but the spell "ends at the beginning of the next
  turn" while its text says the *next turn's* attack gets advantage, so it **can never be used**.
  The rules never state whether durations expire at the start or end of a turn.
  **50 years of editing did not resolve this.**
  ([D&D Beyond](https://www.dndbeyond.com/forums/dungeons-dragons-discussion/rules-game-mechanics/58826-when-does-a-spells-duration-run-out))
- **Stardew Valley** — "grow times exclude the day the seeds were planted": a 5-day crop planted on
  day 1 is ready on day **6**. A shipped, documented, permanent off-by-one.
  ([Stardew Wiki](https://stardewvalleywiki.com/Crops)) — and crop growth is on our list.
- **Slay the Spire** — the convention to copy: stack count *is* duration; **decrement at end of
  turn**; expire at 0. ([StS wiki](https://slaythespire.wiki.gg/wiki/Vulnerable))

**Decisions, to be stated in the component doc comment — not inferred:**
- **Decrement at end of turn, expire at 0** (Slay the Spire). `Duration = 3` then means "acts on 3
  turns", which is what an author means when they type 3.
- **Fire the decrement from exactly one place** — `TurnManager.TurnEnded`. Per RogueBasin's
  *"the tick is the global heartbeat, where you execute all durational effects"*: if each component
  decides for itself when a turn ended, **you get N different off-by-ones**.
- **A turns-axis genre with no `TurnManager` in the tree must `PushWarning`.** Otherwise a buff set
  to 3 turns lasts **forever, silently** — this repo's dominant defect class, exactly.

### Why not a named-clock registry (`GetClock("combat")`)

It was considered and rejected. Beyond Rule of Three:

- **It is a silent-failure generator by construction.** `GetClock("combat")` on a genre with no
  combat clock returns null, the component early-returns, the buff never expires, **nothing is
  logged.** A new lookup seam of exactly the kind that hid unthemed pause/settings/game-over in all
  10 genres.
- **Clock names are game concepts, not framework concepts.** `"combat"`, `"crafting"`, `"weather"`
  are the developer's vocabulary. `CLAUDE.md` § *Scope*: we ship the axis and the turn signal and
  get out of the way. A registry of genre-specific clock names is the framework reaching across the
  line into the developer's canvas.
- **No engine ships one.** Unity (`Time.timeScale`) and Godot (`Engine.time_scale`) are
  single-global-scale; Unreal's per-actor axis is a *multiplier*. Bevy is the closest to named
  clocks — `Time<Real>`/`Time<Virtual>`/`Time<Fixed>` — but those are **engine plumbing**
  (wall vs. pausable vs. fixed-timestep), derived from one root, not gameplay timelines. **No GDC
  talk or architecture text names a multi-clock gameplay pattern.** The absence is evidence.
- **RimWorld is the counterexample that settles it** — day/night, seasons, crop growth, work,
  production, variable speed, *all of our requirements at once*, on **one tick counter**.

### What this fixes beyond items

- **`SeasonalComponent`** reads in-game days from the clock; the 7-second seasons bug dies at the
  root rather than being patched with a magic multiplier.
- **`CraftTime`** (`CraftingRecipe.cs:59`) finally has a meaning — and it is **the same field** in
  both a real-time survival game and a turn-based strategy one.
- **`WorkComponent`** gets its driver, in the shape it was designed for.
- **`CooldownComponent`** works in a turn-based game — "this ability recharges in 2 turns".
- **`CardBattle`'s turn counter** stops being a private field on a screen script and becomes the
  genre's clock.

---

## Work

**Time — note how little there is. That is the point.**

1. **`ecs/TurnManager.cs`** — autoload for turn-based genres only. `CurrentTurn`, `TurnEnded`,
   `EndTurn()`. **No delta, no scale, no `Now`.** Register in `BeepGenreGenerator` beside
   `GameApp`/`Settings`/`Locale`, gated on the genre's axis.
2. **`genre.json` `tuning.time_axis`** — `"turns"` in `cardgame` and `strategy`; default
   `"realtime"`. **Zero C# per genre** — the file-based principle the addon already runs on.
3. **`StatusEffectComponent`** — real-time: keep `_Process` (`:176-182`) and `delta`, so it stays in
   sync with `Timer`/tweens. Turn-based: subscribe to `TurnManager.TurnEnded`, decrement by 1.
   **Land with the Phase 2 `Stat` refactor — not twice** (both touch the only two live effects).
4. **`SeasonalComponent`** — read in-game days from `DayNightCycleComponent`; **delete `_seasonTimer`
   (`:74`)**. Do not patch a multiplier onto it — the correct clock is already in the same scene.
5. **`WorkComponent`** — **keep `Tick(double delta)` exactly.** Real-time: drive from `_Process`.
   Turn-based: drive from `TurnEnded` with `1`. **The signature was always right.**
6. **`CardBattle`** — `OnEndTurn` (`:32`) → `TurnManager.EndTurn()`; keep `TurnChanged` (`:10`) as
   the UI signal.
7. **`PushWarning`** when the genre's axis is `turns` and no `TurnManager` is in the tree — else a
   3-turn buff lasts forever, silently.
8. **Document the edge** in the doc comment: decrement at **end of turn**, expire at **0**.

**Dependencies:**

9. **`GameWeapon.AmmoItem` / `AmmoPerUse`** — consumption; consumed via the wielder's
   `InventoryComponent`; **warn** when set with no inventory.
10. **`GameEquipment.SocketCount`** + **`InventorySlot.Socketed`** — composition, per-instance.
11. **`CraftingIngredient.ItemId` / `CraftingRecipe.OutputItem`** → `GameItem` (with Phase 1).
12. **Cycle check** — recipes, sockets, research. **Make it fail before trusting it.**
13. **`ResearchNode : Resource`** with `Prerequisites` — strategy only; **not** a `GameItem`.

## Decisions

- **Construction deps stay on the recipe; consumption deps go on the item.** The item knows what it
  eats; it does not know how it was made.
- **Composition state is per-instance, never on the `.tres`.** Same rule as durability and
  quantity, same reason, and `resource_local_to_scene` is still not a way out (godot#45350).
- **The genre picks the time axis — one axis per game, from `genre.json`.** Not a `GameClock` type
  (a union interface that desyncs from `Timer`/tweens), not a named registry (a silent-failure
  generator, and clock names are *game* vocabulary), and **not a `{Amount, Unit}` tag per
  modifier** — that pushes the branch into every consumer and invites "3 turns" and "5 seconds" in
  one stat's list. The genre answers it **once**, so no consumer branches. Same argument that killed
  the three-accessor design in Phase 2: **put the decision in one place or every consumer grows a
  copy of it.**
- **Derived clocks are a multiply; only turns are a real clock.** Day/night and seasons are
  `delta * scale` over the one clock — a *physical* clock with a unit conversion, which
  `DayNightCycleComponent:72` already implements correctly. A turn counter is a **Lamport logical
  clock**: no wall time, only causal order. They share no machinery, which is exactly why one
  interface could not hold both.
- **Don't fight `Engine.time_scale`.** `Timer`, `SceneTreeTimer`, `AnimationPlayer` and tweens are
  hardwired to it. A component on a custom clock silently diverges from every built-in node beside
  it — clean build, green validator, "the animation and the cooldown disagree" months later. Godot
  **rejected** per-node `time_scale` ([#2507](https://github.com/godotengine/godot-proposals/issues/2507))
  and its documented workaround is one global scale + multiply delta. **Use the engine's clock.**
- **A technology is not an item.** Do not overload `GameItem` with unlock graphs.
- **Turn-based is not an afterthought.** 2 of 10 genres ship it, and `CardBattle` already has a
  turn counter. A "generic" duration that only works in 8 genres is the same defect as a
  `[GlobalClass]` that only works on one parent type.

## Verification

`dotnet build` → 0 errors; `validate_scenes.sh` → PASS. Neither runs the game.

**Each of these is a thing the framework cannot do today. If one still can't be shown, it didn't
work:**

- a **card buff lasting 3 turns** expires at the **end of the 3rd** `EndTurn` — not the 2nd, not the
  4th. **Assert the edge**: this is the D&D `True Strike` bug, and a 50-year-old ruleset still has
  it. A test that only checks "it expires eventually" would pass on the off-by-one.
- the **same** `StatModifier(Duration = 3)` resource in a real-time genre expires after 3 seconds —
  one field, two axes, **no branch in the consumer**;
- a **turns-axis genre with no `TurnManager`** warns loudly instead of buffing forever;
- **a `Timer` and a cooldown authored to the same value still agree** after `Engine.time_scale`
  changes — the interop check. If we ever put durations on a custom clock, this is the test that
  catches it.
- **seasons take `DaysPerSeason` in-game days**, not 7 real seconds — the shipped bug, gone at the
  root;
- a **gun with no ammo does not fire, and says so** (a `PushWarning`, not silence);
- two swords from one `sword_iron.tres` **socket different gems** — the resource-sharing trap, held;
- an **A → B → A recipe is rejected at author time**, and the check **fails** before you trust it.
