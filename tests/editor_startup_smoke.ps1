param(
    [string]$GodotCommand = "godot",
    [int]$QuitAfterSeconds = 5,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path "$PSScriptRoot\.."
$resolvedGodotCommand = (Get-Command $GodotCommand -ErrorAction Stop).Source
$stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) "beep-godot-editor-smoke.out.log"
$stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) "beep-godot-editor-smoke.err.log"

Remove-Item -LiteralPath $stdoutPath, $stderrPath -ErrorAction SilentlyContinue

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $resolvedGodotCommand
$startInfo.Arguments = "--headless --editor --path `"$($projectRoot.Path)`" --quit-after $QuitAfterSeconds"
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
[void]$process.Start()
$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill($true)
    throw "Godot editor startup smoke timed out after $TimeoutSeconds seconds."
}

$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
$stdout | Set-Content -LiteralPath $stdoutPath
$stderr | Set-Content -LiteralPath $stderrPath
$exitCode = $process.ExitCode

if ($exitCode -ne 0) {
    $stdout
    $stderr
    throw "Godot editor startup smoke exited with code $exitCode."
}

$combined = @($stdout -split "\r?\n") + @($stderr -split "\r?\n")

$fatalLines = $combined | Select-String -Pattern "ERROR:|Exception|C# backtrace"
if ($fatalLines) {
    $fatalLines
    throw "Godot editor startup smoke logged addon errors."
}

$requiredLines = @(
    "[Beep Game Builder] Plugin enabled.",
    "[Beep UI] enabled.",
    "[Godot MCP] Plugin enabled.",
    "[Beep Game Builder] Plugin disabled.",
    "[Godot MCP] Plugin disabled."
)

foreach ($line in $requiredLines) {
    if (-not ($combined | Select-String -SimpleMatch $line)) {
        throw "Godot editor startup smoke did not log expected lifecycle line: $line"
    }
}

Write-Host "[editor-smoke] OK: enabled and disabled all addon plugins without logged errors."
