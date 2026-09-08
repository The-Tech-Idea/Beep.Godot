# GridClockPorts

Internal static helper: the duck-typed lookup for the game clock, so the grid toolkit can be driven by it **without depending on it**.

The grid references no global anywhere — no `GameApp`, no autoload path, no singleton — and that is worth keeping: it is why a grid scene can be opened on its own, and why every headless probe in `tests/` runs without standing up a game. So the clock is found and read **by name**, exactly the way [GridPorts](GridPorts.md) reads a load/unload port and [GridConstructionVisualPorts](GridConstructionVisualPorts.md) reads a construction visual. A GDScript clock of the same shape works as well as the shipped C# one.

The shape: an `Advanced(beats)` signal, a `DayAdvanced(day)` signal, a `BeatsPerDay` property, and a `DayProgress01` property.

## Public API
- `const string AdvancedSignal`, `DayAdvancedSignal` — the signal names the grid connects to.
- `static readonly StringName DayProgressProperty` — the clock's day fraction, read by name for a progress bar.
- `static bool AnswersClockShape(Node? node)` — whether a node has both signals and the `BeatsPerDay` property.
- `static double BeatsPerDay(Node clock)` — the cascade divisor as the clock reports it. A clock answering a nonsense value is treated as `1`, never as `0`.
- `static Node? FindClock(SceneTree? tree)` — the game clock, or null. Searches the root's children and *their* children — deliberately shallow, because autoloads are direct children of the root and own their subsystems — and never walks the game's own scene.

## Dependencies
`Godot.Node`, `HasSignal`, `Get`. No dependency on `GameClock` or `GameApp` by type — that is the point. Consumed only by [GridWorkClockComponent](GridWorkClockComponent.md).

## Notes
- `internal static class` — a pure helper, like `GridPorts`; invisible to the editor and to GDScript callers.
- This is a *second* port for the same clock shape alongside the game-level clock itself, and that is deliberate rather than duplication: the alternative is the grid importing a game type, which is the coupling every other grid port exists to avoid.
