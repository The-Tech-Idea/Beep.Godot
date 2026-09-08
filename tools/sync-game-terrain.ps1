param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '../addons/beep_game_builder_cs')).Path
$game = (Resolve-Path 'C:/Users/f_ald/source/repos/The-Tech-Idea/Beep.OilandGas.Sim/src/OGSim.Game/Oilfield Days/oilfield-days').Path
$target = Join-Path $game 'addons/beep_game_builder_cs'
$backup = Join-Path $env:USERPROFILE ('.codex/migration-backups/oilfield-terrain-' + [Guid]::NewGuid().ToString('N'))
$relocations = @{}
$copies = [System.Collections.Generic.List[object]]::new()
foreach ($module in @('ecs/grid', 'ecs/terrain')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source $module) -File -Recurse) {
        if ($file.Extension -notin @('.cs', '.uid')) { continue }
        $relative = $file.FullName.Substring($source.Length + 1).Replace('\', '/')
        $copies.Add([pscustomobject]@{ Source=$file.FullName; Relative=$relative })
        if ($module -eq 'ecs/grid') {
            $old = "ecs/terrain/$($file.Name)"
            if (Test-Path -LiteralPath (Join-Path $target $old)) { $relocations[$old] = $relative }
        }
    }
}
foreach ($name in @('EntityComponent', 'GameStateManagerComponent')) {
    foreach ($suffix in @('.cs', '.cs.uid')) {
        $relative = "ecs/$name$suffix"
        $copies.Add([pscustomobject]@{ Source=(Join-Path $source $relative); Relative=$relative })
    }
}
foreach ($name in @('terrain_splat.gdshader', 'iso_water.gdshader', 'natural_terrain_green.gdshader', 'water_common.gdshaderinc')) {
    foreach ($suffix in @('', '.uid')) {
        $relative = "shaders/$name$suffix"
        $copies.Add([pscustomobject]@{ Source=(Join-Path $source $relative); Relative=$relative })
    }
}
foreach ($suffix in @('.cs', '.cs.uid')) {
    $relative = "core/GameStateData$suffix"
    $copies.Add([pscustomobject]@{ Source=(Join-Path $source $relative); Relative=$relative })
}
Write-Output "Copy $($copies.Count) module files; relocate $($relocations.Count) old paths."
if (-not $Apply) { return }

function Preserve([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($game + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path outside game checkout: $full"
    }
    if (-not (Test-Path -LiteralPath $full)) { return }
    $relative = $full.Substring($game.Length + 1)
    $saved = Join-Path $backup $relative
    if (Test-Path -LiteralPath $saved) { return }
    New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
    Copy-Item -LiteralPath $full -Destination $saved
}

foreach ($copy in $copies) {
    $destination = Join-Path $target $copy.Relative
    Preserve $destination
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $copy.Source -Destination $destination -Force
}
foreach ($old in $relocations.Keys) {
    $path = Join-Path $target $old
    Preserve $path
    Remove-Item -LiteralPath $path
}

# Update explicit resource paths, never rewrite source identifiers or compiled caches.
foreach ($folder in @('game', 'scenes', 'tests', 'addons')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $game $folder) -File -Recurse) {
        if ($file.Extension -notin @('.tscn', '.tres', '.gd', '.cs')) { continue }
        $original = [IO.File]::ReadAllText($file.FullName)
        $updated = $original
        if ($file.Extension -eq '.cs') {
            $updated = $updated.Replace('GameStateManagerComponent.Instance', 'GameApp.Instance?.Saves')
        }
        foreach ($old in $relocations.Keys) {
            if ($old.EndsWith('.uid')) { continue }
            $updated = $updated.Replace("res://addons/beep_game_builder_cs/$old", "res://addons/beep_game_builder_cs/$($relocations[$old])")
        }
        if ($updated -eq $original) { continue }
        Preserve $file.FullName
        [IO.File]::WriteAllText($file.FullName, $updated, [Text.UTF8Encoding]::new($false))
    }
}
Write-Output "Preserved replaced files at $backup"
