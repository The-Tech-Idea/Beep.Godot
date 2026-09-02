param(
    [string]$GodotCommand = "godot",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path "$PSScriptRoot\.."
$godot = (Get-Command $GodotCommand -ErrorAction Stop).Source

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $godot
$startInfo.Arguments = "--headless --audio-driver Dummy --path `"$($projectRoot.Path)`" --script res://tests/examples/renderer_reporting.gd"
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
    throw "Renderer reporting probe timed out after $TimeoutSeconds seconds."
}
$combined = $stdoutTask.GetAwaiter().GetResult() + $stderrTask.GetAwaiter().GetResult()

if ($process.ExitCode -ne 0 -or -not ($combined | Select-String -SimpleMatch "[renderer-reporting] OK:")) {
    $combined
    throw "Renderer reporting probe did not run to completion."
}

# Every renderer driven with no generator must have reported that it could not
# draw. A renderer that stays quiet leaves an empty view indistinguishable from
# an empty world, which is the failure this probe exists to prevent.
$renderers = [regex]::Matches($combined, 'DRIVEN (\S+)') | ForEach-Object { $_.Groups[1].Value }
if ($renderers.Count -lt 9) {
    $combined
    throw "Renderer reporting probe drove only $($renderers.Count) renderers; expected 9."
}

$quiet = @()
foreach ($r in $renderers) {
    if (-not ($combined | Select-String -SimpleMatch "[$r]")) {
        $quiet += $r
    }
}

if ($quiet.Count -gt 0) {
    $combined
    throw "These renderers drew nothing and reported nothing: $($quiet -join ', ')."
}

Write-Host "[renderer-reporting] OK: all $($renderers.Count) renderers report when they cannot draw."
