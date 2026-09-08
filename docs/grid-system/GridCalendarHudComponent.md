# GridCalendarHudComponent

Compact HUD panel (a `Control`, `[Tool][GlobalClass]`) for `GridCalendarComponent`: it shows the current in-game date, an optional day-progress bar, and an optional "next day" button, so farming/settlement scenes get a working date readout without writing custom calendar UI.

Like the rest of this batch it either binds to a scene-authored `Date`/`DayProgress`/`AdvanceDay` set of children (found by `NodePath` export or by convention name) or, when `GenerateControlsWhenPathsEmpty` is set and nothing is found, builds its own `PanelContainer`/`VBoxContainer`/`Label`/`ProgressBar`/`Button` tree. Its `_Process` is deliberately narrow: the date text and button wiring only change when the calendar's `DayAdvanced`/`SeasonChanged`/`YearChanged` signals fire, so per-frame work is limited to updating the progress bar's fill — the one value ("day progress") that genuinely changes continuously. That fraction is read from [GridWorkClockComponent](GridWorkClockComponent.md), not the calendar: the calendar knows *which* day it is, the clock knows how far through it we are, and reading both from the same clock is what keeps the bar and the date from ever disagreeing.

## Public API

- `[Signal] AdvanceDayRequestedEventHandler()` — fired every time `RequestAdvanceDay()` runs, whether or not a calendar was actually resolved.
- `[Export] NodePath CalendarPath/DateLabelPath/DayProgressPath/AdvanceButtonPath` — component/control wiring.
- `[Export] bool BuildInEditor = true` — whether `RebuildHud()` runs while `Engine.IsEditorHint()` is true.
- `[Export] bool GenerateControlsWhenPathsEmpty = false` — allow generating the panel when no authored/path-referenced controls are found.
- `[Export] bool ShowProgress = true` / `bool ShowAdvanceButton = true` — visibility toggles for the progress bar and the advance button (also gate what `_GetConfigurationWarnings`/`HasAuthoredControls` require to exist).
- `[Export] string AdvanceButtonText = "Next Day"`.
- `[Export] Vector2 PanelMinimumSize = new(184, 82)`.
- `public override void _Ready()` — resolves references, defers `RebuildHud()` per `BuildInEditor`, and (runtime only) subscribes to the calendar's `DayAdvanced`/`SeasonChanged`/`YearChanged` signals.
- `public override void _ExitTree()` — unsubscribes calendar signals and the advance button.
- `public override void _Process(double delta)` — updates only the progress bar's `Value`, and only when `ShowProgress` is on and a work clock resolves; a scene with none shows no progress rather than inventing one.
- `public override string[] _GetConfigurationWarnings()` — warns if `CalendarPath` is unset or no usable label/button surface exists (given `ShowProgress`/`ShowAdvanceButton`) and generation isn't enabled.
- `public void RebuildHud()` — full teardown/rebuild of the panel, binding existing controls first and generating only as a fallback.
- `public void RefreshHud()` — repaints the date label, progress bar, and advance button from current state without rebuilding structure.
- `public bool RequestAdvanceDay()` — emits `AdvanceDayRequested` unconditionally, then calls `GridCalendarComponent.AdvanceDay()` if resolved; returns whether a calendar was actually advanced.
- `public string DateText()` — `_calendar?.DisplayDate() ?? "Date unavailable"`.
- `public float DayProgress01()` — the work clock's `DayProgress01`, or `0` with no clock.
- `public bool UsesSceneControls()` — true if any of the three `NodePath` exports are set or the conventionally-named controls can be found.

## Dependencies

- Reads `GridCalendarComponent.DisplayDate()`, `.AdvanceDay()`, and its `DayAdvanced`/`SeasonChanged`/`YearChanged` signals for the date; reads `GridWorkClockComponent.DayProgress01` for the bar.
- Uses `EntityComponent.FindComponent<GridCalendarComponent>(...)` as the scene-fallback lookup, same as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridCalendarHudComponent` — none of the other 12 UI files reference it.

## Notes

- `RequestAdvanceDay()` emits `AdvanceDayRequested` *before* checking whether `_calendar` is non-null — a listener sees the signal even on a call that then returns `false` and advances nothing. The same "signal fires regardless of outcome" shape recurs in `GridProductionPanelComponent` and `GridWorkerSpawnerPanelComponent` in this batch.
- `ConnectAdvanceButton()` tracks the single previously-wired button in `_connectedAdvanceButton` and only reconnects when the resolved `Button` instance actually changes — avoids double subscription across repeated `RefreshHud()` calls without needing an explicit "already connected" flag.
- The doc comment on `_Process` is unusually explicit about why per-frame work is minimized here; contrast with `GridProductionPanelComponent`/`GridObjectivePanelComponent`/`GridWorkerStatusPanelComponent` in this same batch, which use `AutoRefresh` + an interval accumulator to *periodically* re-poll at runtime rather than relying solely on signals — this file has no runtime polling fallback at all, only the editor-preview `_Process` path.
