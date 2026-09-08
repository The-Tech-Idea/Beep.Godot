# GridListPanelComponent

Abstract `[Tool] [GlobalClass]` base, on top of [GridPanelComponent](GridPanelComponent.md), for the HUD panels that render a keyed, sorted list of rows under a title and a summary line: the job board, the worker status panel, the production panel, and the objective panel.

All four used to carry their own byte-for-byte copy of all of it — the Title/Summary/Rows three-tier lookup, the bind-or-generate bootstrap, the generated `PanelContainer`/`Content`/`Title`/`Summary`/`Rows` layout, and the seen-set row diff. None of that is domain logic: what a row SAYS is the panel's own business, and that is all a subclass supplies.

Rows are updated IN PLACE. A `Label` is created once per id, reused on every later refresh, moved into the caller's sorted position, and freed only when its id leaves the list — recreating every row per refresh was node churn on panels whose row set is almost always identical to the last pass, several times a second.

## Public API
- `[Export] NodePath TitleLabelPath`, `SummaryLabelPath`, `RowsContainerPath` — the authored control slots.
- `[Export] string TitleText`, `[Export] Vector2 PanelMinimumSize` — each subclass sets its own default in its constructor ("Jobs", "Workers", "Production", "Objectives").
- `bool UsesSceneControls()` — whether the scene authored any of the three controls.
- `protected abstract string GeneratedRootName` / `protected abstract string RowNamePrefix` — the generated root's node name, and the prefix (and fallback) for a generated row's name.
- `protected Label? TitleLabel`, `SummaryLabel`, `VBoxContainer? RowsContainer`, `protected bool ControlsReady` — the bound controls and whether the panel can draw at all.
- `protected bool BindExistingControls()` — binds the scene's own controls; a Summary and a Rows container is enough, the title is optional.
- `protected bool HasAuthoredControls()` — for the configuration warning.
- `protected void BuildGeneratedPanel()` — the default layout, built only when the scene authored none and `GenerateControlsWhenPathsEmpty` allows it.
- `protected void ApplyTitleText()`, `protected void ClearGeneratedControls()`, `protected void ClearRows()`.
- `protected void UpdateRows(IEnumerable<GridPanelRow> rows, int maxVisible)` — the row diff. Takes the caller's own order, skips duplicate and empty ids, and prunes rows whose id has left.
- `protected string RowText(string id)`, `protected int RowCount`, `protected int RowsContainerChildCount` — read-back for each panel's public `TextForX`/`VisibleXRowCount` surface.

## Dependencies
[GridPanelComponent](GridPanelComponent.md) for `SetEditedOwner`/`FindControl`/`SafeName`/`GenerateControlsWhenPathsEmpty`; [GridPanelRow](GridPanelRow.md) as the row DTO; `Beep.ECS.UI.Kit.KitChrome` for the theme-override helpers. Subclassed by `GridJobBoardComponent`, `GridWorkerStatusPanelComponent`, `GridProductionPanelComponent`, `GridObjectivePanelComponent`.

## Notes
- `MaxVisibleJobs`/`MaxVisibleWorkers`/`MaxVisibleMachines`/`MaxVisibleObjectives` deliberately stay on the subclasses and are passed into `UpdateRows` — those export names are each panel's public API and appear in scene overrides.
- `RowCount` and `RowsContainerChildCount` are both exposed because the panels' existing public counts differ: the job board reports the container's child count, the others report the number of tracked rows. Preserved rather than unified.
- `ClearRows` frees every child of the rows container, authored ones included — that is the pre-existing behaviour of the job board's "queue missing" branch.
- `tests/addon_contract_scan.ps1` pins this file as the one home of the list-panel surface, and separately pins that each of the four panels actually derives from it.
