# GridCalendarComponent

A `[Tool][GlobalClass]` `Node` and `ISaveable` implementer providing the in-game calendar (day/season/year) for farming and settlement-style loops. It knows **which day it is**; it does not own how time passes.

The calendar **derives** from the game clock and never runs a clock of its own. [GridWorkClockComponent](GridWorkClockComponent.md) calls `AdvanceDay` on every day boundary — from the game clock's cascade when there is one, from its own accumulated turns when the scene runs standalone — exactly as Freeciv advances the year inside `end_turn` rather than beside it. The calendar used to carry an `AutoAdvance` accumulator with its own `SecondsPerDay`, a second owner of "what day is it": enable it alongside the clock and the day advanced twice. Both are gone, and `AdvanceDay` is the only mutator.

The date model is deliberately redundant: `Year`/`Season`/`DayOfSeason` are the human-facing exported fields, while `AbsoluteDay` is a monotonic counter so other systems can key data off a single incrementing integer ("planted on absolute day 41") without season/year arithmetic. Every advance funnels through `AdvanceOneDay()`, so season/year rollover exists exactly once. `SetDate` and `RestoreState` re-emit `DayAdvanced` even though no day "passed": the HUD refreshes its date label only on that signal, so a jumped or loaded date would otherwise sit stale on screen.

## Public API
- `enum GridSeason { Spring, Summer, Fall, Winter }` — advances in this fixed order.
- `[Signal] DayAdvancedEventHandler(int day, int season, int year)` — on every day advance, on `SetDate`, and on `RestoreState`; `day` is `DayOfSeason`, not `AbsoluteDay`.
- `[Signal] SeasonChangedEventHandler(int season, int year)` — only when `DayOfSeason` rolls past `DaysPerSeason` inside `AdvanceOneDay`.
- `[Signal] YearChangedEventHandler(int year)` — only when the season rollover also wraps `Winter -> Spring`.
- `bool ParticipatesInSave` (default `true`), `string SaveKeyPrefix` (default `"grid_calendar"`) — save data lives at `"{SaveKeyPrefix}.state"`.
- `NodePath CellDataPath` — the `GridCellDataComponent` whose crops advance by day; empty resolves scene-wide.
- `int DaysPerSeason` (default `28`, range 1–120) — days before a season rolls over. `EffectiveDaysPerSeason` clamps it to at least 1.
- `int Year` / `int DayOfSeason` / `GridSeason Season` — current date; exported but externally read-only, mutated only through this component's own methods.
- `int AbsoluteDay` — monotonic day counter since game start, not exported.
- `void AdvanceDay(int days = 1)` — **the only mutator that represents time passing.** Clamped to at least 1 day; loops `AdvanceOneDay()`.
- `void SetDate(int year, GridSeason season, int dayOfSeason)` — a raw jump: clamps, recomputes `AbsoluteDay`, emits `DayAdvanced` (not `SeasonChanged`/`YearChanged`), does not tick crops.
- `Godot.Collections.Dictionary CaptureState()` — `absolute_day`, `year`, `season` (int), `day_of_season`, `days_per_season`. **No `day_clock`**: the fraction of a day in progress belongs to the game clock, which persists it once.
- `void RestoreState(Godot.Collections.Dictionary state)` — per-field fallbacks, then re-emits `DayAdvanced`. A stale `day_clock` key from an older save is ignored.
- `string DisplayDate()` — `"Year {Year}, {Season} {DayOfSeason}"`.
- `ISaveable.Save`/`Load` — no-op if `SaveKeyPrefix` is blank.

## Dependencies
- Calls `GridCellDataComponent.AdvanceDay()` once per in-game day from `AdvanceOneDay()` — the mechanism by which crops actually advance; the calendar itself has no crop logic.
- Driven by [GridWorkClockComponent](GridWorkClockComponent.md), which owns the day fraction the HUD's progress bar reads (`DayProgress01`). `GridCalendarHudComponent` reads the date from here and the fraction from the work clock.
- `GridVariantReader.Int`/`.Float` for saved-state parsing; `SaveableHelper.Group` / `ISaveable`, matching the sibling save-participation pattern.

## Notes
- No `_Process`. `tests/addon_contract_scan.ps1` forbids one here, and forbids an `AutoAdvance` or `SecondsPerDay` export and any `_dayClock` field, so the second clock cannot quietly come back.
- `SetDate` does not emit `SeasonChanged`/`YearChanged` even when the jump crosses those boundaries — only the day-by-day path does. A raw jump is not a simulated passage of time.
- `RestoreState` sets the exported `DaysPerSeason` from saved data, so a load can change the season length away from what the scene authored — state reflects what was saved.
