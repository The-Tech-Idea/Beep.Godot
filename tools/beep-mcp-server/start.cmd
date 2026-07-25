@echo off
REM ===========================================================================
REM  beep-mcp — start the Godot MCP server (Windows)
REM
REM  Installs and builds on first run, then starts. Safe to run repeatedly:
REM  prepare.mjs decides whether anything is actually stale.
REM
REM  This is also what Claude Code launches via .mcp.json, so every progress
REM  message goes to STDERR — stdout is the MCP protocol channel and a stray
REM  line on it corrupts the stream.
REM
REM    start.cmd            start the server
REM    start.cmd --check    install/build and verify, but do not start
REM ===========================================================================
setlocal
cd /d "%~dp0"

where node >nul 2>&1
if errorlevel 1 (
  echo [beep-mcp] Node.js is not on PATH. Install Node 18+ from https://nodejs.org 1>&2
  exit /b 1
)

node prepare.mjs
if errorlevel 1 exit /b 1

if "%~1"=="--check" (
  echo [beep-mcp] ready. Start Godot; the addon connects on its own. 1>&2
  exit /b 0
)

node dist\index.js %*
