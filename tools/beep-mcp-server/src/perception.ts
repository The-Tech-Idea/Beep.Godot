/**
 * Phase 3 MCP tools — perception.
 *
 * Captures are returned as MCP **image content**, not a base64 string inside text. That
 * distinction is the whole point: an agent can look at an image block; a wall of base64 in
 * a text block is just tokens it cannot see.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { GodotBridge } from "./bridge.js";
import { RoleTarget } from "./protocol.js";

type ToolResult = {
  content: Array<
    | { type: "text"; text: string }
    | { type: "image"; data: string; mimeType: string }
  >;
  isError?: boolean;
};

interface CaptureResult {
  base64?: string;
  width?: number;
  height?: number;
  [k: string]: unknown;
}

export function registerPerceptionTools(
  server: McpServer,
  bridge: GodotBridge,
  ok: (v: unknown) => { content: Array<{ type: "text"; text: string }>; isError?: boolean },
  fail: (e: unknown) => { content: Array<{ type: "text"; text: string }>; isError?: boolean },
): void {
  const call = (method: string, params: Record<string, unknown>, role: RoleTarget = "editor") =>
    bridge.request(method, params, role, 30_000);

  server.registerTool(
    "godot_capture",
    {
      title: "Screenshot the viewport or one control",
      description:
        "PNG of what is on screen, returned as an image you can actually look at. " +
        "target 'node' crops to a Control's rect — use it to inspect one header or panel instead of the whole screen; it is also far cheaper. " +
        "A zero-size control is reported as an error rather than an empty image, because that is usually the defect itself. " +
        "role 'editor' captures the editor window (no need to run the game); 'runtime' captures the running game.",
      inputSchema: {
        target: z.enum(["viewport", "node"]).optional().describe("Default viewport."),
        node: z.string().optional().describe("Required when target='node'."),
        role: z.enum(["editor", "runtime"]).optional().describe("Default editor."),
        max_width: z.number().int().optional().describe("Downscale cap; default 1280."),
      },
    },
    async ({ target, node, role, max_width }): Promise<ToolResult> => {
      try {
        const res = (await call(
          "view.capture",
          { target: target ?? "viewport", node, max_width },
          role ?? "editor",
        )) as CaptureResult;

        if (!res?.base64) return ok(res);
        const { base64, ...meta } = res;
        return {
          content: [
            { type: "image", data: base64, mimeType: "image/png" },
            { type: "text", text: JSON.stringify(meta, null, 2) },
          ],
        };
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_layout",
    {
      title: "Layout report for a Control subtree",
      description:
        "Rects, min sizes, size flags and visibility for every Control under a node — the numbers a screenshot cannot give you. " +
        "Flags the three faults that have actually shipped here: ZERO_HEIGHT (a button sized (120, 0) is invisible and unclickable), ZERO_WIDTH, and OVERFLOWS_PARENT. Check 'problems' first.",
      inputSchema: {
        node: z.string().optional().describe("Default: scene root."),
        recursive: z.boolean().optional().describe("Default true."),
      },
    },
    async ({ node, recursive }) => {
      try {
        return ok(await call("view.layout", { node, recursive }));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_log_tail",
    {
      title: "Read Godot's log",
      description:
        "Godot's own warnings and errors. This framework uses GD.PushWarning for everything that would otherwise fail silently — after a bake, a theme apply or a scene load, READ THIS instead of assuming success. " +
        "Pass since_line (from godot_log_mark) to see only what happened after a point.",
      inputSchema: {
        level: z.enum(["all", "warning", "error"]).optional(),
        limit: z.number().int().optional().describe("Most recent N; default 100."),
        since_line: z.number().int().optional(),
      },
    },
    async ({ level, limit, since_line }) => {
      try {
        return ok(await call("log.tail", { level, limit, since_line }));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_log_mark",
    {
      title: "Mark the current end of the log",
      description:
        "Returns the current log line count. Pass it to godot_log_tail as since_line to see only what a subsequent action produced. (There is deliberately no log.clear — the log file is Godot's own and truncating it to make a read convenient would be destructive.)",
      inputSchema: {},
    },
    async () => {
      try {
        return ok(await call("log.mark", {}));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_scene_snapshot",
    {
      title: "Snapshot the open scene",
      description:
        "Record the scene's shape under a label, so a later godot_scene_diff can answer 'did only what I intended change?'. Take one before a batch.",
      inputSchema: { label: z.string().optional().describe("Default 'default'.") },
    },
    async ({ label }) => {
      try {
        return ok(await call("scene.snapshot", { label }));
      } catch (err) {
        return fail(err);
      }
    },
  );

  server.registerTool(
    "godot_scene_diff",
    {
      title: "Diff the scene against a snapshot",
      description:
        "Added / removed / changed nodes since a snapshot (or between two snapshots). Compares type, name and Control geometry — where layout bugs live — rather than every property, so the change that matters is not buried.",
      inputSchema: {
        from: z.string().optional().describe("Snapshot label; default 'default'."),
        to: z.string().optional().describe("Second snapshot; omit to compare against the scene now."),
      },
    },
    async ({ from, to }) => {
      try {
        return ok(await call("scene.diff", { from, to }));
      } catch (err) {
        return fail(err);
      }
    },
  );
}
