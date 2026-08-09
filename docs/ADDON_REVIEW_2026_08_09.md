# Addon Review 2026-08-09

## Scope

Reviewed every non-documentation text source/config/template file under:

- `addons/beep_game_builder_cs`
- `addons/beep_ui`
- `addons/godot_mcp`

Excluded from review input: existing docs and plans.

## Findings

### P0: Root build compiles generated artifacts

Fixed: `dotnet build .\Beep.Godot.csproj` now passes after `Beep.Godot.csproj` excludes generated/test artifacts from default item inclusion.

Remaining impact: fixed. The clean build now reports 0 warnings and 0 errors.

### P1: Tween presets fall into a generic fallback

Before the fix, `addons/beep_game_builder_cs/ecs/TweenComponent.cs:14` exposed 22 enum values, but the switch implemented 11 cases and sent the rest to a generic fallback.

Fixed: named presets such as `SlideOut`, `BounceOut`, `ScaleUp`, `ScaleDown`, `RotateIn`, `RotateOut`, `Flip`, `Float`, `SpriteStretch`, `TeleportIn`, and `FlipCard` now have concrete cases. The stale "90+" claim was removed.

### P1: Kit MCP commands leak across addon reloads

Before the fix, `addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs` registered `BeepMcpKitCommands`, but `_ExitTree()` only called `BeepMcpCommands.Unregister()`. `addons/beep_game_builder_cs/mcp/BeepMcpKitCommands.cs` had `Register()` with no matching `Unregister()`.

Fixed: `BeepMcpKitCommands.Unregister()` now removes the `beep.kit_` command prefix, and plugin shutdown calls it.

### P2: UI preset inspector list has two sources of truth

Fixed: `addons/beep_ui/theme/beep_theme.gd` owns the preset registry, and `addons/beep_ui/theme/theme_applier.gd` now derives the inspector enum hint from `BeepPreset.preset_names()` instead of duplicating the list in `@export_enum`.

Impact before fix: adding a preset required synchronized edits in two places. If the registry was updated but the enum was missed, the preset existed but was not selectable in the inspector.

### P2: Project settings still persist a concrete bridge token

Fixed: `addons/godot_mcp/GodotMcpSettings.cs` still supports `GODOT_MCP_BRIDGE_TOKEN`, but an absent project token now uses a process-local generated session token. `project.godot` no longer commits a concrete `bridge/token` value.

Remaining impact: fixed. Distributable project settings no longer include a fixed bridge token.

### P2: MCP project setting metadata logs editor startup errors

Fixed: `GodotMcpSettings.EnsureProjectSettings()` no longer registers project-setting metadata for the optional token key when the token is absent. A headless editor startup smoke now verifies the three addons enable and disable without logged errors.

### P2: Game Builder dock uses obsolete editor dock APIs

Fixed: `BeepGameBuilderPlugin` now creates an `EditorDock`, assigns its default slot and available layouts, adds the `BeepGameBuilderDock` as dock content, and removes it with `RemoveDock()` during shutdown. The obsolete `AddControlToDock()` and `RemoveControlFromDocks()` calls and CS0618 suppressions are gone.

### P3: Ignored probe renders are still scanned by Godot

Fixed: `tmp/.gdignore` prevents Godot from importing ignored probe/render outputs under `tmp/`, while `.gitignore` still keeps the generated images out of source control.

### P2: Binder helpers write values with invalid property names and boxed Variants

Fixed: `DataBinderHostComponent` and `BeepDataBinder` now normalize common target property names such as `Text`, `Value`, and `ButtonPressed` to Godot object property names before `Set()`/`Get()`. They also convert boxed reflection values into typed `Variant` values. `DataBinderHostComponent` now pulls `OneWayToSource` bindings target-to-source instead of pushing in the wrong direction.

## Clean Passes

- All game-builder `.tscn` `res://` resource paths resolved.
- All skin JSON files parsed successfully.
- All JSON `texture_path` and `background_image` references resolved.
- `godot_mcp` keeps `security/allow_editor_writes=false` in `project.godot:41`.
- `BeepMcpCommands.Register()` calls `RegisterSceneCommands()` at `addons/beep_game_builder_cs/mcp/BeepMcpCommands.cs:110`, so scene MCP commands are reachable.
- `BeepGameBuilderPlugin` uses Godot 4.7 `EditorDock` / `AddDock()` / `RemoveDock()` APIs.

## Verification

Command:

```powershell
dotnet build .\Beep.Godot.csproj
```

Result: passes with 0 warnings and 0 errors after the build gate and warning cleanup fixes.

Additional source-level contract check:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\addon_contract_scan.ps1
```

Result: passes.

Full automated addon check chain:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run_addon_checks.ps1
```

Result: passes source contract scan, clean C# build, Godot headless runtime smoke, and Godot headless editor addon startup smoke.
