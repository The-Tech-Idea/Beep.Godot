param(
    [string]$GodotCommand = "godot",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path "$PSScriptRoot\.."
$godot = (Get-Command $GodotCommand -ErrorAction Stop).Source
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $godot
$startInfo.Arguments = "--headless --audio-driver Dummy --path `"$($projectRoot.Path)`" --script res://tests/terrain_autotile_staleness_probe.gd"
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
    throw "Godot terrain autotile staleness probe timed out after $TimeoutSeconds seconds."
}
$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
$combined = $stdout + $stderr
if ($process.ExitCode -ne 0 -or -not ($combined | Select-String -SimpleMatch "[terrain-autotile-staleness] OK")) {
    $combined
    throw "Godot terrain autotile staleness probe failed."
}
$combined
