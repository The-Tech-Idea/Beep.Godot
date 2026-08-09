param(
    [string]$GodotCommand = "godot"
)

$ErrorActionPreference = "Stop"

Write-Host "[addon-checks] Source contract scan"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\addon_contract_scan.ps1"

Write-Host "[addon-checks] Clean C# build"
dotnet clean "$PSScriptRoot\..\Beep.Godot.csproj" | Out-Null
dotnet build "$PSScriptRoot\..\Beep.Godot.csproj"

Write-Host "[addon-checks] Godot headless smoke"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\runtime_smoke.ps1" -GodotCommand $GodotCommand
if ($LASTEXITCODE -ne 0) {
    throw "Godot headless smoke failed."
}

Write-Host "[addon-checks] Godot headless editor startup smoke"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\editor_startup_smoke.ps1" -GodotCommand $GodotCommand
if ($LASTEXITCODE -ne 0) {
    throw "Godot headless editor startup smoke failed."
}

Write-Host "[addon-checks] OK"
