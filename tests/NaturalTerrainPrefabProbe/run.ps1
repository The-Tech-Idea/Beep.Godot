param([string]$Godot = 'H:/dev/Godot/Godot_v4.7-stable_mono_win64_console.exe', [switch]$Render)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$relative = 'addons/beep_game_builder_cs/generated/natural_rock_walls/v1'
New-Item -ItemType Directory -Force "$PSScriptRoot/$relative/small_plateaus" | Out-Null
Copy-Item "$repo/$relative/*_plateau_01_green.png" "$PSScriptRoot/$relative/"
Copy-Item "$repo/$relative/small_plateaus/*_green.png" "$PSScriptRoot/$relative/small_plateaus/"
$shaders = 'addons/beep_game_builder_cs/shaders'
New-Item -ItemType Directory -Force "$PSScriptRoot/$shaders" | Out-Null
Copy-Item "$repo/$shaders/natural_terrain_green.gdshader" "$PSScriptRoot/$shaders/"
& $Godot --headless --path $PSScriptRoot --editor --import
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed' }
dotnet build "$PSScriptRoot/NaturalTerrainPrefabProbe.csproj" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
if ($Render) { & $Godot --path $PSScriptRoot --resolution 1680x760 }
else { & $Godot --headless --path $PSScriptRoot }
if ($LASTEXITCODE -ne 0) { throw 'Probe failed' }
