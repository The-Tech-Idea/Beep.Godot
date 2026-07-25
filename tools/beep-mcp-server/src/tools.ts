/**
 * MCP tools → bridge methods.
 *
 * Deliberately thin. Phase 0 adds NO Godot-side capability; it exposes what the
 * bridge already dispatches, plus discovery so an agent can find the ~40 beep.*
 * commands instead of guessing. Anything cleverer belongs in Phase 1+.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { BridgeError, GodotBridge } from "./bridge.js";
import { RoleTarget, roleForBeepCommand } from "./protocol.js";
import { registerAuthoringTools } from "./authoring.js";
import { registerPerceptionTools } from "./perception.js";

type ToolResult = {
  content: Array<{ type: "text"; text: string }>;
  isError?: boolean;
};

function ok(value: unknown): ToolResult {
  const text = typeof value === "string" ? value : JSON.stringify(value, null, 2);
  return { content: [{ type: "text", text }] };
}

/**
 * Turn a failure into something an agent can act on.
 *
 * The single most useful thing this server does is refuse to report a problem as
 * a bare stack trace. A refused write, a closed editor and a timeout each need a
 * different next step, and the agent can only take it if we say which.
 */
function fail(err: unknown): ToolResult {
  if (err instanceof BridgeError) {
    const detail = err.detail ? `\n\ndetail: ${JSON.stringify(err.detail)}` : "";
    return {
      content: [{ type: "text", text: `[${err.code}] ${err.message}${detail}` }],
      isError: true,
    };
  }
  const e = err as Error;
  return { content: [{ type: "text", text: `[UNEXPECTED] ${e?.message ?? String(err)}` }], isError: true };
}

/** Slow operations. Baking 50 themes writes 200 PNGs and then rescans the
 *  filesystem; 15s is not enough and a timeout there looks like a hang. */
const SLOW_METHOD_TIMEOUT_MS = 180_000;
const SLOW_BEEP_COMMANDS = new Set([
  "beep.bake_textures",
  "beep.generate_project",
  "beep.reload_catalog",
]);

export function registerTools(server: McpServer, bridge: GodotBridge): void {
  const call = (method: string, params: Record<string, unknown>, role: RoleTarget, timeout?: number) =>
    bridge.request(method, params, role, timeout);

  // ── status / discovery ────────────────────────────────────────────────

  server.registerTool(
    "godot_status",
    {
      title: "Godot bridge status",
      description:
        "Which Godot processes are connected (editor / runtime), their versions, and the security flags that decide whether writes are allowed. Always answers, even with Godot closed — start here when anything else reports NOT_CONNECTED.",
      inputSchema: {},
    },
    async () => {
      const local = { connected: bridge.peerInfo(), roles: bridge.connectedRoles() };
      if (bridge.connectedRoles().length === 0) {
        return ok({
          ...local,
          note: "No Godot process is connected. Open the project in Godot — the godot_mcp plugin auto-connects on load.",
        });
      }
      try {
        const remote = await call("status.get", {}, "any");
        return ok({ ...local, godot: remote });
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "beep_list_commands",
    {
      title: "List beep.* commands",
      description:
        "Every beep.* command the connected Godot registered, read live from status.get. Use this to discover the surface rather than guessing command names; pass one to beep_command.",
      inputSchema: {},
    },
    async () => {
      try {
        const status = (await call("status.get", {}, "any")) as Record<string, unknown> | null;
        // The bridge reports its registry under one of a couple of shapes
        // depending on version; take whichever is present rather than assuming.
        const commands =
          (status?.["commands"] as unknown) ??
          (status?.["command_names"] as unknown) ??
          (status?.["registry"] as unknown) ??
          null;
        if (!commands) {
          return ok({
            note: "status.get did not report a command list; call beep_command directly with a known name.",
            raw: status,
          });
        }
        return ok({ commands });
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_capabilities",
    {
      title: "Bridge capabilities",
      description:
        "Machine-readable capability block: every method this bridge dispatches, the beep.* command list, the security flags, the error-code table, and whether batch / dry_run / undo are available. Read this instead of guessing whether a method exists.",
      inputSchema: {},
    },
    async () => {
      try {
        return ok(await call("bridge.capabilities", {}, "any"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  // ── batch: many edits, one undo entry ─────────────────────────────────

  server.registerTool(
    "godot_batch",
    {
      title: "Apply many edits as one undoable action",
      description:
        "Run an ordered list of bridge operations inside ONE Godot undo entry — the user can revert the whole thing with a single Ctrl-Z. Restyling a screen is 30-60 property writes; send them together. " +
        "atomic (default true) aborts and commits nothing if any op fails, naming the failing index. " +
        "dry_run validates every op and mutates nothing — always worth doing first. " +
        "Ops are {method, params}, e.g. {method:'node.set_property', params:{path:'Margin/VBox', property:'theme_override_constants/separation', value:20}}.",
      inputSchema: {
        ops: z
          .array(
            z.object({
              method: z.string(),
              params: z.record(z.unknown()).optional(),
            }),
          )
          .describe("Ordered operations."),
        label: z.string().optional().describe("Undo-history label; shown as 'MCP: <label>'."),
        atomic: z.boolean().optional().describe("Default true — all or nothing."),
        dry_run: z.boolean().optional().describe("Validate only; change nothing."),
      },
    },
    async ({ ops, label, atomic, dry_run }) => {
      try {
        return ok(
          await call(
            "bridge.batch",
            {
              ops: ops.map((o) => ({ method: o.method, params: o.params ?? {} })),
              ...(label ? { label } : {}),
              ...(atomic === undefined ? {} : { atomic }),
              ...(dry_run === undefined ? {} : { dry_run }),
            },
            "editor",
            60_000,
          ),
        );
      } catch (err) {
        return fail(err);
      }
    },
  );

  // ── the extension point that matters: beep.* ───────────────────────────

  server.registerTool(
    "beep_command",
    {
      title: "Run a beep.* command",
      description:
        "Invoke any Beep command (beep.catalog, beep.inspect_scene, beep.bake_textures, beep.add_score, …) via the bridge's game.command. The target Godot process is chosen from the command name — editor commands go to the editor, runtime commands to the running game. Use beep_list_commands to discover names.",
      inputSchema: {
        name: z.string().describe("Command name, e.g. 'beep.inspect_scene'."),
        args: z.record(z.unknown()).optional().describe("Command arguments object."),
      },
    },
    async ({ name, args }) => {
      try {
        const role = roleForBeepCommand(name);
        const timeout = SLOW_BEEP_COMMANDS.has(name) ? SLOW_METHOD_TIMEOUT_MS : undefined;
        const result = await call("game.command", { name, args: args ?? {} }, role, timeout);
        return ok(result);
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "beep_state",
    {
      title: "Read live game state",
      description: "Read a registered Beep state value from the RUNNING game (bridge game.state).",
      inputSchema: { name: z.string().describe("State name, e.g. 'beep.game_state'.") },
    },
    async ({ name }) => {
      try {
        return ok(await call("game.state", { name }, "runtime"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  // ── scene reads (editor) ──────────────────────────────────────────────

  server.registerTool(
    "godot_scene_tree",
    {
      title: "Serialize the open scene tree",
      description: "Full node tree of the scene open in the editor: names, types, scripts, properties.",
      inputSchema: {
        max_depth: z.number().int().min(1).max(32).optional().describe("Depth limit (default 8)."),
      },
    },
    async ({ max_depth }) => {
      try {
        return ok(await call("tree.serialize", max_depth ? { max_depth } : {}, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_current_scene",
    {
      title: "Current scene",
      description: "Which scene is open in the editor, and its root node.",
      inputSchema: {},
    },
    async () => {
      try {
        return ok(await call("scene.current", {}, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_node_get",
    {
      title: "Inspect a node",
      description: "Type, script and properties of one node in the open scene.",
      inputSchema: { path: z.string().describe("Node path relative to the scene root.") },
    },
    async ({ path }) => {
      try {
        return ok(await call("node.get", { path }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_node_properties",
    {
      title: "List node properties",
      description: "Every registered property on a node, with its type — use this before setting one.",
      inputSchema: { path: z.string() },
    },
    async ({ path }) => {
      try {
        return ok(await call("node.list_properties", { path }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  // ── scene writes (editor, gated Godot-side) ───────────────────────────

  server.registerTool(
    "godot_node_set_property",
    {
      title: "Set a node property (validated, undoable)",
      description:
        "Set one property on a node in the open scene, as a Godot undo entry the user can Ctrl-Z. " +
        "Refuses an unknown property instead of silently discarding the value, and refuses a C# [Export] written snake_case — Godot drops that spelling without a word (title_label_path vs TitleLabelPath). " +
        "Pass dry_run to check without changing anything. Requires godot_mcp/security/allow_editor_writes. For several edits use godot_batch, which is one undo entry for the lot.",
      inputSchema: {
        path: z.string(),
        property: z.string(),
        value: z.unknown().describe("JSON value; converted to a Godot Variant."),
        dry_run: z.boolean().optional().describe("Validate only; change nothing."),
      },
    },
    async ({ path, property, value, dry_run }) => {
      try {
        // node.set_property_safe is the guarded, undo-backed path added in Phase 1;
        // the original node.set_property remains for callers that need its old behaviour.
        return ok(
          await call(
            "node.set_property_safe",
            { path, property, value, ...(dry_run === undefined ? {} : { dry_run }) },
            "editor",
          ),
        );
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_node_create",
    {
      title: "Create a node",
      description: "Add a node to the open scene. Requires allow_editor_writes.",
      inputSchema: {
        parent: z.string().describe("Parent node path ('.' for the scene root)."),
        type: z.string().describe("Godot class name, e.g. 'PanelContainer'."),
        name: z.string().optional(),
        dry_run: z.boolean().optional(),
      },
    },
    async ({ parent, type, name, dry_run }) => {
      try {
        return ok(await call("node.create", { parent, type, name, ...(dry_run === undefined ? {} : { dry_run }) }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_node_delete",
    {
      title: "Delete a node",
      description:
        "Remove a node from the open scene, undoably. Refuses while another node's NodePath export still points at it — that would leave those resolving to null in silence. Requires allow_editor_writes.",
      inputSchema: { path: z.string(), dry_run: z.boolean().optional() },
    },
    async ({ path, dry_run }) => {
      try {
        return ok(await call("node.delete", { path, ...(dry_run === undefined ? {} : { dry_run }) }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_node_reparent",
    {
      title: "Reparent a node",
      description: "Move a node under a new parent in the open scene. Requires allow_editor_writes.",
      inputSchema: { path: z.string(), new_parent: z.string(), dry_run: z.boolean().optional() },
    },
    async ({ path, new_parent, dry_run }) => {
      try {
        return ok(await call("node.reparent", { path, new_parent_path: new_parent, ...(dry_run === undefined ? {} : { dry_run }) }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  // ── project settings ──────────────────────────────────────────────────

  server.registerTool(
    "godot_project_setting_get",
    {
      title: "Read a project setting",
      description:
        "Read a ProjectSettings value — including the bridge's own gates, godot_mcp/security/allow_editor_writes and allow_runtime_writes.",
      inputSchema: { name: z.string() },
    },
    async ({ name }) => {
      try {
        return ok(await call("project.setting.get", { name }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_project_setting_set",
    {
      title: "Write a project setting",
      description: "Set a ProjectSettings value. Requires allow_editor_writes.",
      inputSchema: { name: z.string(), value: z.unknown() },
    },
    async ({ name, value }) => {
      try {
        return ok(await call("project.setting.set", { name, value }, "editor"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  // ── runtime ───────────────────────────────────────────────────────────

  server.registerTool(
    "godot_runtime_pause",
    {
      title: "Pause / resume the running game",
      description: "Pause or resume the RUNNING game. Requires the game to be playing (F5).",
      inputSchema: { paused: z.boolean() },
    },
    async ({ paused }) => {
      try {
        return ok(await call(paused ? "runtime.pause" : "runtime.resume", {}, "runtime"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_input_action",
    {
      title: "Send an input action",
      description: "Fire an InputMap action in the running game — e.g. 'ui_accept', 'jump'.",
      inputSchema: {
        action: z.string(),
        pressed: z.boolean().optional().describe("Default true."),
      },
    },
    async ({ action, pressed }) => {
      try {
        return ok(await call("input.action", { action, pressed: pressed ?? true }, "runtime"));
      } catch (err) {
        return fail(err);
      }
    },
  );

  // Phase 2 — resources, themes, animation, signals, scene composition, ClassDB.
  registerAuthoringTools(server, bridge, ok, fail);

  // Phase 3 — captures, layout report, logs, snapshot/diff.
  registerPerceptionTools(server, bridge, ok, fail);

  server.registerTool(
    "godot_ping",
    {
      title: "Ping Godot",
      description: "Round-trip check that the bridge is alive.",
      inputSchema: {},
    },
    async () => {
      try {
        return ok(await call("ping", {}, "any", 5_000));
      } catch (err) {
        return fail(err);
      }
    },
  );
}
