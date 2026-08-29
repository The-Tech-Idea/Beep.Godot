param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

$addonsRoot = Join-Path $ProjectRoot "addons"
$docsRoot = Join-Path $ProjectRoot "docs"
$markdownPath = Join-Path $docsRoot "ADDON_FULL_SCAN.md"
$htmlPath = Join-Path $docsRoot "addon-full-scan.html"

function Get-RelativePath([string]$from, [string]$to) {
    $fromPath = [System.IO.Path]::GetFullPath($from).TrimEnd('\') + '\'
    $fromUri = [System.Uri]::new($fromPath)
    $toUri = [System.Uri]::new([System.IO.Path]::GetFullPath($to))
    return [System.Uri]::UnescapeDataString($fromUri.MakeRelativeUri($toUri).ToString()).Replace('\', '/')
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

function Get-AddonSection([string]$relativePath) {
    $parts = $relativePath.Split('/')
    if ($parts.Count -eq 0) { return "unknown" }
    if ($parts[0] -ne "beep_game_builder_cs") { return $parts[0] }
    if ($parts.Count -eq 1) { return "beep_game_builder_cs/root" }

    switch ($parts[1]) {
        "ecs" {
            if ($parts.Count -ge 3) { return "beep_game_builder_cs/ecs/$($parts[2])" }
            return "beep_game_builder_cs/ecs/root"
        }
        "templates" {
            if ($parts.Count -ge 3) { return "beep_game_builder_cs/templates/$($parts[2])" }
            return "beep_game_builder_cs/templates"
        }
        "catalogs" {
            if ($parts.Count -ge 3) { return "beep_game_builder_cs/catalogs/$($parts[2])" }
            return "beep_game_builder_cs/catalogs"
        }
        "textures" {
            if ($parts.Count -ge 3) { return "beep_game_builder_cs/textures/$($parts[2])" }
            return "beep_game_builder_cs/textures"
        }
        "audio" {
            if ($parts.Count -ge 3) { return "beep_game_builder_cs/audio/$($parts[2])" }
            return "beep_game_builder_cs/audio"
        }
        default { return "beep_game_builder_cs/$($parts[1])" }
    }
}

function Get-FileKind([string]$extension) {
    switch ($extension.ToLowerInvariant()) {
        ".cs" { "C# source" }
        ".gd" { "GDScript source" }
        ".tscn" { "Godot scene" }
        ".json" { "JSON catalog/config" }
        ".cfg" { "Godot config" }
        ".template" { "Template text" }
        ".gdshader" { "Shader" }
        ".png" { "Texture/image" }
        ".jpg" { "Texture/image" }
        ".jpeg" { "Texture/image" }
        ".svg" { "Vector image" }
        ".ogg" { "Audio" }
        ".mp3" { "Audio" }
        ".wav" { "Audio" }
        ".ttf" { "Font" }
        ".uid" { "Godot UID sidecar" }
        ".import" { "Godot import metadata" }
        ".md" { "Markdown" }
        ".txt" { "Text" }
        ".csv" { "CSV" }
        ".translation" { "Godot translation" }
        default { if ([string]::IsNullOrWhiteSpace($extension)) { "Extensionless" } else { "Other" } }
    }
}

function Read-TextFile([System.IO.FileInfo]$file) {
    $textExtensions = @(".cs", ".gd", ".tscn", ".json", ".cfg", ".template", ".gdshader", ".md", ".txt", ".csv", ".translation", ".import", ".svg", ".sh", ".gdignore", ".gitkeep")
    if ($textExtensions -notcontains $file.Extension.ToLowerInvariant()) { return $null }
    return [System.IO.File]::ReadAllText($file.FullName)
}

function Get-CSharpDetail([System.IO.FileInfo]$file, [string]$source) {
    $namespaceMatch = [regex]::Match($source, '(?m)^\s*namespace\s+([\w\.]+)')
    $classes = [regex]::Matches($source, '(?m)^\s*(?<attributes>(?:\[[^\]]+\]\s*)*)\s*(?<access>public|internal|private|protected)?\s*(?<mods>(?:(?:abstract|sealed|static|partial|readonly|record|new)\s+)*)?(?<kind>class|struct|interface|record|enum)\s+(?<name>[A-Za-z_]\w*)\s*(?<base>:[^\{\r\n]+)?') | ForEach-Object {
        $suffix = if ($_.Groups['attributes'].Value -match '\[GlobalClass\]') { " [GlobalClass]" } else { "" }
        $prefix = (($_.Groups['access'].Value, $_.Groups['mods'].Value, $_.Groups['kind'].Value) -join " ").Trim() -replace '\s+', ' '
        if ([string]::IsNullOrWhiteSpace($_.Groups['base'].Value)) {
            "$prefix $($_.Groups['name'].Value)$suffix"
        } else {
            "$prefix $($_.Groups['name'].Value) $($_.Groups['base'].Value.Trim())$suffix"
        }
    }
    $exports = [regex]::Matches($source, '(?ms)^\s*\[Export[^\]]*\]\s*(?<decl>(?:public|private|protected|internal)\s+[^\r\n;]+(?:\{[^\r\n]*\}|;|=\s*[^\r\n;]+;))') | ForEach-Object {
        ($_.Groups['decl'].Value -replace '\s+', ' ').Trim()
    } | Sort-Object -Unique
    $signals = [regex]::Matches($source, '(?m)^\s*\[Signal\]\s*(?<decl>public\s+delegate\s+void\s+[^\r\n;]+;)') | ForEach-Object {
        ($_.Groups['decl'].Value -replace '\s+', ' ').Trim()
    } | Sort-Object -Unique
    $methods = [regex]::Matches($source, '(?m)^\s*(?<decl>(?:public|private|protected|internal)\s+(?:(?:override|virtual|static|async|sealed|new|partial)\s+)*(?:[A-Za-z_][\w<>,\[\]\.?]+\s+)+[A-Za-z_]\w*\s*\([^\)]*\))') | ForEach-Object {
        ($_.Groups['decl'].Value -replace '\s+', ' ').Trim()
    } | Sort-Object -Unique
    $enums = [regex]::Matches($source, '(?ms)\benum\s+(?<name>[A-Za-z_]\w*)\s*\{(?<body>.*?)\}') | ForEach-Object {
        $body = ($_.Groups['body'].Value -replace '//.*', '' -replace '/\*.*?\*/', '' -replace '\s+', ' ').Trim().Trim(',')
        "enum $($_.Groups['name'].Value) { $body }"
    } | Sort-Object -Unique

    return [PSCustomObject]@{
        Namespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups[1].Value } else { "-" }
        Classes = $classes
        Exports = $exports
        Signals = $signals
        Methods = $methods
        Enums = $enums
    }
}

function Get-GdDetail([string]$source) {
    $classMatch = [regex]::Match($source, '(?m)^\s*class_name\s+([A-Za-z_]\w*)')
    $extendsMatch = [regex]::Match($source, '(?m)^\s*extends\s+([A-Za-z_]\w*)')
    $exports = [regex]::Matches($source, '(?m)^\s*(?<decl>@export[^\r\n]*)') | ForEach-Object { $_.Groups['decl'].Value.Trim() } | Sort-Object -Unique
    $signals = [regex]::Matches($source, '(?m)^\s*(?<decl>signal\s+[^\r\n]+)') | ForEach-Object { $_.Groups['decl'].Value.Trim() } | Sort-Object -Unique
    $functions = [regex]::Matches($source, '(?m)^\s*(?<decl>func\s+[^\r\n]+)') | ForEach-Object { $_.Groups['decl'].Value.Trim() } | Sort-Object -Unique

    return [PSCustomObject]@{
        ClassName = if ($classMatch.Success) { $classMatch.Groups[1].Value } else { "-" }
        Extends = if ($extendsMatch.Success) { $extendsMatch.Groups[1].Value } else { "-" }
        Exports = $exports
        Signals = $signals
        Functions = $functions
    }
}

function Get-SceneDetail([string]$source) {
    $rootMatch = [regex]::Match($source, '(?m)^\[node name="([^"]+)" type="([^"]+)"')
    $nodeCount = ([regex]::Matches($source, '(?m)^\[node ')).Count
    $extCount = ([regex]::Matches($source, '(?m)^\[ext_resource ')).Count
    $subCount = ([regex]::Matches($source, '(?m)^\[sub_resource ')).Count
    $nodes = [regex]::Matches($source, '(?m)^\[node name="(?<name>[^"]+)" type="(?<type>[^"]+)"(?: parent="(?<parent>[^"]+)")?') | ForEach-Object {
        $parent = if ([string]::IsNullOrWhiteSpace($_.Groups['parent'].Value)) { "." } else { $_.Groups['parent'].Value }
        "$($_.Groups['name'].Value):$($_.Groups['type'].Value) parent=$parent"
    }
    $externalResources = [regex]::Matches($source, '(?m)^\[ext_resource (?<attrs>[^\]]+)\]') | ForEach-Object {
        ($_.Groups['attrs'].Value -replace '\s+', ' ').Trim()
    }
    return [PSCustomObject]@{
        Root = if ($rootMatch.Success) { "$($rootMatch.Groups[1].Value) : $($rootMatch.Groups[2].Value)" } else { "-" }
        Nodes = $nodeCount
        ExternalResources = $extCount
        SubResources = $subCount
        NodeList = $nodes
        ExternalResourceList = $externalResources
    }
}

function Get-JsonDetail([string]$source) {
    try {
        $json = $source | ConvertFrom-Json -ErrorAction Stop
        $keys = if ($json -is [System.Array]) {
            @("array[$($json.Count)]")
        } else {
            @($json.PSObject.Properties | ForEach-Object Name)
        }
        return ($keys -join ", ")
    } catch {
        return "invalid or nonstandard JSON"
    }
}

function Join-All([object[]]$values) {
    if ($null -eq $values -or $values.Count -eq 0) { return "-" }
    return ($values -join "; ")
}

function ConvertTo-MarkdownCell([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return "-" }
    return (($value.Replace("|", "\|")) -replace "`r?`n", "<br>")
}

function ConvertTo-HtmlList([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -eq "-") { return "-" }
    $items = $value.Split(";") | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    if ($items.Count -eq 0) { return "-" }
    return "<ul>" + (($items | ForEach-Object { "<li><code>$(ConvertTo-Html $_)</code></li>" }) -join "") + "</ul>"
}

$allFiles = Get-ChildItem -LiteralPath $addonsRoot -Recurse -File | Sort-Object FullName
$rows = [System.Collections.Generic.List[object]]::new()
$codeRows = [System.Collections.Generic.List[object]]::new()
$sceneRows = [System.Collections.Generic.List[object]]::new()
$jsonRows = [System.Collections.Generic.List[object]]::new()
$resourceRefRows = [System.Collections.Generic.List[object]]::new()

foreach ($file in $allFiles) {
    $relative = Get-RelativePath $addonsRoot $file.FullName
    $projectRelative = Get-RelativePath $ProjectRoot $file.FullName
    $section = Get-AddonSection $relative
    $kind = Get-FileKind $file.Extension
    $source = Read-TextFile $file
    $lines = if ($null -eq $source) { 0 } else { ([regex]::Matches($source, "`n")).Count + 1 }
    $resourceRefs = @()
    if ($null -ne $source) {
        $resourceRefs = @([regex]::Matches($source, '(?:res|uid)://[^\s"''\)\]\}]+') | ForEach-Object { $_.Value } | Sort-Object -Unique)
        if ($resourceRefs.Count -gt 0) {
            $resourceRefRows.Add([PSCustomObject]@{
                Section = $section
                Path = $projectRelative
                References = Join-All $resourceRefs
            })
        }
    }

    $rows.Add([PSCustomObject]@{
        Section = $section
        Kind = $kind
        Extension = if ([string]::IsNullOrWhiteSpace($file.Extension)) { "(none)" } else { $file.Extension.ToLowerInvariant() }
        Bytes = $file.Length
        Lines = $lines
        Path = $projectRelative
    })

    if ($file.Extension -eq ".cs" -and $null -ne $source) {
        $detail = Get-CSharpDetail $file $source
        $codeRows.Add([PSCustomObject]@{
            Section = $section
            Language = "C#"
            Path = $projectRelative
            Namespace = $detail.Namespace
            TypeOrScript = Join-All @($detail.Classes)
            Enums = Join-All @($detail.Enums)
            Exports = Join-All @($detail.Exports)
            Signals = Join-All @($detail.Signals)
            PublicApi = Join-All @($detail.Methods)
            ResourceRefs = Join-All $resourceRefs
        })
    } elseif ($file.Extension -eq ".gd" -and $null -ne $source) {
        $detail = Get-GdDetail $source
        $codeRows.Add([PSCustomObject]@{
            Section = $section
            Language = "GDScript"
            Path = $projectRelative
            Namespace = "-"
            TypeOrScript = "$($detail.ClassName) extends $($detail.Extends)"
            Enums = "-"
            Exports = Join-All @($detail.Exports)
            Signals = Join-All @($detail.Signals)
            PublicApi = Join-All @($detail.Functions)
            ResourceRefs = Join-All $resourceRefs
        })
    } elseif ($file.Extension -eq ".tscn" -and $null -ne $source) {
        $detail = Get-SceneDetail $source
        $sceneRows.Add([PSCustomObject]@{
            Section = $section
            Path = $projectRelative
            Root = $detail.Root
            Nodes = $detail.Nodes
            ExternalResources = $detail.ExternalResources
            SubResources = $detail.SubResources
            NodeList = Join-All @($detail.NodeList)
            ExternalResourceList = Join-All @($detail.ExternalResourceList)
            ResourceRefs = Join-All $resourceRefs
        })
    } elseif ($file.Extension -eq ".json" -and $null -ne $source) {
        $jsonRows.Add([PSCustomObject]@{
            Section = $section
            Path = $projectRelative
            Keys = Get-JsonDetail $source
        })
    }
}

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss K"
$sectionGroups = @($rows | Group-Object Section | Sort-Object Name)
$extensionGroups = @($rows | Group-Object Extension | Sort-Object @{ Expression = "Count"; Descending = $true }, Name)
$kindGroups = @($rows | Group-Object Kind | Sort-Object @{ Expression = "Count"; Descending = $true }, Name)
$textFilesRead = ($rows | Where-Object Lines -gt 0).Count
$binaryOrOpaque = $allFiles.Count - $textFilesRead

$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# Beep.Godot Addon Full Scan")
[void]$md.AppendLine()
[void]$md.AppendLine("Generated from every file under addons on $generatedAt. Text/source files were opened and parsed; binary/opaque files are inventoried with size, type, and section.")
[void]$md.AppendLine()
[void]$md.AppendLine("## Coverage")
[void]$md.AppendLine()
[void]$md.AppendLine("| Item | Count |")
[void]$md.AppendLine("| --- | ---: |")
[void]$md.AppendLine("| Total addon files | $($allFiles.Count) |")
[void]$md.AppendLine("| Text/source files read | $textFilesRead |")
[void]$md.AppendLine("| Binary/opaque files inventoried | $binaryOrOpaque |")
[void]$md.AppendLine("| Sections | $($sectionGroups.Count) |")
[void]$md.AppendLine("| C#/GDScript files parsed | $($codeRows.Count) |")
[void]$md.AppendLine("| Scene files parsed | $($sceneRows.Count) |")
[void]$md.AppendLine("| JSON files parsed | $($jsonRows.Count) |")
[void]$md.AppendLine("| Text files with Godot resource references | $($resourceRefRows.Count) |")
[void]$md.AppendLine()
[void]$md.AppendLine("## File Types")
[void]$md.AppendLine()
[void]$md.AppendLine("| Extension | Count |")
[void]$md.AppendLine("| --- | ---: |")
foreach ($group in $extensionGroups) {
    [void]$md.AppendLine("| $($group.Name) | $($group.Count) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Content Kinds")
[void]$md.AppendLine()
[void]$md.AppendLine("| Kind | Count |")
[void]$md.AppendLine("| --- | ---: |")
foreach ($group in $kindGroups) {
    [void]$md.AppendLine("| $($group.Name) | $($group.Count) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Code API Scan")
[void]$md.AppendLine()
[void]$md.AppendLine("| Section | Language | Type/script | Enums | Exports | Signals | Functions/methods | Resource refs | Source |")
[void]$md.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |")
foreach ($row in $codeRows | Sort-Object Section, Path) {
    [void]$md.AppendLine("| $($row.Section) | $($row.Language) | $(ConvertTo-MarkdownCell $row.TypeOrScript) | $(ConvertTo-MarkdownCell $row.Enums) | $(ConvertTo-MarkdownCell $row.Exports) | $(ConvertTo-MarkdownCell $row.Signals) | $(ConvertTo-MarkdownCell $row.PublicApi) | $(ConvertTo-MarkdownCell $row.ResourceRefs) | $($row.Path) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Code File Detail")
[void]$md.AppendLine()
foreach ($row in $codeRows | Sort-Object Section, Path) {
    [void]$md.AppendLine("### $($row.Path)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("- Section: $($row.Section)")
    [void]$md.AppendLine("- Language: $($row.Language)")
    [void]$md.AppendLine("- Namespace: $($row.Namespace)")
    [void]$md.AppendLine("- Types/scripts: $(ConvertTo-MarkdownCell $row.TypeOrScript)")
    [void]$md.AppendLine("- Enums: $(ConvertTo-MarkdownCell $row.Enums)")
    [void]$md.AppendLine("- Exports: $(ConvertTo-MarkdownCell $row.Exports)")
    [void]$md.AppendLine("- Signals: $(ConvertTo-MarkdownCell $row.Signals)")
    [void]$md.AppendLine("- Functions/methods: $(ConvertTo-MarkdownCell $row.PublicApi)")
    [void]$md.AppendLine("- Resource refs: $(ConvertTo-MarkdownCell $row.ResourceRefs)")
    [void]$md.AppendLine()
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Scene Scan")
[void]$md.AppendLine()
[void]$md.AppendLine("| Section | Root | Nodes | External resources | Subresources | Resource refs | Source |")
[void]$md.AppendLine("| --- | --- | ---: | ---: | ---: | --- | --- |")
foreach ($row in $sceneRows | Sort-Object Section, Path) {
    [void]$md.AppendLine("| $($row.Section) | $(ConvertTo-MarkdownCell $row.Root) | $($row.Nodes) | $($row.ExternalResources) | $($row.SubResources) | $(ConvertTo-MarkdownCell $row.ResourceRefs) | $($row.Path) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Scene File Detail")
[void]$md.AppendLine()
foreach ($row in $sceneRows | Sort-Object Section, Path) {
    [void]$md.AppendLine("### $($row.Path)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("- Section: $($row.Section)")
    [void]$md.AppendLine("- Root: $(ConvertTo-MarkdownCell $row.Root)")
    [void]$md.AppendLine("- Node count: $($row.Nodes)")
    [void]$md.AppendLine("- Nodes: $(ConvertTo-MarkdownCell $row.NodeList)")
    [void]$md.AppendLine("- External resources: $(ConvertTo-MarkdownCell $row.ExternalResourceList)")
    [void]$md.AppendLine("- Resource refs: $(ConvertTo-MarkdownCell $row.ResourceRefs)")
    [void]$md.AppendLine()
}
[void]$md.AppendLine()
[void]$md.AppendLine("## JSON Catalog Scan")
[void]$md.AppendLine()
[void]$md.AppendLine("| Section | Top-level keys | Source |")
[void]$md.AppendLine("| --- | --- | --- |")
foreach ($row in $jsonRows | Sort-Object Section, Path) {
    [void]$md.AppendLine("| $($row.Section) | $($row.Keys.Replace('|','\|')) | $($row.Path) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Godot Resource Reference Scan")
[void]$md.AppendLine()
[void]$md.AppendLine("| Section | Resource refs | Source |")
[void]$md.AppendLine("| --- | --- | --- |")
foreach ($row in $resourceRefRows | Sort-Object Section, Path) {
    [void]$md.AppendLine("| $($row.Section) | $(ConvertTo-MarkdownCell $row.References) | $($row.Path) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Section File Inventory")
foreach ($group in $sectionGroups) {
    $bytes = ($group.Group | Measure-Object Bytes -Sum).Sum
    [void]$md.AppendLine()
    [void]$md.AppendLine("### $($group.Name)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("Files: $($group.Count). Bytes: $bytes.")
    [void]$md.AppendLine()
    [void]$md.AppendLine("| Kind | Extension | Bytes | Lines read | Source |")
    [void]$md.AppendLine("| --- | --- | ---: | ---: | --- |")
    foreach ($row in $group.Group | Sort-Object Path) {
        [void]$md.AppendLine("| $($row.Kind) | $($row.Extension) | $($row.Bytes) | $($row.Lines) | $($row.Path) |")
    }
}

[System.IO.File]::WriteAllText($markdownPath, (ConvertTo-AsciiText $md.ToString()), [System.Text.UTF8Encoding]::new($false))

$html = [System.Text.StringBuilder]::new()
[void]$html.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>Beep.Godot Addon Full Scan</title>")
[void]$html.AppendLine("<style>:root{--bg:#f5f4ef;--fg:#1f2726;--muted:#63706d;--panel:#fffefa;--panel2:#ece6d9;--line:#cabfad;--accent:#28666e;--accent2:#b86e23;--code:#17353a}@media(prefers-color-scheme:dark){:root{--bg:#121716;--fg:#eef3ef;--muted:#aebbb6;--panel:#1a211f;--panel2:#242d29;--line:#3a4642;--accent:#74cbd1;--accent2:#e4a254;--code:#d9f5f7}}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.45 system-ui,-apple-system,Segoe UI,sans-serif}header{background:var(--panel);border-bottom:1px solid var(--line)}.wrap{max-width:1500px;margin:0 auto;padding:22px}main.wrap{display:grid;grid-template-columns:280px 1fr;gap:22px;align-items:start}nav{position:sticky;top:14px;border:1px solid var(--line);background:var(--panel);border-radius:8px;padding:12px;max-height:calc(100vh - 28px);overflow:auto}nav a{display:block;color:var(--accent);text-decoration:none;padding:6px 8px;border-radius:6px}nav a:hover{background:var(--panel2)}section{border-bottom:1px solid var(--line);padding-bottom:24px;margin-bottom:24px}h1,h2,h3{line-height:1.2;margin:0 0 12px}h1{font-size:32px}h2{font-size:24px}h3{font-size:17px;margin-top:18px}p{margin:0 0 12px;color:var(--muted)}.metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px;margin:14px 0}.metric{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:11px}.metric strong{display:block;font-size:23px;color:var(--accent2)}table{width:100%;border-collapse:collapse;background:var(--panel);border:1px solid var(--line);margin:12px 0 18px}th,td{padding:7px 9px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left}th{background:var(--panel2);color:var(--fg);font-weight:700;position:sticky;top:0}code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;color:var(--code);font-size:.92em}.wide{overflow:auto}.small{font-size:12px}.muted{color:var(--muted)}@media(max-width:900px){main.wrap{grid-template-columns:1fr}nav{position:static;max-height:none}}</style></head><body>")
[void]$html.AppendLine("<header><div class='wrap'><h1>Beep.Godot Addon Full Scan</h1><p>Every file under <code>addons</code>, generated $(ConvertTo-Html $generatedAt). <a href='index.html'>Help home</a> | <a href='ADDON_FULL_SCAN.md'>Markdown</a></p></div></header><main class='wrap'><nav><a href='#coverage'>Coverage</a><a href='#file-types'>File types</a><a href='#code'>Code API scan</a><a href='#code-detail'>Code file detail</a><a href='#scenes'>Scene scan</a><a href='#scene-detail'>Scene file detail</a><a href='#json'>JSON catalogs</a><a href='#resource-refs'>Resource refs</a><a href='#sections'>Sections</a>")
foreach ($group in $sectionGroups) {
    $id = "section-" + (($group.Name -replace '[^A-Za-z0-9]+','-').Trim('-').ToLowerInvariant())
    [void]$html.AppendLine("<a class='small' href='#$id'>$(ConvertTo-Html $group.Name)</a>")
}
[void]$html.AppendLine("</nav><div>")
[void]$html.AppendLine("<section id='coverage'><h2>Coverage</h2><p>Text/source files were opened and parsed. Binary/opaque files are listed by path, section, size, and kind.</p><div class='metrics'><div class='metric'><strong>$($allFiles.Count)</strong>Total files</div><div class='metric'><strong>$textFilesRead</strong>Text/source read</div><div class='metric'><strong>$binaryOrOpaque</strong>Binary/opaque inventoried</div><div class='metric'><strong>$($sectionGroups.Count)</strong>Sections</div><div class='metric'><strong>$($codeRows.Count)</strong>Code files parsed</div><div class='metric'><strong>$($sceneRows.Count)</strong>Scenes parsed</div><div class='metric'><strong>$($jsonRows.Count)</strong>JSON files parsed</div><div class='metric'><strong>$($resourceRefRows.Count)</strong>Files with resource refs</div></div></section>")
[void]$html.AppendLine("<section id='file-types'><h2>File Types</h2><div class='wide'><table><thead><tr><th>Extension</th><th>Count</th></tr></thead><tbody>")
foreach ($group in $extensionGroups) {
    [void]$html.AppendLine("<tr><td><code>$(ConvertTo-Html $group.Name)</code></td><td>$($group.Count)</td></tr>")
}
[void]$html.AppendLine("</tbody></table></div><h3>Content Kinds</h3><div class='wide'><table><thead><tr><th>Kind</th><th>Count</th></tr></thead><tbody>")
foreach ($group in $kindGroups) {
    [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $group.Name)</td><td>$($group.Count)</td></tr>")
}
[void]$html.AppendLine("</tbody></table></div></section>")
[void]$html.AppendLine("<section id='code'><h2>Code API Scan</h2><p>C# and GDScript files with extracted classes/scripts, enums, exports, signals, functions/methods, and resource references.</p><div class='wide'><table><thead><tr><th>Section</th><th>Language</th><th>Type/script</th><th>Enums</th><th>Exports</th><th>Signals</th><th>Functions/methods</th><th>Resource refs</th><th>Source</th></tr></thead><tbody>")
foreach ($row in $codeRows | Sort-Object Section, Path) {
    [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $row.Section)</td><td>$($row.Language)</td><td>$(ConvertTo-HtmlList $row.TypeOrScript)</td><td>$(ConvertTo-HtmlList $row.Enums)</td><td>$(ConvertTo-HtmlList $row.Exports)</td><td>$(ConvertTo-HtmlList $row.Signals)</td><td>$(ConvertTo-HtmlList $row.PublicApi)</td><td>$(ConvertTo-HtmlList $row.ResourceRefs)</td><td><code>$(ConvertTo-Html $row.Path)</code></td></tr>")
}
[void]$html.AppendLine("</tbody></table></div></section>")
[void]$html.AppendLine("<section id='code-detail'><h2>Code File Detail</h2><p>One detail block for every C# and GDScript source file.</p>")
foreach ($row in $codeRows | Sort-Object Section, Path) {
    [void]$html.AppendLine("<article><h3><code>$(ConvertTo-Html $row.Path)</code></h3><table><tbody><tr><th>Section</th><td>$(ConvertTo-Html $row.Section)</td></tr><tr><th>Language</th><td>$($row.Language)</td></tr><tr><th>Namespace</th><td><code>$(ConvertTo-Html $row.Namespace)</code></td></tr><tr><th>Types/scripts</th><td>$(ConvertTo-HtmlList $row.TypeOrScript)</td></tr><tr><th>Enums</th><td>$(ConvertTo-HtmlList $row.Enums)</td></tr><tr><th>Exports</th><td>$(ConvertTo-HtmlList $row.Exports)</td></tr><tr><th>Signals</th><td>$(ConvertTo-HtmlList $row.Signals)</td></tr><tr><th>Functions/methods</th><td>$(ConvertTo-HtmlList $row.PublicApi)</td></tr><tr><th>Resource refs</th><td>$(ConvertTo-HtmlList $row.ResourceRefs)</td></tr></tbody></table></article>")
}
[void]$html.AppendLine("</section>")
[void]$html.AppendLine("<section id='scenes'><h2>Scene Scan</h2><div class='wide'><table><thead><tr><th>Section</th><th>Root</th><th>Nodes</th><th>External resources</th><th>Subresources</th><th>Resource refs</th><th>Source</th></tr></thead><tbody>")
foreach ($row in $sceneRows | Sort-Object Section, Path) {
    [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $row.Section)</td><td><code>$(ConvertTo-Html $row.Root)</code></td><td>$($row.Nodes)</td><td>$($row.ExternalResources)</td><td>$($row.SubResources)</td><td>$(ConvertTo-HtmlList $row.ResourceRefs)</td><td><code>$(ConvertTo-Html $row.Path)</code></td></tr>")
}
[void]$html.AppendLine("</tbody></table></div></section>")
[void]$html.AppendLine("<section id='scene-detail'><h2>Scene File Detail</h2><p>Every scene with its root, authored node list, external resources, and resource references.</p>")
foreach ($row in $sceneRows | Sort-Object Section, Path) {
    [void]$html.AppendLine("<article><h3><code>$(ConvertTo-Html $row.Path)</code></h3><table><tbody><tr><th>Section</th><td>$(ConvertTo-Html $row.Section)</td></tr><tr><th>Root</th><td><code>$(ConvertTo-Html $row.Root)</code></td></tr><tr><th>Node count</th><td>$($row.Nodes)</td></tr><tr><th>Nodes</th><td>$(ConvertTo-HtmlList $row.NodeList)</td></tr><tr><th>External resources</th><td>$(ConvertTo-HtmlList $row.ExternalResourceList)</td></tr><tr><th>Resource refs</th><td>$(ConvertTo-HtmlList $row.ResourceRefs)</td></tr></tbody></table></article>")
}
[void]$html.AppendLine("</section>")
[void]$html.AppendLine("<section id='json'><h2>JSON Catalog Scan</h2><div class='wide'><table><thead><tr><th>Section</th><th>Top-level keys</th><th>Source</th></tr></thead><tbody>")
foreach ($row in $jsonRows | Sort-Object Section, Path) {
    [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $row.Section)</td><td>$(ConvertTo-Html $row.Keys)</td><td><code>$(ConvertTo-Html $row.Path)</code></td></tr>")
}
[void]$html.AppendLine("</tbody></table></div></section>")
[void]$html.AppendLine("<section id='resource-refs'><h2>Godot Resource Reference Scan</h2><div class='wide'><table><thead><tr><th>Section</th><th>Resource refs</th><th>Source</th></tr></thead><tbody>")
foreach ($row in $resourceRefRows | Sort-Object Section, Path) {
    [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $row.Section)</td><td>$(ConvertTo-HtmlList $row.References)</td><td><code>$(ConvertTo-Html $row.Path)</code></td></tr>")
}
[void]$html.AppendLine("</tbody></table></div></section>")
[void]$html.AppendLine("<section id='sections'><h2>Section File Inventory</h2><p>Every addon file is listed in the section tables below.</p></section>")
foreach ($group in $sectionGroups) {
    $id = "section-" + (($group.Name -replace '[^A-Za-z0-9]+','-').Trim('-').ToLowerInvariant())
    $bytes = ($group.Group | Measure-Object Bytes -Sum).Sum
    [void]$html.AppendLine("<section id='$id'><h2>$(ConvertTo-Html $group.Name)</h2><p>Files: $($group.Count). Bytes: $bytes.</p><div class='wide'><table><thead><tr><th>Kind</th><th>Extension</th><th>Bytes</th><th>Lines read</th><th>Source</th></tr></thead><tbody>")
    foreach ($row in $group.Group | Sort-Object Path) {
        [void]$html.AppendLine("<tr><td>$(ConvertTo-Html $row.Kind)</td><td><code>$(ConvertTo-Html $row.Extension)</code></td><td>$($row.Bytes)</td><td>$($row.Lines)</td><td><code>$(ConvertTo-Html $row.Path)</code></td></tr>")
    }
    [void]$html.AppendLine("</tbody></table></div></section>")
}
[void]$html.AppendLine("</div></main></body></html>")

[System.IO.File]::WriteAllText($htmlPath, (ConvertTo-AsciiText $html.ToString()), [System.Text.UTF8Encoding]::new($false))

Write-Output "Generated $markdownPath and $htmlPath from $($allFiles.Count) addon files across $($sectionGroups.Count) sections."
