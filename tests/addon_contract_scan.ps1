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

$cityBuilderGenre = Read "addons/beep_game_builder_cs/catalogs/skins/citybuilder/genre.json"
$oilfieldTheme = Read "addons/beep_game_builder_cs/catalogs/skins/citybuilder/themes/oilfield_days/theme.json"
if ($cityBuilderGenre -notmatch '"oilfield_days"') { Fail "City Builder skin catalog does not list the Oilfield Days theme." }
foreach ($required in @(
    '"id": "oilfield_days"',
    '"hud_panel"',
    '"hud_tab_selected"',
    '"frame_mode": "structural"',
    '"studs": 1',
    '"rim_brightness"',
    '"select_slot": "border\|glow"'
)) {
    if ($oilfieldTheme -notmatch $required) { Fail "Oilfield Days theme is missing required mockup-derived token: $required." }
}

$kitCore = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCore.cs"
$kitChrome = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitChrome.cs"
$kitPanel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanel.cs"
$kitPanelContainer = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanelContainer.cs"
$kitCollapsible = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs"
if ($kitCore -notmatch 'enum\s+KitPanelHeaderStyle') { Fail "KitPanelHeaderStyle enum is missing." }
$kitStyleJson = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitStyleJson.cs"
foreach ($required in @("frame_mode", "studs", "rim_brightness", "height_ratio", "pad_ratio", "well_shade")) {
    if ($kitStyleJson -notmatch ('"' + [regex]::Escape($required) + '"')) { Fail "KitStyleJson does not accept kit.$required from theme.json." }
}
if ($kitChrome -notmatch 'DrawPanelHeader' -or $kitChrome -notmatch 'PanelHeaderRoom' -or $kitChrome -notmatch 'PanelHeaderOverhang') {
    Fail "KitChrome does not expose the shared panel header helpers."
}
if ($kitChrome -notmatch 'DrawFocusRing' -or $kitChrome -notmatch 'IsConfirmKey' -or $kitChrome -notmatch 'DirectionFromKey') {
    Fail "KitChrome does not expose shared keyboard/focus helpers for custom controls."
}
if ($kitChrome -notmatch 'WrapLines' -or $kitChrome -notmatch 'DrawWrappedText') {
    Fail "KitChrome does not expose shared wrapped text helpers."
}
foreach ($panelSource in @($kitPanel, $kitPanelContainer, $kitCollapsible)) {
    if ($panelSource -notmatch 'KitChrome\.DrawPanelHeader') { Fail "A panel control does not draw through KitChrome.DrawPanelHeader." }
}
if ($kitPanelContainer -match 'private void DrawUtilityHeader') { Fail "KitPanelContainer reintroduced a private utility header renderer." }
if ($kitCollapsible -match 'body\.Position\.X \+ \(body\.Size\.X - m\.X\)') { Fail "KitCollapsiblePanel reintroduced direct centered title text drawing." }

$focusControls = @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitBookSpread.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRemovableChip.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"
)
foreach ($relativePath in $focusControls) {
    $source = Read $relativePath
    if ($source -notmatch 'FocusMode\s*=\s*FocusModeEnum\.All') { Fail "$relativePath is interactive but does not opt into keyboard focus." }
    if ($source -notmatch 'InputEventKey') { Fail "$relativePath is interactive but does not handle keyboard input." }
    if ($source -notmatch 'DrawFocusRing') { Fail "$relativePath is interactive but does not draw a visible focus ring." }
}

foreach ($relativePath in @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSpeechBubble.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitToast.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTooltip.cs"
)) {
    $source = Read $relativePath
    if ($source -notmatch 'KitChrome\.DrawWrappedText') { Fail "$relativePath does not use the shared wrapped text helper." }
}

foreach ($relativePath in @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitBookSpread.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSpeechBubble.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitToast.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTooltip.cs"
)) {
    $source = Read $relativePath
    if ($source -notmatch 'override\s+Vector2\s+_GetMinimumSize') { Fail "$relativePath does not report a dynamic minimum size." }
}

$contextMenu = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs"
if ($contextMenu -notmatch 'GrabFocus\(\)' -or $contextMenu -notmatch 'IsCancelKey' -or $contextMenu -notmatch 'InputEventKey') {
    Fail "KitContextMenu does not implement focus, Escape dismissal, and keyboard selection."
}
if ($contextMenu -notmatch 'ClampedPopupPosition' -or $contextMenu -notmatch 'GetVisibleRect' -or $contextMenu -match 'Position\s*=\s*globalPosition') {
    Fail "KitContextMenu does not clamp popup placement to the visible viewport."
}

foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs"; Property = "ShowJump" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"; Property = "Columns" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"; Property = "Rows" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs"; Property = "Total" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"; Property = "Columns" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"; Property = "Tiers" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs"; Property = "PerRow" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitHeartRow.cs"; Property = "MaxHearts" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitHeartRow.cs"; Property = "HeartSize" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitHeartRow.cs"; Property = "Spacing" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitChip.cs"; Property = "Kind" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinner.cs"; Property = "Kind" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitToggle.cs"; Property = "Style" }
)) {
    $source = Read $entry.Path
    $pattern = 'public\s+[^{}]*\s+' + [regex]::Escape($entry.Property) + '\s*\{.*?UpdateMinimumSize\(\).*?\}'
    if (-not [regex]::IsMatch($source, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Fail "$($entry.Path) $($entry.Property) setter does not notify Godot containers of minimum-size changes."
    }
}

$itemCard = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs"
if ($itemCard -notmatch 'ApplyMinimumSize\(force:\s*true\)' -or $itemCard -notmatch 'UpdateMinimumSize\(\)') {
    Fail "KitItemCard layout changes do not force a smaller/larger minimum-size refresh."
}

$kitFiles = Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File
foreach ($file in $kitFiles) {
    if ($file.Name -eq "KitArchetypes.cs") { continue }
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match 'CustomMinimumSize' -and $source -notmatch 'override\s+Vector2\s+_GetMinimumSize') {
        Fail "$($file.FullName) sets CustomMinimumSize but does not expose _GetMinimumSize()."
    }
}

$tmpIgnorePath = Join-Path $root "tmp/.gdignore"
if (-not (Test-Path $tmpIgnorePath)) { Fail "tmp/.gdignore is missing; Godot will scan generated probe renders and may report duplicate UIDs." }

Write-Host "[addon-contract] OK: tween presets, beep_ui preset registry, MCP lifecycle, editor dock API, data binders, token defaults, citybuilder Oilfield Days skin, panel headers, kit focus, wrapped text, minimum sizes, reusable size contracts, size invalidation, runtime harness, and generated-output ignores are consistent."
