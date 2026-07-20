# Phase 1 — round-4 regression fix

The round-4 regression check verified 25 of 26 edits held. The one exception was a regression in a
round-4 fix itself.

## Fix

### `BeepProjectDefaults.RemoveAutoload` — empty-string sentinel defeats the re-enable path
- `core/BeepProjectDefaults.cs:30-35` — `RemoveAutoload` did `Set(key, string.Empty)`, but
  `ProjectSettings.HasSetting("autoload/<name>")` still returns **true** for a key whose value is `""`.
  So `HasAutoload` reports the removed autoload as still present, and `EnsureAutoload` (which only adds
  when `!HasAutoload`) will **refuse to re-register** it on a later regeneration that needs it — leaving
  a permanently empty `autoload/<name>=""` entry in `project.godot`.
- This directly defeats round 4's own goal: "regenerate a genre that re-enables `TurnManager`/
  `GameStateManager`" would silently fail. Switched to `ProjectSettings.Clear(key)`, which removes the
  key so `HasSetting` returns false and the re-enable path works. Persisted by the caller's `SaveAll()`.

## Verify
- `dotnet build` → 0 errors (done — `ProjectSettings.Clear` exists in 4.7).
- Editor: generate genre A (turns) → generate genre B (real-time) → generate genre A again → confirm
  `TurnManager` is present again in `project.godot` autoloads (not stuck empty).
