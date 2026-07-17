# racing — no items; a `VehicleSpec`

> Read `README.md` first. This doc conforms to its rule and its template.

---

## 1. Does this genre need items at all?

**No.** Racing has nothing to pick up, nothing to stack, no inventory, no equip slot. It needs
exactly one thing the framework does not have: **a description of a car**.

The genre currently has neither. Verified absences:

- **No vehicle controller exists.** `grep -ril "throttle\|steering\|brake"` over `addons/`
  → **0 files**. The three shipped controllers are `ecs/PlatformerController.cs`,
  `ecs/ShooterController.cs`, `ecs/TopDownController.cs`. There is no fourth.
- **`racing_main.tscn` contains no car.** The template is a `LevelLoader`, an `Atmosphere`
  instance, a `GameFlow`, and two HUD labels whose text is baked:
  `templates/scenes/racing_main.tscn:44` `text = "Speed: 0 / 320 km/h"`,
  `:47` `text = "Lap: 1 / 3"`. Nothing writes either label. The genre ships a scoreboard for
  a race that cannot happen.

### `MovementComponent` is the wrong model, not merely redundant

This is the load-bearing point and it should not be softened. `MovementComponent`
(`ecs/MovementComponent.cs:25`) exposes `Speed`, `Acceleration`, `Friction`
(`:27-29`) and steers by an omnidirectional `Vector2 DesiredDirection` (`:45`), applied as
`Velocity.MoveToward(direction * Speed, …)` (`:102`, in `Move`), read from
`Input.GetVector("move_left", "move_right", "move_up", "move_down")` — an 8-way vector
(`:72`).

A car does not have a desired direction. It has a **heading**, and inputs that change the
heading (steer) and the speed along it (throttle/brake). The distinction is not a tuning
value:

| | `MovementComponent` | a car |
|---|---|---|
| direction | set instantly, any angle | changes at a bounded **turn rate**, only while moving |
| lateral motion | free — press right, go right | resisted by **grip**; exceeding it is a slide |
| reverse | identical to forward | separate, slower |
| braking | `Friction`, always on | an **input** |

Setting `DesiredDirection = Vector2.Right` on a car travelling north makes it *strafe north-east*.
A car that strafes sideways is not a car. No `[Export]` on `MovementComponent` can fix that —
there is no heading to bound and no lateral axis to resist. The genre needs a different
component, and that component needs data `MovementComponent` cannot hold.

### `CheckpointComponent` cannot count laps

`ecs/CheckpointComponent.cs` is a **respawn point**, and three separate facts stop it being a
lap counter:

1. **Permanent latch.** `if (!IsActive || _activated) return; _activated = true;` (`:36-37`).
   `_activated` is never reset. Each checkpoint fires **once per scene lifetime** — lap 2
   crosses the same line and nothing happens. `[Export] bool SingleUse` (`:19`) does **not**
   gate this; it only sets `_area.Monitoring = false` afterwards (`:54-55`). `SingleUse = false`
   changes nothing about the latch.
2. **It stores a level index, not an order.** `app.SetCheckpoint(app.CurrentLevel)` (`:43`) →
   `LastCheckpointLevel = level` (`ecs/GameApp.cs:320-324`, field at `:81`). There is no
   sequence number, so a lap counter could not verify the driver passed 1→2→3 rather than
   cutting the course.
3. **`HealOnActivate` defaults `true`** (`:16`) and heals the entering body to full (`:45-49`).
   The default behaviour of a racing checkpoint would be to heal the car.

It is a good respawn point. It is not a lap counter, and bending it into one would break
respawn for platformer and topdown.

### The selection is written and never read

`GameApp.SelectedVehicle` (`ecs/GameApp.cs:55`) has exactly three references addon-wide:
its declaration, its reset in `:244`, and one write —
`ecs/scenes/racing/VehicleSelect.cs:28` `app.SelectedVehicle = vehicle;` with the literals
`"Car1"`, `"Car2"`, `"Car3"` (`:19-21`). **Nothing reads it.** The player picks a car and the
choice reaches nothing, because there is nothing for it to reach: no car exists to configure.
(Its twin `GameApp.SelectedCharacter`, `:53`, has the identical defect — written by
`ecs/scenes/shooter/CharacterSelect.cs:24`, read by nothing. Same cause, out of scope here.)

`VehicleSpec` is what turns that string into a lookup, and gives `SelectedVehicle` its first
reader.

### The tuning block is decoration

`catalogs/skins/racing/genre.json:26-27` declares `speedometer_max: 320` and `lap_count: 3`.
Neither appears in `BeepGenreGenerator.KnownTuningKeys` (`core/BeepGenreGenerator.cs:221-231`),
so `WarnUnknownTuning` (`:235-243`) emits *"tuning.lap_count is not read by anything — it has
no effect."* on every generate. The generator's own comment names this case:
*"racing's lap_count…"* (`:236`). These two keys are the genre's whole configuration surface
and both are inert.

---

## 2. The tree

**There is none.** Racing draws **zero** branches from the `GameItem` spine.

### Is a vehicle a `GameItem`?

No — and the test is the spine's own fields, not intuition. `GameItem : Resource` carries
`Id, DisplayName, Description, Icon, Rarity, MaxStack, IsStatic, IsDestructible,
MaxDurability, WorldScene` (README § *The spine*). Against a car:

| spine field | on a car |
|---|---|
| `MaxStack` | meaningless — you do not carry 3 Ferraris |
| `IsStatic` | meaningless — the car is the *only* thing that moves |
| `IsDestructible` / `MaxDurability` | plausible (damage models) but not what makes a car a car |
| `Rarity` | a shop concept; a garage is not a loot table |
| `WorldScene` | **the one that fits** |

One field of ten. Meanwhile everything that *defines* a car — mass, top speed, grip, turn
rate — has no home on `GameItem` and no business there. Inheriting would buy one useful field
and eight dead ones, and would tell a reader that a car stacks, drops, and can be picked up.
The README's traits table only works because `IsStatic`/`IsDestructible` genuinely decide an
item's composition; on a car they decide nothing.

**A vehicle is a SIBLING of `GameItem`, not a subclass** — its own `Resource` root. It is the
same *idea* (data by inheritance, instanced through a `PackedScene`) one level over, which is
exactly what the README's closing paragraph describes.

### The `.tres` set a developer authors

```
res://data/vehicles/
    starter_hatch.tres      VehicleSpec
    muscle.tres             VehicleSpec
    formula.tres            VehicleSpec
```

`Id` values match the strings `VehicleSelect.cs:19-21` already writes (or those literals are
replaced by `[Export] VehicleSpec[]`). Three cars, one class — they differ in **numbers**, so
they are `.tres`. There is no `FerrariSpec`.

---

## 3. New framework classes this genre earns

### `VehicleSpec : Resource` — **earned**

`[Tool][GlobalClass]`, the pattern every other data class in the repo uses (`core/GameInfo.cs:15`,
`ecs/CraftingComponent.cs:53` `CraftingRecipe`, `:65` `CraftingIngredient`,
`ecs/QuestComponent.cs:71` `QuestObjective`).

It earns existence because no class in the repo can express a car. `MovementComponent` has
`Speed`/`Acceleration`/`Friction` and **cannot** hold `GripLateral`, `TurnRateDegPerSec`,
`Mass`, or `BrakeForce` — those are fields, not defaults, and its model has no axis for them
(§1).

```
VehicleSpec : Resource                  [Tool][GlobalClass]
    Id                : string          — matches GameApp.SelectedVehicle
    DisplayName       : string
    Icon              : Texture2D?      — the garage card
    Mass              : float
    TopSpeed          : float
    Acceleration      : float
    BrakeForce        : float
    TurnRateDegPerSec : float           — heading change per second
    GripLateral       : float           — resistance to sideways slide
    Scene             : PackedScene?    — the car in the world
```

`Scene` mirrors the spine's `WorldScene` and the repo's existing
`ProjectileScene`-carries-`ProjectileComponent` shape (MASTER_TODO § *The approach*). It is
the same idea, not an inherited field.

**Deliberately absent — `Handling`, `Rating`, `Tier`, `Colour`.** Aggregate stats for a garage
card are derived or presentational; adding a field nothing reads is, per MASTER_TODO §*What the
analysis changed*, "this repo's signature defect".

### `VehicleSpecTier` / `CarSpec` per car — **not earned**

`starter_hatch` and `formula` differ in numbers. `.tres`. README § *The one rule*, and its
corollary: *"`ferrari.tres` is a `.tres` of a vehicle spec, not a `FerrariSpec` class."*

### A `Bike` / `Hovercraft` subclass — **UNCERTAIN, and not now**

A bike that leans, or a hovercraft with no grip at all, might add a field. Nothing in the repo
implies either. Do not speculate a subclass into existence.

---

## 4. Components this implies

### Serves already

| Component | Role |
|---|---|
| `ecs/GameFlowComponent.cs` | already in `racing_main.tscn:50`; `Score`, `LevelComplete` — a race result is a score |
| `ecs/LevelLoaderComponent.cs` | `racing_main.tscn:16` — tracks are levels |
| `ecs/HealthComponent.cs` | *only if* the design has a damage model. Blind component; `Died` = wrecked |
| `ecs/CheckpointComponent.cs` | fine as a **respawn point** on a track. Not as a lap counter (§1) |

### Forced new — two, and the tree forces exactly one of them

**`VehicleController : ControllerComponent` — required.** The fourth sibling of
`PlatformerController` / `ShooterController` / `TopDownController` (`ecs/categories/ControllerComponent.cs`).
It holds a heading, reads a throttle/brake/steer input set, applies `GripLateral` to the
lateral velocity component, and drives `MoveAndSlide` itself — exactly as the existing
controllers do. `[Export] VehicleSpec? Spec` supplies its numbers.

It must **not** share a body with `MovementComponent`: `MovementComponent._Ready` already warns
about precisely this collision (`ecs/MovementComponent.cs:62-63`, *"they will fight"*), and the
class doc states the rule (`:19-21`). That warning becomes racing's archetype `MUST NOT HAVE`
(Phase 5), for free.

**`LapComponent`** — **UNCERTAIN whether it is earned.** A lap needs an ordered ring of
gates and a per-gate re-arm; `CheckpointComponent` has neither (§1) and cannot grow them
without breaking respawn. The honest read: a lap counter is a real gap, but it is a *flow*
concern (it feeds `GameFlowComponent`), not an item-model one, and this document should not
decide it. Flag it; do not design it here.

### Cites, does not re-derive

- `GameApp.SelectedVehicle` is write-only — §1, `GameApp.cs:55` vs `VehicleSelect.cs:28`.
- `AttackComponent.Range` is never read, `DamageTypeComponent` is dead, the item edges do not
  exist — README § *What is already known*. All irrelevant here: racing has no combat and no
  items. That is the point.

---

## 5. Content vs framework

**We ship (framework):**

- `VehicleSpec : Resource` — the class only. No `.tres` files, no cars.
- `VehicleController : ControllerComponent` — the model, with neutral defaults.
- A reader for `GameApp.SelectedVehicle`, so the garage's choice reaches the car.
- Either wire `speedometer_max` / `lap_count` to something, or delete them from
  `catalogs/skins/racing/genre.json:26-27`. A key that only produces a warning is worse than
  no key.

**The developer authors (content):**

- Every `.tres` — `starter_hatch.tres`, `muscle.tres`, `formula.tres`. Their numbers, their
  balance, their unlock order.
- Every car `PackedScene` — art, collider, wheels.
- Tracks, lap counts, opponents, times.

Per `CLAUDE.md` § *Scope*: framework only. We ship the class that can describe a car; we do
not ship a car.
