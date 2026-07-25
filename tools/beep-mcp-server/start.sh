#!/usr/bin/env bash
# ============================================================================
#  beep-mcp — start the Godot MCP server (macOS / Linux / Git Bash)
#
#  Installs and builds on first run, then starts. Safe to run repeatedly:
#  prepare.mjs decides whether anything is actually stale.
#
#  This is also what Claude Code launches via .mcp.json, so every progress
#  message goes to STDERR — stdout is the MCP protocol channel and a stray line
#  on it corrupts the stream.
#
#    ./start.sh            start the server
#    ./start.sh --check    install/build and verify, but do not start
# ============================================================================
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v node >/dev/null 2>&1; then
  echo "[beep-mcp] Node.js is not on PATH. Install Node 18+ from https://nodejs.org" >&2
  exit 1
fi

node prepare.mjs

if [ "${1:-}" = "--check" ]; then
  echo "[beep-mcp] ready. Start Godot; the addon connects on its own." >&2
  exit 0
fi

exec node dist/index.js "$@"
