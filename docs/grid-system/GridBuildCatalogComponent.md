# GridBuildCatalogComponent

Menu data source: a `[Tool][GlobalClass]` `Node` that holds the list of placeable builds (`GridBuildDefinition` entries) for a grid-builder game and is the single front door a build menu talks to — look up a build by id, list ids by category, check whether it is affordable, and start placing it.

It exists to keep build-menu data in one place rather than scattered across UI code, and to put one gate in front of `GridPlacementComponent.BeginPlacement` instead of letting every menu button reimplement the checks. `BeginPlacement` refuses a build that has no scene or preview texture (`HasPlayableSurface`), refuses one with no resolvable `GridPlacementComponent`, and — when `RequireAffordableToBegin` is set and a wallet is wired — refuses one the player cannot currently pay for, each with a distinct rejection reason rather than a bare `false`. Lookups normalize ids (trimmed, lower-invariant) so a menu's casing does not have to match a `.tres` asset's exactly.

## Public API
- `[Signal] BuildSelectedEventHandler(string buildId)` — fired when `BeginPlacement` actually hands the build to placement.
- `[Signal] BuildRejectedEventHandler(string buildId, string reason)` — fired instead, with a machine-readable reason (`missing_build`, `missing_scene_or_preview`, `missing_placement`, `missing_resources`).
- `[Export] NodePath PlacementPath` — the `GridPlacementComponent` builds are started on.
- `[Export] NodePath ResourceWalletPath` — the `GridResourceWalletComponent` affordability and cost-summing read from; optional.
- `[Export] bool RequireAffordableToBegin = true` — whether `BeginPlacement` itself blocks an unaffordable build, versus letting placement start regardless and leaving affordability to whatever charges the wallet on confirm.
- `[Export] Godot.Collections.Array Builds` — the raw catalog entries (dictionaries and/or `GridBuildDefinition` resources), read through `GridBuildDefinition.Enumerate`.
- `public override void _Ready()` — resolves references and refreshes configuration warnings.
- `public override string[] _GetConfigurationWarnings()` — warns if `PlacementPath` is unset.
- `public GridBuildDefinition? FindBuild(string buildId)` — normalized (case/whitespace-insensitive) lookup by id, or `null`.
- `public Godot.Collections.Array<string> BuildIdsForCategory(string category)` — ids of builds in a category; a blank/empty category returns every build's id.
- `public bool CanAfford(string buildId)` — `true` if the build is unknown-but-no-wallet-wired, or if the wired wallet can afford its `Costs`.
- `public bool BeginPlacement(string buildId)` — validates existence, playable surface, resolvable placement, and (if required) affordability, then calls `GridPlacementComponent.BeginPlacement(build, chargeCostOnConfirm: wallet != null)` and emits `BuildSelected`; emits `BuildRejected` and returns `false` on any failed check.
- `public Godot.Collections.Dictionary CostSummary(string buildId)` — sums a build's `Costs` entries by normalized resource id (defends against a raw `Costs` array listing the same resource more than once).

## Dependencies
- Resolves `GridPlacementComponent` (calls `BeginPlacement` on it) and `GridResourceWalletComponent` (`CanAfford`), each via `NodePath` or, if unset, `EntityComponent.FindComponent` over `GetTree().CurrentScene`.
- Reads catalog entries through `GridBuildDefinition.Enumerate`/`GridResourceAmount.Enumerate` and `GridVariantReader.Int`.
- Within this batch: `GridBuildSiteComponent` resolves this component and calls `FindBuild(buildId)` on it to re-fetch a `GridBuildDefinition` when creating, checking, or cancelling a build site — this file is a dependency of that one, not the other way around.

## Notes
- `RequireAffordableToBegin` only gates whether `BeginPlacement` itself refuses; the `chargeCostOnConfirm` flag passed to placement is instead based purely on whether `_wallet` resolved at all, so setting `RequireAffordableToBegin = false` does not also disable charging on confirm — the two concerns (start-time gate, confirm-time charge) are controlled by separate signals here.
- `ResolveReferences()` is called again inside `CanAfford` and `BeginPlacement` even though `_Ready()` already calls it once — every entry point re-resolves rather than trusting the cached fields, consistent with the addon-wide pattern of re-validating with `GodotObject.IsInstanceValid` rather than caching aggressively.
- `CostSummary` is the only place in this file that actually walks and aggregates `Costs`; `CanAfford`/`BeginPlacement` instead hand the raw `Costs` array to the wallet unsummed, so the wallet's own `CanAfford` must already tolerate a resource listed more than once.
