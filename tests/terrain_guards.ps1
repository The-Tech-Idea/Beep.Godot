param(
    [string]$GodotCommand = "godot",
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path "$PSScriptRoot\.."
$godot = (Get-Command $GodotCommand -ErrorAction Stop).Source

# The guards for the terrain engine. Each one builds a real world through the
# shipped scenes and asserts something about the result, then exits non-zero if
# any check failed - so the exit code is the whole contract here.
#
# capture.gd, worldmap.gd and vegprobe.gd are deliberately absent: the first two
# render PNGs to a directory named by an environment variable and the third
# prints noise distributions. They are tools for looking at the generator, not
# checks that can pass or fail.
$guards = @(
    "addon_selfcontained",
    "demo_scenes",
    "landmass",
    "beach",
    "erosion",
    "relief",
    "vegetation",
    "views",
    "tile_layers",
    "iso_layers",
    "stack_order",
    "cell_data",
    "resources",
    "biomes",
    "perf"
)

$failed = @()
foreach ($guard in $guards) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $godot
    $startInfo.Arguments = "--headless --audio-driver Dummy --path `"$($projectRoot.Path)`" --script res://tests/examples/$guard.gd"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    $startInfo.Environment["GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME"] = "false"

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { $process.Kill() }
        throw "Terrain guard $guard timed out after $TimeoutSeconds seconds."
    }
    $combined = $stdoutTask.GetAwaiter().GetResult() + $stderrTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) {
        $combined
        Write-Host "[terrain-guards] FAIL: $guard"
        $failed += $guard
    }
    else {
        Write-Host "[terrain-guards] ok: $guard"
    }
}

if ($failed.Count -gt 0) {
    throw "Terrain guards failed: $($failed -join ', ')."
}
Write-Host "[terrain-guards] OK: $($guards.Count) guards passed."
