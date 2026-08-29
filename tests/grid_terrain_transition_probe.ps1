param(
    [string]$GodotCommand = "godot",
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path "$PSScriptRoot\.."
$resolvedGodotCommand = (Get-Command $GodotCommand -ErrorAction Stop).Source
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $resolvedGodotCommand
$startInfo.Arguments = "--headless --audio-driver Dummy --path `"$($projectRoot.Path)`" --script res://tests/grid_terrain_transition_probe.gd"
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
    throw "Godot grid-terrain transition probe timed out after $TimeoutSeconds seconds."
}

$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
if ($process.ExitCode -ne 0) {
    $stdout
    $stderr
    throw "Godot grid-terrain transition probe exited with code $($process.ExitCode)."
}

$combined = @($stdout -split "\r?\n") + @($stderr -split "\r?\n")
$fatalLines = $combined | Select-String -Pattern "SCRIPT ERROR|ERROR:|Exception|C# backtrace"
if ($fatalLines) {
    $fatalLines
    throw "Godot grid-terrain transition probe logged errors."
}

if (-not ($combined | Select-String -SimpleMatch "[grid-terrain-transition] OK:")) {
    $stdout
    $stderr
    throw "Godot grid-terrain transition probe did not report success."
}

$stdout
