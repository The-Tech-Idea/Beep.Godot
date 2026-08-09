# Phase 5: Token, Tests, And Enhancements

## Why

Before this phase, `addons/godot_mcp/GodotMcpSettings.cs:78` supported `GODOT_MCP_BRIDGE_TOKEN`, but `project.godot` still committed a concrete token. The addons also lacked an automated test layer that exercised the cross-addon contracts found in this review.

Status: fixed for token defaults, source contract tests, Godot headless runtime smoke, and Godot headless editor startup smoke.

## Work

- Removed the concrete `bridge/token` value from committed `project.godot`.
- Kept `project.godot` as `security/allow_editor_writes=false`.
- Changed `GodotMcpSettings` so an absent token uses a process-local generated session token instead of immediately writing a token to `project.godot`.
- Avoided optional token project-setting metadata registration when the token is absent, which prevents Godot editor startup `AddPropertyInfo` errors.
- Added `tests/addon_contract_scan.ps1` as the first automated source contract target for:
  - MCP command registration/unregistration.
  - `BeepThemeApplier` preset registry round-trip.
  - `TweenComponent` no-default coverage after Phase 2.
- Shared theming contract remains documented in the new help files; a dedicated contract spec can be added later if the API expands.

## Gotchas

- Do not replace the generated-token behavior with a hardcoded empty token if the bridge expects auth by default.
- If project settings regenerate a token in editor, ensure it is local-only or ignored.
- Tests may need a Godot-aware harness, not plain xUnit, for editor/runtime APIs.

## Verify

- Fresh checkout has no concrete token in `project.godot`.
- `GodotMcpSettings.GetToken()` still honors `GODOT_MCP_BRIDGE_TOKEN` before local/project fallback.
- Editor writes remain disabled unless explicitly opted in.
- `powershell -ExecutionPolicy Bypass -File .\tests\addon_contract_scan.ps1` runs the new source contract checks.
- `powershell -ExecutionPolicy Bypass -File .\tests\run_addon_checks.ps1` runs source, build, runtime, and editor startup checks.
