# GridResourceWalletComponent

Gameplay component: a small string-keyed integer resource store — wood, stone, coins, oil, parts, food, seeds — for a top-down settlement/builder, standing in for a slot-inventory UI. It's the shared sink and source both other economy files in this batch write through: `GridResourceNodeComponent` adds gathered amounts to it, and `GridProductionComponent` spends its inputs from it and adds its outputs back to it.

Its `StartingResourceAmounts` export is deliberately a plain `Godot.Collections.Dictionary` authored as scene data (`{ "wood": 120, "stone": 35 }`) rather than C# `Resource` subresources — the doc comment notes Godot can deserialize a typed resource as a plain `Resource` before the managed script type is bound, which would lose the actual amounts. `TrySpendAmount` is kept to the same rejection contract as `Spend`: a short balance emits `ResourceSpendRejected` and changes nothing, so any HUD listening for failed multi-resource purchases also sees a failed single-resource spend (e.g. a seed cost) through the same signal.

## Public API
- Signals: `ResourceChanged(resourceId, amount)`, `ResourcesChanged()`, `ResourceSpendRejected(resourceId, required, available)`.
- `[Export] bool ParticipatesInSave` / `string SaveKey` — gates and keys this wallet's entry in the save system.
- `[Export] Godot.Collections.Dictionary StartingResourceAmounts` / `bool ApplyStartingResourcesOnReady` — see above.
- `int GetAmount(string resourceId)` — normalized lookup, 0 if absent.
- `void SetAmount(string resourceId, int amount)` — clamps to non-negative; removes the key entirely at zero (see Notes); emits both change signals.
- `void AddAmount(string resourceId, int amount)` — delta via `SetAmount`; no-op for a zero delta.
- `bool CanAfford(Godot.Collections.Array costs)` — true only if every `(resourceId, amount)` pair in `costs` (read via `GridResourceAmount.Enumerate`) is affordable.
- `bool Spend(Godot.Collections.Array costs)` — atomic: if unaffordable, emits `ResourceSpendRejected` for the first short resource and changes nothing; otherwise deducts every cost.
- `bool TrySpendAmount(string resourceId, int amount)` — single-resource equivalent of `Spend`, same rejection contract.
- `void Refund(Godot.Collections.Array costs)` — adds every cost back; used to undo a spend (e.g. a cancelled production run).
- `Godot.Collections.Dictionary CaptureState()` — resourceId → amount snapshot.
- `void RestoreState(Godot.Collections.Dictionary state)` — clears and repopulates from a captured dictionary (via `GridVariantReader.Int`), skipping non-positive amounts.
- `Godot.Collections.Dictionary GetAmounts()` — identical to `CaptureState()` (see Notes).
- `void LoadStartingResourceAmounts(Godot.Collections.Dictionary amounts)` — clears and repopulates via `AddAmount` from a dictionary shape.
- `void LoadAmounts(Godot.Collections.Array amounts)` — clears and repopulates via `GridResourceAmount.TryRead` from an array-of-entries shape.
- `void Save(GameBuilder.GameStateData state)` / `void Load(GameBuilder.GameStateData state)` — `ISaveable` implementation; writes/reads `CaptureState()`/`RestoreState()` under `SaveKey` in `state.GameData`.

## Dependencies
- Implements `ISaveable`; joins/leaves `SaveableHelper.Group` in `_Ready()`/`_ExitTree()` when `ParticipatesInSave` and not running in the editor.
- Uses `GridResourceAmount.Enumerate`/`TryRead` to walk cost/amount arrays, and `GridVariantReader.Int` to coerce stored values.
- **Consumed within this batch** by `GridResourceNodeComponent.GatherForJob` (`_wallet?.AddAmount(id, gathered)`) and by `GridProductionComponent` (`_wallet.Spend(recipe.Inputs)` on start, `_wallet.AddAmount(...)` per output on completion, `_wallet.Refund(recipe.Inputs)` on a refunding cancel).

## Notes
- `GetAmounts()` is a second public name for exactly `CaptureState()` — `GetAmounts() => CaptureState();` — with no distinct behavior; a caller has two names to choose between for the same read.
- Two bulk-load entry points exist with overlapping purpose but different accepted shapes: `LoadStartingResourceAmounts` reads a `Dictionary` (matching the `StartingResourceAmounts` export's shape) and `LoadAmounts` reads an `Array` of entries via `GridResourceAmount.TryRead` (matching the shape `GridProductionRecipe.Inputs`/`Outputs` use elsewhere in this batch). Both clear `_amounts` first, so they are not composable — this looks like intentional support for two different serialization conventions rather than a duplicate, but the asymmetric naming (one names "StartingResourceAmounts", the other just "Amounts") makes the two easy to reach for interchangeably by mistake.
- `SetAmount` removes the dictionary key entirely rather than storing an explicit `0` once a resource balance is spent to exactly zero — a caller enumerating `CaptureState()`/`GetAmounts()` to render every resource the wallet has ever held (rather than just those it currently holds a positive amount of) would not see a zero entry for that resource.
