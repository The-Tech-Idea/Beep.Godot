/**
 * Phase 4 MCP tools — autonomy.
 *
 * These are the only tools in the server that do NOT go through the Godot bridge. They
 * run processes on the host: `dotnet build`, `validate_scenes.sh`, and Godot itself.
 * That is deliberate — an agent needs them precisely when Godot is closed, broken, or
 * refusing to load the addon, which is exactly when the bridge cannot answer.
 *
 * The output is PARSED, not returned as a wall of text. "Did the texture check pass" and
 * "which file has the compile error" should be answerable without an agent regexing
 * console output.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

type ToolResult = { content: Array<{ type: "text"; text: string }>; isError?: boolean };

/** Repo root: this file lives at tools/beep-mcp-server/dist|src/. */
const PROJECT_ROOT =
  process.env.BEEP_PROJECT ?? resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

const GODOT_BIN = process.env.BEEP_GODOT_BIN ?? "";

interface RunResult {
  code: number | null;
  stdout: string;
  stderr: string;
  timedOut: boolean;
}

function run(cmd: string, args: string[], cwd: string, timeoutMs: number): Promise<RunResult> {
  return new Promise((res) => {
    const p = spawn(cmd, args, { cwd, shell: false });
    let stdout = "";
    let stderr = "";
    let timedOut = false;
    const timer = setTimeout(() => {
      timedOut = true;
      p.kill();
    }, timeoutMs);

    p.stdout.on("data", (d) => (stdout += d.toString()));
    p.stderr.on("data", (d) => (stderr += d.toString()));
    p.on("error", (e) => {
      clearTimeout(timer);
      res({ code: null, stdout, stderr: stderr + `\n[spawn failed] ${e.message}`, timedOut });
    });
    p.on("close", (code) => {
      clearTimeout(timer);
      res({ code, stdout, stderr, timedOut });
    });
  });
}

/** `Path\File.cs(12,34): error CS0117: message [proj]` → structured. */
function parseDotnetDiagnostics(output: string) {
  const re = /^(.*?)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+):\s+(.*?)(?:\s+\[.*\])?$/gm;
  const seen = new Set<string>();
  const out: Array<Record<string, unknown>> = [];
  let m: RegExpExecArray | null;
  while ((m = re.exec(output)) !== null) {
    const key = `${m[1]}:${m[2]}:${m[5]}`;
    if (seen.has(key)) continue; // dotnet prints each diagnostic once per target framework
    seen.add(key);
    out.push({ file: m[1], line: Number(m[2]), column: Number(m[3]), severity: m[4], code: m[5], message: m[6] });
  }
  return out;
}

/** validate_scenes.sh prints `--- name ---` then either `  ok` or failure lines. */
function parseValidator(stdout: string) {
  const checks: Array<{ check: string; ok: boolean; failures: string[] }> = [];
  let current: { check: string; ok: boolean; failures: string[] } | null = null;
  for (const raw of stdout.split("\n")) {
    const line = raw.replace(/\r$/, "");
    const header = line.match(/^---\s+(.*?)\s+---$/);
    if (header) {
      if (current) checks.push(current);
      current = { check: header[1], ok: true, failures: [] };
      continue;
    }
    if (!current) continue;
    if (line.trim() === "ok") continue;
    if (line.startsWith("PASS:") || line.startsWith("FAIL:")) continue;
    if (line.trim().length > 0) {
      current.ok = false;
      current.failures.push(line.trim());
    }
  }
  if (current) checks.push(current);
  return checks;
}

export function registerAutonomyTools(
  server: McpServer,
  ok: (v: unknown) => ToolResult,
  fail: (e: unknown) => ToolResult,
): void {
  server.registerTool(
    "beep_gate_build",
    {
      title: "Run dotnet build",
      description:
        "Compile the C# project and return diagnostics as DATA — file, line, column, code, message — rather than console output. Run this after any script change: Godot only registers a [GlobalClass] it has compiled, and a file that does not build takes every component in the addon down with it.",
      inputSchema: { timeout_ms: z.number().int().optional().describe("Default 300000.") },
    },
    async ({ timeout_ms }) => {
      const r = await run("dotnet", ["build"], PROJECT_ROOT, timeout_ms ?? 300_000);
      const diags = parseDotnetDiagnostics(r.stdout + "\n" + r.stderr);
      const errors = diags.filter((d) => d.severity === "error");
      return ok({
        ok: r.code === 0 && errors.length === 0,
        exit_code: r.code,
        timed_out: r.timedOut,
        error_count: errors.length,
        warning_count: diags.length - errors.length,
        errors,
        // ~148 nullable warnings are pre-existing noise in this repo; surfacing them all
        // would bury the errors that matter.
        note: "Warnings are counted, not listed — this project carries ~148 pre-existing nullable warnings.",
      });
    },
  );

  server.registerTool(
    "beep_gate_scenes",
    {
      title: "Run validate_scenes.sh",
      description:
        "The scene validator, parsed per check so you can ask 'did the texture check pass' and get a boolean. Every check in it exists because it caught a real shipped bug — snake_case [Export] names Godot silently drops, bad parent paths, duplicate button names, missing skin textures.",
      inputSchema: { timeout_ms: z.number().int().optional().describe("Default 600000.") },
    },
    async ({ timeout_ms }) => {
      const cwd = resolve(PROJECT_ROOT, "addons", "beep_game_builder_cs", "templates", "scenes");
      if (!existsSync(resolve(cwd, "validate_scenes.sh")))
        return fail(new Error(`validate_scenes.sh not found under ${cwd}`));

      const r = await run("bash", ["./validate_scenes.sh"], cwd, timeout_ms ?? 600_000);
      const checks = parseValidator(r.stdout);
      const failed = checks.filter((c) => !c.ok);
      return ok({
        ok: r.code === 0,
        exit_code: r.code,
        timed_out: r.timedOut,
        check_count: checks.length,
        failed_count: failed.length,
        failed,
        checks: checks.map((c) => ({ check: c.check, ok: c.ok })),
      });
    },
  );

  server.registerTool(
    "beep_gate_all",
    {
      title: "Run both gates",
      description:
        "dotnet build, then validate_scenes.sh — short-circuiting, because a validator run against code that does not compile tells you nothing. Note what this does NOT prove: neither gate runs the game. Compile-clean + validator-PASS means the code loads, not that it works.",
      inputSchema: {},
    },
    async () => {
      const b = await run("dotnet", ["build"], PROJECT_ROOT, 300_000);
      const diags = parseDotnetDiagnostics(b.stdout + "\n" + b.stderr);
      const errors = diags.filter((d) => d.severity === "error");
      if (b.code !== 0 || errors.length > 0)
        return ok({ ok: false, stopped_at: "build", error_count: errors.length, errors });

      const cwd = resolve(PROJECT_ROOT, "addons", "beep_game_builder_cs", "templates", "scenes");
      const v = await run("bash", ["./validate_scenes.sh"], cwd, 600_000);
      const checks = parseValidator(v.stdout);
      const failed = checks.filter((c) => !c.ok);
      return ok({
        ok: v.code === 0,
        stopped_at: v.code === 0 ? null : "scenes",
        build: { ok: true, warning_count: diags.length },
        scenes: { ok: v.code === 0, failed_count: failed.length, failed },
      });
    },
  );

  server.registerTool(
    "beep_headless_run",
    {
      title: "Run a scene headlessly and report what Godot said",
      description:
        "Launch the project in a headless Godot for N seconds, then return its warnings and errors as data. This is the only tool here that actually RUNS the game — the two gates prove the code loads, not that it works. Needs BEEP_GODOT_BIN.",
      inputSchema: {
        scene: z.string().optional().describe("res:// scene; omit for the project's main scene."),
        seconds: z.number().optional().describe("Default 8."),
        import_only: z.boolean().optional().describe("Import assets and quit — use after baking textures or adding scripts."),
      },
    },
    async ({ scene, seconds, import_only }) => {
      if (!GODOT_BIN || !existsSync(GODOT_BIN))
        return fail(
          new Error(
            `BEEP_GODOT_BIN is not set to a Godot binary${GODOT_BIN ? ` (got '${GODOT_BIN}')` : ""}. Set it to your Godot 4.7 mono executable.`,
          ),
        );

      const args = import_only
        ? ["--headless", "--path", PROJECT_ROOT, "--import"]
        : ["--headless", "--path", PROJECT_ROOT, ...(scene ? [scene] : []), "--quit-after", String(Math.max(1, Math.round((seconds ?? 8) * 60)))];

      const r = await run(GODOT_BIN, args, PROJECT_ROOT, ((seconds ?? 8) + 120) * 1000);
      const text = r.stdout + "\n" + r.stderr;
      const lines = text.split("\n").map((l) => l.replace(/\r$/, ""));
      const errors = lines.filter((l) => /^(ERROR|SCRIPT ERROR|USER ERROR)/.test(l));
      const warnings = lines.filter((l) => /^(WARNING|USER WARNING)/.test(l));

      return ok({
        ok: r.code === 0 && errors.length === 0,
        exit_code: r.code,
        timed_out: r.timedOut,
        mode: import_only ? "import" : "run",
        scene: scene ?? "(project main scene)",
        error_count: errors.length,
        warning_count: warnings.length,
        errors: errors.slice(0, 40),
        warnings: warnings.slice(0, 40),
      });
    },
  );
}
