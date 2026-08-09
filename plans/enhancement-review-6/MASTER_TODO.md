# Enhancement Review 6 Master TODO

## How This Review Was Produced

| Step | Input | Result |
| --- | --- | --- |
| Scope inventory | `addons/**/*.{cs,gd,cfg,tscn,json}` excluding docs/plans | 909 text source/config/template files |
| `beep_game_builder_cs` read pass | 357 C#, 420 JSON, 84 `.tscn`, 1 config | 307 `[GlobalClass]` declarations; no missing template/catalog resource refs |
| `beep_ui` read pass | 30 GDScript/config files | 22 preset files; registry and inspector enum compared |
| `godot_mcp` read pass | 17 C#/config files | settings, token, write gates, lifecycle, dispatch reviewed |
| Verification | `dotnet build .\Beep.Godot.csproj` | Clean build passes with 0 warnings and 0 errors |
| Runtime smoke | `godot --headless --path . --script res://tests/headless_runtime_smoke.gd` | Passes project-setting, `beep_ui`, and tween preset endpoint checks |
| Editor smoke | `tests/editor_startup_smoke.ps1` | Enables and disables all three addons without logged errors |

## Decisions

| ID | Decision | Rationale |
| --- | --- | --- |
| D1 | Count C# addon components by `[GlobalClass]` occurrences under `addons/beep_game_builder_cs/**/*.cs`. | Matches what Godot exposes as editor-addable global script classes after a successful C# build. |
| D2 | Treat old docs/plans as replaceable receipts and recreate only current docs. | User requested deletion of existing plans/documentation before new docs. |
| D3 | Preserve license files while deleting documentation. | License files are legal metadata, not review/planning documentation. |
| D4 | Apply high-confidence addon fixes immediately, then record them in this plan. | The user clarified the task is to audit and fix the Godot addon, not only document issues. |

## Tracker

| Phase | Status | Goal |
| --- | --- | --- |
| Phase 0 | Done | Exhaustive addon scan and verification baseline |
| Phase 1 | Fixed | Restore build verification by excluding generated artifacts |
| Phase 2 | Fixed | Finish `TweenComponent` preset behavior |
| Phase 3 | Fixed | Fix MCP command lifecycle leaks |
| Phase 4 | Fixed | Make `beep_ui` preset selection one-source-of-truth |
| Phase 5 | Fixed | Clean MCP token defaults and add first automated tests |
| Phase 6 | Fixed | Reduce C# nullable/member-hiding warnings without changing editor UI behavior |
| Phase 7 | Fixed | Add Godot headless runtime smoke verification |
| Phase 8 | Fixed | Add Godot headless editor startup smoke and fix MCP metadata startup error |
| Phase 9 | Fixed | Migrate Game Builder dock to Godot 4.7 `EditorDock` API |
| Phase 10 | Fixed | Strengthen tween runtime smoke from load-only to endpoint behavior checks |
| Phase 11 | Fixed | Stop Godot editor from scanning ignored `tmp/` probe renders |
| Phase 12 | Fixed | Fix binder direction/property/Variant runtime failures and harden runtime smoke logging |

## Verification Gates

| Gate | Required Result |
| --- | --- |
| Build | `dotnet build .\Beep.Godot.csproj` passes without duplicate generated assembly attributes |
| Catalog refs | JSON skin paths and template `res://` references resolve |
| MCP lifecycle | Disable/re-enable `beep_game_builder_cs` without duplicate `beep.*` or `beep.kit_*` handlers |
| UI presets | Add/remove a preset in `BeepPreset._PRESET_SCRIPTS` and inspector choices update from that registry |
| Security defaults | `security/allow_editor_writes=false` remains default; no committed fixed bridge token |
| Headless runtime smoke | `tests/run_addon_checks.ps1` completes source, build, and Godot runtime checks |
| Headless editor startup smoke | `tests/editor_startup_smoke.ps1` enables/disables all three addons without `ERROR`, `Exception`, or C# backtrace output |
| Editor dock API | `BeepGameBuilderPlugin` uses `EditorDock`, `AddDock()`, and `RemoveDock()` with no obsolete dock API calls or `CS0618` suppression |
| Generated output ignores | `tmp/.gdignore` exists while generated render files stay ignored by Git |
| Binder runtime behavior | `tests/DataBinderHostSmoke.cs` validates one-way, two-way, and one-way-to-source bindings against a C# `GameInfo` source |
| Runtime smoke error handling | `tests/runtime_smoke.ps1` fails on Godot `SCRIPT ERROR`, `ERROR:`, exceptions, or C# backtraces even when Godot returns exit code 0 |

## Findings Index

- Fixed: `Beep.Godot.csproj:8` excludes `.godot/**`, `tests/**`, `bin`, and `obj` from default item inclusion.
- Fixed: `addons/beep_game_builder_cs/ecs/TweenComponent.cs:8` no longer claims "90+" presets.
- Fixed: `addons/beep_game_builder_cs/ecs/TweenComponent.cs:14` exposes 22 enum values and all 22 now have switch cases.
- Fixed: `addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs:29` unregisters kit MCP commands during addon shutdown.
- Fixed: `addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs` uses Godot 4.7 `EditorDock` / `AddDock()` / `RemoveDock()` instead of obsolete dock APIs.
- Fixed: `addons/beep_game_builder_cs/mcp/BeepMcpKitCommands.cs:46` adds `Unregister()` for the `beep.kit_` prefix.
- `addons/beep_game_builder_cs/mcp/BeepMcpCommands.cs:110`: scene commands are registered through the main command surface.
- Fixed: `addons/beep_ui/theme/beep_theme.gd:130` remains the preset registry.
- Fixed: `addons/beep_ui/theme/theme_applier.gd:14` no longer hardcodes duplicated inspector choices and now exposes a dynamic property list from `BeepPreset.preset_names()`.
- `addons/godot_mcp/GodotMcpSettings.cs:78`: token env override.
- Fixed: `addons/godot_mcp/GodotMcpSettings.cs:128`: generated session token fallback is local-only when no env var or project setting exists.
- Fixed: `addons/godot_mcp/GodotMcpSettings.cs` no longer registers optional token metadata when the token setting is absent, avoiding Godot editor startup `AddPropertyInfo` errors.
- Fixed: `project.godot` no longer contains a committed `bridge/token` value.
- `project.godot:41`: editor writes safely disabled by default.
- Fixed: warning cleanup pass made lifecycle-created fields/events nullable or explicitly initialized in utility controls and helper classes.
- Fixed: clean `dotnet build .\Beep.Godot.csproj` now reports 0 warnings and 0 errors.
- Fixed: `tests/headless_runtime_smoke.gd` verifies Godot can load the real addon scripts, dynamic `beep_ui` preset property list, MCP security defaults, and every `TweenComponent` preset on both `Control` and `Node2D` targets with endpoint assertions for non-looping presets.

## Second Scan Fix Log

| Bug | Fix | Verification |
| --- | --- | --- |
| Root project compiled generated/test artifacts and produced duplicate assembly attributes. | Added `DefaultItemExcludes` in `Beep.Godot.csproj`. | `dotnet build .\Beep.Godot.csproj` passes. |
| `TweenComponent` advertised 22 presets but 11 fell through to a generic default branch. | Implemented `SlideOut`, `BounceOut`, `ScaleUp`, `ScaleDown`, `RotateIn`, `RotateOut`, `Flip`, `Float`, `SpriteStretch`, `TeleportIn`, and `FlipCard`; removed the default warning branch. | Static enum/case check reports 22 enum values and 22 switch cases; build passes. |
| `BeepMcpKitCommands` registered static handlers but never unregistered them. | Added `BeepMcpKitCommands.Unregister()` and called it from plugin `_ExitTree()`. | Build passes; lifecycle intent is symmetric in code. |
| Warning noise hid real addon regressions. | Cleaned nullable/member-hiding warnings across utility controls, binders, dock lifecycle fields, ECS data defaults, MCP editor access, and generated-code warning policy. | Clean build is down from 147 warnings to 0 warnings with 0 errors; contract scan still passes. |
| Runtime/editor regressions were only checked statically. | Added `tests/headless_runtime_smoke.gd`, `tests/editor_startup_smoke.ps1`, and `tests/run_addon_checks.ps1`. | Wrapper passes source contract scan, clean build, Godot headless runtime smoke, and Godot headless editor startup smoke. |
| MCP settings metadata logged a Godot editor startup error after removing the committed token. | Stopped registering project-setting metadata for the optional token key when the key is absent. | `tests/editor_startup_smoke.ps1` passes and scans logs for `ERROR`, `Exception`, and C# backtraces. |
| Game Builder dock used obsolete dock APIs behind a CS0618 suppression. | Migrated to `EditorDock`, configured title/default slot/layouts, and removed it with `RemoveDock()`. | Contract scan rejects obsolete dock APIs and the full addon check chain passes. |
| Tween runtime smoke only checked that presets could start. | Added endpoint assertions for non-looping presets on both `Control` offset transforms and `Node2D` transforms. | `godot --headless --path . --script res://tests/headless_runtime_smoke.gd` passes. |
| Godot editor scanned ignored `tmp/` probe renders and reported duplicate asset UIDs. | Added a tracked `tmp/.gdignore` and unignored only that marker in `.gitignore`. | Editor startup smoke no longer logs duplicate UID warnings; generated render files remain ignored. |
| Binder helpers used C# property names and boxed reflection values with Godot `Set()`, and `OneWayToSource` refreshed in the wrong direction. | Normalized common target property names, converted boxed values to typed Variants, and made `OneWayToSource` pull target-to-source. | `tests/DataBinderHostSmoke.cs` passes inside Godot headless runtime smoke. |
| Runtime smoke could miss logged GDScript/C# errors when Godot exited with code 0. | Added `tests/runtime_smoke.ps1` to capture stdout/stderr and fail on script/runtime error patterns. | Full `tests/run_addon_checks.ps1` passes through the new wrapper. |

## Remaining Bugs / Risks From Second Scan

- Automated coverage now includes source-level checks, clean build, Godot headless runtime smoke with binder direction and tween endpoint checks, and Godot headless editor addon startup smoke. Manual editor visual checks are still needed for dock layout persistence, MCP live bridge workflow, and tween animation feel.
- Godot editor startup still logs `Scan thread aborted...` when the headless editor is intentionally quit by the smoke test. Duplicate UID warnings from `tmp/` probe renders are fixed.
