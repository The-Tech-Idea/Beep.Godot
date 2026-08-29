param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

$addonsRoot = Join-Path $ProjectRoot "addons"
$docsRoot = Join-Path $ProjectRoot "docs"
$markdownPath = Join-Path $docsRoot "ADDON_REFERENCE.md"
$htmlPath = Join-Path $docsRoot "addon-reference.html"

function ConvertTo-Summary([string]$source, [int]$classIndex) {
    $prefixStart = [Math]::Max(0, $classIndex - 1800)
    $prefix = $source.Substring($prefixStart, $classIndex - $prefixStart)
    $matches = [regex]::Matches($prefix, '(?s)///\s*<summary>\s*(.*?)\s*///\s*</summary>')
    if ($matches.Count -eq 0) {
        return ""
    }

    $summary = $matches[$matches.Count - 1].Groups[1].Value
    $summary = [regex]::Replace($summary, '///', ' ')
    $summary = [regex]::Replace($summary, '<[^>]+>', ' ')
    $summary = [regex]::Replace($summary, '\b[Ss]ee\s+phase-\d+\.?\s*', '')
    $summary = [regex]::Replace($summary, '\b[Pp]hase\s+\d+\s*[-:]\s*', '')
    $summary = [regex]::Replace($summary, '\b[Pp]hase-\d+\b', 'review pass')
    $summary = [regex]::Replace($summary, '\s+', ' ').Trim()
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

function Get-RelativePath([string]$from, [string]$to) {
    $fromPath = [System.IO.Path]::GetFullPath($from).TrimEnd('\') + '\'
    $fromUri = [System.Uri]::new($fromPath)
    $toUri = [System.Uri]::new([System.IO.Path]::GetFullPath($to))
    return [System.Uri]::UnescapeDataString($fromUri.MakeRelativeUri($toUri).ToString()).Replace('\', '/')
}

$csFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File -Filter "*.cs" | Sort-Object FullName
$gdFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File -Filter "*.gd" | Sort-Object FullName
$sceneFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File -Filter "*.tscn" | Sort-Object FullName
$classes = [System.Collections.Generic.List[object]]::new()

foreach ($file in $csFiles) {
    $source = [System.IO.File]::ReadAllText($file.FullName)
    $namespaceMatch = [regex]::Match($source, '(?m)^\s*namespace\s+([\w\.]+)')
    $namespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups[1].Value } else { "Global" }
    $relativePath = Get-RelativePath $ProjectRoot $file.FullName
    $area = Get-RelativePath $addonsRoot $file.DirectoryName
    if ([string]::IsNullOrWhiteSpace($area)) { $area = "root" }

    $pattern = '(?ms)(?<attributes>(?:\s*\[[^\]]+\]\s*)*)\s*public\s+(?<abstract>abstract\s+)?(?<partial>partial\s+)?class\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<base>[^\{\r\n]+))?'
    foreach ($match in [regex]::Matches($source, $pattern)) {
        $attributes = $match.Groups['attributes'].Value
        $classes.Add([PSCustomObject]@{
            Addon = (Get-RelativePath $addonsRoot $file.FullName).Split('/')[0]
            Area = $area
            Name = $match.Groups['name'].Value
            Namespace = $namespace
            Base = $match.Groups['base'].Value.Trim()
            GlobalClass = $attributes -match '\[GlobalClass\]'
            Summary = ConvertTo-Summary $source $match.Index
            Path = $relativePath
        })
    }
}

$gdScripts = foreach ($file in $gdFiles) {
    $source = [System.IO.File]::ReadAllText($file.FullName)
    $classNameMatch = [regex]::Match($source, '(?m)^\s*class_name\s+([A-Za-z_]\w*)')
    [PSCustomObject]@{
        Addon = (Get-RelativePath $addonsRoot $file.FullName).Split('/')[0]
        Name = if ($classNameMatch.Success) { $classNameMatch.Groups[1].Value } else { [System.IO.Path]::GetFileNameWithoutExtension($file.Name) }
        Path = Get-RelativePath $ProjectRoot $file.FullName
    }
}

$genres = Get-ChildItem -LiteralPath (Join-Path $addonsRoot "beep_game_builder_cs/catalogs/skins") -Directory | Sort-Object Name | ForEach-Object {
    $themesPath = Join-Path $_.FullName "themes"
    $themeCount = if (Test-Path -LiteralPath $themesPath) { (Get-ChildItem -LiteralPath $themesPath -Directory).Count } else { 0 }
    "$($_.Name) ($themeCount themes)"
}

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss K"
$globalClassCount = ($classes | Where-Object GlobalClass).Count

$markdown = [System.Text.StringBuilder]::new()
[void]$markdown.AppendLine("# Beep.Godot Addon Reference")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Generated from every C# and GDScript source file under addons/ on $generatedAt. Do not edit this file by hand; run powershell -ExecutionPolicy Bypass -File tools/Generate-AddonReference.ps1 after changing addon source.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Inventory")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Item | Count |")
[void]$markdown.AppendLine("| --- | ---: |")
[void]$markdown.AppendLine("| C# source files | $($csFiles.Count) |")
[void]$markdown.AppendLine("| Public C# classes | $($classes.Count) |")
[void]$markdown.AppendLine("| Godot [GlobalClass] types | $globalClassCount |")
[void]$markdown.AppendLine("| GDScript files | $($gdFiles.Count) |")
[void]$markdown.AppendLine("| Template scenes | $($sceneFiles.Count) |")
[void]$markdown.AppendLine("| Skin genres | $($genres.Count) |")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Addon Boundaries")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("- beep_game_builder_cs: C# gameplay components, world systems, UI components, Game UI Kit, templates, skins, and editor tooling.")
[void]$markdown.AppendLine("- beep_ui: GDScript UI skin/preset support.")
[void]$markdown.AppendLine("- godot_mcp: optional editor/runtime MCP bridge. It is not required by normal gameplay scenes.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Skin Catalog")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine(($genres -join ", ") + ".")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## C# Public Types")

foreach ($areaGroup in $classes | Group-Object Addon, Area | Sort-Object Name) {
    $sample = $areaGroup.Group[0]
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("### $($sample.Addon) / $($sample.Area)")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("| Type | Base | Editor-addable | Summary | Source |")
    [void]$markdown.AppendLine("| --- | --- | --- | --- | --- |")
    foreach ($type in $areaGroup.Group | Sort-Object Name) {
        $base = if ([string]::IsNullOrWhiteSpace($type.Base)) { "-" } else { $type.Base.Replace("|", "\\|") }
        $summary = if ([string]::IsNullOrWhiteSpace($type.Summary)) { "-" } else { $type.Summary.Replace("|", "\\|") }
        $editorAddable = if ($type.GlobalClass) { "Yes" } else { "No" }
        [void]$markdown.AppendLine("| $($type.Name) | $base | $editorAddable | $summary | $($type.Path) |")
    }
}

[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## GDScript Files")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Addon | Script | Source |")
[void]$markdown.AppendLine("| --- | --- | --- |")
foreach ($script in $gdScripts | Sort-Object Addon, Name) {
    [void]$markdown.AppendLine("| $($script.Addon) | $($script.Name) | $($script.Path) |")
}

[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Template Scenes")
[void]$markdown.AppendLine()
foreach ($scene in $sceneFiles) {
    $relativeScene = Get-RelativePath $ProjectRoot $scene.FullName
    [void]$markdown.AppendLine("- $relativeScene")
}

[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Runnable Examples")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("- tests/examples/grid_world_kit_hud_example.tscn: playable top-down builder slice with terrain, resources, tasks, jobs, workers, and kit HUD.")
[void]$markdown.AppendLine("- tests/examples/base_worker_templates_example.tscn: reusable depot and worker templates.")

[System.IO.File]::WriteAllText($markdownPath, (ConvertTo-AsciiText $markdown.ToString()), [System.Text.UTF8Encoding]::new($false))

$html = [System.Text.StringBuilder]::new()
[void]$html.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>Beep.Godot Addon Reference</title><style>body{margin:0;background:#141a1d;color:#e7edf0;font:15px/1.45 Inter,Segoe UI,sans-serif}header{background:#202b2f;border-bottom:3px solid #dc9a27;padding:28px}main{max-width:1380px;margin:auto;padding:24px}h1,h2,h3{color:#fff}h1{margin:0;font-size:32px}p,.muted{color:#b9c5c9}a{color:#f2bd55}table{border-collapse:collapse;width:100%;margin:14px 0 30px;background:#1b2428}th,td{padding:8px 10px;border:1px solid #35454c;vertical-align:top;text-align:left}th{background:#29373c;color:#fff;position:sticky;top:0}code{font:13px Consolas,monospace;color:#f5d48d}.pill{display:inline-block;background:#29444d;color:#cceef5;padding:3px 8px;border-radius:4px;margin:3px}</style></head><body>")
[void]$html.AppendLine("<header><h1>Beep.Godot Addon Reference</h1><p>Generated from all addon source on $(ConvertTo-Html $generatedAt). <a href='index.html'>Help home</a> | <a href='ADDON_REFERENCE.md'>Markdown reference</a></p></header><main>")
[void]$html.AppendLine("<h2>Inventory</h2><p><span class='pill'>$($csFiles.Count) C# files</span><span class='pill'>$($classes.Count) public classes</span><span class='pill'>$globalClassCount editor-addable types</span><span class='pill'>$($gdFiles.Count) GDScript files</span><span class='pill'>$($sceneFiles.Count) templates</span></p>")
[void]$html.AppendLine("<h2>Skin Catalog</h2><p>$(ConvertTo-Html ($genres -join ', ')).</p>")
foreach ($areaGroup in $classes | Group-Object Addon, Area | Sort-Object Name) {
    $sample = $areaGroup.Group[0]
    [void]$html.AppendLine("<section><h2>$(ConvertTo-Html $sample.Addon) / $(ConvertTo-Html $sample.Area)</h2><table><thead><tr><th>Type</th><th>Base</th><th>Editor-addable</th><th>Summary</th><th>Source</th></tr></thead><tbody>")
    foreach ($type in $areaGroup.Group | Sort-Object Name) {
        $editorAddable = if ($type.GlobalClass) { "Yes" } else { "No" }
        [void]$html.AppendLine("<tr><td><code>$(ConvertTo-Html $type.Name)</code></td><td><code>$(ConvertTo-Html $type.Base)</code></td><td>$editorAddable</td><td>$(ConvertTo-Html $type.Summary)</td><td><code>$(ConvertTo-Html $type.Path)</code></td></tr>")
    }
    [void]$html.AppendLine("</tbody></table></section>")
}
[void]$html.AppendLine("<h2>Template Scenes</h2><ul>")
foreach ($scene in $sceneFiles) {
    [void]$html.AppendLine("<li><code>$(ConvertTo-Html (Get-RelativePath $ProjectRoot $scene.FullName))</code></li>")
}
[void]$html.AppendLine("</ul></main></body></html>")
[System.IO.File]::WriteAllText($htmlPath, (ConvertTo-AsciiText $html.ToString()), [System.Text.UTF8Encoding]::new($false))

Write-Output "Generated $markdownPath and $htmlPath from $($csFiles.Count) C# files, $($classes.Count) public classes, and $($gdFiles.Count) GDScript files."
