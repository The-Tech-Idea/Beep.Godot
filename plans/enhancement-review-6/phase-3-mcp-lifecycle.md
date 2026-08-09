# Phase 3: MCP Lifecycle

## Why

`addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs:21` registers `BeepMcpKitCommands`, but `_ExitTree` only unregisters `BeepMcpCommands` at `BeepGameBuilderPlugin.cs:28`. `addons/beep_game_builder_cs/mcp/BeepMcpKitCommands.cs:37` has `Register()` with no matching `Unregister()`.

Status: fixed.

## Work

- Added `Unregister()` to `BeepMcpKitCommands`.
- Mirrored `addons/beep_game_builder_cs/mcp/BeepMcpCommands.cs:113` by unregistering the `beep.kit_` prefix.
- Called `BeepMcpKitCommands.Unregister()` from `BeepGameBuilderPlugin._ExitTree()`.
- Confirmed `BeepMcpCommands.Register()` still calls `RegisterSceneCommands()` at `addons/beep_game_builder_cs/mcp/BeepMcpCommands.cs:110`.

## Gotchas

- The main command prefix is `beep.`; unregistering it before `beep.kit_` may already remove kit commands if prefix matching is broad. Implement and test explicit unregister anyway so lifecycle intent is clear.
- Static registries retain state across addon reloads inside the same editor process.

## Verify

- `dotnet build .\Beep.Godot.csproj` completes with 0 errors.
- Manual editor verification is still recommended: enable addon, list commands, disable addon, list commands again, then re-enable twice.
