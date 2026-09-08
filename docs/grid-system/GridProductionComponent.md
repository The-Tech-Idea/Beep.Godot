# GridProductionComponent

Gameplay component: attach to a building node to run a simple timed production cycle — consume a recipe's input resources from a `GridResourceWalletComponent`, wait out its duration, then add the recipe's outputs back to the wallet. This is the batch's production half of the economy, mirroring `GridResourceNodeComponent`'s gathering half; both write through the same wallet.

## Public API
- `enum ProductionState { Idle, Producing, Paused }`
- Signals: `ProductionStarted(recipeId)`, `ProductionCompleted(recipeId)`, `ProductionRejected(recipeId, reason)`, `ProductionStateChanged(state)`.
- `[Export] NodePath ResourceWalletPath` — resolved to `GridResourceWalletComponent`.
- `[Export] NodePath WorkClockPath` — the [GridWorkClockComponent](GridWorkClockComponent.md) that decides what a turn of production is; empty finds one scene-wide. With none anywhere the machine runs off its own frame delta at one turn per second, so a template scene or a headless probe still works.
- `[Export] Godot.Collections.Array Recipes` — untyped array of `GridProductionRecipe` resources and/or dictionary entries (read via `GridProductionRecipe.Enumerate`).
- `[Export] string ActiveRecipeId` — the recipe used when `StartProduction` is called with no explicit id.
- `[Export] bool AutoStart` — starts `ActiveRecipeId` on `_Ready()` outside the editor.
- `[Export] bool Loop` — restarts the same recipe immediately on completion.
- `[Export] bool ConsumeInputsOnStart` — whether inputs are spent at start (and therefore refundable on cancel) versus not consumed at all.
- `ProductionState State { get; }`, `float RemainingTurns { get; }`, `string CurrentRecipeId { get; }` — read-only run state; the remainder is in turns, the same unit as the recipe's `DurationTurns`.
- `float Progress01 { get; }` — `1 - RemainingTurns / recipe.EffectiveDurationTurns`, clamped; `0` if no current recipe resolves.
- `float EffectiveRemainingTurns { get; }` — `RemainingTurns` clamped to a finite non-negative value. Saved under `remaining_turns`.
- `void AdvanceWork(float turns)` — burns turns off the recipe in progress; calls `CompleteProduction()` at zero. Bound to the work clock's `WorkTick` through a composed [GridWorkClockBinding](GridWorkClockBinding.md).
- `void Tick(double delta)` — kept for callers that step production deliberately; forwards to `AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta))`, so one second is one turn on the real-time axis and the meaning is unchanged there.
- `bool StartProduction(string recipeId = "")` — resolves a recipe (explicit id, else `ActiveRecipeId`, else the first recipe in `Recipes`), requires it to have outputs, optionally spends its inputs, and transitions to `Producing`; emits `ProductionRejected` and returns `false` for a missing wallet, an already-running production, a missing/output-less recipe, or unaffordable inputs.
- `void PauseProduction()` / `ResumeProduction()` — toggle between `Producing` and `Paused`.
- `void CancelProduction(bool refundInputs = false)` — returns to `Idle`; refunds the current recipe's inputs to the wallet only if both `refundInputs` and `ConsumeInputsOnStart` are true.
- `bool CompleteProduction()` — adds every positive-amount output to the wallet, emits `ProductionCompleted`, returns to `Idle`, and — if `Loop` — immediately calls `StartProduction` again with the same recipe id.
- `GridProductionRecipe? FindRecipe(string recipeId)` — case/format-normalized lookup in `Recipes`.

## Dependencies
- Resolves `GridResourceWalletComponent` (this batch) via NodePath or `EntityComponent.FindComponent`.
- Reads `GridProductionRecipe.Enumerate`/`HasOutputs`/`EffectiveDurationSeconds` (this batch) over `Recipes`, and `GridResourceAmount.Enumerate` to walk a completed recipe's `Outputs`.
- Calls `GridResourceWalletComponent.Spend`, `AddAmount`, `Refund` (this batch).
- Not established from this batch alone: no other file in this batch wires up or calls into `GridProductionComponent` — its caller (a building UI, a construction manager) is outside this batch.

## Notes
- When `Loop` is true, `CompleteProduction()` calls `StartProduction(completedRecipe)` synchronously within the same call stack. If `ConsumeInputsOnStart` is true and the wallet can no longer afford the recipe's inputs, `StartProduction` rejects and the component simply drops back to `Idle` — there is no retry timer, so a building whose inputs run dry mid-loop stays idle until something external (not in this batch) calls `StartProduction` again, rather than resuming automatically once the wallet is restocked.
- `Recipes` follows the same untyped-`Array`-of-resource-or-dictionary shape as `GridCropCatalogComponent.Crops` in this batch — both lean on a sibling `*.TryRead`/`Enumerate` pair (`GridProductionRecipe`/`GridCropDefinition`) rather than a shared generic reader, despite the two `TryRead` implementations being structurally identical (see `GridProductionRecipe.md` Notes).
- `_Process` runs **only when no work clock was found** — `_Ready` does `SetProcess(!bound)` after binding. With a clock present the machine never self-ticks, which is what stops a factory running in real time inside a turn-based game while everything else waits for a turn. `tests/addon_contract_scan.ps1` pins the binding.
