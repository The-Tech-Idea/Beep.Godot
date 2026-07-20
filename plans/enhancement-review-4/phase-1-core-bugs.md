# Phase 1 — core/ utility bugs

The `core/` utility widgets (form builder, tree, data grid, debug console, service locator) were never
touched by rounds 1–3, which stayed in `ecs/`. They hold the round's most user-facing breakage: a crash,
a widget you can't type into, and several that render wrong. Plus the one MCP-bridge runtime-save bug.

## Fixes

### 1. `BeepFormBuilder.cs` — Vector2 editor crash + submit button vanishes
- **`:118-119`** — the Vector2 spinbox `ValueChanged` does `((Vector2)GetFormValue()).Y` / `.X`, but
  `GetFormValue()` returns `_dataObject` (the whole form object), not a `Vector2` →
  **`InvalidCastException` the moment you edit either axis.** Track the live x/y in captured locals and
  build the new `Vector2` from those.
- **`:30-31,38,74`** — `_submitBtn` is created once in `_Ready`; on a second `BuildForm`, `ClearChildren`
  `QueueFree`s it, then `:74` re-`AddChild`s the *same* node still flagged for deletion → the submit
  button (and its `Pressed` handler) **disappears at frame end.** Recreate the button inside `BuildForm`.
- **`:63-64` (polish)** — the property setter's `Convert.ChangeType` sits in a bare `catch {}` that
  swallows enum/format failures silently. `PushWarning` in the catch.

### 2. `BeepTreeView.cs` — phantom root, every node one level too deep
- **`:59-64`** — `BuildTree` calls `Clear()` (which already does `base.Clear()` + `_root = CreateItem()`),
  then **again** `_root = CreateItem()` on `:61`. With a root already present the second `CreateItem`
  creates a *child*, so `_root` points at an empty phantom row and every real node is one level too deep
  (an empty row shows when `HideRoot` is false). **Delete the redundant `:61`.**

### 3. `BeepDataGrid.cs` — row striping/click render as a narrow left column
- **`:134-161`** — the row-background `ColorRect` and the invisible select `Button` are added as
  **children of the row `HBox`**, so they take column slots instead of overlaying the row. Striping paints
  a colored *left column*; the click target is one narrow column, not the whole row. Restructure the row as
  a `PanelContainer`/`Control` with the `ColorRect` + `Button` as full-rect overlays *behind* the cell HBox.
- **`:123,151,170` (polish)** — `int.Parse(HeaderFontSize/RowFontSize)` throws on a non-numeric export;
  `(int)GetProperty("Count")?.GetValue` unboxes null → NRE. `TryParse` with a default; null-guard the count.

### 4. `BeepAchievementDebug.cs` (`BeepDebugConsole`) — can't type into the console
- **`:149-154`** — `_Input` calls `AcceptEvent()` for **every** key (the call sits after the up/down
  if/else, not inside it). `_Input` runs before GUI input, so the child `LineEdit` never receives a
  keystroke — **the console is untypeable.** Move `AcceptEvent()` inside the Up/Down history branches only.
- **`:182-186` (polish)** — `Trim()` split/rejoin over the `RichTextLabel.Text` mangles BBCode. Track a
  line buffer instead of round-tripping the rendered text.

### 5. `BeepServiceLocator.cs` (`BeepGridNavigator`) — infinite loop on a ragged grid
- **`:65-73`** — `Move`'s skip-empty loop hangs on a ragged grid's trailing empty cell: at the last
  column `Clamp(_cx+1)` stays put and the cell is null, so the `while` never terminates → **freeze.**
  Bound the scan by the column count and stop at the last non-null cell.

### 6. `godot_mcp/GodotMcpBridgeController.cs` — runtime `ProjectSettings.Save()`
- **`:475`** — `ProjectSettingSet` calls `ProjectSettings.Save()` unconditionally (gated only by
  `RequireWrites()`); with `_role="runtime"` + `allow_runtime_writes`, a runtime message rewrites
  `project.godot` — exactly the "reload from disk?" prompt the rest of this file is careful to avoid.
  Guard: `if (Engine.IsEditorHint()) ProjectSettings.Save();` — the setting still applies in-memory.
  *(godot_mcp is the portable addon — this is its own bug, no Beep reference involved.)*

### 7. `BeepMcpCommands.cs:481` (polish)
- `Str` does `args[key]?.GetValue<string>()`; a non-string JSON value throws `FormatException` instead of
  a clean arg error. Coerce via `?.ToString()`.

## Verify
- `dotnet build` → 0 errors. `validate_scenes.sh` → PASS (no scene changes, but run it).
- Editor pass: drop a `BeepFormBuilder` with a Vector2 field and edit it (no crash); a `BeepTreeView` with
  `HideRoot=false` (no empty top row); type into a `BeepDebugConsole`.
