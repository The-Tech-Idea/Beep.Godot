param(
    [string]$GodotExe = "H:\dev\Godot\Godot_v4.7-stable_mono_win64_console.exe",
    [switch]$SkipBuild,
    [switch]$Capture
)

$ErrorActionPreference = "Stop"
$workspace = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $GodotExe -PathType Leaf)) {
    throw "Godot console executable not found: $GodotExe"
}
$previousBridge = $env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME
Push-Location $workspace
try {
    $env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME = "false"
    if (-not $SkipBuild) {
        & dotnet build Beep.Godot.csproj --no-restore -v:q
        if ($LASTEXITCODE -ne 0) { throw "C# build failed" }
    }
    $checks = @(
        "actor_integration_probe.gd",
        "production_elapsed_probe.gd",
        "production_scheduling_probe.gd",
        "production_simulation_probe.gd",
        "production_residency_probe.gd",
        "job_execution_probe.gd",
        "job_execution_service_probe.gd",
        "worker_execution_binding_probe.gd",
        "actor_travel_probe.gd",
        "worker_dispatch_probe.gd",
        "economy_lab_probe.gd",
        "work_deadlines_probe.gd",
        "component_lookup_probe.gd",
        "actor_formation_probe.gd",
        "actor_formation_save_probe.gd",
        "actor_motion_abilities_probe.gd",
        "actor_flight_probe.gd",
        "actor_flocking_probe.gd",
        "actor_spatial_probe.gd",
        "actor_overview_probe.gd",
        "actor_visual_culling_probe.gd",
        "actor_wall_jump_probe.gd",
        "actor_air_abilities_probe.gd",
        "actor_slide_probe.gd",
        "actor_ability_save_probe.gd",
        "actor_contact_ability_save_probe.gd",
        "actor_animation_probe.gd",
        "actor_platformer_scene_probe.gd",
        "actor_party_probe.gd",
        "actor_party_handoff_probe.gd",
        "actor_party_lab_probe.gd",
        "actor_follow_probe.gd",
        "actor_follow_spacing_probe.gd",
        "actor_follow_index_probe.gd",
        "actor_escort_lab_probe.gd",
        "actor_rpg_health_probe.gd",
        "actor_rpg_progression_probe.gd",
        "actor_reattachment_probe.gd",
        "actor_residency_probe.gd",
        "actor_scale_probe.gd",
        "actor_lab_probe.gd",
        "terrain_surface_streaming_probe.gd",
        "terrain_iso_surface_overview_probe.gd",
        "terrain_iso_publication_probe.gd",
        "terrain_world_iso_publication_probe.gd",
        "terrain_feature_streaming_probe.gd",
        "terrain_relief_streaming_probe.gd",
        "terrain_iso_feature_streaming_probe.gd",
        "terrain_water_surface_probe.gd",
        "terrain_resource_scale_probe.gd",
        "terrain_start_scale_probe.gd",
        "terrain_scratch_lifetime_probe.gd",
        "terrain_sample_values_probe.gd",
        "terrain_identity_probe.gd",
        "terrain_collision_probe.gd",
        "terrain_collision_chunks_probe.gd",
        "terrain_collision_readiness_probe.gd",
        "terrain_collision_budget_probe.gd",
        "terrain_motion_gate_probe.gd",
        "terrain_generation_job_probe.gd",
        "terrain_world_generation_probe.gd",
        "terrain_world_collision_publication_probe.gd",
        "terrain_world_ownership_probe.gd",
        "terrain_lab_generation_probe.gd",
        "terrain_data_storage_probe.gd",
        "terrain_chunk_archive_probe.gd",
        "terrain_chunk_availability_probe.gd",
        "terrain_chunk_loading_probe.gd",
        "terrain_chunk_saving_probe.gd",
        "terrain_capture_budget_probe.gd",
        "terrain_chunk_revisions_probe.gd",
        "terrain_chunk_pins_probe.gd",
        "terrain_chunk_eviction_probe.gd",
        "terrain_chunk_demand_probe.gd",
        "terrain_camera_demand_probe.gd",
        "terrain_chunk_budget_probe.gd",
        "terrain_cell_budget_probe.gd",
        "terrain_archive_io_budget_probe.gd",
        "terrain_painted_archive_probe.gd",
        "terrain_painted_publication_probe.gd",
        "streaming_lab_probe.gd",
        "terrain_navigation_binding_probe.gd",
        "terrain_navigation_height_probe.gd",
        "terrain_navigation_edges_probe.gd",
        "terrain_navigation_requests_probe.gd",
        "terrain_navigation_invalidation_probe.gd",
        "terrain_follower_requests_probe.gd",
        "terrain_worker_arrival_probe.gd",
        "terrain_job_reservations_probe.gd",
        "terrain_cross_queue_reservations_probe.gd",
        "storage_material_reservations_probe.gd",
        "terrain_build_approach_probe.gd",
        "terrain_haul_demand_probe.gd",
        "terrain_route_demand_probe.gd",
        "terrain_search_eviction_probe.gd",
        "terrain_route_retirement_probe.gd",
        "grid_worker_build_effects_probe.gd",
        "save_scope_probe.gd"
    )
    foreach ($check in $checks) {
        Write-Host "Running $check"
        & $GodotExe --headless --path $workspace --script "tests/$check"
        if ($LASTEXITCODE -ne 0) { throw "Failed: $check" }
    }
    if ($Capture) {
        foreach ($check in @("economy_lab_probe.gd", "actor_lab_probe.gd", "actor_escort_lab_probe.gd", "actor_party_lab_probe.gd", "actor_platformer_scene_probe.gd", "actor_overview_probe.gd", "actor_visual_culling_probe.gd",
                "terrain_surface_streaming_probe.gd", "terrain_iso_surface_overview_probe.gd", "terrain_iso_publication_probe.gd", "terrain_world_iso_publication_probe.gd", "terrain_iso_feature_streaming_probe.gd", "streaming_lab_probe.gd")) {
            Write-Host "Rendering $check"
            & $GodotExe --path $workspace --rendering-method gl_compatibility --script "tests/$check"
            if ($LASTEXITCODE -ne 0) { throw "Render check failed: $check" }
        }
    }
}
finally {
    $env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME = $previousBridge
    Pop-Location
}
