#!/usr/bin/env node
/**
 * beep-mcp — the missing half of the Beep.Godot MCP bridge.
 *
 * Two servers in one process:
 *   • an MCP server on stdio, which is what `claude mcp add` talks to;
 *   • a WebSocket server on :8789, which the godot_mcp addon dials into.
 *
 * Start order matters: bring the WebSocket listener up FIRST so a Godot editor
 * that is already open (and retrying on its reconnect timer) attaches before the
 * agent's first tool call. Then hand stdio to MCP.
 *
 * Nothing may ever be written to stdout except MCP frames — all logging goes to
 * stderr. A stray console.log here corrupts the protocol.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { BridgeError, GodotBridge } from "./bridge.js";
import { registerTools } from "./tools.js";

const PORT = Number(process.env.BEEP_MCP_PORT ?? 8789);
const HOST = process.env.BEEP_MCP_HOST ?? "127.0.0.1";
const TOKEN = process.env.BEEP_MCP_TOKEN ?? "";
const TIMEOUT = Number(process.env.BEEP_MCP_TIMEOUT_MS ?? 15_000);
const VERBOSE = process.env.BEEP_MCP_QUIET !== "1";

async function main(): Promise<void> {
  const bridge = new GodotBridge({
    port: PORT,
    host: HOST,
    token: TOKEN || undefined,
    defaultTimeoutMs: TIMEOUT,
    verbose: VERBOSE,
  });

  try {
    await bridge.start();
  } catch (err) {
    // A port clash is the single most likely setup failure, and the message has
    // to name the fix — the agent cannot see this process's stderr otherwise.
    const msg = err instanceof BridgeError ? `[${err.code}] ${err.message}` : String(err);
    process.stderr.write(`[beep-mcp] fatal: ${msg}\n`);
    process.exit(1);
  }

  const server = new McpServer(
    { name: "beep-godot", version: "0.1.0" },
    {
      instructions:
        "Bridge to a Godot 4.7 project running the Beep game-builder addons. " +
        "Call godot_status first: Godot is often closed, and every other tool then reports NOT_CONNECTED. " +
        "Use beep_list_commands to discover the beep.* surface, then beep_command to invoke it. " +
        "Writes are refused unless godot_mcp/security/allow_editor_writes (editor) or allow_runtime_writes (running game) is enabled in Project Settings. " +
        "For UI work use the Game UI Kit rather than raw Godot controls: beep.kit_widgets lists the 32 widgets, " +
        "beep.kit_scene_audit reports what a scene still has as generic Button/PanelContainer, and " +
        "beep.kit_convert_scene attaches the kit drop-ins (dry_run defaults to true). " +
        "After ANY UI change, render the scene and look at it — a converted control that draws blank still compiles and still passes the scene validator.",
    },
  );

  registerTools(server, bridge);

  const shutdown = async () => {
    await bridge.stop().catch(() => {});
    process.exit(0);
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);

  await server.connect(new StdioServerTransport());
  process.stderr.write(`[beep-mcp] MCP stdio ready; bridge on ws://${HOST}:${PORT}\n`);
}

main().catch((err) => {
  process.stderr.write(`[beep-mcp] fatal: ${(err as Error)?.stack ?? String(err)}\n`);
  process.exit(1);
});
