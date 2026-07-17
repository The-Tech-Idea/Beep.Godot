# Component audit — round 2 (Controller / Gameplay / World / UI)

Same four-lens review as the atmosphere + first component pass, now applied to the component
**types the first pass didn't cover systematically**: all `ControllerComponent` (18),
`GameplayComponent` (43), non-atmosphere `WorldComponent` (13), `UIComponent` (51),
`EffectComponent` (4). Four parallel audit agents scanned by category; every finding below was
then re-verified by hand (file:line + grep) before landing here. Already-fixed items from the
prior pass were excluded from scope.

**Dominant finding:** a *systemic* missing editor guard. 36 `[Tool]` components mutate their
parent / a sibling / the scene root (or inject nodes into the parent) from `_Ready`/`_Process`
with no `if (Engine.IsEditorHint()) return;`. This is the exact class already fixed ~10× this
session, and it is the direct cause of the "asking to reload from disk when I open/close a scene"
pain reported earlier — opening a scene runs these and dirties it. A parallel set (Dialog,
SceneTransition, Settings, Localization, Trail…) already carries the guard, so the fix pattern is
established and one-line per file.

Self-building widgets (`AddChild` on `this`) and the deliberate editor-preview components
(ThemePresetComponent, Vignette, Chromatic — they gate on `IsEditorHint` by design) are **not**
flagged.

---

## Tier 1 — Crashes + shipped-scene hazards (highest priority)

| Component | file:line | Problem | Fix |
|---|---|---|---|
| **InteractionPromptComponent** | `ecs/ui/…:47-49` | **NRE crash.** `parent = GetParent() as Control` is null-checked for `AddChild`, then `parent.IsInsideTree()` runs unconditionally → null-deref when parent isn't a Control. Its own doc says "place on a HUD **CanvasLayer**" (not a Control) → documented usage crashes. | Null-check parent before `IsInsideTree()`; add editor guard. |
| **AnimatedMenuComponent** | `ecs/ui/…:28-33` | In **5 generated scenes** (main_menu, game_over, level_results, level_complete, character_select). `_Ready`→`ShowAnimated` sets every sibling's Modulate α=0 + repositions at editor time. | Editor guard in `_Ready`. |
| **PickupComponent** | `ecs/…:40-57` | In **pickup_template.tscn**, attached to the Area2D **root** → `_Process` bobs/spins the scene root in-editor. | Editor guard at top of `_Process`. |
| **LifetimeComponent** | `ecs/…:29-46` | In **projectile_template.tscn**. `_Process` runs in editor and after `Lifetime` (2s) calls `GetParent().QueueFree()` → **deletes the parent** being edited. | Editor guard in `_Process`. |
| **Load/SaveGameMenuComponent** | `ecs/ui/…:32-39` | Ship in load_game_menu.tscn / save_game_menu.tscn. Throwing `GetNode<T>` before `if (x != null)` null-checks (unreachable); also unguarded root-iteration in editor. | `GetNode<T>`→`GetNodeOrNull<T>`; editor guard. |
| **BossHealthBar / BuffBar / ComboCounter** | `ecs/ui/…` | Inject into parent **and set `.Owner`** → the injected nodes **persist into the saved scene** (bakes duplicates on open+save). | Editor guard (stops the bake). |

## Tier 2 — Editor guards, remaining parent-injectors/mutators (mechanical, 1 line each)

All confirmed `[Tool]`, `IsActive`/`AutoStart` default true, no guard, mutate/inject into
parent-or-root. None currently in shipped scenes, but each corrupts a *user's* scene on open.

- **World:** LightingComponent (injects DirectionalLight2D into parent), RotateComponent, BobComponent, WindFieldComponent (sets parent Area2D gravity override).
- **Controller/Effect:** WallJumpComponent (injects 2 RayCast2D into body), PulseComponent, SlideInOutComponent, FollowTargetComponent.
- **Gameplay:** ProjectileComponent (latent — `_area==null` early-out saves it, but guard for consistency), TweenComponent, AnimalBehaviorComponent.
- **UI widgets:** Badge, Chip, Rating, SearchBar, Stepper, Table, ToggleSwitch, UIEffect (PlayOnReady default true; Scope=Scene/Global reaches root), Menu (only under EnableRipple), Loading, Modal, Accordion, Carousel, TabGroup, Marquee, FlipCard, Typewriter, SkeletonLoader.

## Tier 3 — Broken contracts (correctness, targeted per-file)

- **CheckpointComponent** `:41` — `app.SetLevel(app.CurrentLevel)` is a self-assignment **no-op**; the real API `GameApp.SetCheckpoint(int)` (GameApp.cs:296) is never called → checkpoints record nothing. Fix: call `SetCheckpoint` (persist the checkpoint).
- **DoorSwitchComponent** `:53-64` — key-gated door only checks that a `PickupComponent` **exists** on the player, never compares it to `RequiredItem` → any player opens any gated door. Fix: compare the carried item id to `RequiredItem`.
- **Resistance/DamageType** — `ApplyResistance()`/`GetDamage()` have **0 callers**; `HealthComponent.TakeDamage` never consults them despite the documented contract. **[DECISION: wire into HealthComponent.TakeDamage, or delete the pair.]**
- **FlashComponent** `:34/60` — `Damaged += (a,h)=>Flash()` and `-= (a,h)=>Flash()` are **different lambda instances** → `-=` is a no-op, handler leaks. Fix: store the handler in a field.
- **AIController** `:113` + **FollowTargetComponent** `:30` — throwing `GetNode<Node2D>` defeats the following null-guard. Fix: `GetNodeOrNull`.
- **SquashAndStretchComponent** `:47` — `OnLand()` (the squash half) has 0 callers; JumpComponent has no Landed signal. Fix: subscribe to the controller's `Landed` → `OnLand()`, or drop the land claim. (low; 0-ref)

## Tier 4 — Dedup / dead code

- **PowerSource + PowerReceiver** — orphaned pair: 0 external refs, no `PowerSystem` to connect them (non-functional as shipped), **and** class names (`PowerSource`/`PowerReceiver`) don't match filenames (`PowerSourceComponent.cs`/…). **[DECISION: delete both, or build a PowerSystem + rename.]**
- **TypewriterComponent** — 0 refs; a richer typewriter already exists in `UIEffectComponent` (EffectType.Typewriter, handles Label + RichTextLabel). Recommend **keep** as a lightweight standalone widget (it's a valid drag-drop node) — low value in removing. (optional)
- **GameStateManagerComponent.FindSaveables** `:226-233` — reimplements `SaveableHelper.Collect` (IGameStateable.cs), which is otherwise unused. Fix: call the helper. (low)

## Non-goals / explicitly not flagged
- 0-ref `[GlobalClass]` widgets (Chip, Stepper, Crop, Quest, Crafting…) are drag-and-drop palette
  nodes — 0 refs is normal, **not** dead code. Only Power* (non-functional + name bug) is flagged.
- Editor-preview components (ThemePreset, Vignette, Chromatic) intentionally act in-editor — left alone.
- Group-read "no joiner" false alarms: `weather_system`, `screen_shake`, `players`/`enemies`
  (via `ComponentGroup` export / SpawnerComponent) all have real join paths — verified, not bugs.

## Verification (after each tier)
1. `dotnet build` → 0 errors.
2. Scene validator → PASS.
3. `grep -L IsEditorHint` across the guarded set → empty (every flagged file now guarded).
4. Open pickup_template / projectile_template / main_menu in Godot → no runtime nodes appear,
   no "reload from disk" prompt. (User-run — I can't drive the editor.)

## Decisions needed before executing
- **A. Editor-guard scope:** all 36 (Tier 1+2), or Tier 1 only?
- **B. PowerSource/PowerReceiver:** delete, or keep + build a system?
- **C. Resistance/DamageType:** wire into HealthComponent, or delete?

Nothing committed. Deletions need explicit go-ahead per the standing rule.
