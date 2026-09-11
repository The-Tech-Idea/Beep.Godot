# DUP-16 — Duck-node registry: one pruning registry behind the transport and extraction managers

**Type:** duplication · **Area:** `GridTransportManagerComponent`, `GridExtractionManagerComponent`, new `DuckTypedNodeRegistry` base · **Status:** **PROPOSED 2026-09-11** · **Effort:** M (~1 day) · **Risk:** low

## Finding

`GridTransportManagerComponent` and `GridExtractionManagerComponent` each hand-roll the same capability — a pruning registry of duck-typed `Node`s — and the extractor's own code says so: the comment at `GridExtractionManagerComponent.cs:36-42` explicitly points at the transporter's `HasMethod` check as the thing it is paralleling. The shared shape, member for member:

1. **Backing list.** `GridTransportManagerComponent.cs:28` `private readonly List<Node> _transporters = new();` vs `GridExtractionManagerComponent.cs:34` `private readonly List<Node> _extractors = new();`.
2. **Register (dup guard + contract check + signal).** Transport `:31-45` — null/`IsInstanceValid`/`Contains` guard (`:33`), contract check (`:36-37`), `Add` + `EmitSignal(TransporterRegistered)` (`:43-44`). Extraction `:51-70` — the same guard split across two returns (null/invalid → `false` at `:53-54`; already-present → `true` at `:56-58`), contract check (`:59-65`), `Add` + `EmitSignal(ExtractorRegistered)` + `return true` (`:67-69`).
3. **Unregister (remove + valid + signal).** Transport `:47-54` and Extraction `:72-79` are line-for-line identical bar the signal name: `_list.Remove(node)` short-circuit, then `EmitSignal(...Unregistered)` only if the node is still valid.
4. **Count-with-prune.** `TransporterCount` `:56-63` and `ExtractorCount` `:81-88` are the same getter: `Prune(); return _list.Count;`.
5. **Prune (reverse-loop over invalid nodes).** Transport `:124-131` and Extraction `:147-154` are byte-identical apart from the field name — reverse `for`, `!GodotObject.IsInstanceValid(...)` → `RemoveAt(i)`.

Only two things genuinely differ, and only one of them is essential:

- **The contract test** — the real, intended divergence. Transport asks by *method*: `HasMethod("RequestHaul"/"CanAccept"/"Load"/"Unload")` (`:36-37`). Extraction asks by *property*: `Get(IsExtractingProperty/ActiveResourceIdProperty).VariantType == Variant.Type.Nil` (`:59-60`). This is a natural override point, not duplication.
- **The `Register` return type** — an incidental drift: transport returns `void` (its one caller `GridHaulerComponent.cs:559` ignores it), extraction returns `bool` (its caller `GridExtractorComponent.cs:476` reads it: `_registered = _extractionManager.Register(this);`). The `bool` contract is the more useful one and the probe already relies on it (`grid_terrain_subsurface_probe.gd:543`).

Each manager then adds its own genuinely distinct domain surface on top — extraction has `Extractors()` (`:91-98`), `ActiveCountFor` (`:101-111`), `EstimatedRatePerTurn` (`:120-137`), `IsActivelyExtracting` (`:139-145`); transport has `RequestHaul`/`RateOf`/`OrderCandidates`/`Transfer` (`:72-122`) — none of which is in scope to touch.

Why it matters: this is the rule-3 defect. The registry mechanics (items 1–5) are one capability living in two files. A future change to the mechanics — a signal-timing fix, an indexed lookup to kill the linear iteration, remainder/valid-node handling — has to land in both `Prune`s and both `Register`s and will drift the moment it lands in only one. The `subsurface_probe` exercises both registries' register/refuse/prune paths today (`:431-440`, `:528-545`), so the behaviour is pinned; the *structure* is not.

## Design

Extract a shared base that owns the mechanics and exposes the contract test as a hook.

- **New `DuckTypedNodeRegistry : Node`** (abstract `partial`, in `ecs/grid/`, **no** `[GlobalClass]` — it is never instantiated directly, only derived). It owns the single `List<Node>` and the five shared members:
  - `public bool Register(Node node)` — null/`IsInstanceValid` guard → `false`; `Contains` → `true` (the "twice is a no-op that still reports success" contract extraction already documents); `!AnswersContract(node)` → named `PushWarning` + `false`; otherwise `Add`, `OnRegistered(node)`, `true`.
  - `public void Unregister(Node node)` — the shared remove-then-signal-if-valid, calling `OnUnregistered(node)`.
  - `public int Count { get { Prune(); return _nodes.Count; } }`.
  - `protected void Prune()` — the one reverse-loop.
  - `protected IReadOnlyList<Node> Registered { get { Prune(); return _nodes; } }` — the pruned view the domain methods iterate.
  - Hooks: `protected abstract bool AnswersContract(Node node);`, `protected abstract string ContractSummary { get; }` (fed into the one warning so each manager keeps its named message — "CanAccept, Load, Unload, RequestHaul" vs "IsExtracting, ActiveResourceId"), and `protected virtual void OnRegistered/OnUnregistered(Node)` that each manager overrides to emit its own typed signal.
- **`GridTransportManagerComponent : DuckTypedNodeRegistry`** keeps `[Tool][GlobalClass]`, its four `[Signal]`s, and overrides `AnswersContract` (the `HasMethod` block), `ContractSummary`, `OnRegistered`/`OnUnregistered` (emit `TransporterRegistered`/`Unregistered`). Its `RequestHaul`/`Transfer`/`RateOf`/`OrderCandidates` stay, iterating `Registered` instead of `_transporters`. `public int TransporterCount => Count;` stays as a domain-named forwarder — the GDScript probe (`:433`, `:440`) and any HUD read that name.
- **`GridExtractionManagerComponent : DuckTypedNodeRegistry`** likewise keeps its signals and overrides the property-`Nil` `AnswersContract`, and keeps `Extractors()`/`ActiveCountFor`/`EstimatedRatePerTurn`/`IsActivelyExtracting` iterating `Registered`. `public int ExtractorCount => Count;` forwards; `GridObjectiveEventBinderComponent.cs:109` and the probe (`:531`) keep working unchanged.
- **Register unifies to `bool`.** Transport's caller ignores the value so the change compiles clean (no-legacy rule — rename/retype, let the compiler sweep, no shim). Extraction's `bool` caller is unaffected.

Every added base member has a consumer in the same change (both managers call `Register`/`Unregister`/`Count`/`Registered`; the base calls each override), so nothing is introduced orphaned. This is **not** a per-genre variant merge — the memory carve-out ("genre variants are not duplicates") is about classes meant to diverge in look/texture; these two diverge only in a contract test, which the design preserves as the override point rather than collapsing.

## Guards (fail first)

1. **Scan pin (structural) — `tests/addon_contract_scan.ps1`.** Assert both `GridTransportManagerComponent.cs` and `GridExtractionManagerComponent.cs` declare `: DuckTypedNodeRegistry`, and that neither file still contains its own `List<Node> _transporters`/`_extractors` field nor a `private void Prune(`. **Fails first:** before the fix the base type does not exist and both managers carry their own list + `Prune`, so the `: DuckTypedNodeRegistry` assertion fails. **Mutation after the fix:** revert either manager to a private `List<Node>` + inline `Prune()` → the "no private backing list / no local Prune" assertion trips.
2. **Behavioural — `tests/grid_terrain_subsurface_probe.gd` (already present, must stay green).** It already covers the shared mechanics through both concrete managers: a contract-failing node is refused (`TransporterCount == 0` at `:433`; `not refused and ExtractorCount == 0` at `:543-544`), a duck-typed GDScript node registers (`TransporterCount == 1` at `:440`), and a freed node leaves via `Prune` (`ExtractorCount == 0` at `:535`, `:566`). **Mutation:** drop the `RemoveAt(i)` in the base `Prune` → a freed extractor/transporter is still counted → `ExtractorCount == 0` / freed-node checks red; or drop the base `Contains` guard → registering the shipped extractor twice pushes `ExtractorCount` to 2. Add one line to the probe asserting a second `Register` of the same node returns `true` and leaves `Count` unchanged, so the dup-guard has its own explicit red.

## Dependencies / collisions

- **DUP-05** (GridIds one normaliser) and **DUP-13** (TerrainKindCatalog) are sibling one-owner resolves; no code overlap with these two manager files.
- **ENH-01** (typed `CellsChanged` + affected-chunks payload), **ENH-02** (edit-kind classification), **ENH-12** (job-queue spatial claim index) are unrelated subsystems; ENH-12 is the natural home for any *indexed* registry lookup, which this plan deliberately does not add.
- **FIX-12** (`GridPorts.Transfer` remainder) touches `GridPorts.Transfer`, which `GridTransportManagerComponent.Transfer` (`:121-122`) delegates to. This refactor does not touch `Transfer`'s body or `GridPorts`, so the two are adjacent but non-colliding — sequence either first.
- The concurrent session owning `TerrainWorldComponent`/streaming/archive does not touch these two resource-manager files; no collision.

## Out of scope

- The two contract checks themselves — they stay per-manager as the `AnswersContract` override; this is the intended divergence, not something to unify.
- Any indexed/spatial lookup or signal-timing change to the registry mechanics — behaviour is preserved exactly (extract-only); performance work belongs to its own item.
- `GridPorts.Transfer`'s dropped-remainder bug (FIX-12) and the transporter/extractor domain methods (`RequestHaul`, `EstimatedRatePerTurn`, …).
- Adding the missing HUD panel the extraction manager's own doc notes is absent (`:13-17`).
