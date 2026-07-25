@echo off
REM ===========================================================================
REM  Drag a Godot game folder onto this file.
REM
REM  Or just double-click it and paste the path when asked.
REM
REM  Installs the Beep addons + a private MCP server into that game, enables the
REM  plugins, turns on Claude's write permission, and tells you the two commands
REM  left to run.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set "TARGET=%~1"
if "%TARGET%"=="" (
  echo.
  echo   Beep -- install into a Godot game
  echo   --------------------------------
  echo   Tip: next time, just drag the game folder onto this file.
  echo.
  set /p "TARGET=Path to your Godot game folder: "
)

if "%TARGET%"=="" (
  echo No folder given. Nothing done.
  pause
  exit /b 1
)

where node >nul 2>&1
if errorlevel 1 (
  echo.
  echo   Node.js is required but is not on PATH.
  echo   Install Node 18+ from https://nodejs.org and run this again.
  echo.
  pause
  exit /b 1
)

node "tools\install-to-game.mjs" "%TARGET%"
echo.
pause
