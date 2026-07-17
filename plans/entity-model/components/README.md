# Component Disposition — index

Per-component disposition and fix for **every** component in `addons/beep_game_builder_cs/ecs/`.
One doc per area. Every entry carries a verdict, `file:line` evidence, and the actual fix.

`../phase-0-component-disposition.md` is the **summary and rationale**. These are the **work
orders**. Where they disagree, these win — they are the detail.

## The docs

| Doc | Covers | Components |
|---|---|---|
| [`infra.md`](infra.md) | bases, autoloads, app spine, skin layer | 24 |
| [`combat.md`](combat.md) | health, damage, attack, AI, projectiles | 22 |
| [`movement.md`](movement.md) | controllers, movement abilities, camera | 17 |
| [`world.md`](world.md) | atmosphere, weather, world FX, juice | 24 |
| [`items.md`](items.md) | inventory, crafting, drops, pickups, interaction, production | 17 |
| [`ui-widgets.md`](ui-widgets.md) | the drop-in widget library | 40 |
| [`ui-screens.md`](ui-screens.md) | HUD, menus, the 33 screen scripts | 22 |
| [`DELETE.md`](DELETE.md) | everything to remove, with justification | 16 |

## Verdicts used

| Verdict | Means | Default action |
|---|---|---|
| **ALIVE** | reached and doing its job | keep; fix listed bugs |
| **ALIVE (crippled)** | reached, but a core promise doesn't work | fix |
| **INERT — wire** | correct code, one missing edge | add the edge |
| **INERT — driver** | correct code, the thing that would call it doesn't exist | build the driver, or delete |
| **INERT-BY-DESIGN** | a widget; works when a developer drops it on the right parent. **This is the product, not a defect** | keep; document |
| **BROKEN** | will not work even if called | fix |
| **REDUNDANT** | something else already wins | delete the loser |
| **MISPLACED** | wrong domain for this framework | delete |
| **DELETE** | dead residue | delete |

## The four structural facts

Most verdicts follow from these. Read them once; they explain why so much is inert.

**1. Zero `[connection]` lines in all 74 shipped scenes.** Every component here communicates by
signal, and **no scene connects any signal**. Only six code-side subscriptions exist framework-
wide (`AutoHealComponent.cs:40-41`, `FlashComponent.cs:39`, `HitSparkComponent.cs:29`,
`HitStopComponent.cs:34`, `HudComponent.cs:45`). So *inert* is overwhelmingly **structural** —
one missing edge per component, not 60 bad components.

**2. Nothing in any shipped scene can deal damage.** All three `TakeDamage` call sites are dead
(`AttackComponent.cs:102` inside a 0-caller method; `ProjectileComponent.cs:81` only spawned by
0-caller paths; `TemperatureComponent.cs:180` whose `_health` is permanently null). So
`HealthComponent` ships in 6 scenes and **can only ever be at full health** — and the five
components correctly wired to `Damaged`/`Died` listen to a signal that cannot fire.
**Eight components are gated behind this one fact.**

**3. Scene-instantiated ≠ reached.** `FloatingTextComponent` ships in `pickup_template.tscn` and
its `ShowText()` has 0 callers. `WeatherAudioController` is mounted in `atmosphere.tscn` and
builds an entire audio bus at `VolumeDb = -80f` to mix permanent silence. Presence in a `.tscn`
proves nothing.

**4. Three of four entity templates are orphans.** `player_template`, `pickup_template`,
`robot_npc_template` are instanced by **zero** scenes; only `enemy_template` is reached, via a
`PackedScene` export on `SpawnerComponent`. The genre mains inline their own `Player` carrying a
controller + Health and **no `AttackComponent`** — so wiring `Attack()` to input still leaves the
player with nothing to attack with.

## Decide once, before any wiring

Fact 1 makes **"wire A → B" ambiguous** here — scene `[connection]` or code-side `+=`? There are
zero of the first and six of the second, so there is no convention to follow. ~8 wiring fixes are
queued; without a decision they land in two styles.

**Decision: code-side `+=` in `_Ready`, resolved via `GetSiblingComponent`/`FindComponent`.**
It is what the six working subscriptions do, it survives a developer rebuilding the scene, and it
keeps the component self-contained — the premise of a drop-in library. Reserve `[connection]` for
edges the *developer* owns in *their* scene.

## Effort key

**S** ≈ one line / one node. **M** ≈ one method or one scene restructure. **L** ≈ touches
several files or a signature.

## Verification

Per fix: `dotnet build` → 0 errors, `validate_scenes.sh` → PASS, **and** an editor check that
the thing it unblocks now actually happens. Neither gate runs the game (`CLAUDE.md` § *Testing*).
Per deletion: `grep` the symbol repo-wide, confirm 0 references outside its own file and docs.

> **Nothing in these docs has been run.** Every claim is from reading code and grepping. Where a
> report says "verified", that means verified *by inspection*.
