#!/usr/bin/env bash
#  Run me from a terminal: ./setup.sh
#  Sets this Godot game up so Claude can see and edit it.
set -euo pipefail
cd "$(dirname "$0")"
command -v node >/dev/null 2>&1 || { echo "Node.js is required — https://nodejs.org"; exit 1; }
node setup.mjs "$@"
