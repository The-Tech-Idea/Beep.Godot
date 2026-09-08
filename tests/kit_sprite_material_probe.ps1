param(
    [string]$GodotCommand = "godot",
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = "Stop"

# Needs a real display driver: it samples the rendered viewport, like kit_button_badge_probe and
# kit_check_controls_contrast_probe. There is no headless way to prove artwork reached a plate.

$projectRoot = Resolve-Path "$PSScriptRoot\.."
$resolvedGodotCommand = (Get-Command $GodotCommand -ErrorAction Stop).Source
$stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) "beep-godot-kit-sprite-material.out.log"
$stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) "beep-godot-kit-sprite-material.err.log"

Remove-Item -LiteralPath $stdoutPath, $stderrPath -ErrorAction SilentlyContinue

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $resolvedGodotCommand
$startInfo.Arguments = "--display-driver windows --audio-driver Dummy --path `"$($projectRoot.Path)`" --script res://tests/kit_sprite_material_probe.gd"
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
    throw "Godot kit sprite material probe timed out after $TimeoutSeconds seconds."
}

$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
$stdout | Set-Content -LiteralPath $stdoutPath
$stderr | Set-Content -LiteralPath $stderrPath

if ($process.ExitCode -ne 0) {
    $stdout
    $stderr
    throw "Godot kit sprite material probe exited with code $($process.ExitCode)."
}

$combined = @($stdout -split "\r?\n") + @($stderr -split "\r?\n")
$fatalLines = $combined | Select-String -Pattern "SCRIPT ERROR|ERROR:|Exception|C# backtrace|CrashHandlerException"
if ($fatalLines) {
    $fatalLines
    throw "Godot kit sprite material probe logged errors."
}

if (-not ($combined | Select-String -SimpleMatch "[kit-sprite-material] OK:")) {
    $stdout
    $stderr
    throw "Godot kit sprite material probe did not report success."
}

$stdout
