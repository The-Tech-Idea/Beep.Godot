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

Write-Host "[addon-checks] Godot game clock axes probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\game_clock_axes_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot game clock axes probe failed."
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

Write-Host "[addon-checks] Godot badge placement probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\badge_placement_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot badge placement probe failed."
}

Write-Host "[addon-checks] Godot kit sprite material probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_sprite_material_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit sprite material probe failed."
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

Write-Host "[addon-checks] Godot kit theme switch stability probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_theme_switch_stability_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit theme switch stability probe failed."
}

Write-Host "[addon-checks] Godot kit inventory carry probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_inventory_carry_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit inventory carry probe failed."
}

Write-Host "[addon-checks] Godot kit ability bar probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_ability_bar_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit ability bar probe failed."
}

Write-Host "[addon-checks] Godot kit tooltip probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\kit_tooltip_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 45
if ($LASTEXITCODE -ne 0) {
    throw "Godot kit tooltip probe failed."
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

Write-Host "[addon-checks] Godot grid terrain building probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_terrain_building_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid terrain building probe failed."
}

Write-Host "[addon-checks] Godot grid terrain subsurface probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_terrain_subsurface_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid terrain subsurface probe failed."
}

Write-Host "[addon-checks] Godot terrain world recipe probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_world_recipe_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain world recipe probe failed."
}

Write-Host "[addon-checks] Godot terrain water material probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_water_material_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain water material probe failed."
}

Write-Host "[addon-checks] Godot terrain feature sheets probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_feature_sheets_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain feature sheets probe failed."
}

Write-Host "[addon-checks] Godot terrain variant choice probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_variant_choice_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain variant choice probe failed."
}

Write-Host "[addon-checks] Godot terrain autotile staleness probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_autotile_staleness_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 120
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain autotile staleness probe failed."
}

Write-Host "[addon-checks] Godot grid ids probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_ids_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid ids probe failed."
}

Write-Host "[addon-checks] Godot grid projection probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_projection_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid projection probe failed."
}

Write-Host "[addon-checks] Godot grid interaction hover probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_interaction_hover_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid interaction hover probe failed."
}

Write-Host "[addon-checks] Godot grid cell overlay probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_cell_overlay_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid cell overlay probe failed."
}

Write-Host "[addon-checks] Godot grid minimap probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_minimap_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid minimap probe failed."
}

Write-Host "[addon-checks] Godot grid job queue probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_job_queue_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid job queue probe failed."
}

Write-Host "[addon-checks] Godot terrain change kind probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot	errain_change_kind_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain change kind probe failed."
}

Write-Host "[addon-checks] Godot terrain generation baseline probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_generation_baseline_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 300
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain generation baseline probe failed."
}

Write-Host "[addon-checks] Godot terrain kind catalog probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\terrain_kind_catalog_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot terrain kind catalog probe failed."
}

Write-Host "[addon-checks] Godot resource wallet key probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\resource_wallet_key_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot resource wallet key probe failed."
}

Write-Host "[addon-checks] Godot job execution requeue probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\job_execution_requeue_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot job execution requeue probe failed."
}

Write-Host "[addon-checks] Godot grid resource catalog ports probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_resource_catalog_ports_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 60
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid resource catalog ports probe failed."
}

Write-Host "[addon-checks] Godot grid worker build effects probe"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\grid_worker_build_effects_probe.ps1" -GodotCommand $GodotCommand -TimeoutSeconds 90
if ($LASTEXITCODE -ne 0) {
    throw "Godot grid worker build effects probe failed."
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
