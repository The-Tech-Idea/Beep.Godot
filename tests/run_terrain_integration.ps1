param(
    [string]$GodotCommand = "godot",
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 120,
    [switch]$SkipRendering
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outputDirectory = Join-Path $PSScriptRoot "output/terrain_integration"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$godot = (Get-Command $GodotCommand -ErrorAction Stop).Source
$previousBridge = $env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME
$results = [System.Collections.Generic.List[object]]::new()
$probes = [ordered]@{
    grid_terrain_fill_probe = "[grid-terrain-fill] OK"
    terrain_exact_recipe_probe = "[terrain-exact-recipe] OK"
    terrain_world_ownership_probe = "[terrain-world-ownership] OK"
    terrain_world_recipe_probe = "RESULT: all checks passed"
    terrain_world_live_source_probe = "[terrain-world-live] OK"
    terrain_live_cells_probe = "[terrain-live] OK"
    terrain_view_grid_probe = "[terrain-view-grid] OK"
    terrain_surface_height_probe = "[surface-height] OK"
    terrain_painted_origin_probe = "[terrain-painted-origin] OK"
    terrain_repaint_profile = "[terrain-repaint-profile] OK"
    terrain_generated_coast_probe = "[terrain-generated-coast] OK"
    terrain_beach_footprint_probe = "[terrain-beach-footprint] OK"
    terrain_coastal_grass_probe = "[terrain-coastal-grass] OK"
    terrain_lake_bank_probe = "[terrain-lake-bank] OK"
    terrain_shoreline_contour_probe = "[shoreline-contours] CPU OK"
    terrain_final_topology_probe = "[terrain-final-topology] OK"
    terrain_ground_cover_probe = "[terrain-ground-cover] OK"
    terrain_prop_sizing_probe = "[terrain-prop-sizing] OK"
    terrain_meadow_texture_probe = "[terrain-meadow-texture] OK"
    terrain_bedrock_texture_probe = "[terrain-bedrock-texture] OK"
    terrain_lava_material_probe = "[terrain-lava-material] OK"
    terrain_water_surface_probe = "[terrain-water-surface] OK"
    terrain_iso_shutdown_probe = "[terrain-iso-shutdown] OK"
    grid_terrain_topology_probe = "[grid-terrain-topology] OK"
    terrain_live_coast_shape_probe = "[terrain-live-coast-shape] OK"
    terrain_feature_grid_probe = "[terrain-feature-grid] OK"
    terrain_data_origin_probe = "[terrain-data-origin] OK"
    terrain_start_area_probe = "[terrain-start-area] OK"
    terrain_start_area_play_probe = "[terrain-start-area-play] OK"
    terrain_spawn_markers_probe = "[terrain-spawn-markers] OK"
    terrain_faction_assignment_probe = "[terrain-faction-assignment] OK"
    terrain_navigation_binding_probe = "[terrain-navigation-binding] OK"
    terrain_navigation_height_probe = "[terrain-navigation-height] OK"
    terrain_follower_live_probe = "[terrain-follower-live] OK"
    terrain_worker_arrival_probe = "[terrain-worker-arrival] OK"
    terrain_job_preflight_probe = "[terrain-job-preflight] OK"
    terrain_placement_live_probe = "[terrain-placement-live] OK"
    terrain_build_approach_probe = "[terrain-build-approach] OK"
    terrain_collision_probe = "[terrain-collision] OK"
    terrain_live_relief_probe = "[terrain-live-relief] OK"
    terrain_live_resource_view_probe = "[terrain-live-resource-view] OK"
    terrain_survey_overlay_probe = "[terrain-survey-overlay] OK"
    terrain_lab_grid_probe = "[terrain-lab-grid] OK"
    terrain_view_parity_probe = "[terrain-view-parity] OK"
    terrain_grid_playground_probe = "[terrain-grid-playground] OK"
}

function Invoke-TerrainProbe([string]$Name, [string]$Script, [string]$Marker, [bool]$Render, [bool]$Capture,
    [string]$RenderingMethod = "gl_compatibility", [int]$QuitAfter = 3600, [int]$ProbeTimeoutSeconds = $TimeoutSeconds) {
    $stdout = Join-Path $outputDirectory "$Name.stdout.log"
    $stderr = Join-Path $outputDirectory "$Name.stderr.log"
    $arguments = @("--path", "`"$root`"", "--script", "res://tests/$Script.gd")
    if ($QuitAfter -gt 0) { $arguments += @("--quit-after", "$QuitAfter") }
    if ($Render) {
        # An empty method leaves the project's own renderer, which is what F6 uses.
        if ($RenderingMethod) { $arguments += @("--rendering-method", $RenderingMethod) }
        $arguments += @("--resolution", "1280x800")
    }
    else { $arguments += "--headless" }
    if ($Capture) { $arguments += @("--", "--capture") }
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $godot -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    # Keep the native handle open so Windows PowerShell can read exit status after termination.
    $null = $process.Handle
    $timedOut = -not $process.WaitForExit($ProbeTimeoutSeconds * 1000)
    if ($timedOut) {
        # Kill the whole tree. $godot is often a launcher (a .cmd shim on PATH): Start-Process
        # hands back the launcher, and killing it alone leaves the engine it started running -
        # and competing with every probe after it. taskkill /T works under Windows PowerShell and
        # PowerShell 7 alike, where Process.Kill(true) exists only in the latter.
        & taskkill /PID $process.Id /T /F 2>&1 | Out-Null
        $process.WaitForExit()
    }
    $exitCode = $process.ExitCode
    $text = (Get-Content -LiteralPath $stdout -Raw) + "`n" + (Get-Content -LiteralPath $stderr -Raw)
    $passed = -not $timedOut -and $exitCode -eq 0 -and
        $text -match ("(?m)^" + [regex]::Escape($Marker) + "\r?$") -and
        $text -notmatch "(?m)SCRIPT ERROR:|^ERROR:|\bFAILED\b|FAIL:"
    $status = if ($passed) { "passed" } else { "failed" }
    $results.Add([pscustomobject]@{name=$Name; status=$status; seconds=[Math]::Round($timer.Elapsed.TotalSeconds, 2); exitCode=$exitCode; timedOut=$timedOut; rendering=$Render; stdout=$stdout; stderr=$stderr})
    Write-Host "[terrain-integration] $Name : $status"
    $process.Dispose()
}

try {
    $env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME = "false"
    & dotnet build (Join-Path $root "Beep.Godot.csproj") --no-restore -v:q
    if ($LASTEXITCODE -ne 0) { throw "Terrain integration build failed." }
    foreach ($probe in $probes.GetEnumerator()) {
        Invoke-TerrainProbe $probe.Key $probe.Key $probe.Value $false $false
    }
    Invoke-TerrainProbe "iso_layers" "examples/iso_layers" "PASS: sea -> ground -> hills -> peaks -> props by level" $false $false
    Invoke-TerrainProbe "landmass" "examples/landmass" "RESULT: all checks passed" $false $false
    # The last column is the frame cap. 0 marks a probe that waits on the terrain lab's world build
    # (tests/terrain_lab_build.gd): that build runs on a worker and takes wall-clock seconds, while
    # --quit-after counts frames, and a rendered probe can run through 3600 of them before a Tiny
    # world has published - the process then quits with no marker and no error. Those are bounded
    # by time instead, long enough for the lab wait (two minutes) to give up first and say why. A
    # failed assert does not end a probe, so a failing one of these runs to that bound.
    foreach ($render in @(
        @("shader_alignment", "terrain_shader_alignment_probe", "[terrain-shader-alignment] OK", $false, 3600),
        @("art_styles", "terrain_art_styles_probe", "[terrain-art-styles] OK", $false, 0),
        @("lab_styles", "terrain_lab_styles_probe", "[terrain-lab-styles] OK", $false, 0),
        @("lake_banks", "terrain_lake_bank_probe", "[terrain-lake-bank] OK", $false, 3600),
        @("style_beach", "terrain_style_beach_probe", "[terrain-style-beach] OK", $false, 0),
        @("shoreline_contours", "terrain_shoreline_contour_probe", "[shoreline-contours] GPU threshold, fixed coast, width controls and fixture captures OK", $false, 3600),
        @("water_alpha", "terrain_water_alpha_probe", "[terrain-water-alpha] OK", $false, 3600),
        @("item_modulate", "terrain_item_modulate_probe", "[terrain-item-modulate] OK", $false, 3600),
        @("painted_blend", "terrain_painted_blend_probe", "[terrain-painted-blend] OK", $false, 3600),
        @("painted_shading", "terrain_painted_shading_probe", "[terrain-painted-shading] OK", $false, 3600),
        @("material_scale", "terrain_material_scale_probe", "[terrain-material-scale] OK", $false, 3600),
        @("material_origin", "terrain_material_origin_probe", "[terrain-material-origin] OK", $false, 3600),
        @("lava_material", "terrain_lava_material_probe", "[terrain-lava-material] OK", $false, 3600),
        @("bedrock_repeat", "terrain_bedrock_texture_probe", "[terrain-bedrock-texture] OK", $false, 3600),
        @("coast_centres", "terrain_live_coast_shape_probe", "[terrain-live-coast-shape] OK", $false, 3600),
        @("coast_filter", "terrain_coast_filter_probe", "[terrain-coast-filter] OK", $false, 3600),
        @("generated_coast_centres", "terrain_water_surface_probe", "[terrain-water-surface] OK", $false, 3600),
        @("iso_water_origin", "terrain_iso_water_origin_probe", "[terrain-iso-water-origin] OK", $false, 0),
        @("iso_art", "terrain_iso_art_probe", "[terrain-iso-art] OK", $false, 0),
        @("iso_river", "terrain_iso_river_probe", "[terrain-iso-river] OK", $false, 3600),
        @("iso_cliff", "terrain_iso_cliff_probe", "[terrain-iso-cliff] OK", $false, 3600),
        @("lab_capture", "terrain_lab_grid_probe", "[terrain-lab-grid] OK", $true, 0),
        @("playground_capture", "terrain_grid_playground_probe", "[terrain-grid-playground] OK", $true, 3600)
    )) {
        if ($SkipRendering) { $results.Add([pscustomobject]@{name=$render[0]; status="skipped"; rendering=$true}) }
        elseif ($render[4] -eq 0) { Invoke-TerrainProbe $render[0] $render[1] $render[2] $true $render[3] -QuitAfter 0 -ProbeTimeoutSeconds 180 }
        else { Invoke-TerrainProbe $render[0] $render[1] $render[2] $true $render[3] -QuitAfter $render[4] }
    }
    # The lab's tile views as F6 runs them: project renderer, a dozen generations, no frame cap.
    if ($SkipRendering) { $results.Add([pscustomobject]@{name="lab_tile_views"; status="skipped"; rendering=$true}) }
    else {
        Invoke-TerrainProbe "lab_tile_views" "terrain_lab_tile_views_probe" "[terrain-lab-tile-views] OK" $true $false `
            -RenderingMethod "" -QuitAfter 0 -ProbeTimeoutSeconds 600
    }
} finally {
    $env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME = $previousBridge
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputDirectory "results.json") -Encoding UTF8
}
if (@($results | Where-Object status -eq "failed").Count -gt 0) { throw "Terrain integration failed. See tests/output/terrain_integration/results.json and per-probe logs." }
Write-Host "[terrain-integration] Passed $(@($results | Where-Object status -eq 'passed').Count); skipped $(@($results | Where-Object status -eq 'skipped').Count)."
