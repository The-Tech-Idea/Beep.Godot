param(
    [string]$GodotCommand = "godot",
    [string]$ScenePath = "res://addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn",
    [string]$OutputPath = "res://tmp/scene_capture.png",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path "$PSScriptRoot\.."
$resolvedGodotCommand = (Get-Command $GodotCommand -ErrorAction Stop).Source
$stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) "beep-godot-scene-capture.out.log"
$stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) "beep-godot-scene-capture.err.log"

foreach ($path in @($stdoutPath, $stderrPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue
    }
}

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $resolvedGodotCommand
$startInfo.Arguments = "--display-driver windows --audio-driver Dummy --path `"$($projectRoot.Path)`" --script res://tests/render_scene_capture.gd"
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true
$startInfo.Environment["GODOT_CAPTURE_SCENE"] = $ScenePath
$startInfo.Environment["GODOT_CAPTURE_OUTPUT"] = $OutputPath
$startInfo.Environment["GODOT_CAPTURE_WIDTH"] = [string]$Width
$startInfo.Environment["GODOT_CAPTURE_HEIGHT"] = [string]$Height
$startInfo.Environment["GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME"] = "false"

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
[void]$process.Start()
$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try {
        try { $process.Kill($true) } catch { $process.Kill() }
    } catch {
        $process.Kill()
    }

    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $stdout | Set-Content -LiteralPath $stdoutPath
    $stderr | Set-Content -LiteralPath $stderrPath
    $stdout
    $stderr
    if ((@($stdout -split "\r?\n") + @($stderr -split "\r?\n")) | Select-String -SimpleMatch "[scene-capture] OK:") {
        exit 0
    }
    throw "Godot scene capture timed out after $TimeoutSeconds seconds."
}

$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
$stdout | Set-Content -LiteralPath $stdoutPath
$stderr | Set-Content -LiteralPath $stderrPath

if ($process.ExitCode -ne 0) {
    $stdout
    $stderr
    throw "Godot scene capture exited with code $($process.ExitCode)."
}

$combined = @($stdout -split "\r?\n") + @($stderr -split "\r?\n")
$fatalLines = $combined | Select-String -Pattern "SCRIPT ERROR|ERROR:|Exception|C# backtrace|CrashHandlerException"
if ($fatalLines) {
    $fatalLines
    throw "Godot scene capture logged errors."
}

if (-not ($combined | Select-String -SimpleMatch "[scene-capture] OK:")) {
    $stdout
    $stderr
    throw "Godot scene capture did not report success."
}

$stdout
