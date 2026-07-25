/**
 * Phase 2 MCP tools — creative authoring.
 *
 * Phase 0/1 let an agent read a scene and change a property safely. These let it MAKE
 * things: resources, themes, animations, signal wiring, scene composition. Kept in its own
 * file because tools.ts is the stable plumbing and this is the surface that will keep
 * growing.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { GodotBridge } from "./bridge.js";
import { RoleTarget } from "./protocol.js";

type ToolResult = { content: Array<{ type: "text"; text: string }>; isError?: boolean };

export function registerAuthoringTools(
  server: McpServer,
  bridge: GodotBridge,
  ok: (v: unknown) => ToolResult,
  fail: (e: unknown) => ToolResult,
): void {
  const call = (method: string, params: Record<string, unknown>, role: RoleTarget = "editor") =>
    bridge.request(method, params, role);

  const run = async (method: string, params: Record<string, unknown>, role: RoleTarget = "editor") => {
    try {
      return ok(await call(method, params, role));
    } catch (err) {
      return fail(err);
    }
  };

  // ── ClassDB: stop guessing ────────────────────────────────────────────

  server.registerTool(
    "godot_class_list",
    {
      title: "List instantiable Godot classes",
      description:
        "Instantiable classes, optionally filtered by what they inherit ('Control', 'Resource', 'StyleBox') and a name substring. Use this before node.create or resource.create rather than guessing a type name.",
      inputSchema: {
        inherits: z.string().optional().describe("e.g. 'Control', 'Resource'."),
        filter: z.string().optional().describe("Case-insensitive substring."),
      },
    },
    async ({ inherits, filter }) => run("classdb.list", { inherits, filter }, "any"),
  );

  server.registerTool(
    "godot_class_describe",
    {
      title: "Describe a Godot class",
      description:
        "Properties (with Variant types), signals and parent class. The authoritative answer to 'what can I set on this?'.",
      inputSchema: { class: z.string() },
    },
    async ({ class: cls }) => run("classdb.describe", { class: cls }, "any"),
  );

  // ── Resources ─────────────────────────────────────────────────────────

  server.registerTool(
    "godot_resource_create",
    {
      title: "Create a Resource (.tres)",
      description:
        "Build a Resource of any class, set properties, and save it. This is how you author a UISkin, a ColorPalette, a GeometryProfile or a GameInfo without the dock. " +
        "Property names are case-sensitive and a C# [Export] is PascalCase — PatchMargin, not patch_margin; the snake_case form is refused rather than silently dropped.",
      inputSchema: {
        type: z.string().describe("Resource class, e.g. 'UISkin'."),
        path: z.string().describe("res:// path ending .tres"),
        properties: z.record(z.unknown()).optional(),
      },
    },
    async ({ type, path, properties }) => run("resource.create", { type, path, properties }),
  );

  server.registerTool(
    "godot_resource_get",
    {
      title: "Read a Resource",
      description: "Load a .tres and dump its type and every property value.",
      inputSchema: { path: z.string() },
    },
    async ({ path }) => run("resource.load", { path }),
  );

  server.registerTool(
    "godot_resource_set",
    {
      title: "Edit a Resource",
      description: "Set properties on an existing .tres and re-save it. Same PascalCase rule as create.",
      inputSchema: { path: z.string(), properties: z.record(z.unknown()) },
    },
    async ({ path, properties }) => run("resource.set", { path, properties }),
  );

  // ── Themes ────────────────────────────────────────────────────────────

  server.registerTool(
    "godot_theme_create",
    {
      title: "Create a Theme",
      description:
        "Create an empty Theme resource. Note ThemePresetComponent builds Beep's own themes at runtime from the skin catalog — this is for a developer's own Theme, or for baking one to disk to inspect.",
      inputSchema: { path: z.string() },
    },
    async ({ path }) => run("theme.create", { path }),
  );

  server.registerTool(
    "godot_theme_set_stylebox",
    {
      title: "Set a Theme StyleBox",
      description:
        "Attach a StyleBox to a theme type/name (e.g. type 'Button', name 'normal'). The box is built from a spec so any StyleBox class works: { class: 'StyleBoxFlat', properties: { BgColor: {r,g,b,a}, CornerRadiusTopLeft: 8 } }.",
      inputSchema: {
        path: z.string().describe("Theme .tres"),
        type: z.string().describe("Theme type, e.g. 'Button'."),
        name: z.string().describe("Stylebox slot, e.g. 'normal' | 'hover' | 'panel'."),
        stylebox: z.object({
          class: z.string().optional().describe("Default StyleBoxFlat."),
          properties: z.record(z.unknown()).optional(),
        }),
      },
    },
    async ({ path, type, name, stylebox }) => run("theme.set_stylebox", { path, type, name, stylebox }),
  );

  server.registerTool(
    "godot_theme_set_value",
    {
      title: "Set a Theme color / font size / constant",
      description: "One call for all three: kind is 'color', 'font_size' or 'constant'.",
      inputSchema: {
        path: z.string(),
        kind: z.enum(["color", "font_size", "constant"]),
        type: z.string().describe("Theme type, e.g. 'Label'."),
        name: z.string().describe("e.g. 'font_color', 'font_size', 'separation'."),
        value: z.unknown(),
      },
    },
    async ({ path, kind, type, name, value }) => run("theme.set_value", { path, kind, type, name, value }),
  );

  server.registerTool(
    "godot_theme_add_variation",
    {
      title: "Register a Theme type variation",
      description:
        "Add a type variation (default base 'Label'). Beep registers exactly four — BeepTitle, BeepSubtitle, BeepValue, BeepCaption — and validate_scenes.sh FAILS on a scene using any other, so inventing a fifth returns a warning alongside the result.",
      inputSchema: { path: z.string(), variation: z.string(), base: z.string().optional() },
    },
    async ({ path, variation, base }) => run("theme.add_type_variation", { path, variation, base }),
  );

  // ── Animation ─────────────────────────────────────────────────────────

  server.registerTool(
    "godot_animation_create",
    {
      title: "Create an Animation",
      description: "Add a named Animation to an AnimationPlayer's default library.",
      inputSchema: {
        player_path: z.string(),
        name: z.string(),
        length: z.number().optional().describe("Seconds; default 1."),
        loop: z.boolean().optional(),
      },
    },
    async ({ player_path, name, length, loop }) =>
      run("animation.create", { player_path, name, length, loop }),
  );

  server.registerTool(
    "godot_animation_add_track",
    {
      title: "Add a keyframed value track",
      description:
        "Key a property over time. REFUSES position/scale/rotation on a Control inside a Container — the container re-sorts every layout pass and overwrites the value, so the track would silently do nothing. Use offset_transform_position / _scale / _rotation there (and set pivot_offset first for scale or rotation, since it defaults to the top-left corner).",
      inputSchema: {
        player_path: z.string(),
        name: z.string().describe("Animation name."),
        node_path: z.string().describe("Target, relative to the AnimationPlayer."),
        property: z.string().describe("e.g. 'offset_transform_scale', 'modulate'."),
        keys: z
          .array(z.object({ time: z.number(), value: z.unknown() }))
          .optional(),
      },
    },
    async ({ player_path, name, node_path, property, keys }) =>
      run("animation.add_track", { player_path, name, node_path, property, keys }),
  );

  // ── Signals ───────────────────────────────────────────────────────────

  server.registerTool(
    "godot_signal_list",
    {
      title: "List a node's signals and connections",
      description: "Every signal on a node plus what is currently connected to it.",
      inputSchema: { path: z.string() },
    },
    async ({ path }) => run("signal.list", { path }),
  );

  server.registerTool(
    "godot_signal_connect",
    {
      title: "Connect a signal",
      description:
        "Wire a signal to a method on another node, PERSISTED so it survives in the .tscn. Refuses an unknown signal, and refuses a method the target does not have — which would otherwise fire into nothing.",
      inputSchema: {
        path: z.string().describe("Emitter node."),
        signal: z.string(),
        to: z.string().describe("Receiver node."),
        method: z.string(),
      },
    },
    async ({ path, signal, to, method }) => run("signal.connect", { path, signal, to, method }),
  );

  server.registerTool(
    "godot_signal_disconnect",
    {
      title: "Disconnect a signal",
      description: "Remove a signal connection. Reports plainly when it did not exist.",
      inputSchema: { path: z.string(), signal: z.string(), to: z.string(), method: z.string() },
    },
    async ({ path, signal, to, method }) => run("signal.disconnect", { path, signal, to, method }),
  );

  // ── Scene composition ─────────────────────────────────────────────────

  server.registerTool(
    "godot_scene_instance",
    {
      title: "Instance a PackedScene",
      description:
        "Add an instance of a .tscn into the open scene. Prefer this over building a copy node-by-node: an instance keeps tracking the template's future edits, a hand-built copy does not. Undoable.",
      inputSchema: {
        scene: z.string().describe("res:// path to the .tscn"),
        parent: z.string().optional().describe("Default: scene root."),
        name: z.string().optional(),
      },
    },
    async ({ scene, parent, name }) => run("scene.instance", { scene, parent, name }),
  );

  server.registerTool(
    "godot_scene_save_as",
    {
      title: "Save the open scene to a path",
      description:
        "Pack the open scene and write it to a new .tscn. Nodes without their Owner set to the scene root are dropped by Godot — the error says so if packing fails.",
      inputSchema: { path: z.string() },
    },
    async ({ path }) => run("scene.save_as", { path }),
  );

  server.registerTool(
    "godot_node_duplicate",
    {
      title: "Duplicate a node",
      description: "Copy a node and its children in place, undoably.",
      inputSchema: { path: z.string(), new_name: z.string().optional() },
    },
    async ({ path, new_name }) => run("scene.duplicate_node", { path, new_name }),
  );

  server.registerTool(
    "godot_script_attach",
    {
      title: "Attach a script to a node",
      description:
        "Attach an existing script. C# must be COMPILED first — Godot only registers a script it has built, and the file name must equal the class name. To generate a new screen script use beep_command with beep.new_screen, which emits a shape known to build.",
      inputSchema: { path: z.string().describe("Node path."), script: z.string().describe("res:// script path.") },
    },
    async ({ path, script }) => run("script.attach", { path, script }),
  );
}
