# MCP Help

This document covers the Beep command surface layered onto the `godot_mcp` bridge.

## Bridge Model

`godot_mcp` owns transport, command dispatch, safe-write policy, screenshot/perception helpers, editor lifecycle helpers, and runtime bridge behavior. `beep_game_builder_cs` registers Beep-specific commands into the shared `McpCommandRegistry`.

The bridge supports editor and runtime roles. Write access is gated by settings:

- `godot_mcp/security/allow_editor_writes`
- `godot_mcp/security/allow_runtime_writes`
- `godot_mcp/security/allow_node_method_calls`

Keep all three disabled unless the caller intentionally needs mutation.

## Connection Settings

| Setting | Environment override | Notes |
| --- | --- | --- |
| `godot_mcp/bridge/url` | `GODOT_MCP_BRIDGE_URL` | Defaults to `ws://127.0.0.1:8789` |
| `godot_mcp/bridge/token` | `GODOT_MCP_BRIDGE_TOKEN` | Generated when absent |

The committed project file no longer contains a concrete token. If no environment variable or project setting is present, the bridge uses a process-local generated session token.

## Command Groups

### Catalog

- `beep.list_genres`: list skin catalog genres.
- `beep.list_themes`: list themes for a genre.
- `beep.list_palettes`: list palettes for a genre/theme.
- `beep.catalog`: return full catalog information.
- `beep.genre_info`: return one genre's catalog metadata.
- `beep.list_scene_templates`: list template scenes for a genre.
- `beep.list_weather_types`: list weather enum values.
- `beep.reload_catalog`: refresh catalog caches.

### Components

- `beep.list_components`: discover C# components by category/search.
- `beep.component_info`: inspect exports/signals/defaults for a component type.
- `beep.add_component`: add a C# component to the open scene. Editor write gated.

### Game Info And Generation

- `beep.get_game_info`: read exported `GameInfo` fields.
- `beep.set_game_info`: write exported `GameInfo` fields. Uses PascalCase export names.
- `beep.apply_skin`: apply a skin to the currently edited scene. Editor write gated.
- `beep.generate_project`: generate project scaffolding from selected genre/theme settings.

### Runtime Game State

- `beep.game_state`
- `beep.list_saves`
- `beep.save_game`
- `beep.load_game`
- `beep.delete_save`
- `beep.new_game`
- `beep.add_score`
- `beep.game_over`
- `beep.level_complete`
- `beep.set_level`
- `beep.get_weather`
- `beep.set_weather`
- `beep.get_time`
- `beep.set_time`
- `beep.get_settings`
- `beep.set_setting`
- `beep.list_locales`
- `beep.set_language`
- `beep.translate`

### Editor Scene Work

- `beep.list_scenes`: list `.tscn` scenes under the supplied root or default roots.
- `beep.open_scene`: open a scene in the Godot editor.
- `beep.inspect_scene`: serialize the edited scene tree.
- `beep.get_node_property`: read a node property from the edited scene.
- `beep.set_node_property`: set a node property. Rejects likely snake_case writes to C# PascalCase exports.
- `beep.add_node`: add a built-in Godot node type.
- `beep.remove_node`: remove a node after checking `NodePath` referrers.
- `beep.save_scene`: save the edited scene.
- `beep.screenshot`: return a viewport screenshot payload.
- `beep.bake_textures`: bake skin textures.
- `beep.new_screen`: generate a themed screen script and scene.

### Game UI Kit

- `beep.kit_widgets`: list kit widgets and drop-in replacements.
- `beep.kit_scene_audit`: audit a scene for controls that can receive kit drop-in scripts.
- `beep.kit_template_audit`: audit addon templates for kit conversion status.
- `beep.kit_convert_scene`: attach kit drop-in scripts to supported nodes. Defaults to dry run.

## Fixed MCP Issues

- `BeepMcpKitCommands.Unregister()` now removes the `beep.kit_*` command surface during addon shutdown.
- `BeepGameBuilderPlugin._ExitTree()` now unregisters both the main Beep command surface and kit command surface.
- The project file no longer stores a concrete bridge token.
- Optional token metadata no longer logs Godot project-setting errors during headless editor startup.
