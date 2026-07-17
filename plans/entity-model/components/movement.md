# Movement & controllers — 17 components

Locomotion, abilities, camera.

> **The recurring defect here is `MoveAndSlide()` called twice per frame.** A controller calls it,
> then an ability component calls it again on the same body. Godot does not warn. The rule below
> is the fix for a whole class.

---

## The rule: one `MoveAndSlide` per body per frame

`GlideComponent` gets this **right** — `_PhysicsProcess:37` modifies velocity and **does not**
call `MoveAndSlide`, composing cleanly with `PlatformerController`. `DashComponent:58`,
`KnockbackComponent:49` and `FlyComponent` get it **wrong**.

**Ability components adjust `Velocity`. The controller moves the body. Exactly one owner.**

---

## ALIVE

### `MovementComponent` — genre-neutral, AI-steerable
**Evidence:** `player_template`, `enemy_template`, `robot_npc_template`. `_PhysicsProcess:66`
drives the body (`:78-79`). **Not redundant with the controllers** — documented at `:19-21`,
actively guarded at `:62` (warns if a `ControllerComponent` sibling exists).
`DesiredDirection` (`:45`) is public and settable, consumed at `:74`; its doc (`:21`) calls it
*"the one an AI can steer by setting DesiredDirection."* `ReadInput` (`:32`) gates player input.

**Caveat (fact 4):** the three templates carrying it are instanced by no scene.

**Not a `VehicleController`** — verified against all three primitives:
- **Heading:** none. Never touches `Rotation`; `DesiredDirection` is a world vector.
- **Turn rate:** none. `:102` `Velocity.MoveToward(direction * Speed, Acceleration*delta)` is
  symmetric and omnidirectional — a 180° reverse costs the same as accelerating from rest.
- **Lateral grip:** none. No forward/lateral decomposition anywhere; `Friction` (`:29`) is
  isotropic (`:108`). `grep throttle` → 0 hits.

A car that can strafe sideways is not a car. `VehicleController` is genuinely new; inherit only
`ControllerComponent.ResolveBody2D()`.

**Fix (S):** none required. Keep.

### `PlatformerController`
**Evidence:** `platformer_main.tscn:32`; `_PhysicsProcess:50`. Reads `GameInfo` (`:44`). Emits
`Landed`, consumed by `SquashAndStretchComponent.cs:49`. Defers jump to a sibling `JumpComponent`
(`:47`) if present.

**Defect:** `speedMod` (`:79`) is permanently `1f` — it reads `StatusEffectComponent`, whose
`Modifiers` are always empty (see `combat.md`).

**Fix:** none here; fixed by the `StatusEffectComponent` work.

### `TopDownController`
**Evidence:** `topdown_main.tscn`; `_PhysicsProcess:35`.

**Defect:** **`Stopped` (`:53`) fires every frame while idle** — an edge signal emitted as a level.
Contrast `MovementComponent.cs:109-113`, which latches correctly with `_isMoving`.

**Fix (S):** latch it, matching `MovementComponent`.

### `ShooterController`
**Evidence:** `shooter_main.tscn`; `_PhysicsProcess:48`. Spawns projectiles (`:76-105`), sets
`Shooter` (`:101`).

**Defects:**
- **`GetViewport().SetInputAsHandled()` (`:67`) is called from `_PhysicsProcess`** — outside input
  dispatch, where it is a no-op.
- **`_Ready` does `MoveSpeed = info.MoveSpeed; FireRate = info.FireRate` (`:45`) unconditionally**
  — a *per-project* value overwriting a *per-weapon* one. **A `GameWeapon` setting `FireRate` is
  clobbered on every scene load.**
- It **bypasses `AttackComponent` entirely** — its only sibling lookup is `StatusEffectComponent`
  (`:43`), consulted for `speed_boost` only (`:55`). Damage is never modified by anything.
- Inline cooldown (`:34,:64,:68`) duplicates `CooldownComponent`.

**Fix (S/L):** drop `SetInputAsHandled`. **(Phase 3)** the weapon owns fire rate; `GameInfo` is the
project default — no conditional-fallback dance. Read the `damage` `Stat` directly (this is why
`Stat` must be owned by the **entity**, not by `AttackComponent` — otherwise the whole shooter
genre stays inert).

---

## INERT — missing wire (all cheap)

### `JumpComponent`
**Evidence:** `PlatformerController.cs:47` **already defers to it**, and
`SquashAndStretchComponent.cs:38` subscribes to it. **Zero scenes contain the node**, so
`platformer_main` always uses the controller's inline jump (`PlatformerController.cs:71`).
`ForceJump` (`:121`) 0 callers.

**Fix (S):** add the node to `platformer_main`'s player. The consumers are already looking for it.

### `SquashAndStretchComponent`
**Evidence:** self-wires to sibling `JumpComponent` (`:38`) **and** sibling
`PlatformerController.Landed` (`:47`) — **and the `Landed` producer is live in
`platformer_main.tscn` today.** Zero scenes contain the node.

**Defect:** categorized `: ControllerComponent` (`proc=0`) — it is a visual effect that controls
nothing.

**Fix (S):** add to `platformer_main`'s player — **cheapest juice win in the addon**. Reclassify
to `WorldComponent`.

### `DashComponent`
**Evidence:** `_PhysicsProcess:48`; correct `ResolveBody2D()` (`:36`). 0 refs —
`MovementComponent.cs:35-36` says dash was removed *in favour of* this.

**Defect:** **calls `MoveAndSlide()` (`:58`) and so does `PlatformerController`** → double-move.

**Fix (S):** remove its `MoveAndSlide`; wire into `player_template`.

### `GlideComponent`
**Evidence:** `_PhysicsProcess:37`. **Correctly does not call `MoveAndSlide`** — the right pattern.

**Defect:** `GlideAction` defaults to `"jump"` (`:21`) — **the same action `JumpComponent`
consumes.** Both fire on one key.

**Fix (S):** default it to a distinct action; wire in.

### `WallJumpComponent`
**Evidence:** `_PhysicsProcess:81`; creates its own rays (`:50-79`).

**Defect:** `CollisionMask = 0xFFFFFFFF` (`:19`) → the rays hit the player's **own body layer** →
likely self-collides.

**Fix (S):** narrow the default mask; wire into `platformer_main`.

### `SlideComponent`
**Evidence:** `_PhysicsProcess:60`.

**Defect:** **`:93-94` mutates `rect.Size` on the `RectangleShape2D` *Resource*** — which is a
shared `SubResource` in `player_template.tscn`. **Every entity using that shape shrinks.** The
same resource-sharing trap Phase 1 documents for items.

**Fix (S):** `rect.Duplicate()` first; then wire.

### `HoverComponent`, `MovingPlatformComponent`, `FollowTargetComponent`, `CameraZoomComponent`, `FootstepComponent`
All correct, self-contained, 0 scenes. Notes:
- `MovingPlatformComponent` — parent `AnimatableBody2D` (`:31`, safe cast, **no warning**);
  waypoints are `Marker2D` children of **the component**, not the body (`:44-47`) — surprising, so
  document it.
- `FollowTargetComponent` — `TargetReached` re-emits every frame within 1px (`:57-58`), no latch.
  Godot's `Camera2D.position_smoothing` covers the camera case natively.
- `FootstepComponent` — gated on `_body.IsOnFloor()` (`:41`), so it **can only work in the
  platformer**; a top-down `CharacterBody2D` never reports on-floor.
- `CameraZoomComponent` — parent `Camera2D` (`:28`); `_Input:36` + `_Process:70`.

**Fix (S each):** wire where they belong; add the missing parent-mismatch warnings
(`CLAUDE.md` § *Never fail silently*); make `FootstepComponent`'s floor gate an `[Export]`.

---

## REDUNDANT

### `FlyComponent` → `TopDownController`
**Evidence:** **its own doc (`:11-12`) says it "can replace TopDownController or
ShooterController".** Same 8-dir `Input.GetAxis` + `MoveToward` accel/friction + `MoveAndSlide` as
the scene-reached `TopDownController`. The only deltas are banking (`:79-83`) and a boost timer
(`:60`). 0 refs.

**Fix:** fold banking + boost into `TopDownController` as `[Export]`s; delete. → `DELETE.md`

---

## Infrastructure

### `ControllerComponent` (category base) — the only base with behaviour
**Evidence:** `ResolveBody2D()` (`:28-33`) — pattern-match, not a hard cast, and it **warns**
rather than silently nulling. It encodes the child-of-body contract. 20 xrefs.

**Fix:** none. **This is the pattern the other parent-type resolvers should copy** — seven
components hand-roll an unguarded `GetParent() as Area2D` and two are broken by it (see
`items.md`). `AreaTriggerComponent` should be `ControllerComponent`'s equivalent for `Area2D`.

---

## Order

1. `SquashAndStretch` + `Jump` into `platformer_main` (S) — both have live producers waiting.
2. Remove the double `MoveAndSlide` from `Dash` and `Knockback` (S).
3. `SlideComponent` `Duplicate()` the shape (S) — a shared-resource corruption.
4. `TopDownController.Stopped` latch (S); `ShooterController` `SetInputAsHandled` (S).
5. `GlideComponent` action collision (S); `WallJumpComponent` mask (S).
6. Delete `FlyComponent` after folding in banking + boost (M).
7. **(Phase 3)** `ShooterController` reads the `damage` `Stat`; the weapon owns `FireRate`.
8. **New:** `VehicleController` — racing ships **no player controller at all**
   (`racing_main.tscn` has a `LevelLoaderComponent` and nothing else).
