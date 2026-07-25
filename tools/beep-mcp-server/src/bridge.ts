/**
 * The half of the MCP bridge that never existed.
 *
 * `addons/godot_mcp/McpWebSocketClient` is a WebSocket CLIENT — it dials OUT to
 * ws://127.0.0.1:8789/{role}?token=… and retries forever. Nothing listened. This
 * is the listener: one WebSocket server, one socket per Godot role, and
 * request/response correlation over the {id, method, params} envelope.
 */
import { WebSocketServer, WebSocket } from "ws";
import { randomUUID } from "node:crypto";
import {
  BridgeRequest,
  BridgeResponse,
  Role,
  RoleTarget,
  isBridgeResponse,
  isHelloFrame,
} from "./protocol.js";

export interface GodotPeer {
  socket: WebSocket;
  role: Role;
  bridge?: string;
  version?: string;
  godotVersion?: string;
  editorHint?: boolean;
  connectedAt: number;
}

interface Pending {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
  timer: NodeJS.Timeout;
  method: string;
}

/** Raised when a request cannot even be sent. Carries a `code` so the MCP layer
 *  can turn it into an actionable message instead of a stack trace. */
export class BridgeError extends Error {
  constructor(
    message: string,
    readonly code: string,
    readonly detail?: Record<string, unknown>,
  ) {
    super(message);
    this.name = "BridgeError";
  }
}

export interface BridgeOptions {
  port: number;
  host: string;
  token?: string;
  defaultTimeoutMs: number;
  /** Log to stderr. stdout is the MCP stdio channel and must carry nothing else. */
  verbose: boolean;
}

export class GodotBridge {
  private wss?: WebSocketServer;
  private readonly peers = new Map<Role, GodotPeer>();
  private readonly pending = new Map<string, Pending>();

  constructor(private readonly opts: BridgeOptions) {}

  start(): Promise<void> {
    return new Promise((resolve, reject) => {
      const wss = new WebSocketServer({ host: this.opts.host, port: this.opts.port });
      this.wss = wss;

      wss.on("listening", () => {
        this.log(`listening on ws://${this.opts.host}:${this.opts.port}`);
        if (!this.opts.token) {
          this.log(
            "no BEEP_MCP_TOKEN set — accepting unauthenticated connections on loopback only",
          );
        }
        resolve();
      });

      wss.on("error", (err: NodeJS.ErrnoException) => {
        if (err.code === "EADDRINUSE") {
          reject(
            new BridgeError(
              `Port ${this.opts.port} is already in use. Another beep-mcp server is probably running; stop it, or set BEEP_MCP_PORT and godot_mcp/bridge/url to match.`,
              "PORT_IN_USE",
              { port: this.opts.port },
            ),
          );
          return;
        }
        reject(err);
      });

      wss.on("connection", (socket, req) => this.onConnection(socket, req.url ?? "/"));
    });
  }

  async stop(): Promise<void> {
    for (const [, p] of this.peers) p.socket.close();
    this.peers.clear();
    await new Promise<void>((r) => (this.wss ? this.wss.close(() => r()) : r()));
  }

  // ── connection lifecycle ────────────────────────────────────────────────

  private onConnection(socket: WebSocket, url: string): void {
    // The addon connects to ws://host:port/{role}?token=X — path-based because
    // Godot 4's WebSocketPeer rejects query params on connect, so the role is a
    // path segment (see BuildUrlWithToken).
    const parsed = new URL(url, "http://localhost");
    const rawRole = decodeURIComponent(parsed.pathname.replace(/^\//, "")) || "editor";
    const token = parsed.searchParams.get("token") ?? "";

    if (this.opts.token && token !== this.opts.token) {
      this.log(`rejected connection for role '${rawRole}': bad token`);
      socket.close(1008, "bad token");
      return;
    }

    const role: Role = rawRole === "runtime" ? "runtime" : "editor";

    // A second connection for a role replaces the first. Godot reconnects on a
    // timer after a crash or a scene reload, and the stale socket would otherwise
    // keep receiving requests that never get answered.
    const existing = this.peers.get(role);
    if (existing) {
      this.log(`role '${role}' reconnected — dropping the previous socket`);
      existing.socket.terminate();
    }

    const peer: GodotPeer = { socket, role, connectedAt: Date.now() };
    this.peers.set(role, peer);
    this.log(`role '${role}' connected`);

    socket.on("message", (data) => this.onMessage(peer, data.toString()));
    socket.on("close", () => this.onClose(peer));
    socket.on("error", (err) => this.log(`socket error (${role}): ${err.message}`));
  }

  private onClose(peer: GodotPeer): void {
    if (this.peers.get(peer.role) === peer) this.peers.delete(peer.role);
    this.log(`role '${peer.role}' disconnected`);

    // Fail every in-flight request rather than letting the agent sit until the
    // timeout. "Godot closed" is a far better answer than 15s of nothing.
    for (const [id, p] of [...this.pending]) {
      this.pending.delete(id);
      clearTimeout(p.timer);
      p.reject(
        new BridgeError(
          `Godot (${peer.role}) disconnected while '${p.method}' was in flight.`,
          "DISCONNECTED_MID_REQUEST",
          { role: peer.role, method: p.method },
        ),
      );
    }
  }

  private onMessage(peer: GodotPeer, raw: string): void {
    let frame: unknown;
    try {
      frame = JSON.parse(raw);
    } catch {
      this.log(`unparseable frame from '${peer.role}': ${raw.slice(0, 200)}`);
      return;
    }

    // hello arrives unprompted and has no id — check it before response handling.
    if (isHelloFrame(frame)) {
      peer.bridge = frame.params?.bridge;
      peer.version = frame.params?.version;
      peer.godotVersion = frame.params?.godot_version;
      peer.editorHint = frame.params?.editor_hint;
      this.log(
        `hello from '${peer.role}': ${peer.bridge ?? "?"} v${peer.version ?? "?"} (Godot ${peer.godotVersion ?? "?"})`,
      );
      return;
    }

    if (!isBridgeResponse(frame)) {
      this.log(`ignoring non-response frame from '${peer.role}'`);
      return;
    }

    const p = this.pending.get(frame.id);
    if (!p) {
      // A late reply after a timeout. Worth a line — a pattern of these means
      // defaultTimeoutMs is too tight for what the agent is asking for.
      this.log(`late/unknown response id=${frame.id}`);
      return;
    }
    this.pending.delete(frame.id);
    clearTimeout(p.timer);

    if (frame.ok) {
      p.resolve(frame.result ?? null);
    } else {
      p.reject(
        new BridgeError(
          frame.error ?? "Godot reported an error with no message.",
          frame.error_type ?? "GodotError",
          { method: p.method },
        ),
      );
    }
  }

  // ── requests ────────────────────────────────────────────────────────────

  peerFor(target: RoleTarget): GodotPeer | undefined {
    if (target === "any") return this.peers.get("editor") ?? this.peers.get("runtime");
    return this.peers.get(target);
  }

  connectedRoles(): Role[] {
    return [...this.peers.keys()];
  }

  peerInfo(): Record<string, unknown> {
    const out: Record<string, unknown> = { editor: false, runtime: false };
    for (const [role, p] of this.peers) {
      out[role] = {
        bridge: p.bridge ?? null,
        version: p.version ?? null,
        godot_version: p.godotVersion ?? null,
        editor_hint: p.editorHint ?? null,
        connected_seconds: Math.round((Date.now() - p.connectedAt) / 1000),
      };
    }
    return out;
  }

  /** Send a request and await Godot's reply. Rejects with a BridgeError carrying a
   *  code — never a bare timeout with no explanation. */
  request(
    method: string,
    params: Record<string, unknown>,
    target: RoleTarget,
    timeoutMs?: number,
  ): Promise<unknown> {
    const peer = this.peerFor(target);
    if (!peer) {
      throw new BridgeError(this.notConnectedMessage(target), "NOT_CONNECTED", {
        needed: target,
        connected: this.connectedRoles(),
      });
    }

    const id = randomUUID();
    const req: BridgeRequest = { id, method, params };
    const wait = timeoutMs ?? this.opts.defaultTimeoutMs;

    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(
          new BridgeError(
            `Godot did not answer '${method}' within ${wait}ms. It may be busy (a large bake or import), or the request may have been dropped.`,
            "TIMEOUT",
            { method, timeout_ms: wait },
          ),
        );
      }, wait);

      this.pending.set(id, { resolve, reject, timer, method });

      try {
        peer.socket.send(JSON.stringify(req));
      } catch (err) {
        this.pending.delete(id);
        clearTimeout(timer);
        reject(
          new BridgeError(
            `Failed to send '${method}' to Godot: ${(err as Error).message}`,
            "SEND_FAILED",
            { method },
          ),
        );
      }
    });
  }

  /** The message an agent sees most often, so it has to say what to DO. */
  private notConnectedMessage(target: RoleTarget): string {
    const connected = this.connectedRoles();
    const have = connected.length ? `Connected: ${connected.join(", ")}.` : "Nothing is connected.";
    if (target === "runtime") {
      return `This needs the running game, but no Godot runtime is connected. ${have} Press Play (F5) in the Godot editor.`;
    }
    if (target === "editor") {
      return `This needs the Godot editor, but it is not connected. ${have} Open the project in Godot — the godot_mcp plugin auto-connects on load.`;
    }
    return `No Godot process is connected. ${have} Open the project in Godot (the godot_mcp plugin auto-connects), and check that godot_mcp/bridge/url is ws://127.0.0.1:${this.opts.port}.`;
  }

  private log(msg: string): void {
    // stderr only: stdout is the MCP stdio transport.
    if (this.opts.verbose) process.stderr.write(`[beep-mcp] ${msg}\n`);
  }
}
