# GridPanelRow

Readonly struct describing one row a [GridListPanelComponent](GridListPanelComponent.md) renders: the key it is reused and sorted by, the text to show, the colour for its state, and hover text.

The key is what makes the panel's row diff possible — a row `Label` is created once per id and reused on every later refresh, instead of the whole list being freed and rebuilt several times a second. Each panel yields these from its own domain (a job dictionary, a worker component, a machine, an objective definition) and knows nothing about how they are rendered.

## Public API
- `GridPanelRow(string id, string text, Color color, string tooltip = "")` — an empty tooltip falls back to the id, which is what three of the four panels want.
- `string Id` — the reuse/sort key: a job id, a worker id, a machine's node path, an objective's normalized id.
- `string Text` — the rendered line.
- `Color Color` — the row's state colour, applied through `KitChrome.SetColorOverrideIfChanged`.
- `string Tooltip` — hover text; the objective panel passes the goal's description, the others let it default to the id.

## Dependencies
`Godot.Color` only. Produced by the four list panels, consumed by `GridListPanelComponent.UpdateRows`.

## Notes
- `public` rather than `internal` because it appears in the signature of a `protected` member on a public base class — a subclass outside this addon could supply rows too.
- A struct, not a class: rows are created per refresh tick, several per panel, and never outlive the call that renders them.
