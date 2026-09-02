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
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\runtime_smoke.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot headless smoke failed."
}

Write-Host "[addon-checks] Godot render probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\render_scene_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot render probe failed."
}

Write-Host "[addon-checks] Godot theme gallery layout probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\theme_gallery_layout_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot theme gallery layout probe failed."
}

Write-Host "[addon-checks] Godot kit gallery desktop capture"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\render_scene_capture.ps1" -GodotCommand $GodotCommand -ScenePath "res://addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn" -OutputPath "res://tmp/kit_gallery_desktop.png" -Width 1280 -Height 720 -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit gallery desktop capture failed."
}

Write-Host "[addon-checks] Godot kit gallery mobile capture"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\render_scene_capture.ps1" -GodotCommand $GodotCommand -ScenePath "res://addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn" -OutputPath "res://tmp/kit_gallery_mobile.png" -Width 390 -Height 844 -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit gallery mobile capture failed."
}

Write-Host "[addon-checks] Godot kit gallery layout probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_gallery_layout_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit gallery layout probe failed."
}

Write-Host "[addon-checks] Godot kit button badge probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_button_badge_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit button badge probe failed."
}

Write-Host "[addon-checks] Godot kit check-controls contrast probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_check_controls_contrast_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit check-controls contrast probe failed."
}

Write-Host "[addon-checks] Godot kit compact minimum probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_compact_minimum_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit compact minimum probe failed."
}

Write-Host "[addon-checks] Godot kit context menu viewport probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_context_menu_viewport_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit context menu viewport probe failed."
}

Write-Host "[addon-checks] Godot kit collection API probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_collection_api_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit collection API probe failed."
}

Write-Host "[addon-checks] Godot kit empty collection probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_empty_collection_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit empty collection probe failed."
}

Write-Host "[addon-checks] Godot weather forecast item scene probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\weather_forecast_item_scene_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot weather forecast item scene probe failed."
}

Write-Host "[addon-checks] Godot weather behavior probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\weather_behavior_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot weather behavior probe failed."
}

Write-Host "[addon-checks] Godot weather lifecycle probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\weather_lifecycle_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot weather lifecycle probe failed."
}

Write-Host "[addon-checks] Godot terrain guards"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_guards.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 180
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain guards failed."
}

Write-Host "[addon-checks] Godot renderer reporting probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\renderer_reporting_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot renderer reporting probe failed."
}

Write-Host "[addon-checks] Godot grid terrain topology probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_terrain_topology_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid terrain topology probe failed."
}

Write-Host "[addon-checks] Godot grid terrain feature probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_terrain_feature_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid terrain feature probe failed."
}

Write-Host "[addon-checks] Godot grid terrain lake scatter probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_terrain_lake_scatter_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid terrain lake scatter probe failed."
}

Write-Host "[addon-checks] Godot grid terrain transition probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_terrain_transition_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid terrain transition probe failed."
}

Write-Host "[addon-checks] Godot kit label role probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_label_role_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit label role probe failed."
}

Write-Host "[addon-checks] Godot kit color rect fallback probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_color_rect_fallback_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit color rect fallback probe failed."
}

Write-Host "[addon-checks] Godot kit panel ornament probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_panel_ornament_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit panel ornament probe failed."
}

Write-Host "[addon-checks] Godot kit browser desktop capture"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\render_scene_capture.ps1" -GodotCommand $GodotCommand -ScenePath "res://addons/beep_game_builder_cs/templates/scenes/kit_browser.tscn" -OutputPath "res://tmp/kit_browser_desktop.png" -Width 1280 -Height 720 -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit browser desktop capture failed."
}

Write-Host "[addon-checks] Godot kit browser mobile capture"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\render_scene_capture.ps1" -GodotCommand $GodotCommand -ScenePath "res://addons/beep_game_builder_cs/templates/scenes/kit_browser.tscn" -OutputPath "res://tmp/kit_browser_mobile.png" -Width 390 -Height 844 -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit browser mobile capture failed."
}

Write-Host "[addon-checks] Godot kit browser layout probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_browser_layout_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit browser layout probe failed."
}

Write-Host "[addon-checks] Godot showcase interaction probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\showcase_interaction_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot showcase interaction probe failed."
}

Write-Host "[addon-checks] Godot headless editor startup smoke"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\editor_startup_smoke.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 40
if ($LASTEXITCODE -ne 0) {
    throw "Godot headless editor startup smoke failed."
}

Write-Host "[addon-checks] OK"
