param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

$addonsRoot = Join-Path $ProjectRoot "addons"
$builderRoot = Join-Path $addonsRoot "beep_game_builder_cs"
$docsRoot = Join-Path $ProjectRoot "docs"
$guideMdPath = Join-Path $docsRoot "ADDON_GUIDE.md"
$guideHtmlPath = Join-Path $docsRoot "addon-guide.html"

function Get-RelativePath([string]$from, [string]$to) {
    $fromPath = [System.IO.Path]::GetFullPath($from).TrimEnd('\') + '\'
    $fromUri = [System.Uri]::new($fromPath)
    $toUri = [System.Uri]::new([System.IO.Path]::GetFullPath($to))
    return [System.Uri]::UnescapeDataString($fromUri.MakeRelativeUri($toUri).ToString()).Replace('\', '/')
}

function ConvertTo-Summary([string]$source, [int]$classIndex) {
    $prefixStart = [Math]::Max(0, $classIndex - 1800)
    $prefix = $source.Substring($prefixStart, $classIndex - $prefixStart)
    $matches = [regex]::Matches($prefix, '(?s)///\s*<summary>\s*(.*?)\s*///\s*</summary>')
    if ($matches.Count -eq 0) { return "" }

    $summary = $matches[$matches.Count - 1].Groups[1].Value
    $summary = [regex]::Replace($summary, '///', ' ')
    $summary = [regex]::Replace($summary, '<[^>]+>', ' ')
    $summary = [regex]::Replace($summary, '\b[Ss]ee\s+phase-\d+\.?\s*', '')
    $summary = [regex]::Replace($summary, '\b[Pp]hase\s+\d+\s*[-:]\s*', '')
    $summary = [regex]::Replace($summary, '\b[Pp]hase-\d+\b', 'review pass')
    $summary = [regex]::Replace($summary, '\s+', ' ').Trim()
    if ($summary.Length -gt 320) {
        $summary = $summary.Substring(0, 317) + "..."
    }
    return $summary
}

function ConvertTo-Html([string]$value) {
    return [System.Net.WebUtility]::HtmlEncode($value)
}

function ConvertTo-AsciiText([string]$value) {
    $text = $value
    $replacements = @{
        ([string][char]0x2013) = '-'
        ([string][char]0x2014) = '-'
        ([string][char]0x2026) = '...'
        ([string][char]0x00A7) = 'section'
        ([string][char]0x2022) = '*'
        ([string][char]0x00B0) = ' deg'
        ([string][char]0x2192) = '->'
        ([string][char]0x2194) = '<->'
        ([string][char]0x00D7) = 'x'
        ([string][char]0x2500) = '-'
        ([string][char]0x2514) = '+'
        ([string][char]0x271B) = '+'
    }
    foreach ($key in $replacements.Keys) {
        $text = $text.Replace($key, $replacements[$key])
    }
    return $text -replace '[^\x00-\x7F]', '?'
}

function New-Section([string]$id, [string]$title, [string[]]$areas, [string]$purpose, [string]$useWhen, [string]$scenePattern) {
    [PSCustomObject]@{
        Id = $id
        Title = $title
        Areas = $areas
        Purpose = $purpose
        UseWhen = $useWhen
        ScenePattern = $scenePattern
    }
}

$csFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File -Filter "*.cs" | Sort-Object FullName
$gdFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File -Filter "*.gd" | Sort-Object FullName
$sceneFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File -Filter "*.tscn" | Sort-Object FullName
$templateFiles = Get-ChildItem -LiteralPath (Join-Path $builderRoot "templates") -Recurse -File -Filter "*.tscn" | Sort-Object FullName
$textureFiles = Get-ChildItem -LiteralPath (Join-Path $builderRoot "textures") -Recurse -File -Include "*.png","*.jpg","*.jpeg","*.webp" | Sort-Object FullName
$audioFiles = Get-ChildItem -LiteralPath (Join-Path $builderRoot "audio") -Recurse -File -Include "*.ogg","*.mp3","*.wav" | Sort-Object FullName

$classes = [System.Collections.Generic.List[object]]::new()
foreach ($file in $csFiles) {
    $source = [System.IO.File]::ReadAllText($file.FullName)
    $namespaceMatch = [regex]::Match($source, '(?m)^\s*namespace\s+([\w\.]+)')
    $namespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups[1].Value } else { "Global" }
    $relativePath = Get-RelativePath $ProjectRoot $file.FullName
    $relativeUnderAddons = Get-RelativePath $addonsRoot $file.DirectoryName
    $addonName = (Get-RelativePath $addonsRoot $file.FullName).Split('/')[0]
    if ($addonName -eq "beep_game_builder_cs") {
        $area = Get-RelativePath $builderRoot $file.DirectoryName
        if ([string]::IsNullOrWhiteSpace($area)) { $area = "root" }
    } else {
        $area = $relativeUnderAddons
        if ([string]::IsNullOrWhiteSpace($area)) { $area = $addonName }
    }

    $pattern = '(?ms)(?<attributes>(?:\s*\[[^\]]+\]\s*)*)\s*public\s+(?<abstract>abstract\s+)?(?<partial>partial\s+)?class\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<base>[^\{\r\n]+))?'
    foreach ($match in [regex]::Matches($source, $pattern)) {
        $attributes = $match.Groups['attributes'].Value
        $classes.Add([PSCustomObject]@{
            Name = $match.Groups['name'].Value
            Namespace = $namespace
            Base = $match.Groups['base'].Value.Trim()
            GlobalClass = $attributes -match '\[GlobalClass\]'
            Summary = ConvertTo-Summary $source $match.Index
            Path = $relativePath
            Area = $area
            Addon = $addonName
        })
    }
}

$gdScripts = foreach ($file in $gdFiles) {
    $source = [System.IO.File]::ReadAllText($file.FullName)
    $classNameMatch = [regex]::Match($source, '(?m)^\s*class_name\s+([A-Za-z_]\w*)')
    $extendsMatch = [regex]::Match($source, '(?m)^\s*extends\s+([A-Za-z_]\w*)')
    [PSCustomObject]@{
        Addon = (Get-RelativePath $addonsRoot $file.FullName).Split('/')[0]
        Name = if ($classNameMatch.Success) { $classNameMatch.Groups[1].Value } else { [System.IO.Path]::GetFileNameWithoutExtension($file.Name) }
        Extends = if ($extendsMatch.Success) { $extendsMatch.Groups[1].Value } else { "-" }
        Path = Get-RelativePath $ProjectRoot $file.FullName
    }
}

$sceneRows = foreach ($file in $templateFiles) {
    $source = [System.IO.File]::ReadAllText($file.FullName)
    $rootMatch = [regex]::Match($source, '(?m)^\[node name="([^"]+)" type="([^"]+)"\]')
    [PSCustomObject]@{
        Name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        Root = if ($rootMatch.Success) { "$($rootMatch.Groups[1].Value) : $($rootMatch.Groups[2].Value)" } else { "-" }
        Path = Get-RelativePath $ProjectRoot $file.FullName
    }
}

$genreRows = Get-ChildItem -LiteralPath (Join-Path $builderRoot "catalogs/skins") -Directory | Sort-Object Name | ForEach-Object {
    $themesPath = Join-Path $_.FullName "themes"
    $themes = if (Test-Path -LiteralPath $themesPath) {
        @(Get-ChildItem -LiteralPath $themesPath -Directory | Sort-Object Name | ForEach-Object Name)
    } else {
        @()
    }
    [PSCustomObject]@{
        Genre = $_.Name
        ThemeCount = $themes.Count
        Themes = ($themes -join ", ")
    }
}

$sections = @(
    New-Section "core" "Core Builder Services" @("root", "core", "ui") "Project generation, editor dock support, file/template helpers, state data, data binding, screen generation, skin catalog loading, command history, utility math, and debug helpers." "Start here when you are wiring project-wide services, generated scenes, save state, input maps, forms, data-driven UI, or editor workflows." "Use a normal Godot scene root, add the required resource/control classes from the Add Node dialog, and keep generator/editor helpers out of runtime gameplay nodes unless they are explicitly runtime-safe."
    New-Section "ecs-foundation" "ECS Foundation" @("ecs", "ecs/categories", "ecs/stats") "Base node categories and common gameplay systems: entity lifecycle, controllers, world components, UI screens, stats, saveability, health, combat, inventory, progression, quests, movement, pooling, particles, audio, interactions, and game flow." "Use these as attachable component nodes on Godot Node2D, Control, CharacterBody2D, Area2D, or resource-backed entities." "Author parent nodes in the scene, add components as children, set exported NodePath fields in the inspector, and let components communicate through Godot signals and exported resources."
    New-Section "algorithms" "Algorithms And Motion" @("ecs/algorithms") "Reusable algorithm components and helpers for easing, ball motion, flocking, height, procedural level generation, steering, loot affixes, and adaptive difficulty." "Use this section when gameplay needs deterministic movement, procedural content, agent steering, or reusable math." "Keep simulation nodes separate from visual nodes; connect generated values to existing Sprite2D, CharacterBody2D, TileMapLayer, or Control nodes."
    New-Section "atmosphere" "Atmosphere, Weather, Day Night" @("ecs/atmosphere") "World atmosphere stack for ambient audio, cloud sprites, day/night phase changes, fog, lightning, shelter zones, seasonal changes, weather audio, weather sprites, and weather system orchestration." "Use this when a scene needs mood, time-of-day, weather-driven modifiers, or lightweight overlays." "Add the controller/world components to the scene, bind overlay/audio paths through exported fields, and keep weather visuals above terrain but below HUD."
    New-Section "items" "Items, Equipment, Catalogs" @("ecs/items") "Resource classes and catalog utilities for item identity, rarity, consumables, liquids, equipment, weapons, armor, shields, and item lookup." "Use this when inventory, crafting, loot, stores, or equipment need reusable data resources." "Create item resources in the inspector, assign them to inventory/crafting/drop components, and keep item definitions in resources instead of hard-coded gameplay scripts."
    New-Section "genre-scenes" "Genre Scene Components" @("ecs/scenes", "ecs/scenes/cardgame", "ecs/scenes/citybuilder", "ecs/scenes/platformer", "ecs/scenes/puzzle", "ecs/scenes/racing", "ecs/scenes/rpg", "ecs/scenes/shooter", "ecs/scenes/strategy", "ecs/scenes/survival", "ecs/scenes/topdown") "Scene controllers for starter genre templates and genre-specific screens." "Use these as working examples or as starter shells when creating a new game from the addon." "Instance the matching template scene, then replace placeholder art and data while preserving the named nodes that the script expects."
    New-Section "terrain" "Terrain Engine" @("ecs/terrain") "A seeded terrain engine: the 17-stage field builder, painted, tile and isometric views, feature and prop scatter, transitions, overlays, data layers, and the TerrainWorldComponent front door." "Use this to generate and draw deterministic maps for top-down and isometric games." "Let TerrainWorldComponent create and draw the map - its Generate map button does so in the editor and the result is saved with the scene. Read the map through TerrainDataLayersComponent rather than the drawing layers."
    New-Section "grid" "2D Grid Builder Systems" @("ecs/grid", "ecs/grid/ui") "Grid projection, cell data, crops and calendar, tilemap bridging, selection, placement, roads, navigation, path following, job queues, resources, workers, spawners, and the builder HUD/resource panels under ecs/grid/ui." "Use this for city-builder, RTS, farming, survival, and Settlers-style workflows on top of authored or generated terrain." "Build the world at design time with authored Node2D/Control nodes, then attach grid components and bind exported NodePaths. Pair with the terrain engine through GridCellDataComponent or a DataLayersPath."
    New-Section "ui" "Runtime UI Components" @("ecs/ui", "ecs/ui/hud") "Reusable UI components for HUD, dialogs, data views, menus, settings, save/load, progress, inventory displays, countdowns, notifications, health bars, minimap, and screen transitions." "Use these for gameplay HUD and menus when you need reusable behavior on existing Control scenes." "Create the Control tree in the editor, attach components to the authored nodes, and bind exported child paths. Do not construct HUD panels entirely in runtime code for normal game screens."
    New-Section "kit" "Game UI Kit" @("ecs/ui/kit") "Custom and extended controls for buttons, panels, tabs, item cards, slots, meters, minimaps, charts, trees, labels, sliders, knobs, toggles, and genre-aware visual styling." "Use this as the default UI layer for game HUDs, panels, menus, and toolbars." "Apply ThemePresetComponent once near the UI root, use KitPanelContainer for framed panels, use KitLabel/KitPushButton/KitIconButton for controls, and keep panel headers as normal layout rows instead of decorative nested cards."
    New-Section "mcp-csharp" "Godot MCP Bridge" @("godot_mcp") "Optional C# bridge for editor/runtime inspection, command routing, scene perception, safe write gates, undo integration, JSON transport, WebSocket transport, settings, and lifecycle control." "Use this only when an external MCP client is controlling or inspecting the Godot project." "Keep write guards enabled by default, use the command registry for explicit operations, and treat runtime/editor writes as separate permission surfaces."
)

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss K"
$globalCount = ($classes | Where-Object GlobalClass).Count

function Append-TypeTableMarkdown([System.Text.StringBuilder]$builder, [array]$types) {
    [void]$builder.AppendLine("| Type | Base | Editor-addable | Summary | Source |")
    [void]$builder.AppendLine("| --- | --- | --- | --- | --- |")
    foreach ($type in $types | Sort-Object Area, Name) {
        $base = if ([string]::IsNullOrWhiteSpace($type.Base)) { "-" } else { $type.Base.Replace("|", "\|") }
        $summary = if ([string]::IsNullOrWhiteSpace($type.Summary)) { "-" } else { $type.Summary.Replace("|", "\|") }
        $addable = if ($type.GlobalClass) { "Yes" } else { "No" }
        [void]$builder.AppendLine("| $($type.Name) | $base | $addable | $summary | $($type.Path) |")
    }
}

function Append-TypeTableHtml([System.Text.StringBuilder]$builder, [array]$types) {
    [void]$builder.AppendLine("<table><thead><tr><th>Type</th><th>Base</th><th>Add Node</th><th>Summary</th><th>Source</th></tr></thead><tbody>")
    foreach ($type in $types | Sort-Object Area, Name) {
        $addable = if ($type.GlobalClass) { "Yes" } else { "No" }
        [void]$builder.AppendLine("<tr><td><code>$(ConvertTo-Html $type.Name)</code></td><td><code>$(ConvertTo-Html $type.Base)</code></td><td>$addable</td><td>$(ConvertTo-Html $type.Summary)</td><td><code>$(ConvertTo-Html $type.Path)</code></td></tr>")
    }
    [void]$builder.AppendLine("</tbody></table>")
}

$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# Beep.Godot Full Addon Guide")
[void]$md.AppendLine()
[void]$md.AppendLine("Generated from the addon source tree on $generatedAt. Regenerate with powershell -ExecutionPolicy Bypass -File tools/Generate-AddonGuide.ps1.")
[void]$md.AppendLine()
[void]$md.AppendLine("## Inventory")
[void]$md.AppendLine()
[void]$md.AppendLine("| Item | Count |")
[void]$md.AppendLine("| --- | ---: |")
[void]$md.AppendLine("| Addon C# source files | $($csFiles.Count) |")
[void]$md.AppendLine("| Public C# classes | $($classes.Count) |")
[void]$md.AppendLine("| Godot [GlobalClass] editor-addable types | $globalCount |")
[void]$md.AppendLine("| GDScript files across addons | $($gdFiles.Count) |")
[void]$md.AppendLine("| Template scenes | $($templateFiles.Count) |")
[void]$md.AppendLine("| Texture assets | $($textureFiles.Count) |")
[void]$md.AppendLine("| Audio assets | $($audioFiles.Count) |")
[void]$md.AppendLine("| Skin genres | $($genreRows.Count) |")
[void]$md.AppendLine()
[void]$md.AppendLine("## How To Use This Addon")
[void]$md.AppendLine()
[void]$md.AppendLine("1. Add a normal Godot scene root first: Node2D for world scenes, Control for HUD/menu scenes, or Resource files for data.")
[void]$md.AppendLine("2. Add Beep components from Godot's Add Node dialog. The editor-addable column below means the class is marked with [GlobalClass].")
[void]$md.AppendLine("3. Build UI and HUD layout at design time. Attach kit/runtime UI scripts to authored Control nodes instead of creating whole panels in code.")
[void]$md.AppendLine("4. For grid games, use TerrainWorldComponent plus GridProjectionComponent, GridCellDataComponent, GridPlacementComponent, GridNavigationComponent, GridJobQueueComponent, and GridWorkerSpawnerComponent as separate nodes.")
[void]$md.AppendLine("5. Use templates as starter scenes, not as black boxes. Preserve expected node names when a template controller depends on them.")
[void]$md.AppendLine()

foreach ($section in $sections) {
    $sectionTypes = @($classes | Where-Object { $section.Areas -contains $_.Area })
    [void]$md.AppendLine("## $($section.Title)")
    [void]$md.AppendLine()
    [void]$md.AppendLine($section.Purpose)
    [void]$md.AppendLine()
    [void]$md.AppendLine("Use when: $($section.UseWhen)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("Scene pattern: $($section.ScenePattern)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("Source areas: " + ($section.Areas -join ", "))
    [void]$md.AppendLine()
    [void]$md.AppendLine("Types in this section: $($sectionTypes.Count)")
    [void]$md.AppendLine()
    Append-TypeTableMarkdown $md $sectionTypes
    [void]$md.AppendLine()
}

[void]$md.AppendLine("## GDScript Addons")
[void]$md.AppendLine()
[void]$md.AppendLine("| Addon | Script | Extends | Source |")
[void]$md.AppendLine("| --- | --- | --- | --- |")
foreach ($script in $gdScripts | Sort-Object Addon, Name) {
    [void]$md.AppendLine("| $($script.Addon) | $($script.Name) | $($script.Extends) | $($script.Path) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Skin Catalog")
[void]$md.AppendLine()
[void]$md.AppendLine("| Genre | Themes |")
[void]$md.AppendLine("| --- | --- |")
foreach ($genre in $genreRows) {
    [void]$md.AppendLine("| $($genre.Genre) | $($genre.Themes) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Template Scenes")
[void]$md.AppendLine()
[void]$md.AppendLine("| Template | Root | Source |")
[void]$md.AppendLine("| --- | --- | --- |")
foreach ($scene in $sceneRows) {
    [void]$md.AppendLine("| $($scene.Name) | $($scene.Root) | $($scene.Path) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Runnable Examples")
[void]$md.AppendLine()
[void]$md.AppendLine("- addons/beep_game_builder_cs/templates/scenes/terrain/grid_world_kit_hud_example.tscn: playable Settlers-style builder slice using terrain, resources, tools, workers, and kit HUD.")
[void]$md.AppendLine("- addons/beep_game_builder_cs/templates/scenes/terrain/base_worker_templates_example.tscn: focused depot/worker template scene.")
[void]$md.AppendLine()
[void]$md.AppendLine("## Verification")
[void]$md.AppendLine()
[void]$md.AppendLine("- dotnet build Beep.Godot.csproj --no-restore")
[void]$md.AppendLine("- powershell -ExecutionPolicy Bypass -File tests/runtime_smoke.ps1 -GodotCommand H:/dev/Godot/Godot_v4.7-stable_mono_win64.exe -TimeoutSeconds 90")
[void]$md.AppendLine("- powershell -ExecutionPolicy Bypass -File tests/render_scene_probe.ps1 -GodotCommand H:/dev/Godot/Godot_v4.7-stable_mono_win64.exe -ScenePath res://addons/beep_game_builder_cs/templates/scenes/terrain/grid_world_kit_hud_example.tscn -TimeoutSeconds 90")

[System.IO.File]::WriteAllText($guideMdPath, (ConvertTo-AsciiText $md.ToString()), [System.Text.UTF8Encoding]::new($false))

$html = [System.Text.StringBuilder]::new()
[void]$html.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>Beep.Godot Full Addon Guide</title>")
[void]$html.AppendLine("<style>:root{--bg:#f5f4ef;--fg:#1f2726;--muted:#63706d;--panel:#fffefa;--panel2:#ece6d9;--line:#cabfad;--accent:#28666e;--accent2:#b86e23;--code:#17353a}@media(prefers-color-scheme:dark){:root{--bg:#121716;--fg:#eef3ef;--muted:#aebbb6;--panel:#1a211f;--panel2:#242d29;--line:#3a4642;--accent:#74cbd1;--accent2:#e4a254;--code:#d9f5f7}}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.48 system-ui,-apple-system,Segoe UI,sans-serif}header{background:var(--panel);border-bottom:1px solid var(--line)}.wrap{max-width:1480px;margin:0 auto;padding:24px 22px}main.wrap{display:grid;grid-template-columns:270px 1fr;gap:24px;align-items:start}nav{position:sticky;top:16px;border:1px solid var(--line);background:var(--panel);border-radius:8px;padding:12px}nav a{display:block;color:var(--accent);text-decoration:none;padding:7px 8px;border-radius:6px}nav a:hover{background:var(--panel2)}section{border-bottom:1px solid var(--line);padding-bottom:28px;margin-bottom:28px}h1,h2,h3{line-height:1.2;margin:0 0 12px}h1{font-size:34px}h2{font-size:25px}h3{font-size:18px;margin-top:18px}p{margin:0 0 13px;color:var(--muted)}.inventory{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px;margin:16px 0}.metric{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:12px}.metric strong{display:block;font-size:24px;color:var(--accent2)}table{width:100%;border-collapse:collapse;background:var(--panel);border:1px solid var(--line);margin:13px 0 20px}th,td{padding:8px 10px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left}th{background:var(--panel2);color:var(--fg);font-weight:700}code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;color:var(--code);font-size:.93em}.callout{background:var(--panel);border-left:4px solid var(--accent2);padding:12px 14px;margin:14px 0}.areas{color:var(--muted);font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:.9em}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:12px}.card{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:14px}@media(max-width:900px){main.wrap{grid-template-columns:1fr}nav{position:static}} </style></head><body>")
[void]$html.AppendLine("<header><div class='wrap'><h1>Beep.Godot Full Addon Guide</h1><p>Section-by-section guide generated from the real addon source on $(ConvertTo-Html $generatedAt). <a href='index.html'>Help home</a> | <a href='ADDON_GUIDE.md'>Markdown</a> | <a href='addon-reference.html'>Raw reference</a></p></div></header><main class='wrap'><nav><a href='#start'>How to use</a>")
foreach ($section in $sections) {
    [void]$html.AppendLine("<a href='#$($section.Id)'>$(ConvertTo-Html $section.Title)</a>")
}
[void]$html.AppendLine("<a href='#gdscript'>GDScript addons</a><a href='#skins'>Skin catalog</a><a href='#templates'>Template scenes</a><a href='#examples'>Examples</a><a href='#verify'>Verification</a></nav><div>")
[void]$html.AppendLine("<section id='start'><h2>Inventory</h2><div class='inventory'><div class='metric'><strong>$($csFiles.Count)</strong>C# source files</div><div class='metric'><strong>$($classes.Count)</strong>public classes</div><div class='metric'><strong>$globalCount</strong>editor-addable types</div><div class='metric'><strong>$($gdFiles.Count)</strong>GDScript files</div><div class='metric'><strong>$($templateFiles.Count)</strong>template scenes</div><div class='metric'><strong>$($textureFiles.Count)</strong>texture assets</div><div class='metric'><strong>$($audioFiles.Count)</strong>audio assets</div><div class='metric'><strong>$($genreRows.Count)</strong>skin genres</div></div>")
[void]$html.AppendLine("<div class='callout'><strong>Use the addon as Godot nodes first.</strong> Create the scene tree in the editor, add Beep components from Add Node, set exported NodePath/resource fields in the inspector, and reserve code for game-specific behavior.</div>")
[void]$html.AppendLine("<div class='grid'><div class='card'><h3>World scenes</h3><p>Use Node2D plus terrain, grid, placement, navigation, roads, jobs, resources, workers, and camera/HUD nodes.</p></div><div class='card'><h3>HUD and menus</h3><p>Use Control scenes with ThemePresetComponent and Game UI Kit controls. Keep panels authored in the scene.</p></div><div class='card'><h3>Data</h3><p>Use Resource classes for item, inventory, crafting, weather, game state, and template definitions.</p></div><div class='card'><h3>Templates</h3><p>Instance templates as starting points, replace art/data, and preserve expected named nodes.</p></div></div></section>")

foreach ($section in $sections) {
    $sectionTypes = @($classes | Where-Object { $section.Areas -contains $_.Area })
    [void]$html.AppendLine("<section id='$($section.Id)'><h2>$(ConvertTo-Html $section.Title)</h2><p>$(ConvertTo-Html $section.Purpose)</p><p><strong>Use when:</strong> $(ConvertTo-Html $section.UseWhen)</p><p><strong>Scene pattern:</strong> $(ConvertTo-Html $section.ScenePattern)</p><p class='areas'>Source areas: $(ConvertTo-Html ($section.Areas -join ', '))</p><p><strong>$($sectionTypes.Count)</strong> public types in this section.</p>")
    Append-TypeTableHtml $html $sectionTypes
    [void]$html.AppendLine("</section>")
}

[void]$html.AppendLine("<section id='gdscript'><h2>GDScript Addons</h2><p>These scripts support the GDScript-side UI addon and optional bridge tooling.</p><table><thead><tr><th>Addon</th><th>Script</th><th>Extends</th><th>Source</th></tr></thead><tbody>")
foreach ($script in $gdScripts | Sort-Object Addon, Name) {
    [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $script.Addon)</td><td><code>$(ConvertTo-Html $script.Name)</code></td><td><code>$(ConvertTo-Html $script.Extends)</code></td><td><code>$(ConvertTo-Html $script.Path)</code></td></tr>")
}
[void]$html.AppendLine("</tbody></table></section>")

[void]$html.AppendLine("<section id='skins'><h2>Skin Catalog</h2><p>Theme data lives under <code>addons/beep_game_builder_cs/catalogs/skins</code>. Use ThemePresetComponent and kit controls to apply these palettes and geometry values consistently.</p><table><thead><tr><th>Genre</th><th>Themes</th></tr></thead><tbody>")
foreach ($genre in $genreRows) {
    [void]$html.AppendLine("<tr><td><code>$(ConvertTo-Html $genre.Genre)</code></td><td>$(ConvertTo-Html $genre.Themes)</td></tr>")
}
[void]$html.AppendLine("</tbody></table></section>")

[void]$html.AppendLine("<section id='templates'><h2>Template Scenes</h2><p>Template scenes are starter scene trees for genre screens, menus, gameplay shells, entities, particles, and builder examples.</p><table><thead><tr><th>Template</th><th>Root</th><th>Source</th></tr></thead><tbody>")
foreach ($scene in $sceneRows) {
    [void]$html.AppendLine("<tr><td><code>$(ConvertTo-Html $scene.Name)</code></td><td><code>$(ConvertTo-Html $scene.Root)</code></td><td><code>$(ConvertTo-Html $scene.Path)</code></td></tr>")
}
[void]$html.AppendLine("</tbody></table></section>")

[void]$html.AppendLine("<section id='examples'><h2>Runnable Examples</h2><table><thead><tr><th>Scene</th><th>What it demonstrates</th></tr></thead><tbody><tr><td><code>addons/beep_game_builder_cs/templates/scenes/terrain/grid_world_kit_hud_example.tscn</code></td><td>Settlers-style builder slice with generated terrain, grid systems, resources, jobs, workers, tool buttons, and kit HUD.</td></tr><tr><td><code>addons/beep_game_builder_cs/templates/scenes/terrain/base_worker_templates_example.tscn</code></td><td>Depot and worker templates with art-backed Sprite2D nodes.</td></tr></tbody></table></section>")
[void]$html.AppendLine("<section id='verify'><h2>Verification</h2><p>Use these gates after addon changes:</p><table><thead><tr><th>Check</th><th>Command</th></tr></thead><tbody><tr><td>C# build</td><td><code>dotnet build Beep.Godot.csproj --no-restore</code></td></tr><tr><td>Godot runtime smoke</td><td><code>powershell -ExecutionPolicy Bypass -File tests/runtime_smoke.ps1 -GodotCommand H:/dev/Godot/Godot_v4.7-stable_mono_win64.exe -TimeoutSeconds 90</code></td></tr><tr><td>Render probe</td><td><code>powershell -ExecutionPolicy Bypass -File tests/render_scene_probe.ps1 -GodotCommand H:/dev/Godot/Godot_v4.7-stable_mono_win64.exe -ScenePath res://addons/beep_game_builder_cs/templates/scenes/terrain/grid_world_kit_hud_example.tscn -TimeoutSeconds 90</code></td></tr></tbody></table></section>")
[void]$html.AppendLine("</div></main></body></html>")

[System.IO.File]::WriteAllText($guideHtmlPath, (ConvertTo-AsciiText $html.ToString()), [System.Text.UTF8Encoding]::new($false))

Write-Output "Generated $guideMdPath and $guideHtmlPath from $($csFiles.Count) C# files, $($classes.Count) public classes, $($gdFiles.Count) GDScript files, and $($templateFiles.Count) template scenes."
