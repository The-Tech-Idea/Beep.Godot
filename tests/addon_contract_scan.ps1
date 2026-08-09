Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Fail($message) {
    throw "[addon-contract] $message"
}

function Read($relativePath) {
    Get-Content -Path (Join-Path $root $relativePath) -Raw
}

$tween = Read "addons/beep_game_builder_cs/ecs/TweenComponent.cs"
$enumMatch = [regex]::Match($tween, 'public enum Preset\s*\{(?<items>.*?)\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $enumMatch.Success) { Fail "TweenComponent.Preset enum not found." }
$enumItems = $enumMatch.Groups["items"].Value -split "," | ForEach-Object { ($_ -replace "\s+", "").Trim() } | Where-Object { $_ }
$cases = [regex]::Matches($tween, 'case Preset\.([A-Za-z0-9_]+):') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$missingCases = @($enumItems | Where-Object { $_ -notin $cases })
if ($missingCases.Count -gt 0) { Fail "Tween presets missing switch cases: $($missingCases -join ', ')." }
if ($tween -match 'All 90\+') { Fail "TweenComponent still contains stale 90+ preset claim." }
if ($tween -match 'not implemented') { Fail "TweenComponent still contains unimplemented preset warning text." }

$beepTheme = Read "addons/beep_ui/theme/beep_theme.gd"
$applier = Read "addons/beep_ui/theme/theme_applier.gd"
$presetMatches = [regex]::Matches($beepTheme, '"([^"]+)":\s*"res://addons/beep_ui/theme/preset_[^"]+\.gd"')
$presetNames = @($presetMatches | ForEach-Object { $_.Groups[1].Value })
if ($presetNames.Count -lt 1) { Fail "BeepPreset registry entries not found." }
if ($applier -match '@export_enum') { Fail "BeepThemeApplier still hardcodes @export_enum choices." }
if ($applier -notmatch '_get_property_list') { Fail "BeepThemeApplier does not expose dynamic property list." }
if ($applier -notmatch 'BeepPreset\.preset_names\(\)') { Fail "BeepThemeApplier does not read BeepPreset.preset_names()." }

$plugin = Read "addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs"
$kit = Read "addons/beep_game_builder_cs/mcp/BeepMcpKitCommands.cs"
if ($plugin -notmatch 'BeepMcpKitCommands\.Unregister\(\)') { Fail "BeepGameBuilderPlugin does not unregister kit MCP commands." }
if ($plugin -notmatch 'new EditorDock') { Fail "BeepGameBuilderPlugin does not use EditorDock for its dock surface." }
if ($plugin -match 'AddControlToDock|RemoveControlFromDocks|CS0618') { Fail "BeepGameBuilderPlugin still uses obsolete dock APIs or suppresses CS0618." }
if ($kit -notmatch 'UnregisterPrefix\("beep\.kit_"\)') { Fail "BeepMcpKitCommands does not unregister beep.kit_ prefix." }

$project = Read "project.godot"
if ($project -match 'bridge/token\s*=') { Fail "project.godot contains a committed MCP bridge token." }
if ($project -notmatch 'security/allow_editor_writes=false') { Fail "project.godot does not keep editor writes disabled by default." }

$settings = Read "addons/godot_mcp/GodotMcpSettings.cs"
if ($settings -notmatch 'GODOT_MCP_BRIDGE_TOKEN') { Fail "GodotMcpSettings no longer supports GODOT_MCP_BRIDGE_TOKEN." }
if ($settings -notmatch '_sessionToken') { Fail "GodotMcpSettings does not keep a local-only generated session token." }

$binderHost = Read "addons/beep_game_builder_cs/ecs/ui/DataBinderHostComponent.cs"
$staticBinder = Read "addons/beep_game_builder_cs/core/BeepDataBinder.cs"
foreach ($binderSource in @($binderHost, $staticBinder)) {
    if ($binderSource -notmatch 'NormalizeTargetProperty') { Fail "Data binder source does not normalize Godot target property names." }
    if ($binderSource -notmatch 'ToVariant') { Fail "Data binder source does not convert boxed values to typed Variants." }
}
if ($binderHost -notmatch 'Mode == BindingMode\.OneWayToSource\)\s*\r?\n\s*binding\.RefreshToSource\(\)') {
    Fail "DataBinderHostComponent does not initialize OneWayToSource by pulling target to source."
}
if ($binderHost -notmatch 'BindingMode\.TwoWay \|\| binding\.Mode == BindingMode\.OneWayToSource') {
    Fail "DataBinderHostComponent RefreshTwoWay does not include OneWayToSource bindings."
}

$projectFile = Read "Beep.Godot.csproj"
if ($projectFile -notmatch '<Compile Include="tests/DataBinderHostSmoke\.cs" />') { Fail "Beep.Godot.csproj does not compile the binder smoke helper." }
if ($projectFile -match 'tests/\*\*/\*\.cs') { Fail "Beep.Godot.csproj includes all test C# files; that can reintroduce generated test obj files." }

$runtimeSmoke = Read "tests/runtime_smoke.ps1"
if ($runtimeSmoke -notmatch 'SCRIPT ERROR\|ERROR:\|Exception\|C# backtrace') { Fail "runtime_smoke.ps1 does not scan Godot output for script/runtime errors." }

$tmpIgnorePath = Join-Path $root "tmp/.gdignore"
if (-not (Test-Path $tmpIgnorePath)) { Fail "tmp/.gdignore is missing; Godot will scan generated probe renders and may report duplicate UIDs." }

Write-Host "[addon-contract] OK: tween presets, beep_ui preset registry, MCP lifecycle, editor dock API, data binders, token defaults, runtime harness, and generated-output ignores are consistent."
