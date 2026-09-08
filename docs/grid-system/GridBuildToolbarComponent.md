# GridBuildToolbarComponent

Build palette HUD (a `Control`, `[Tool][GlobalClass]`) for grid builder/farming/settler scenes: it reads a `GridBuildCatalogComponent`'s registered builds, groups them into category tabs, and renders one button per build that calls `BeginPlacement` on the catalog when pressed. It is the standard "pick what to build" surface for the grid system's construction layer, sitting alongside (but never itself driving) `GridInteractionModeComponent`/`GridPlacementComponent`, which actually carry out the placement.

Like every panel in this batch it supports two independent ways of getting its Controls: bind to scene-authored nodes (an `HBoxContainer` named `Categories` and a `GridContainer` named `Builds`, found by convention name or by explicit `CategoryRowPath`/`BuildGridPath` exports) or, when `GenerateControlsWhenPathsEmpty` is true and no authored surface is found, build its own `VBoxContainer`/`HBoxContainer`/`ScrollContainer`/`GridContainer` tree at runtime. This dual mode is the same pattern used throughout the UI batch and exists so a game can either hand-author its build palette's look in the scene editor or get a working one for free. Buttons are disabled (and dimmed via `Modulate`) rather than hidden when unaffordable, unless `HideUnaffordable` opts into removing them from the grid entirely — kept as two separate knobs because "can't afford yet" and "shouldn't be discoverable" are different UX decisions a game may want independently.

## Public API

- `[Signal] BuildButtonPressedEventHandler(string buildId)` — fired after a build button successfully begins placement via the catalog.
- `[Signal] BuildButtonRejectedEventHandler(string buildId, string reason)` — fired with reason `"missing_catalog"` or `"catalog_rejected"` when `SelectBuild` fails.
- `[Signal] CategoryChangedEventHandler(string category)` — fired when `SelectCategory` runs.
- `[Export] NodePath BuildCatalogPath/ResourceWalletPath/InteractionModePath/CategoryRowPath/BuildGridPath` — component/control wiring; the last two are optional (see Notes on binding order).
- `[Export] bool BuildInEditor = true` — whether `RebuildToolbar()` runs while `Engine.IsEditorHint()` is true.
- `[Export] bool GenerateControlsWhenPathsEmpty = false` — allow generating the category row/build grid when no scene-authored or path-referenced surface is found.
- `[Export] bool AutoSwitchInteractionMode = true` — when true, a successful `SelectBuild` also calls `GridInteractionModeComponent.BuildMode()`.
- `[Export] bool HideUnaffordable = false` — skip creating a button at all for builds the wallet can't currently afford, instead of creating it disabled.
- `[Export] Vector2 ButtonMinimumSize = new(120, 56)` — desired build-button size; use `EffectiveButtonMinimumSize` for the sanitized value actually applied.
- `[Export] string CurrentCategory = ""` — the active category tab; auto-set to the first category on first populate if empty or invalid.
- `Vector2 EffectiveButtonMinimumSize` — `ButtonMinimumSize` clamped to at least `(1,1)` and with non-finite components replaced by the `(120,56)` default.
- `public override void _Ready()` — resolves references, defers `RebuildToolbar()` per `BuildInEditor`, and (runtime only) subscribes to `_wallet.ResourcesChanged` for affordability refresh.
- `public override void _ExitTree()` — disconnects all button handlers and the wallet subscription.
- `public override string[] _GetConfigurationWarnings()` — warns if `BuildCatalogPath` is unset, or if neither a category-row/build-grid surface path is set nor `GenerateControlsWhenPathsEmpty` is enabled.
- `public void RebuildToolbar()` — full teardown/rebuild: resolves references, disconnects old buttons, binds an existing surface or (if allowed) generates one, then populates categories and builds.
- `public void SelectCategory(string category)` — sets `CurrentCategory`, updates tab toggle state, rebuilds the build-button grid for that category, refreshes affordability, emits `CategoryChanged`.
- `public bool SelectBuild(string buildId)` — calls `GridBuildCatalogComponent.BeginPlacement(buildId)`; on success optionally switches interaction mode and emits `BuildButtonPressed`; on failure emits `BuildButtonRejected`. Always refreshes affordability afterward.
- `public int VisibleBuildButtonCount()` — count of currently instantiated build buttons.
- `public void RefreshAffordability()` — re-evaluates `GridBuildCatalogComponent.CanAfford(buildId)` for every visible button and toggles `Disabled`/`Modulate` accordingly (no wallet resolved ⇒ everything is treated as affordable).
- `public bool UsesSceneControls()` — true if a `Categories`/`Builds` surface can be found (by path or by convention name), used to decide whether generation would even be attempted.

## Dependencies

- Reads `GridBuildCatalogComponent.Builds`, `.BeginPlacement(string)`, `.CanAfford(string)` and iterates it via `GridBuildDefinition.Enumerate(...)`/`.PreviewTexture`/`.Costs`/`.Category`/`.DisplayName`/`.EffectiveFootprint` (all outside this batch).
- Reads `GridResourceWalletComponent.ResourcesChanged` and iterates costs via `GridResourceAmount.Enumerate(...)` (outside this batch).
- Calls `GridInteractionModeComponent.BuildMode()` (outside this batch) when `AutoSwitchInteractionMode` is set.
- Uses `EntityComponent.FindComponent<T>(...)` as the scene-wide fallback when a `NodePath` export is empty — the same lookup-by-scan pattern every component in this batch uses.
- Uses `KitChrome.SetConstantOverrideIfChanged(...)` (from `Beep.ECS.UI.Kit`, outside this batch) to apply container spacing without redundant re-sets.
- Not established from this batch alone whether anything calls into `GridBuildToolbarComponent` — none of the other 12 UI files in this batch reference it.

## Notes

- `SelectBuild`'s rejection reasons are two fixed string literals (`"missing_catalog"`, `"catalog_rejected"`) rather than an enum — callers pattern-matching `BuildButtonRejected`'s `reason` argument are matching on hardcoded strings.
- `RebuildToolbar()`'s failure path when no build surface can be bound and `GenerateControlsWhenPathsEmpty` is false silently returns with no buttons and no runtime warning (only `_GetConfigurationWarnings()` covers this, and only in the editor).
- The `SafeName(string)` static helper (sanitizing a build/category id into a valid node name) appears with near-identical bodies in several other files in this batch (`GridJobBoardComponent`, `GridObjectivePanelComponent`, `GridProductionPanelComponent`, `GridResourceBarComponent`, `GridWorkerStatusPanelComponent`) rather than being shared from one place.
- `PopulateToolbar()` always calls `BuildButtonsForCategory(CurrentCategory)` and `RefreshAffordability()` even though `SelectCategory` (which the category tabs call) already does the same two calls — redundant on the very first populate but not incorrect.
