# Beep.Godot

Beep.Godot is a Godot 4 addon workspace containing three addons:

- `beep_game_builder_cs`: C# game-builder addon with gameplay components, UI components, Game UI Kit controls, scene templates, skin catalogs, weather, save/load, project generation, and Beep MCP commands.
- `beep_ui`: GDScript UI theming addon with 22 presets, theme applier, widget factory, effects, toast host, and editor theme studio.
- `godot_mcp`: C# MCP bridge for Godot editor/runtime inspection, perception, safe writes, and command dispatch.

## Documentation

Fresh documentation was rebuilt from the addon source scan on 2026-08-09:

- `docs/ADDONS.md`: full addon manual.
- `docs/COMPONENT_REFERENCE.md`: C# component and UI kit reference.
- `docs/MCP_HELP.md`: MCP bridge and Beep command help.
- `docs/SKIN_AND_TEMPLATE_HELP.md`: skin catalog and scene template guide.
- `docs/BEEP_UI_HELP.md`: GDScript `beep_ui` guide.
- `docs/ADDON_REVIEW_2026_08_09.md`: review findings.
- `docs/index.html`: browser-readable HTML help.

Active fix/enhancement planning:

- `plans/enhancement-review-6/MASTER_TODO.md`

## Verified Scan Boundary

The review read all non-documentation text source/config/template files under all three addon roots:

| Addon | Files | Breakdown |
| --- | ---: | --- |
| `beep_game_builder_cs` | 862 | 357 C#, 420 JSON, 84 `.tscn`, 1 config |
| `beep_ui` | 30 | GDScript source and config |
| `godot_mcp` | 17 | C# source and config |

Measured counts:

- 909 scanned addon text files.
- 307 C# `[GlobalClass]` declarations under `addons/beep_game_builder_cs`.
- 10 skin genres.
- 50 skin themes.
- 84 game-builder `.tscn` templates.
- 41 registered Beep MCP commands.

## Current Verification Status

Clean checks:

- All game-builder template `res://` references resolved.
- All skin JSON files parsed.
- All JSON `texture_path` and `background_image` references resolved.
- Source contracts, clean C# build, Godot headless runtime smoke, and Godot headless editor addon startup smoke pass through `tests/run_addon_checks.ps1`.
- Runtime smoke scans Godot output for `SCRIPT ERROR`, `ERROR:`, exceptions, and C# backtraces so logged script failures cannot be hidden behind exit code 0.
- Runtime smoke validates `DataBinderHostComponent` one-way, two-way, and one-way-to-source directions with a compiled C# source object.
- Generated probe renders under `tmp/` are ignored by Godot through `tmp/.gdignore`, so they do not create duplicate UID noise during editor startup.

Build status:

- `dotnet build .\Beep.Godot.csproj` now passes after excluding generated/test artifacts from root compile inclusion. The clean build reports 0 warnings and 0 errors.

## Main Open Issues

- Visually runtime-check dock layout persistence, MCP live bridge workflow, and tween animation feel inside the Godot editor.
- Visual-tune the newly implemented `TweenComponent` presets in a Godot scene if the authored distances/easing need polish beyond the automated endpoint checks.
- Run `powershell -ExecutionPolicy Bypass -File .\tests\run_addon_checks.ps1` for the full automated addon check chain.
