# GridWorkClockComponent

`[Tool] [GlobalClass]` `Node`. The one node in the grid toolkit that knows what a unit of work is, and the only seam between the grid and the game clock.

**The unit is the turn, and a turn is a day.** A build that declares 5 turns takes five end-turns in a turn-based game and five in-game days in a real-time one — the same authored number, the same amount of world time, on both axes. The game clock counts beats and cascades them into days; this component divides by that cascade (`beats / BeatsPerDay`) so the grid always measures in days. Forwarding beats straight through would make "5 turns" mean five *seconds* in an RTS.

It finds the game clock **by name**, through [GridClockPorts](GridClockPorts.md), never by type — so the grid keeps depending on no global. With no clock anywhere it drives itself in real time at `SecondsPerTurn`, which is what lets a template scene open on its own and every headless probe run without standing up a game.

## Public API
- `[Signal] WorkTick(float turns)` — a unit of work happened; `1.0` is a whole turn. Every timed grid subsystem advances off this.
- `[Export] NodePath CalendarPath` — the calendar this drives; empty finds one scene-wide.
- `[Export] float SecondsPerTurn` (default `1`) — pacing in the no-clock standalone case only. A real game gets its pacing from the clock's own `BeatsPerDay`.
- `bool FollowsGameClock` — true when a game clock is driving this; false when self-ticking.
- `double ElapsedTurns` — turns since the scene started.
- `float DayProgress01` — how far through the current day, 0..1. Read from the game clock when there is one, so a progress bar and the calendar can never disagree about the same fraction.
- `void AdvanceTurns(float turns)` — steps work deliberately; how a test or a tool advances the grid without caring which axis the game is on.

## Dependencies
[GridClockPorts](GridClockPorts.md) to find and read the clock by shape; [GridCalendarComponent](GridCalendarComponent.md), whose `AdvanceDay` it calls on every day boundary. Consumed through [GridWorkClockBinding](GridWorkClockBinding.md) by `GridWorkerComponent`, `GridProductionComponent`, `GridExtractorComponent`, `GridTransportChainComponent`, `GridHaulerComponent`; read directly by `GridCalendarHudComponent` for its day-progress bar.

## Notes
- **The calendar derives from this; it never runs a clock of its own.** With a game clock, the clock's `DayAdvanced` drives `AdvanceDay` and this node does not accumulate days itself — doing both would advance the date twice. Standalone, it accumulates whole turns and advances the calendar one day per turn. Freeciv advances the year inside `end_turn` for the same reason.
- Signal `Callable`s are built once and kept: `Callable.From` wraps a fresh delegate every call, so disconnecting with a newly-built one would not match what was connected and the handler would outlive this node.
- Pinned by `tests/addon_contract_scan.ps1` as the one place the grid converts beats to turns.
