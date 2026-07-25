@echo off
REM  Double-click me, or run me from a terminal.
REM  Sets this Godot game up so Claude can see and edit it.
setlocal
cd /d "%~dp0"
where node >nul 2>&1 || (
  echo.
  echo   Node.js is required. Get the LTS build from https://nodejs.org
  echo.
  pause
  exit /b 1
)
node setup.mjs %*
pause
