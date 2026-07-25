/**
 * The wire protocol, mirrored from `addons/godot_mcp/`.
 *
 * Do not invent shapes here. Every type below is what
 * `GodotMcpBridgeController.HandleBridgeMessage` and `McpWebSocketClient` actually
 * send and expect. If you change one, change the C# in the same commit.
 */

/** Which Godot process a request needs. The addon connects one socket per role. */
export type Role = "editor" | "runtime";

/** A request that needs the editor cannot be served by a running game, and vice
 *  versa. "any" prefers the editor and falls back to the runtime. */
export type RoleTarget = Role | "any";

/** Server -> Godot. `GodotMcpBridgeController` reads exactly these three fields. */
export interface BridgeRequest {
  id: string;
  method: string;
  params: Record<string, unknown>;
}

/** Godot -> server. `SendOk` / `SendError` produce these. `error_type` is the C#
 *  exception type name, which is useful for telling a refused write (an
 *  InvalidOperationException we raised on purpose) from a genuine crash. */
export interface BridgeResponse {
  id: string;
  ok: boolean;
  result?: unknown;
  error?: string;
  error_type?: string;
  /** Phase 1 additions (McpBridgeException). `error`/`error_type` remain for
   *  compatibility; these are what an agent should actually act on. */
  code?: string;
  fix?: string;
  detail?: Record<string, unknown>;
}

/** Godot -> server, unprompted, immediately on socket open (`SendHelloOnce`).
 *  It arrives as a REQUEST-shaped frame with no id, which is why the frame
 *  handler must check for `method === "hello"` before treating a frame as a
 *  response. */
export interface HelloFrame {
  method: "hello";
  params: {
    token?: string;
    bridge?: string;
    version?: string;
    role?: string;
    editor_hint?: boolean;
    godot_version?: string;
  };
}

export function isHelloFrame(frame: unknown): frame is HelloFrame {
  return (
    typeof frame === "object" &&
    frame !== null &&
    (frame as { method?: unknown }).method === "hello"
  );
}

export function isBridgeResponse(frame: unknown): frame is BridgeResponse {
  if (typeof frame !== "object" || frame === null) return false;
  const f = frame as Record<string, unknown>;
  return typeof f.id === "string" && typeof f.ok === "boolean";
}

/**
 * Every method `GodotMcpBridgeController.ExecuteMethod` dispatches, with the role
 * it requires. An unknown method throws Godot-side ("Unknown MCP bridge method"),
 * so this table is the contract.
 */
export const BRIDGE_METHODS: Record<string, RoleTarget> = {
  ping: "any",
  "status.get": "any",
  "bridge.capabilities": "any",
  "bridge.batch": "editor",
  "node.set_property_safe": "editor",

  "tree.serialize": "editor",
  "scene.current": "editor",
  "editor.selection.get": "editor",
  "editor.selection.set": "editor",

  "node.get": "editor",
  "node.list_properties": "editor",
  "node.set_property": "editor",
  "node.call_method": "editor",
  "node.create": "editor",
  "node.delete": "editor",
  "node.reparent": "editor",

  "shader.attach_canvas_item": "editor",
  "shader.set_uniform": "editor",

  "tween.property": "runtime",
  "particles.create_2d": "runtime",
  "projectile.sample_arc_2d": "any",
  "sprite.move_to": "runtime",

  "runtime.pause": "runtime",
  "runtime.resume": "runtime",
  "runtime.screenshot": "runtime",
  "input.action": "runtime",

  "game.command": "any",
  "game.state": "runtime",

  "project.setting.get": "editor",
  "project.setting.set": "editor",

  // Phase 2 — authoring. All editor-side: they write .tres/.tscn or edit the open scene.
  "resource.create": "editor",
  "resource.load": "editor",
  "resource.set": "editor",
  "theme.create": "editor",
  "theme.set_stylebox": "editor",
  "theme.set_value": "editor",
  "theme.add_type_variation": "editor",
  "animation.create": "editor",
  "animation.add_track": "editor",
  "signal.list": "editor",
  "signal.connect": "editor",
  "signal.disconnect": "editor",
  "scene.instance": "editor",
  "scene.save_as": "editor",
  "scene.duplicate_node": "editor",
  "script.attach": "editor",
  "classdb.list": "any",
  "classdb.describe": "any",

  // Phase 3 — perception. view.capture works in either role (editor window vs game).
  "view.capture": "any",
  "view.layout": "editor",
  "log.tail": "any",
  "log.mark": "any",
  "scene.snapshot": "editor",
  "scene.diff": "editor",

  // Phase 4 — editor lifecycle and play control.
  "editor.rescan_filesystem": "editor",
  "editor.reload_scripts": "editor",
  "editor.save_all": "editor",
  "play.scene": "editor",
  "play.current": "editor",
  "play.stop": "editor",
  "play.state": "editor",
};

/**
 * Which role a `beep.*` command needs.
 *
 * `game.command` itself is role-agnostic at the bridge layer, but the handlers are
 * not: `beep.add_component` requires an open editor scene and `beep.add_score`
 * requires a running game. Routing on the prefix keeps an agent from getting
 * "No scene is open in the editor" when the real problem is that it asked the
 * wrong process.
 */
const RUNTIME_BEEP_COMMANDS = new Set([
  "beep.game_state",
  "beep.list_saves",
  "beep.save_game",
  "beep.load_game",
  "beep.delete_save",
  "beep.new_game",
  "beep.add_score",
  "beep.game_over",
  "beep.level_complete",
  "beep.set_level",
  "beep.get_weather",
  "beep.set_weather",
  "beep.get_time",
  "beep.set_time",
  "beep.get_settings",
  "beep.set_setting",
  "beep.set_language",
]);

const EDITOR_BEEP_COMMANDS = new Set([
  "beep.generate_project",
  "beep.apply_skin",
  "beep.add_component",
  "beep.set_game_info",
  "beep.list_scenes",
  "beep.open_scene",
  "beep.inspect_scene",
  "beep.get_node_property",
  "beep.set_node_property",
  "beep.add_node",
  "beep.remove_node",
  "beep.save_scene",
  "beep.bake_textures",
  "beep.new_screen",
]);

export function roleForBeepCommand(name: string): RoleTarget {
  if (RUNTIME_BEEP_COMMANDS.has(name)) return "runtime";
  if (EDITOR_BEEP_COMMANDS.has(name)) return "editor";
  return "any"; // catalog reads work in either process
}
