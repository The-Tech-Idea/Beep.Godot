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
if ($tween -notmatch 'case Preset\.BtnHoverWobble:[\s\S]*float hoverDuration = Mathf\.Clamp\(Duration,\s*0\.001f,\s*0\.13f\)[\s\S]*TweenProperty\(node,\s*\$"\{sProp\}:x",\s*1\.2f,\s*hoverDuration\)[\s\S]*TweenProperty\(node,\s*\$"\{sProp\}:y",\s*0\.75f,\s*hoverDuration\)') {
    Fail "TweenComponent BtnHoverWobble must respect the Duration export while keeping hover timing capped."
}

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
if ($settings -notmatch 'GODOT_MCP_"\s*\+\s*key\["godot_mcp/"\.Length\.\.\]' -or $settings -notmatch 'bool\.TryParse\(value,\s*out bool parsed\)') {
    Fail "GodotMcpSettings must allow boolean project settings to be overridden by environment variables for isolated smoke tests."
}
$runtimeSmokeRunner = Read "tests/runtime_smoke.ps1"
$renderProbeRunner = Read "tests/render_scene_probe.ps1"
$showcaseInteractionRunner = Read "tests/showcase_interaction_probe.ps1"
$renderCaptureRunner = Read "tests/render_scene_capture.ps1"
$kitGalleryLayoutRunner = Read "tests/kit_gallery_layout_probe.ps1"
$kitBrowserLayoutRunner = Read "tests/kit_browser_layout_probe.ps1"
$themeGalleryLayoutRunner = Read "tests/theme_gallery_layout_probe.ps1"
foreach ($runner in @($runtimeSmokeRunner, $renderProbeRunner, $showcaseInteractionRunner, $renderCaptureRunner, $kitGalleryLayoutRunner, $kitBrowserLayoutRunner, $themeGalleryLayoutRunner)) {
    if ($runner -notmatch 'GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME"\]\s*=\s*"false"') {
        Fail "Godot smoke/render/capture/interaction runners must disable runtime MCP autoconnect so validation is not coupled to a live bridge token."
    }
}
if ($renderCaptureRunner -notmatch 'C# backtrace') {
    Fail "render_scene_capture.ps1 must fail on C# backtraces, not just native/GDScript errors."
}

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
if ($binderHost -notmatch 'public bool AutoRefresh[\s\S]*UpdateProcessing\(\)' -or
    $binderHost -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $binderHost -notmatch 'Bind\(object source[\s\S]*_bindings\.Add\(binding\)[\s\S]*UpdateProcessing\(\)' -or
    $binderHost -notmatch 'Clear\(\)[\s\S]*_bindings\.Clear\(\)[\s\S]*UpdateProcessing\(\)' -or
    $binderHost -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && AutoRefresh && _bindings\.Count > 0\)') {
    Fail "DataBinderHostComponent must keep _Process disabled until it has active bindings to poll."
}

$projectFile = Read "Beep.Godot.csproj"
if ($projectFile -notmatch '<Compile Include="tests/DataBinderHostSmoke\.cs" />') { Fail "Beep.Godot.csproj does not compile the binder smoke helper." }
if ($projectFile -notmatch '<Compile Include="tests/GridPlacementSmoke\.cs" />') { Fail "Beep.Godot.csproj does not compile the grid placement smoke helper." }
if ($projectFile -match 'tests/\*\*/\*\.cs') { Fail "Beep.Godot.csproj includes all test C# files; that can reintroduce generated test obj files." }

$weatherSystem = Read "addons/beep_game_builder_cs/ecs/atmosphere/WeatherSystemComponent.cs"
if ($weatherSystem -notmatch 'private bool EnsureNodes\(\)' -or
    $weatherSystem -notmatch 'if \(!EnsureNodes\(\)\) return;[\s\S]*_nodesReady = true;' -or
    $weatherSystem -notmatch 'return _particles != null && _overlayLayer != null && _weatherSprites != null;') {
    Fail "WeatherSystemComponent must only mark runtime nodes ready after EnsureNodes proves the required nodes exist."
}
$cloudSpriteLayer = Read "addons/beep_game_builder_cs/ecs/atmosphere/CloudSpriteLayer.cs"
if ($cloudSpriteLayer -notmatch 'WarnMissingSprites\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or
    $cloudSpriteLayer -notmatch 'WarnMissingSprites && !Engine\.IsEditorHint\(\) && !_warned') {
    Fail "CloudSpriteLayer must keep empty optional sprite layers quiet by default and never warn while opened in the editor."
}
$areaTrigger = Read "addons/beep_game_builder_cs/ecs/categories/AreaTriggerComponent.cs"
if ($areaTrigger -notmatch '_Ready\(\)[\s\S]*base\._Ready\(\);[\s\S]*if \(Engine\.IsEditorHint\(\)\) return;[\s\S]*TriggerArea = ResolveArea2D\(\);') {
    Fail "AreaTriggerComponent must not wire body signals or emit parent warnings while running as a tool script in the editor."
}
$levelLoader = Read "addons/beep_game_builder_cs/ecs/LevelLoaderComponent.cs"
if ($levelLoader -notmatch '\[Export\]\s*public Godot\.Collections\.Array Levels' -or
    $levelLoader -notmatch 'TryGetLevelScene\(idx,\s*out PackedScene\? scene\)' -or
    $levelLoader -notmatch 'value\.VariantType != Variant\.Type\.Object' -or
    $levelLoader -notmatch 'value\.AsGodotObject\(\) as PackedScene' -or
    $levelLoader -notmatch '"invalid level scene"') {
    Fail "LevelLoaderComponent must validate loose authored level entries instead of indexing a typed Array<PackedScene>."
}
$coreSmoke = Read "tests/CoreGameplaySmoke.cs"
if ($coreSmoke -notmatch 'VerifyLevelLoaderLooseLevelEntries' -or
    $coreSmoke -notmatch 'loader\.Levels\.Add\(new Resource\(\)\)' -or
    $coreSmoke -notmatch 'failedReason == "invalid level scene"') {
    Fail "CoreGameplaySmoke must cover invalid loose LevelLoader entries."
}
# The painted view is no longer a CPU image compositor (PainterlyTerrainComponent,
# removed in 70ab70ca) but a shader surface fed by the generator. What survives is
# the contract that mattered: a world is not rebuilt just because a scene was
# opened, there is a design-time trigger, and what that trigger generates is kept.
$terrainWorld = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs"
if ($terrainWorld -notmatch 'BuildOnReady && !Engine\.IsEditorHint\(\)' -or
    $terrainWorld -notmatch 'CallDeferred\(nameof\(Build\)\)') {
    Fail "TerrainWorldComponent must not build a world while opening editor scenes, and runtime ready generation must be deferred."
}
if ($terrainWorld -notmatch '\[ExportToolButton\("Generate map"\)\]' -or
    $terrainWorld -notmatch 'Callable\.From\(Build\)') {
    Fail "TerrainWorldComponent must expose the design-time Generate map trigger, or every component is [Tool] and inert in the editor."
}
foreach ($required in @("PaintedRendererPath", "TileRendererPath", "IsometricRendererPath", "DataLayersPath", "BuiltSize", "Diagnostics()", "StatusLine()")) {
    if ($terrainWorld -notmatch [regex]::Escape($required)) {
        Fail "TerrainWorldComponent must own the whole world-creation surface, so a demo is a configured node and not another controller script: $required."
    }
}

# A node added with AddChild belongs to the tree but not to the scene FILE, so a
# generated map without an owner looks right in the viewport and vanishes on
# reload - with no error and nothing to notice until the work is gone.
$terrainAuthoring = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainAuthoring.cs"
if ($terrainAuthoring -notmatch 'generated\.Owner = root' -or
    $terrainAuthoring -notmatch 'EditedSceneRoot' -or
    $terrainAuthoring -notmatch 'IsAncestorOf\(generated\)') {
    Fail "TerrainAuthoring.Adopt must give a generated node the edited scene root as owner, guarded by the ancestor rule Godot enforces."
}
foreach ($creator in @(
    "TerrainPaintedRendererComponent.cs",
    "TerrainTileRendererComponent.cs",
    "TerrainIsometricRendererComponent.cs",
    "TerrainIsometricFeatureRendererComponent.cs",
    "TerrainIsometricAutotileRendererComponent.cs",
    "TerrainDataLayersComponent.cs")) {
    $creatorSource = Read "addons/beep_game_builder_cs/ecs/terrain/$creator"
    if ($creatorSource -notmatch 'TerrainAuthoring\.(Adopt|EnsureLayer)\(') {
        Fail "$creator creates map nodes and must adopt them through TerrainAuthoring (directly, or via EnsureLayer which adopts internally), or a generated map is lost when the scene is reloaded."
    }
}
$runAddonChecks = Read "tests/run_addon_checks.ps1"
if ($runAddonChecks -notmatch 'terrain_guards\.ps1') {
    Fail "run_addon_checks.ps1 must run the terrain guards."
}
$audioComponent = Read "addons/beep_game_builder_cs/ecs/AudioComponent.cs"
if ($audioComponent -notmatch '_Ready\(\)[\s\S]*base\._Ready\(\);[\s\S]*if \(Engine\.IsEditorHint\(\)\) return;[\s\S]*SetupAudioPlayer' -or
    $audioComponent -notmatch 'if \(_player != null && GodotObject\.IsInstanceValid\(_player\)\) return;' -or
    $audioComponent -notmatch 'PlayOneShot[\s\S]*Engine\.IsEditorHint\(\) \|\| !IsActive') {
    Fail "AudioComponent must not spawn runtime AudioStreamPlayer nodes in the editor and setup must be idempotent."
}
$footstepComponent = Read "addons/beep_game_builder_cs/ecs/FootstepComponent.cs"
if ($footstepComponent -notmatch '_Ready\(\)[\s\S]*base\._Ready\(\);[\s\S]*if \(Engine\.IsEditorHint\(\)\) return;[\s\S]*_body = GetParent\(\) as CharacterBody2D;' -or
    $footstepComponent -notmatch 'if \(_player != null && GodotObject\.IsInstanceValid\(_player\)\) return;') {
    Fail "FootstepComponent must not spawn runtime AudioStreamPlayer nodes in the editor and setup must be idempotent."
}
foreach ($required in @("EffectiveMinSpeed => NonNegativeFinite", "EffectiveStepInterval => float.IsFinite", "EffectivePitchVariation => float.IsFinite", "DeltaSeconds(double delta)", "float.IsFinite(_body.Velocity.X)")) {
    if ($footstepComponent -notmatch [regex]::Escape($required)) {
        Fail "FootstepComponent must bound invalid speed, cadence, pitch, velocity, and frame deltas: $required."
    }
}

$mainGame = Read "addons/beep_game_builder_cs/ecs/MainGameComponent.cs"
if ($mainGame -notmatch 'BuildDefaultHud\s*\{\s*get;\s*set;\s*\}\s*=\s*false') {
    Fail "MainGameComponent must keep the legacy BuildDefaultHud export false for serialized scene compatibility."
}
if ($mainGame -match 'public void StartGame\(\)[\s\S]*?EnsureDefaultHud\(\);') {
    Fail "MainGameComponent.StartGame must not create RuntimeHud; authored scenes should own HUD nodes."
}
if ($mainGame -match 'EnsureDefaultHud|RuntimeHud|new\s+Kit(Label|LabelValue|Panel|PanelContainer)|Build(Common|Rpg|Shooter|Survival|CityBuilder|Strategy|Puzzle|Racing|CardGame)Hud') {
    Fail "MainGameComponent must not contain runtime HUD builders; HUD controls belong in authored template/game scenes."
}

$cityBuildToolbar = Read "addons/beep_game_builder_cs/ecs/ui/BuildToolbarComponent.cs"
if ($cityBuildToolbar -notmatch 'CategoryRowPath' -or $cityBuildToolbar -notmatch 'PaletteContainerPath' -or $cityBuildToolbar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $cityBuildToolbar -notmatch 'BindExistingControls' -or $cityBuildToolbar -notmatch 'UsesSceneControls' -or $cityBuildToolbar -notmatch 'BuildGeneratedSurface') {
    Fail "BuildToolbarComponent must bind authored citybuilder toolbar containers by default and only generate fallback UI when explicitly enabled."
}
if ($cityBuildToolbar -match 'FocusMode\s*=\s*Godot\.Control\.FocusModeEnum\.None') {
    Fail "BuildToolbarComponent must not disable focus on interactive toolbar buttons."
}
foreach ($required in @("FindCategoryRow", "FindPalette", 'FindChild\("Categories"', 'FindChild\("Palette"')) {
    if ($cityBuildToolbar -notmatch $required) {
        Fail "BuildToolbarComponent must auto-bind conventional Categories/Palette containers before generated fallback: $required."
    }
}
$gameSpeed = Read "addons/beep_game_builder_cs/ecs/ui/GameSpeedComponent.cs"
if ($gameSpeed -notmatch 'BoundButtonPaths' -or $gameSpeed -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gameSpeed -notmatch 'BindExistingButtons' -or $gameSpeed -notmatch 'UsesSceneButtons' -or $gameSpeed -notmatch 'BuildGeneratedButtons') {
    Fail "GameSpeedComponent must bind authored speed buttons by default and only generate fallback UI when explicitly enabled."
}
if ($gameSpeed -match 'FocusMode\s*=\s*Godot\.Control\.FocusModeEnum\.None') {
    Fail "GameSpeedComponent must not disable focus on interactive speed buttons."
}
foreach ($required in @("HasAuthoredSpeedButtons", "FindSpeedButton", 'Name = \$"Speed\{i\}"', 'FindChild\(name')) {
    if ($gameSpeed -notmatch $required) {
        Fail "GameSpeedComponent must auto-bind conventional Speed0-Speed3 buttons before generated fallback: $required."
    }
}
$searchBar = Read "addons/beep_game_builder_cs/ecs/ui/SearchBarComponent.cs"
if ($searchBar -notmatch 'InputPath' -or $searchBar -notmatch 'ClearButtonPath' -or $searchBar -notmatch 'IconButtonPath' -or $searchBar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $searchBar -notmatch 'BindExistingControls' -or $searchBar -notmatch 'UsesSceneControls' -or $searchBar -notmatch 'BuildGeneratedSearch') {
    Fail "SearchBarComponent must bind authored search controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindInput", "FindClearButton", "FindIconButton", 'Name = "SearchIcon"', 'Name = "Input"', 'Name = "ClearButton"', 'FindChild\("Input"', 'FindChild\("ClearButton"', 'FindChild\("SearchIcon"')) {
    if ($searchBar -notmatch $required) {
        Fail "SearchBarComponent must auto-bind conventional Input/ClearButton/SearchIcon controls before generated fallback: $required."
    }
}
if ($searchBar -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $searchBar -notmatch 'OnTextChanged[\s\S]*_debouncePending = true[\s\S]*SetProcess\(true\)' -or
    $searchBar -notmatch '_Process[\s\S]*_debouncePending = false[\s\S]*SetProcess\(false\)' -or
    $searchBar -notmatch 'Clear\(\)[\s\S]*_debouncePending = false[\s\S]*SetProcess\(false\)') {
    Fail "SearchBarComponent must keep _Process disabled except while a debounce emit is pending."
}
$stepper = Read "addons/beep_game_builder_cs/ecs/ui/StepperComponent.cs"
if ($stepper -notmatch 'MinusButtonPath' -or $stepper -notmatch 'ValueDisplayPath' -or $stepper -notmatch 'PlusButtonPath' -or $stepper -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $stepper -notmatch 'BindExistingControls' -or $stepper -notmatch 'UsesSceneControls' -or $stepper -notmatch 'BuildGeneratedStepper') {
    Fail "StepperComponent must bind authored stepper controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindMinusButton", "FindValueDisplay", "FindPlusButton", 'Name = "MinusButton"', 'Name = "ValueDisplay"', 'Name = "PlusButton"', 'FindChild\("MinusButton"', 'FindChild\("ValueDisplay"', 'FindChild\("PlusButton"')) {
    if ($stepper -notmatch $required) {
        Fail "StepperComponent must use conventional design-time child names before generated fallback: $required."
    }
}
$interactionPrompt = Read "addons/beep_game_builder_cs/ecs/ui/InteractionPromptComponent.cs"
if ($interactionPrompt -notmatch 'PromptLabelPath' -or $interactionPrompt -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $interactionPrompt -notmatch 'BindExistingLabel' -or $interactionPrompt -notmatch 'UsesSceneControls') {
    Fail "InteractionPromptComponent must bind an authored prompt label by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindPromptLabel", 'FindChild\("PromptLabel"')) {
    if ($interactionPrompt -notmatch $required) {
        Fail "InteractionPromptComponent must auto-bind conventional PromptLabel before generated fallback: $required."
    }
}
$matchTimer = Read "addons/beep_game_builder_cs/ecs/ui/MatchTimerComponent.cs"
if ($matchTimer -notmatch 'TimerLabelPath' -or $matchTimer -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $matchTimer -notmatch 'BindExistingLabel' -or $matchTimer -notmatch 'UsesSceneControls') {
    Fail "MatchTimerComponent must bind an authored timer label by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindTimerLabel", 'Name = "TimerLabel"', 'FindChild\("TimerLabel"')) {
    if ($matchTimer -notmatch $required) {
        Fail "MatchTimerComponent must auto-bind conventional TimerLabel before generated fallback: $required."
    }
}
if ($matchTimer -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $matchTimer -notmatch 'Start\(\)[\s\S]*_running = true[\s\S]*UpdateProcessing\(\)' -or
    $matchTimer -notmatch 'Stop\(\)[\s\S]*_running = false[\s\S]*UpdateProcessing\(\)' -or
    $matchTimer -notmatch 'Reset\(\)[\s\S]*_running = false[\s\S]*UpdateProcessing\(\)' -or
    $matchTimer -notmatch '_Process[\s\S]*!_running \|\| !IsActive[\s\S]*UpdateProcessing\(\)' -or
    $matchTimer -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && _running\)') {
    Fail "MatchTimerComponent must keep _Process disabled except while a timer is actively running."
}
$meterBar = Read "addons/beep_game_builder_cs/ecs/ui/MeterBarComponent.cs"
if ($meterBar -notmatch 'LabelPath' -or $meterBar -notmatch 'MeterPath' -or $meterBar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $meterBar -notmatch 'BindExistingControls' -or $meterBar -notmatch 'UsesSceneControls' -or $meterBar -notmatch 'BuildGeneratedMeter') {
    Fail "MeterBarComponent must bind authored meter controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindMeter", "FindLabel", 'Name = "MeterLabel"', 'Name = "MeterFill"', 'FindChild\("MeterLabel"', 'FindChild\("MeterFill"')) {
    if ($meterBar -notmatch $required) {
        Fail "MeterBarComponent must auto-bind conventional MeterLabel/MeterFill controls before generated fallback: $required."
    }
}
if ($meterBar -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $meterBar -notmatch 'Refresh\(\)[\s\S]*UpdateProcessing\(\)' -or
    $meterBar -notmatch '_Process[\s\S]*!Pulse \|\| _bar == null \|\| _level != "critical"[\s\S]*UpdateProcessing\(\)' -or
    $meterBar -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && Pulse && _bar != null && _level == "critical"\)') {
    Fail "MeterBarComponent must only process while an active critical pulse is visible."
}
$bossHealthBar = Read "addons/beep_game_builder_cs/ecs/ui/BossHealthBarComponent.cs"
if ($bossHealthBar -notmatch 'NameLabelPath' -or $bossHealthBar -notmatch 'BarPath' -or $bossHealthBar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $bossHealthBar -notmatch 'BindExistingControls' -or $bossHealthBar -notmatch 'UsesSceneControls' -or $bossHealthBar -notmatch 'BuildGeneratedControls') {
    Fail "BossHealthBarComponent must bind an authored name label and meter by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindNameLabel", "FindBar", 'FindChild\("BossName"', 'FindChild\("BossBar"')) {
    if ($bossHealthBar -notmatch $required) {
        Fail "BossHealthBarComponent must auto-bind conventional BossName/BossBar children before generated fallback: $required."
    }
}
$comboCounter = Read "addons/beep_game_builder_cs/ecs/ui/ComboCounterComponent.cs"
if ($comboCounter -notmatch 'ComboLabelPath' -or $comboCounter -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $comboCounter -notmatch 'BindExistingLabel' -or $comboCounter -notmatch 'UsesSceneControls') {
    Fail "ComboCounterComponent must bind an authored combo label by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindComboLabel", 'FindChild\("ComboLabel"')) {
    if ($comboCounter -notmatch $required) {
        Fail "ComboCounterComponent must auto-bind conventional ComboLabel before generated fallback: $required."
    }
}
if ($comboCounter -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $comboCounter -notmatch 'Increment\(\)[\s\S]*_count\+\+[\s\S]*UpdateProcessing\(\)' -or
    $comboCounter -notmatch 'ResetCombo\(\)[\s\S]*_count = 0[\s\S]*UpdateProcessing\(\)' -or
    $comboCounter -notmatch '_Process[\s\S]*_count == 0 \|\| !IsActive[\s\S]*UpdateProcessing\(\)' -or
    $comboCounter -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && _count > 0\)') {
    Fail "ComboCounterComponent must keep _Process disabled except while a combo reset timer is active."
}
$badge = Read "addons/beep_game_builder_cs/ecs/ui/BadgeComponent.cs"
if ($badge -notmatch 'BadgePath' -or $badge -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $badge -notmatch 'BindExistingBadge' -or $badge -notmatch 'UsesSceneControls') {
    Fail "BadgeComponent must bind an authored badge chip by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindBadge", 'Name = "Badge"', 'FindChild\("Badge"')) {
    if ($badge -notmatch $required) {
        Fail "BadgeComponent must auto-bind conventional Badge before generated fallback: $required."
    }
}
$chip = Read "addons/beep_game_builder_cs/ecs/ui/ChipComponent.cs"
if ($chip -notmatch 'ChipPath' -or $chip -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $chip -notmatch 'BindExistingChip' -or $chip -notmatch 'UsesSceneControls') {
    Fail "ChipComponent must bind an authored removable chip by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindChip", 'Name = "Chip"', 'FindChild\("Chip"')) {
    if ($chip -notmatch $required) {
        Fail "ChipComponent must auto-bind conventional Chip before generated fallback: $required."
    }
}
$rating = Read "addons/beep_game_builder_cs/ecs/ui/RatingComponent.cs"
if ($rating -notmatch 'RatingPath' -or $rating -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $rating -notmatch 'BindExistingRating' -or $rating -notmatch 'UsesSceneControls') {
    Fail "RatingComponent must bind an authored KitStarRating by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindRating", 'Name = "Rating"', 'FindChild\("Rating"')) {
    if ($rating -notmatch $required) {
        Fail "RatingComponent must auto-bind conventional Rating before generated fallback: $required."
    }
}
$toggleSwitch = Read "addons/beep_game_builder_cs/ecs/ui/ToggleSwitchComponent.cs"
if ($toggleSwitch -notmatch 'VisualPath' -or $toggleSwitch -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $toggleSwitch -notmatch 'BindExistingSwitch' -or $toggleSwitch -notmatch 'UsesSceneControls') {
    Fail "ToggleSwitchComponent must bind an authored KitSwitchVisual by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindSwitchVisual", 'Name = "SwitchVisual"', 'FindChild\("SwitchVisual"')) {
    if ($toggleSwitch -notmatch $required) {
        Fail "ToggleSwitchComponent must auto-bind conventional SwitchVisual before generated fallback: $required."
    }
}
$accordion = Read "addons/beep_game_builder_cs/ecs/ui/AccordionComponent.cs"
if ($accordion -notmatch 'HeaderPath' -or $accordion -notmatch 'ContentRootPath' -or $accordion -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $accordion -notmatch 'BindExistingHeader' -or $accordion -notmatch 'UsesSceneControls' -or $accordion -notmatch 'BuildGeneratedHeader') {
    Fail "AccordionComponent must bind an authored header/content by default and only generate fallback UI when explicitly enabled."
}
if ($accordion -match 'FocusMode\s*=\s*Godot\.Control\.FocusModeEnum\.None') {
    Fail "AccordionComponent must not disable focus on its interactive header."
}
foreach ($required in @("FindHeader", 'Name = "AccordionHeader"', 'FindChild\("AccordionHeader"', 'GetParent\(\)\?\.FindChild')) {
    if ($accordion -notmatch $required) {
        Fail "AccordionComponent must auto-bind conventional AccordionHeader before generated fallback: $required."
    }
}
$buffBar = Read "addons/beep_game_builder_cs/ecs/ui/BuffBarComponent.cs"
if ($buffBar -notmatch 'ContainerPath' -or $buffBar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $buffBar -notmatch 'BindExistingContainer' -or $buffBar -notmatch 'UsesSceneControls' -or $buffBar -notmatch 'BuildGeneratedContainer') {
    Fail "BuffBarComponent must bind an authored buff row container by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindBuffBar", 'Name = "BuffBar"', 'FindChild\("BuffBar"')) {
    if ($buffBar -notmatch $required) {
        Fail "BuffBarComponent must auto-bind conventional BuffBar before generated fallback: $required."
    }
}
if ($buffBar -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $buffBar -notmatch 'OnEffectApplied[\s\S]*_icons\[effectId\] = ring;[\s\S]*UpdateProcessing\(\)' -or
    $buffBar -notmatch 'OnEffectExpired[\s\S]*_icons\.Remove\(effectId\)[\s\S]*UpdateProcessing\(\)' -or
    $buffBar -notmatch 'private void UpdateProcessing\(\)[\s\S]*SetProcess\(!Engine\.IsEditorHint\(\)[\s\S]*_icons\.Count > 0\)') {
    Fail "BuffBarComponent must keep _Process disabled unless it is showing active buff rings."
}
$parentAwareUiFinders = @{
    "BadgeComponent" = $badge
    "BossHealthBarComponent" = $bossHealthBar
    "BuffBarComponent" = $buffBar
    "BuildToolbarComponent" = $cityBuildToolbar
    "ChipComponent" = $chip
    "ComboCounterComponent" = $comboCounter
    "GameSpeedComponent" = $gameSpeed
    "InteractionPromptComponent" = $interactionPrompt
    "MatchTimerComponent" = $matchTimer
    "MeterBarComponent" = $meterBar
    "RatingComponent" = $rating
    "SearchBarComponent" = $searchBar
    "StepperComponent" = $stepper
    "ToggleSwitchComponent" = $toggleSwitch
}
foreach ($entry in $parentAwareUiFinders.GetEnumerator()) {
    if ($entry.Value -notmatch 'GetParent\(\)\?\.FindChild') {
        Fail "$($entry.Key) conventional control lookup must search parent/sibling authored controls, not only component children."
    }
}
$collapsiblePanel = Read "addons/beep_game_builder_cs/ecs/ui/CollapsiblePanelComponent.cs"
if ($collapsiblePanel -notmatch 'ToggleButtonPath' -or $collapsiblePanel -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $collapsiblePanel -notmatch 'BindExistingHeader' -or $collapsiblePanel -notmatch 'UsesSceneControls' -or $collapsiblePanel -notmatch 'BuildHeader') {
    Fail "CollapsiblePanelComponent must bind an authored toggle button by default and only generate fallback UI when explicitly enabled."
}
if ($collapsiblePanel -match 'FocusMode\s*=\s*Godot\.Control\.FocusModeEnum\.None') {
    Fail "CollapsiblePanelComponent must not disable focus on its interactive collapse toggle."
}
foreach ($required in @("FindToggleButton", 'FindChild\("CollapseToggle"', 'GetParent\(\)\?\.FindChild')) {
    if ($collapsiblePanel -notmatch $required) {
        Fail "CollapsiblePanelComponent must auto-bind conventional collapse toggle buttons before generated fallback: $required."
    }
}
$hudCollapse = Read "addons/beep_game_builder_cs/ecs/ui/HudCollapseComponent.cs"
if ($hudCollapse -notmatch 'AutoAttachPanels\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $hudCollapse -notmatch 'if \(!AutoAttachPanels\) return;' -or $hudCollapse -notmatch 'GenerateControlsWhenPathsEmpty\s*=\s*true') {
    Fail "HudCollapseComponent must not inject CollapsiblePanelComponent nodes by default; legacy auto attach must be explicit and generated handles must opt in."
}
$crosshairComponent = Read "addons/beep_game_builder_cs/ecs/ui/CrosshairComponent.cs"
if ($crosshairComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $crosshairComponent -notmatch 'AddSpread\(float amount\)[\s\S]*UpdateProcessing\(\)' -or
    $crosshairComponent -notmatch '_Process[\s\S]*CurrentSpread = Mathf\.MoveToward[\s\S]*UpdateProcessing\(\)' -or
    $crosshairComponent -notmatch 'private void UpdateProcessing\(\)[\s\S]*CurrentSpread > MinSpread') {
    Fail "CrosshairComponent must redraw continuously only while spread is recovering."
}
$coroutineHost = Read "addons/beep_game_builder_cs/ecs/ui/CoroutineHostComponent.cs"
if ($coroutineHost -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $coroutineHost -notmatch 'Delay\(double seconds[\s\S]*_jobs\.Add\(job\)[\s\S]*UpdateProcessing\(\)' -or
    $coroutineHost -notmatch 'Cancel\(string jobId\)[\s\S]*_jobs\.RemoveAt\(idx\)[\s\S]*UpdateProcessing\(\)' -or
    $coroutineHost -notmatch 'CancelAll\(\)[\s\S]*_jobs\.Clear\(\)[\s\S]*UpdateProcessing\(\)' -or
    $coroutineHost -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && _jobs\.Count > 0\)') {
    Fail "CoroutineHostComponent must keep _Process disabled unless jobs are scheduled."
}
$settingsComponent = Read "addons/beep_game_builder_cs/ecs/ui/SettingsComponent.cs"
if ($settingsComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $settingsComponent -notmatch 'SaveSettings\(\)[\s\S]*_saveDirty = true[\s\S]*UpdateProcessing\(\)' -or
    $settingsComponent -notmatch 'FlushSettings\(\)[\s\S]*_saveDirty = false[\s\S]*UpdateProcessing\(\)' -or
    $settingsComponent -notmatch '_Process[\s\S]*Engine\.IsEditorHint\(\) \|\| !_saveDirty[\s\S]*UpdateProcessing\(\)' -or
    $settingsComponent -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && _saveDirty\)') {
    Fail "SettingsComponent must process only while a debounced save is pending."
}
$minimapComponent = Read "addons/beep_game_builder_cs/ecs/ui/MinimapComponent.cs"
if ($minimapComponent -notmatch 'RefreshInterval' -or
    $minimapComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $minimapComponent -notmatch '_Process[\s\S]*_refreshElapsed \+= delta[\s\S]*_refreshElapsed < RefreshInterval[\s\S]*return' -or
    $minimapComponent -notmatch 'NotificationVisibilityChanged[\s\S]*UpdateProcessing\(\)' -or
    $minimapComponent -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsVisibleInTree\(\)\)') {
    Fail "MinimapComponent must throttle redraws and disable processing while hidden or in editor."
}
$counterComponent = Read "addons/beep_game_builder_cs/ecs/ui/CounterComponent.cs"
if ($counterComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $counterComponent -notmatch 'CountTo\(float target\)[\s\S]*_counting = true[\s\S]*SetProcess\(true\)' -or
    $counterComponent -notmatch 'SetImmediate\(float value\)[\s\S]*_counting = false[\s\S]*SetProcess\(false\)' -or
    $counterComponent -notmatch '_Process[\s\S]*if \(!_counting \|\| _label == null\)[\s\S]*SetProcess\(false\)' -or
    $counterComponent -notmatch '_elapsed >= Duration[\s\S]*_counting = false[\s\S]*SetProcess\(false\)') {
    Fail "CounterComponent must keep _Process disabled except while an animated count is running."
}
$loadingScreen = Read "addons/beep_game_builder_cs/ecs/ui/LoadingScreenComponent.cs"
if ($loadingScreen -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $loadingScreen -notmatch 'LoadScene\(string path\)[\s\S]*Show\(\)[\s\S]*SetProcess\(true\)' -or
    $loadingScreen -notmatch '_pendingPath == null[\s\S]*SetProcess\(false\)' -or
    $loadingScreen -notmatch 'ThreadLoadStatus\.Failed[\s\S]*_pendingPath = null[\s\S]*Hide\(\)[\s\S]*SetProcess\(false\)' -or
    $loadingScreen -notmatch 'LoadComplete[\s\S]*_pendingPath = null[\s\S]*SetProcess\(false\)') {
    Fail "LoadingScreenComponent must keep _Process disabled except while polling an active threaded load."
}
$skeletonLoader = Read "addons/beep_game_builder_cs/ecs/ui/SkeletonLoaderComponent.cs"
if ($skeletonLoader -notmatch 'public bool AutoPlay[\s\S]*if \(_autoPlay == value\) return[\s\S]*if \(_autoPlay\) Start\(\)[\s\S]*else Stop\(\)' -or
    $skeletonLoader -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)[\s\S]*if \(AutoPlay\)[\s\r\n]*\s*Start\(\)' -or
    $skeletonLoader -notmatch 'private void EnsureMaterial\(\)' -or
    $skeletonLoader -notmatch 'private void RefreshMaterialColors\(\)' -or
    $skeletonLoader -notmatch 'public void Start\(\)[\s\S]*if \(!_running\)[\s\r\n]*\s*_priorMaterial = _control\.Material[\s\S]*_control\.Material = _shimmerMat[\s\S]*_running = true[\s\S]*SetProcess\(true\)' -or
    $skeletonLoader -notmatch 'public void Stop\(\)[\s\S]*_control\.Material == _shimmerMat[\s\S]*_control\.Material = _priorMaterial[\s\S]*_running = false[\s\S]*SetProcess\(false\)' -or
    $skeletonLoader -notmatch '_Process[\s\S]*!_running[\s\S]*Stop\(\)' -or
    $skeletonLoader -notmatch 'Godot\.Control\.NotificationThemeChanged' -or
    $skeletonLoader -notmatch '_ExitTree\(\)[\s\S]*Stop\(\)') {
    Fail "SkeletonLoaderComponent must apply shimmer material and process only while explicitly running."
}
$marqueeComponent = Read "addons/beep_game_builder_cs/ecs/ui/MarqueeComponent.cs"
if ($marqueeComponent -notmatch 'public bool AutoStart[\s\S]*if \(_autoStart == value\) return[\s\S]*if \(_autoStart\) Start\(\)[\s\S]*else Stop\(\)' -or
    $marqueeComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)[\s\S]*if \(AutoStart\)[\s\r\n]*\s*Start\(\)' -or
    $marqueeComponent -notmatch 'public void Start\(\)[\s\S]*_running = true[\s\S]*SetProcess\(true\)' -or
    $marqueeComponent -notmatch 'public void Stop\(\)[\s\S]*_running = false[\s\S]*SetProcess\(false\)' -or
    $marqueeComponent -notmatch '_Process[\s\S]*!_running[\s\S]*SetProcess\(false\)') {
    Fail "MarqueeComponent must keep _Process disabled except while the ticker is running."
}
$shakeComponent = Read "addons/beep_game_builder_cs/ecs/ui/ShakeComponent.cs"
if ($shakeComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $shakeComponent -notmatch 'Shake\(float intensity = -1,\s*float duration = -1\)[\s\S]*SetProcess\(true\)' -or
    $shakeComponent -notmatch '_Process[\s\S]*!IsActive[\s\S]*FinishShake\(emitSignal:\s*false\)' -or
    $shakeComponent -notmatch 'FinishShake\(emitSignal:\s*true\)' -or
    $shakeComponent -notmatch 'FinishShake[\s\S]*OffsetTransformPosition = Vector2\.Zero[\s\S]*_shaking\.Clear\(\)[\s\S]*SetProcess\(false\)') {
    Fail "ShakeComponent must process only during an active shake and always restore offset transforms before stopping."
}
$uiEffectComponent = Read "addons/beep_game_builder_cs/ecs/ui/UIEffectComponent.cs"
if ($uiEffectComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $uiEffectComponent -notmatch 'Stop\(\)[\s\S]*StopTypewriter\(\)[\s\S]*UpdateProcessing\(\)' -or
    $uiEffectComponent -notmatch 'ExecuteEffect\(\)[\s\S]*UpdateProcessing\(\)' -or
    $uiEffectComponent -notmatch 'OnAllCompleted\(\)[\s\S]*_isPlaying = false[\s\S]*UpdateProcessing\(\)' -or
    $uiEffectComponent -notmatch '_typewriterStates\.Count == 0[\s\S]*_isPlaying = false[\s\S]*UpdateProcessing\(\)' -or
    $uiEffectComponent -notmatch 'private bool ShouldProcess\(\)[\s\S]*Effect == EffectType\.Bob \|\| \(Effect == EffectType\.Typewriter && _typewriterStates\.Count > 0\)' -or
    $uiEffectComponent -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(ShouldProcess\(\)\)') {
    Fail "UIEffectComponent must keep _Process disabled except for active Bob/Typewriter work."
}
$effectComponent = Read "addons/beep_game_builder_cs/ecs/ui/EffectComponent.cs"
$pulseComponent = Read "addons/beep_game_builder_cs/ecs/ui/PulseComponent.cs"
if ($effectComponent -notmatch 'protected virtual void ResolveTargets\(\)' -or
    $pulseComponent -notmatch 'public override void _Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $pulseComponent -notmatch 'protected override void ResolveTargets\(\)[\s\S]*base\.ResolveTargets\(\)[\s\S]*UpdateProcessing\(\)' -or
    $pulseComponent -notmatch 'public bool AutoStart[\s\S]*UpdateProcessing\(\)' -or
    $pulseComponent -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && AutoStart && Targets\.Count > 0\)') {
    Fail "PulseComponent must enable processing only after EffectComponent resolves live targets."
}
$progressRing = Read "addons/beep_game_builder_cs/ecs/ui/ProgressRingComponent.cs"
if ($progressRing -notmatch 'Value[\s\S]*EmitSignal\(SignalName\.ValueChanged,\s*value\)[\s\S]*UpdateProcessing\(\)' -or
    $progressRing -notmatch 'MaxValue[\s\S]*UpdateProcessing\(\)' -or
    $progressRing -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $progressRing -notmatch '_Process[\s\S]*Mathf\.IsEqualApprox\(_displayValue,\s*target\)[\s\S]*SetProcess\(false\)' -or
    $progressRing -notmatch 'private void UpdateProcessing\(\)[\s\S]*SetProcess\(true\)') {
    Fail "ProgressRingComponent must redraw on value changes and stop processing after interpolation settles."
}
$carouselComponent = Read "addons/beep_game_builder_cs/ecs/ui/CarouselComponent.cs"
if ($carouselComponent -notmatch 'public bool AutoPlay[\s\S]*UpdateProcessing\(\)' -or
    $carouselComponent -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $carouselComponent -notmatch 'InitSlides\(\)[\s\S]*UpdateProcessing\(\)' -or
    $carouselComponent -notmatch '_Process[\s\S]*!AutoPlay \|\| !IsActive \|\| _slides\.Count == 0[\s\S]*UpdateProcessing\(\)' -or
    $carouselComponent -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!Engine\.IsEditorHint\(\) && IsActive && AutoPlay && _slides\.Count > 0\)') {
    Fail "CarouselComponent must process only while autoplay is enabled and slides exist."
}
$weatherHud = Read "addons/beep_game_builder_cs/ecs/ui/WeatherHUDComponent.cs"
if ($weatherHud -notmatch 'PollInterval' -or
    $weatherHud -notmatch '_Ready\(\)[\s\S]*SetProcess\(false\)' -or
    $weatherHud -notmatch 'Bind\(\)[\s\S]*RefreshAll\(\)[\s\S]*UpdateProcessing\(\)' -or
    $weatherHud -notmatch '_pollElapsed \+= delta[\s\S]*_pollElapsed < EffectivePollInterval[\s\S]*return' -or
    $weatherHud -notmatch 'private void UpdateProcessing\(\)[\s\S]*SetProcess\(!Engine\.IsEditorHint\(\)[\s\S]*\(_intensity != null \|\| _forecast != null \|\| _time != null \|\| _wind != null\)\)') {
    Fail "WeatherHUDComponent must avoid every-frame polling until a live weather system and dynamic labels are bound."
}
foreach ($required in @("WarnMissingWeatherSystem { get; set; } = false", "EffectivePollInterval", "double.IsFinite(PollInterval)", "!double.IsFinite(delta)", "NormalizeHour(_dayNight.TimeOfDay)", "IsFinite(_ws.WindForce)")) {
    if ($weatherHud -notmatch [regex]::Escape($required)) {
        Fail "WeatherHUDComponent must bound invalid polling, time, intensity, and wind display values: $required."
    }
}
$chromaticAberration = Read "addons/beep_game_builder_cs/ecs/ui/ChromaticAberrationComponent.cs"
$vignette = Read "addons/beep_game_builder_cs/ecs/ui/VignetteComponent.cs"
if ($chromaticAberration -notmatch '_Ready\(\)[\s\S]*SetProcess\(Engine\.IsEditorHint\(\)\)' -or
    $chromaticAberration -notmatch '_ExitTree\(\)[\s\S]*SetProcess\(false\)' -or
    $vignette -notmatch 'Intensity[\s\S]*_mat\?\.SetShaderParameter\("intensity"' -or
    $vignette -notmatch '_Ready\(\)[\s\S]*SetProcess\(Engine\.IsEditorHint\(\)\)' -or
    $vignette -notmatch '_ExitTree\(\)[\s\S]*SetProcess\(false\)') {
    Fail "Post-process UI components must process only for editor live preview and push runtime export changes through property setters."
}
$dialogUi = Read "addons/beep_game_builder_cs/ecs/ui/DialogUIComponent.cs"
if ($dialogUi -notmatch 'DialogPanelPath' -or $dialogUi -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $dialogUi -notmatch 'BindExistingPanel' -or $dialogUi -notmatch 'UsesSceneControls' -or $dialogUi -notmatch 'BuildGeneratedPanel') {
    Fail "DialogUIComponent must bind an authored KitDialogBox by default and only generate fallback UI when explicitly enabled."
}
if ($dialogUi -match '\[Export\]\s*public\s+NodePath\?\s+DialogEnginePath' -or
    $dialogUi -notmatch '\[Export\]\s*public\s+NodePath\s+DialogEnginePath\s*\{\s*get;\s*set;\s*\}\s*=\s*new\(""\)' -or
    $dialogUi -match 'DialogEnginePath == null') {
    Fail "DialogUIComponent.DialogEnginePath must export a non-null empty NodePath and treat empty as unwired."
}
foreach ($required in @("FindDialogPanel", 'Name = "DialogPanel"', 'FindChild\("DialogPanel"', 'GetParent\(\)\?\.FindChild')) {
    if ($dialogUi -notmatch $required) {
        Fail "DialogUIComponent must auto-bind conventional DialogPanel before generated fallback: $required."
    }
}
$table = Read "addons/beep_game_builder_cs/ecs/ui/TableComponent.cs"
if ($table -notmatch 'HeaderRowPath' -or $table -notmatch 'RowsContainerPath' -or $table -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $table -notmatch 'BindExistingControls' -or $table -notmatch 'UsesSceneControls' -or $table -notmatch 'BuildGeneratedHeaderRow' -or $table -notmatch 'FindHeaderRow' -or $table -notmatch 'FindRowsContainer') {
    Fail "TableComponent must bind an authored header row by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @('FindChild\("HeaderRow"', 'FindChild\("Rows"', 'GetParent\(\)\?\.FindChild')) {
    if ($table -notmatch $required) {
        Fail "TableComponent must auto-bind conventional HeaderRow/Rows containers before generated fallback: $required."
    }
}
if ($table -notmatch 'FocusMode\s*=\s*Control\.FocusModeEnum\.All' -or
    $table -notmatch 'rowPanel\.FocusEntered \+=' -or
    $table -notmatch 'rowPanel\.FocusExited \+=' -or
    $table -notmatch 'OnRowGuiInput\(Control row,\s*InputEvent e,\s*int rowIdx,\s*string\[\] values\)' -or
    $table -notmatch 'InputEventMouseButton\s*\{\s*Pressed:\s*true,\s*ButtonIndex:\s*MouseButton\.Left\s*\}' -or
    $table -notmatch 'InputEventKey key && KitChrome\.IsConfirmKey\(key\)' -or
    $table -notmatch 'row\.AcceptEvent\(\)') {
    Fail "TableComponent rows must be focusable and activate only on left-click or keyboard confirm."
}
$weatherForecastUi = Read "addons/beep_game_builder_cs/ecs/ui/WeatherForecastUI.cs"
if ($weatherForecastUi -notmatch 'RootPath' -or $weatherForecastUi -notmatch 'SlidePath' -or $weatherForecastUi -notmatch 'ForecastContainerPath' -or $weatherForecastUi -notmatch 'ToggleButtonPath' -or $weatherForecastUi -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $weatherForecastUi -notmatch 'BindExistingControls' -or $weatherForecastUi -notmatch 'UsesSceneControls' -or $weatherForecastUi -notmatch 'BuildGeneratedSurface' -or $weatherForecastUi -notmatch 'HasAuthoredControls' -or $weatherForecastUi -notmatch 'FindSlide' -or $weatherForecastUi -notmatch 'FindForecastContainer' -or $weatherForecastUi -notmatch 'FindToggleButton') {
    Fail "WeatherForecastUI must bind authored root/slide/toggle/container controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @('FindChild\("WeatherRoot"', 'FindChild\("Slide"', 'FindChild\("ForecastContainer"', 'FindChild\("WeatherToggle"', 'GetParent\(\)\?\.FindChild')) {
    if ($weatherForecastUi -notmatch $required) {
        Fail "WeatherForecastUI must auto-bind conventional weather controls before generated fallback: $required."
    }
}
if ($weatherForecastUi -notmatch 'ForecastItemScene != null' -or
    $weatherForecastUi -notmatch 'ForecastItemScene\.Instantiate\(\)' -or
    $weatherForecastUi -notmatch 'ConfigureForecastItem\(control,\s*dayIndex,\s*dayData\)' -or
    $weatherForecastUi -notmatch 'new KitWeatherForecastCard\(\)' -or
    $weatherForecastUi -notmatch 'SetForecastLabel\(control,\s*"Day"' -or
    $weatherForecastUi -notmatch 'SetForecastLabel\(control,\s*"Weather"' -or
    $weatherForecastUi -notmatch 'SetForecastLabel\(control,\s*"Temperature"' -or
    $weatherForecastUi -notmatch 'SetForecastLabel\(control,\s*"Wind"') {
    Fail "WeatherForecastUI must use ForecastItemScene when supplied and bind common authored card label names before falling back to KitWeatherForecastCard."
}
foreach ($required in @("EffectiveItemSize", "EffectiveItemSpacing", "EffectiveSlideSeconds", "float.IsFinite(ItemSize.X)", "Mathf.RoundToInt(EffectiveItemSpacing)", "float seconds = EffectiveSlideSeconds", "dayData.EffectiveWeatherType", "dayData.EffectiveTemperature", "dayData.EffectiveWindSpeed")) {
    if ($weatherForecastUi -notmatch [regex]::Escape($required)) {
        Fail "WeatherForecastUI must bound invalid layout, slide, and authored forecast values: $required."
    }
}
$weatherForecastItemProbe = Read "tests/weather_forecast_item_scene_probe.gd"
if ($weatherForecastItemProbe -notmatch 'ForecastItemScene' -or
    $weatherForecastItemProbe -notmatch '_build_card_scene' -or
    $weatherForecastItemProbe -notmatch 'UsesSceneControls\(\)' -or
    $weatherForecastItemProbe -notmatch 'WeatherForecastUI uses authored item scenes') {
    Fail "weather_forecast_item_scene_probe.gd must exercise authored WeatherForecastUI shell controls plus ForecastItemScene card binding."
}
$weatherForecastRunChecks = Read "tests/run_addon_checks.ps1"
if ($weatherForecastRunChecks -notmatch 'weather_forecast_item_scene_probe\.ps1' -or
    $weatherForecastRunChecks -notmatch 'weather_behavior_probe\.ps1' -or
    $weatherForecastRunChecks -notmatch 'weather_lifecycle_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the weather forecast item scene, behavior, and lifecycle probes."
}
$weatherForecastData = Read "addons/beep_game_builder_cs/core/WeatherForecast.cs"
foreach ($required in @("BaseTemperature", "PickWeather", "SeverityFor", "WindFor", "WeatherType.Snow", "WeatherType.Heatwave", "EffectiveWeatherType", "EffectiveIntensity", "EffectiveTemperature", "EffectiveWindSpeed", "EffectiveForecastDayCount", "EffectivePerlinNoiseScale", "EffectiveTemperatureVariance", "EffectiveBaseTemperature", "NormalizeDaysForward", "WeatherData[] days = NormalizeDaysForward()")) {
    if ($weatherForecastData -notmatch [regex]::Escape($required)) {
        Fail "WeatherForecast must generate coherent multi-type weather data: $required."
    }
}
$weatherSpriteLayer = Read "addons/beep_game_builder_cs/ecs/atmosphere/WeatherSpriteLayer.cs"
foreach ($required in @("UsePixelArtSampling", "SnapToPixelGrid", "TextureFilterEnum.Linear", "TextureFilterEnum.Nearest", "SnapIfNeeded")) {
    if ($weatherSpriteLayer -notmatch [regex]::Escape($required)) {
        Fail "WeatherSpriteLayer must default away from forced pixel-art rendering but keep explicit pixel-art controls: $required."
    }
}
$weatherSystem = Read "addons/beep_game_builder_cs/ecs/atmosphere/WeatherSystemComponent.cs"
foreach ($required in @("_nodesReady", "if (!IsActive) return;", "if (!EnsureNodes()) return;", "SetWeather(CurrentWeather);")) {
    if ($weatherSystem -notmatch [regex]::Escape($required)) {
        Fail "WeatherSystemComponent must lazily create runtime weather nodes only when active: $required."
    }
}
$dynamicFog = Read "addons/beep_game_builder_cs/ecs/atmosphere/DynamicFogLayer.cs"
foreach ($required in @("_initialized", "if (!IsActive) return;", "if (Engine.IsEditorHint() || !IsActive) return;", "DeferredInit();", "WarnMissingWeatherSystem { get; set; } = false")) {
    if ($dynamicFog -notmatch [regex]::Escape($required)) {
        Fail "DynamicFogLayer must defer runtime overlay setup while inactive and initialize when activated: $required."
    }
}
$weatherAudioController = Read "addons/beep_game_builder_cs/ecs/atmosphere/WeatherAudioController.cs"
foreach ($required in @("_setupComplete", "if (_setupComplete) return;", "if (IsActive && !_setupComplete)", "Setup();", "OnWeatherChanged((int)_weather.CurrentWeather);", "WarnMissingTracks { get; set; } = false")) {
    if ($weatherAudioController -notmatch [regex]::Escape($required)) {
        Fail "WeatherAudioController must defer audio bus/player setup while inactive and initialize when activated: $required."
    }
}
foreach ($required in @("EffectiveCrossFadeDuration <= 0f", "player.VolumeDb = targetDb", "EffectiveBusName", "EffectiveThunderDelayMin", "EffectiveThunderDelayMax")) {
    if ($weatherAudioController -notmatch [regex]::Escape($required)) {
        Fail "WeatherAudioController must avoid invalid tweens for zero/negative fade durations: $required."
    }
}
$dayNightCycle = Read "addons/beep_game_builder_cs/ecs/atmosphere/DayNightCycleComponent.cs"
foreach ($required in @("MinimumDayLengthSeconds", "EffectiveDayLengthSeconds", "float.IsFinite(DayLengthSeconds)", "NormalizeHour(TimeOfDay)", "float.IsNaN(hours)", "float.IsInfinity(hours)")) {
    if ($dayNightCycle -notmatch [regex]::Escape($required)) {
        Fail "DayNightCycleComponent must bound invalid day-length/time values before advancing the clock: $required."
    }
}
$seasonalComponent = Read "addons/beep_game_builder_cs/ecs/atmosphere/SeasonalComponent.cs"
foreach ($required in @("EffectiveDaysPerSeason", "double.IsFinite(DaysPerSeason)", "EffectiveSeasonTintStrength", "EffectiveTransitionDuration <= 0f", "EffectiveFoliageWindStrength")) {
    if ($seasonalComponent -notmatch [regex]::Escape($required)) {
        Fail "SeasonalComponent must bound invalid season tint/cycle/transition tuning: $required."
    }
}
$shelterZone = Read "addons/beep_game_builder_cs/ecs/atmosphere/ShelterZoneComponent.cs"
foreach ($required in @("LiveZones", "UpdateWeatherShelter(_weather)", "HasLiveOccupants", "weather.GetInstanceId()", "zone._weather.GetInstanceId() == weatherId", "LiveZones.Remove(this)")) {
    if ($shelterZone -notmatch [regex]::Escape($required)) {
        Fail "ShelterZoneComponent must aggregate overlapping zones before writing WeatherSystemComponent.InsideShelter: $required."
    }
}
$ambientAudioComponent = Read "addons/beep_game_builder_cs/ecs/atmosphere/AmbientAudioComponent.cs"
foreach ($required in @("_setupComplete", "if (_setupComplete || !IsActive) return;", "public override void _Process", "if (IsActive && !_setupComplete)", "WarnMissingTracks { get; set; } = false", "WarnInvalidParent { get; set; } = true")) {
    if ($ambientAudioComponent -notmatch [regex]::Escape($required)) {
        Fail "AmbientAudioComponent must defer audio-player setup while inactive and initialize when activated: $required."
    }
}
foreach ($required in @("if (!_setupComplete)", "Setup();", "if (_combatPlayer == null) return;", "EffectiveCrossfadeDuration <= 0f", "player.VolumeDb = targetDb", "EffectiveBus", "EffectiveThunderDelayMin", "EffectiveThunderDelayMax")) {
    if ($ambientAudioComponent -notmatch [regex]::Escape($required)) {
        Fail "AmbientAudioComponent public audio controls and fades must be safe before setup and with invalid fade durations: $required."
    }
}
$ambientController = Read "addons/beep_game_builder_cs/ecs/atmosphere/AmbientController.cs"
if ($ambientController -notmatch 'ForTree\(Node node, bool warnMissing = false\)' -or
    $ambientController -notmatch 'found == null && warnMissing') {
    Fail "AmbientController.ForTree must be quiet by default for standalone weather scenes, with explicit warning opt-in."
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/HealthComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive \|\| IsDead' },
    @{ Path = "addons/beep_game_builder_cs/ecs/AutoHealComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _health == null \|\| !IsActive \|\| _health\.IsDead' },
    @{ Path = "addons/beep_game_builder_cs/ecs/AggroComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/AttackComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/CameraZoomComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _cam == null \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/CropGrowthComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/HealthBarComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive \|\| _bar == null' },
    @{ Path = "addons/beep_game_builder_cs/ecs/HungerStaminaComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/InventoryComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ParticleComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ShooterCombatComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive \|\| !IsReloading' },
    @{ Path = "addons/beep_game_builder_cs/ecs/StatusEffectComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/SurvivalVitalsComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive \|\| IsDead' },
    @{ Path = "addons/beep_game_builder_cs/ecs/CooldownComponent.cs"; Pattern = '!Engine\.IsEditorHint\(\) && StartOnReady[\s\S]*Engine\.IsEditorHint\(\) \|\| !IsActive' }
)) {
    $source = Read $entry.Path
    if ($source -notmatch $entry.Pattern) {
        Fail "$($entry.Path) must not advance gameplay simulation while inactive or running as a Tool script in the editor."
    }
}
$coreGameplaySmoke = Read "tests/CoreGameplaySmoke.cs"
$csproj = Read "Beep.Godot.csproj"
$cooldownComponent = Read "addons/beep_game_builder_cs/ecs/CooldownComponent.cs"
$statusEffectComponent = Read "addons/beep_game_builder_cs/ecs/StatusEffectComponent.cs"
$healthComponent = Read "addons/beep_game_builder_cs/ecs/HealthComponent.cs"
$cameraZoomComponent = Read "addons/beep_game_builder_cs/ecs/CameraZoomComponent.cs"
$attackComponent = Read "addons/beep_game_builder_cs/ecs/AttackComponent.cs"
$projectileComponent = Read "addons/beep_game_builder_cs/ecs/ProjectileComponent.cs"
$projectileModifierComponent = Read "addons/beep_game_builder_cs/ecs/ProjectileModifierComponent.cs"
$consumableUseComponent = Read "addons/beep_game_builder_cs/ecs/ConsumableUseComponent.cs"
$gameOverOnDeathComponent = Read "addons/beep_game_builder_cs/ecs/GameOverOnDeathComponent.cs"
$hitStopComponent = Read "addons/beep_game_builder_cs/ecs/HitStopComponent.cs"
$flashComponent = Read "addons/beep_game_builder_cs/ecs/FlashComponent.cs"
$hitSoundComponent = Read "addons/beep_game_builder_cs/ecs/HitSoundComponent.cs"
$hitSparkComponent = Read "addons/beep_game_builder_cs/ecs/HitSparkComponent.cs"
$hazardComponent = Read "addons/beep_game_builder_cs/ecs/HazardComponent.cs"
$aggroComponent = Read "addons/beep_game_builder_cs/ecs/AggroComponent.cs"
$aiController = Read "addons/beep_game_builder_cs/ecs/AIController.cs"
$knockbackComponent = Read "addons/beep_game_builder_cs/ecs/KnockbackComponent.cs"
$statResource = Read "addons/beep_game_builder_cs/ecs/stats/Stat.cs"
$statsComponent = Read "addons/beep_game_builder_cs/ecs/stats/StatsComponent.cs"
$movementComponent = Read "addons/beep_game_builder_cs/ecs/MovementComponent.cs"
$topDownController = Read "addons/beep_game_builder_cs/ecs/TopDownController.cs"
$platformerController = Read "addons/beep_game_builder_cs/ecs/PlatformerController.cs"
$shooterController = Read "addons/beep_game_builder_cs/ecs/ShooterController.cs"
$dashComponent = Read "addons/beep_game_builder_cs/ecs/DashComponent.cs"
$jumpComponent = Read "addons/beep_game_builder_cs/ecs/JumpComponent.cs"
$slideComponent = Read "addons/beep_game_builder_cs/ecs/SlideComponent.cs"
$wallJumpComponent = Read "addons/beep_game_builder_cs/ecs/WallJumpComponent.cs"
$glideComponent = Read "addons/beep_game_builder_cs/ecs/GlideComponent.cs"
$hoverComponent = Read "addons/beep_game_builder_cs/ecs/HoverComponent.cs"
$flyComponent = Read "addons/beep_game_builder_cs/ecs/FlyComponent.cs"
$adaptiveDifficultyComponent = Read "addons/beep_game_builder_cs/ecs/algorithms/AdaptiveDifficultyComponent.cs"
$flockingComponent = Read "addons/beep_game_builder_cs/ecs/algorithms/FlockingComponent.cs"
$ballComponent = Read "addons/beep_game_builder_cs/ecs/algorithms/BallComponent.cs"
$steeringBehavior = Read "addons/beep_game_builder_cs/ecs/algorithms/SteeringBehavior.cs"
$heightComponent = Read "addons/beep_game_builder_cs/ecs/algorithms/HeightComponent.cs"
$cityEconomyComponent = Read "addons/beep_game_builder_cs/ecs/CityEconomyComponent.cs"
$survivalVitalsComponent = Read "addons/beep_game_builder_cs/ecs/SurvivalVitalsComponent.cs"
$raceStateComponent = Read "addons/beep_game_builder_cs/ecs/RaceStateComponent.cs"
$shooterCombatComponent = Read "addons/beep_game_builder_cs/ecs/ShooterCombatComponent.cs"
$windFieldComponent = Read "addons/beep_game_builder_cs/ecs/WindFieldComponent.cs"
$temperatureComponent = Read "addons/beep_game_builder_cs/ecs/TemperatureComponent.cs"
$spawnerComponent = Read "addons/beep_game_builder_cs/ecs/SpawnerComponent.cs"
$pickupComponent = Read "addons/beep_game_builder_cs/ecs/PickupComponent.cs"
$movingPlatformComponent = Read "addons/beep_game_builder_cs/ecs/MovingPlatformComponent.cs"
$turretComponent = Read "addons/beep_game_builder_cs/ecs/TurretComponent.cs"
$workComponent = Read "addons/beep_game_builder_cs/ecs/WorkComponent.cs"
$gameStateManagerComponent = Read "addons/beep_game_builder_cs/ecs/GameStateManagerComponent.cs"
$gameAppComponent = Read "addons/beep_game_builder_cs/ecs/GameApp.cs"
$inventoryComponent = Read "addons/beep_game_builder_cs/ecs/InventoryComponent.cs"
$inventoryDisplayComponent = Read "addons/beep_game_builder_cs/ecs/InventoryComponent.Display.cs"
$equipmentComponent = Read "addons/beep_game_builder_cs/ecs/EquipmentComponent.cs"
$stateMachineComponent = Read "addons/beep_game_builder_cs/ecs/StateMachineComponent.cs"
$particleComponent = Read "addons/beep_game_builder_cs/ecs/ParticleComponent.cs"
$trailComponent = Read "addons/beep_game_builder_cs/ecs/TrailComponent.cs"
$bootComponent = Read "addons/beep_game_builder_cs/ecs/BootComponent.cs"
$followTargetComponent = Read "addons/beep_game_builder_cs/ecs/FollowTargetComponent.cs"
$healthBarComponent = Read "addons/beep_game_builder_cs/ecs/HealthBarComponent.cs"
$screenShakeComponent = Read "addons/beep_game_builder_cs/ecs/ScreenShakeComponent.cs"
$rpgPartyComponent = Read "addons/beep_game_builder_cs/ecs/RpgPartyComponent.cs"
$respawnComponent = Read "addons/beep_game_builder_cs/ecs/RespawnComponent.cs"
$despawnOnDeathComponent = Read "addons/beep_game_builder_cs/ecs/DespawnOnDeathComponent.cs"
$dropTableComponent = Read "addons/beep_game_builder_cs/ecs/DropTableComponent.cs"
$gameFlowComponent = Read "addons/beep_game_builder_cs/ecs/GameFlowComponent.cs"
$craftingComponent = Read "addons/beep_game_builder_cs/ecs/CraftingComponent.cs"
$levelingComponent = Read "addons/beep_game_builder_cs/ecs/LevelingComponent.cs"
$cardDeckComponent = Read "addons/beep_game_builder_cs/ecs/CardDeckComponent.cs"
$strategyEmpireComponent = Read "addons/beep_game_builder_cs/ecs/StrategyEmpireComponent.cs"
$objectPoolComponent = Read "addons/beep_game_builder_cs/ecs/ObjectPoolComponent.cs"
$puzzleLevelComponent = Read "addons/beep_game_builder_cs/ecs/PuzzleLevelComponent.cs"
$resistanceComponent = Read "addons/beep_game_builder_cs/ecs/ResistanceComponent.cs"
$gameItem = Read "addons/beep_game_builder_cs/ecs/items/GameItem.cs"
$gameEquipment = Read "addons/beep_game_builder_cs/ecs/items/GameEquipment.cs"
$gameWeapon = Read "addons/beep_game_builder_cs/ecs/items/GameWeapon.cs"
$gameArmor = Read "addons/beep_game_builder_cs/ecs/items/GameArmor.cs"
$gameShield = Read "addons/beep_game_builder_cs/ecs/items/GameShield.cs"
$headlessSmoke = Read "tests/headless_runtime_smoke.gd"
foreach ($required in @("VerifyCooldown", "VerifyStatusEffects", "VerifyHealth", "VerifyCameraZoom", "VerifyCombatComponents", "VerifyMovementComponents", "VerifyAlgorithmComponentBounds", "VerifySpawnPickupPlatformBounds", "VerifyRuntimeManagerBounds", "VerifyLevelLoaderLooseLevelEntries", "VerifyGenreStateComponentBounds", "VerifyCityEconomyBounds", "VerifyMovementComponent", "VerifyControllerEffectiveValues", "VerifyJumpAndAbilityEffectiveValues", "VerifyAttack", "VerifyProjectile", "VerifyStatsFiniteValues", "VerifyDeathConsumableAndImpactFeedbackBounds", "VerifyHazard", "VerifyAggro", "VerifyAiController", "VerifyKnockback", "AdaptiveDifficultyComponent", "FlockingComponent", "BallComponent", "SteeringBehavior", "HeightComponent", "WindFieldComponent", "TemperatureComponent", "SpawnerComponent", "PickupComponent", "MovingPlatformComponent", "TurretComponent", "WorkComponent", "GameStateManagerComponent", "GameApp", "InventoryComponent", "ParticleComponent", "TrailComponent", "BootComponent", "FollowTargetComponent", "ScreenShakeComponent", "LevelLoaderComponent", "RpgPartyComponent", "RespawnComponent", "DespawnOnDeathComponent", "DropTableComponent", "GameFlowComponent", "ConsumableUseComponent", "GameOverOnDeathComponent", "HitStopComponent", "FlashComponent", "HitSoundComponent", "HitSparkComponent", "CraftingComponent", "LevelingComponent", "CardDeckComponent", "StrategyEmpireComponent", "ObjectPoolComponent", "PuzzleLevelComponent", "ResistanceComponent", "GameItem", "GameEquipment", "GameWeapon", "GameArmor", "GameShield", "CityEconomyComponent", "RaceStateComponent", "ShooterCombatComponent", "StateMachineComponent", "invalid saved economy state", "malformed saved building data", "malformed saved quest data", "malformed saved race values", "malformed saved ammo/wave values", "malformed saved resource values", "malformed saved puzzle values", "malformed saved vital values", "invalid saved ammo/wave", "invalid saved lap timing", "invalid saved state time", "malformed saved state time values", "malformed saved equipment data", "negative true damage", "non-finite", "ResetZoom", "inactive modifier", "mutated its threat table", "unnormalized direction", "invalid mana regeneration", "invalid default intensity", "non-finite follow speed", "invalid capacity", "non-finite follow offset", "invalid loose-array level entry", "malformed saved card piles", "Lifecycle/drop components accepted invalid timer", "ConsumableUseComponent accepted non-finite heal/default duration", "InventoryComponent accepted invalid item stack", "CraftingComponent accepted an invalid recipe", "CraftingComponent rejected a craftable recipe", "CraftingComponent failed to craft a valid recipe", "CraftingComponent did not apply effective input/output counts", "ObjectPoolComponent accepted invalid preload", "LevelingComponent accepted invalid authored XP", "CardDeckComponent accepted invalid deck health", "CardDeckComponent did not normalize malformed saved card piles", "StrategyEmpireComponent accepted invalid resource", "PuzzleLevelComponent accepted invalid target", "ResistanceComponent accepted invalid damage", "WeatherForecast accepted invalid day arrays", "WeatherHUDComponent accepted invalid poll timing", "WeatherForecastUI accepted invalid item layout")) {
    if ($coreGameplaySmoke -notmatch $required) {
        Fail "CoreGameplaySmoke must cover core gameplay component bounds and lifecycle behavior: $required."
    }
}
if ($headlessSmoke -notmatch 'CoreGameplaySmoke\.cs') {
    Fail "headless runtime smoke must run CoreGameplaySmoke."
}
if ($csproj -notmatch 'tests/CoreGameplaySmoke\.cs') {
    Fail "Beep.Godot.csproj must compile CoreGameplaySmoke because tests/** is excluded by default."
}
if ($cooldownComponent -notmatch 'EffectiveDuration' -or
    $cooldownComponent -notmatch 'Mathf\.Clamp\(1f - \(Remaining / EffectiveDuration\), 0f, 1f\)' -or
    $cooldownComponent -notmatch '_timer = EffectiveDuration') {
    Fail "CooldownComponent must clamp invalid durations and progress against an effective duration."
}
if ($equipmentComponent -notmatch 'v\.VariantType != Variant\.Type\.Dictionary' -or
    $equipmentComponent -notmatch 'Withdraw whatever _Ready equipped only after a real saved loadout is present' -or
    $equipmentComponent -notmatch 'var dict = v\.AsGodotDictionary\(\)') {
    Fail "EquipmentComponent.Load must ignore malformed saved equipment payloads before clearing current equipment."
}
if ($stateMachineComponent -notmatch 'ReadFloat\(Variant value, float fallback\)' -or
    $stateMachineComponent -notmatch 'double\.TryParse\(value\.AsString\(\)' -or
    $stateMachineComponent -notmatch 'double\.IsFinite\(seconds\)' -or
    $stateMachineComponent -notmatch '_stateTimers\[current\] = Mathf\.Max\(0f, ReadFloat\(timeObj, 0f\)\)') {
    Fail "StateMachineComponent.Load must ignore malformed saved state timers and parse numeric timer strings."
}
foreach ($entry in @(
    @{ Name = "BootComponent"; Source = $bootComponent; Required = @("EffectiveMinBootTime", "DeltaSeconds(double delta)", "double.IsFinite(MinBootTime)", "double.IsFinite(delta)") },
    @{ Name = "FollowTargetComponent"; Source = $followTargetComponent; Required = @("EffectiveFollowSpeed", "EffectiveMaxDistance", "Mathf.Clamp(EffectiveFollowSpeed * DeltaSeconds(delta), 0f, 1f)", "FiniteVector(_target.GlobalPosition + Offset", "double.IsFinite(delta)") },
    @{ Name = "HealthBarComponent"; Source = $healthBarComponent; Required = @("PositiveFinite(Size.X", "EffectiveHideDelay => NonNegativeFinite", "DeltaSeconds(double delta)", "double.IsFinite(delta)") },
    @{ Name = "ScreenShakeComponent"; Source = $screenShakeComponent; Required = @("EffectiveDefaultIntensity", "EffectiveDefaultDuration", "EffectiveMaxTrauma", "DeltaSeconds(double delta)", "Mathf.Clamp(_trauma / EffectiveMaxTrauma", "float.IsFinite(intensity)") },
    @{ Name = "RpgPartyComponent"; Source = $rpgPartyComponent; Required = @("EffectiveMax(BaseMaxHealth", "EffectiveManaRegenPerSecond", "EffectiveLowThreshold", "DeltaSeconds(double delta)", "VariantToInt(Variant value", "double.IsFinite(number)", "ClampPositiveInt", "qs.VariantType == Variant.Type.Dictionary", "kv.Value.VariantType != Variant.Type.Array") },
    @{ Name = "InventoryComponent"; Source = $inventoryComponent; Required = @("EffectiveMaxSlots", "EffectiveColumns", "EffectiveSlotSize", "EffectiveHoverDelay", "EffectiveSlotCount", "Mathf.Clamp(MaxSlots, 1, 512)", "Slots = new InventorySlot?[EffectiveMaxSlots]", "item.EffectiveMaxStack", "Item.EffectiveMaxStack", "item.EffectiveMaxDurability", "quantity <= 0", "public bool HasItem(string itemId, int quantity = 1) => quantity > 0") },
    @{ Name = "InventoryComponent.Display"; Source = $inventoryDisplayComponent; Required = @("Columns = EffectiveColumns", "for (int i = 0; i < EffectiveSlotCount; i++)", "CustomMinimumSize = slotSize", "_hoverTimer -= DeltaSeconds(delta)", "_hoverTimer = EffectiveHoverDelay", "double.IsFinite(delta)") },
    @{ Name = "ParticleComponent"; Source = $particleComponent; Required = @("EffectiveOffset", "IsFinite(Offset)", "_particles.Position = EffectiveOffset", "_particles.GlobalPosition = parent.GlobalPosition + EffectiveOffset", "IsFinite(parent.GlobalPosition)") },
    @{ Name = "TrailComponent"; Source = $trailComponent; Required = @("EffectiveMaxPoints", "EffectiveWidth", "EffectiveTrailColor", "while (points.Count > EffectiveMaxPoints)", "IsFinite(parent2D.GlobalPosition)", "IsFinite(TrailColor)") },
    @{ Name = "RespawnComponent"; Source = $respawnComponent; Required = @("EffectiveRespawnDelay", "tree.CreateTimer(EffectiveRespawnDelay)", "NonNegativeFinite(RespawnDelay)", "float.IsFinite(value)") },
    @{ Name = "DespawnOnDeathComponent"; Source = $despawnOnDeathComponent; Required = @("EffectiveDespawnDelay", "float delay = EffectiveDespawnDelay", "tree.CreateTimer(delay)", "NonNegativeFinite(DespawnDelay)", "float.IsFinite(value)") },
    @{ Name = "GameFlowComponent"; Source = $gameFlowComponent; Required = @("EffectiveNavigateDelay", "float delay = EffectiveNavigateDelay", "tree.CreateTimer(delay)", "NonNegativeFinite(NavigateDelay)", "float.IsFinite(value)") },
    @{ Name = "DropTableComponent"; Source = $dropTableComponent; Required = @("EffectiveMinDrops", "EffectiveMaxDrops", "EffectiveDropChance", "EffectiveDifficultyWeightMultiplier", "EffectiveScatterRadius", "EffectiveDropLifetimeSeconds", "EffectiveMaxPlacementAttempts", "EffectiveMinimumSpacing", "GD.Randf() > EffectiveDropChance", "int minDrops = EffectiveMinDrops", "float multiplier = EffectiveDifficultyWeightMultiplier", "tree.CreateTimer(lifetime)", "float.IsFinite(e.Weight) && e.Weight > 0f", "Engine.IsEditorHint()") },
    @{ Name = "ConsumableUseComponent"; Source = $consumableUseComponent; Required = @("EffectiveDefaultEffectDuration", "float.IsFinite(healAmount) && healAmount > 0f", "float.IsFinite(duration) && duration > 0f", "EffectiveDefaultEffectDuration", "NonNegativeFinite(DefaultEffectDuration)") },
    @{ Name = "GameOverOnDeathComponent"; Source = $gameOverOnDeathComponent; Required = @("EffectiveLivesToLose", "int livesToLose = EffectiveLivesToLose", "if (livesToLose <= 0) return", "flow.LoseLife(livesToLose)") },
    @{ Name = "HitStopComponent"; Source = $hitStopComponent; Required = @("EffectiveFreezeDuration", "EffectiveMinDamageThreshold", "!float.IsFinite(amount)", "duration <= 0f", "Mathf.CeilToInt(duration * 1000f)", "NonNegativeFinite(FreezeDuration)") },
    @{ Name = "FlashComponent"; Source = $flashComponent; Required = @("EffectiveFlashColor", "EffectiveFlashDuration", "EffectiveFlashCount", "int flashCount = EffectiveFlashCount", "float halfDuration = EffectiveFlashDuration * 0.5f", "PositiveFinite(FlashDuration, 0.1f)", "IsFinite(FlashColor)") },
    @{ Name = "HitSoundComponent"; Source = $hitSoundComponent; Required = @("EffectiveMinDamage", "EffectiveVolumeDb", "EffectivePitchVariation", "EffectiveBus", "PickSound()", "!float.IsFinite(amount)", "Mathf.Max(0.01f, 1f + (float)GD.RandRange(-variation, variation))", "NonNegativeFinite(MinDamage)") },
    @{ Name = "HitSparkComponent"; Source = $hitSparkComponent; Required = @("EffectiveSparkColor", "EffectiveMinDamage", "!float.IsFinite(amount)", "canvas.Modulate = EffectiveSparkColor", "NonNegativeFinite(MinDamage)", "IsFinite(SparkColor)") },
    @{ Name = "CraftingComponent"; Source = $craftingComponent; Required = @("EffectiveOutputCount", "EffectiveCraftTime", "EffectiveCount", "recipe.OutputItem == null", "recipe.EffectiveOutputCount <= 0", "inventory.HasItem(input.Item.Id, input.EffectiveCount)", "int outputCount = recipe.EffectiveOutputCount", "inventory.CanFit(recipe.OutputItem, outputCount)", "inventory.RemoveItem(input.Item!.Id, input.EffectiveCount)", "inventory.AddItem(recipe.OutputItem, outputCount)") },
    @{ Name = "LevelingComponent"; Source = $levelingComponent; Required = @("EffectiveMaxLevel", "EffectiveLevel", "EffectiveBaseXp", "EffectiveXpGrowthMultiplier", "EffectiveStatPointsPerLevel", "NormalizeProgressionState", "!float.IsFinite(amount)", "PositiveFinite(BaseXp, 100f)") },
    @{ Name = "CardDeckComponent"; Source = $cardDeckComponent; Required = @("EffectiveMaxHealth", "EffectiveStartingGold", "EffectiveEnergyPerTurn", "EffectiveHandSize", "!string.IsNullOrWhiteSpace(card)", "Energy = EffectiveEnergyPerTurn", "int handSize = EffectiveHandSize", "energyCost <= 0", "ReadInt(Variant value, int fallback)", "v.VariantType != Variant.Type.Array", "double.IsFinite(raw)", "int.TryParse(value.AsString()") },
    @{ Name = "StrategyEmpireComponent"; Source = $strategyEmpireComponent; Required = @("EffectiveStartingGold", "EffectiveStartingFood", "EffectiveStartingWood", "EffectiveGoldPerTurn", "EffectiveFoodPerTurn", "EffectiveWoodPerTurn", "EffectiveGoldUpkeepPerUnit", "EffectiveFoodPerUnit", "EffectiveStarvationLossPerTurn", "GoldDelta => EffectiveGoldPerTurn", "private static int Cost(int value)", "int goldCost = Cost(gold)", "ReadInt(Variant value, int fallback)", "double.IsFinite(raw)", "int.TryParse(value.AsString()") },
    @{ Name = "ObjectPoolComponent"; Source = $objectPoolComponent; Required = @("EffectivePreloadCount", "EffectiveMaxSize", "Mathf.Min(EffectivePreloadCount, EffectiveMaxSize)", "parent == null", "EffectiveMaxSize <= 0") },
    @{ Name = "PuzzleLevelComponent"; Source = $puzzleLevelComponent; Required = @("EffectiveTargetScore", "EffectiveMoveBudget", "EffectiveTwoStarMultiple", "EffectiveThreeStarMultiple", "EffectiveLowMovesThreshold", "FiniteOr(TwoStarMultiple, 1.5f)", "ReadInt(Variant value, int fallback)", "ReadBool(Variant value, bool fallback)", "double.IsFinite(raw)", "int.TryParse(value.AsString()", "Mathf.Clamp(ReadInt(m, EffectiveMoveBudget), 0, EffectiveMoveBudget)") },
    @{ Name = "ResistanceComponent"; Source = $resistanceComponent; Required = @("EffectivePhysical", "EffectiveFire", "EffectiveIce", "EffectivePoison", "EffectiveHoly", "EffectiveDark", "EffectiveLightning", "EffectiveTrue", "!float.IsFinite(amount)", "Multiplier(float value)") },
    @{ Name = "GameItem"; Source = $gameItem; Required = @("EffectiveMaxStack", "EffectiveMaxDurability", "Mathf.Max(1, MaxStack)", "float.IsFinite(MaxDurability)") },
    @{ Name = "GameEquipment"; Source = $gameEquipment; Required = @("EffectiveSocketCount", "Mathf.Clamp(SocketCount, 0, 16)") },
    @{ Name = "GameWeapon"; Source = $gameWeapon; Required = @("EffectiveDamage", "EffectiveRange", "EffectiveCooldown", "EffectiveAmmoPerUse", "NonNegativeFinite(Damage)", "Mathf.Max(0, AmmoPerUse)") },
    @{ Name = "GameArmor"; Source = $gameArmor; Required = @("EffectiveDefense", "Amount = EffectiveDefense", "Multiplier(Fire)", "NonNegativeFinite(Defense)") },
    @{ Name = "GameShield"; Source = $gameShield; Required = @("EffectiveDefense", "EffectiveBlockChance", "Amount = EffectiveDefense", "Multiplier(Poison)", "NonNegativeFinite(Defense)") }
)) {
    foreach ($required in $entry.Required) {
        if ($entry.Source -notmatch [regex]::Escape($required)) {
            Fail "$($entry.Name) must bound invalid authored values, saved values, and frame deltas: $required."
        }
    }
}
foreach ($entry in @(
    @{ Name = "AdaptiveDifficultyComponent"; Source = $adaptiveDifficultyComponent; Required = @("EffectiveBaseDifficulty", "EffectiveAdaptSpeed", "EffectiveStruggleHealthThreshold", "EffectiveDeathPenalty", "EffectiveDeathMemorySeconds", "DeltaSeconds", "float.IsFinite") },
    @{ Name = "FlockingComponent"; Source = $flockingComponent; Required = @("EffectiveMaxSpeed", "EffectiveNeighborRadius", "EffectiveSeparationRadius", "EffectiveSteerLerp", "SteeringBehavior.Limit(_velocity, EffectiveMaxSpeed)", "float.IsFinite") },
    @{ Name = "BallComponent"; Source = $ballComponent; Required = @("EffectiveRollFriction", "EffectiveRestitution", "EffectiveBounceGroundRetention", "EffectiveGravity", "EffectiveClaimRadius", "EffectiveReclaimDelay", "DeltaSeconds(delta)", "float.IsFinite") },
    @{ Name = "SteeringBehavior"; Source = $steeringBehavior; Required = @("NonNegative(maxSpeed)", "!IsFinite(pos)", "float.IsFinite", "return Vector2.Zero") },
    @{ Name = "HeightComponent"; Source = $heightComponent; Required = @("EffectiveHeight", "EffectiveHalfThickness", "EffectiveZIndexPerPixel", "EffectiveShadowFadeHeight", "CallDeferred(nameof(EnsureShadow))", "NonNegative(height)", "SanitizedColor") },
    @{ Name = "WindFieldComponent"; Source = $windFieldComponent; Required = @("EffectivePhysicsWindScale => NonNegative", "EffectiveCharacterPushAccel => NonNegative", "EffectiveMaxCharacterWindSpeed => NonNegative", "DeltaSeconds(delta)", "IsFinite(_weather.WindForce)", "IsFinite(body.Velocity)") },
    @{ Name = "TemperatureComponent"; Source = $temperatureComponent; Required = @("EffectiveFrozenDamagePerSec => NonNegative", "EffectiveFrozenSpeedPenalty => UnitOr", "DeltaSeconds(delta)", "FiniteOr(WinterTempOffset", "return Mathf.Clamp(FiniteOr(ambient, 20f)", "UnitOr(float value, float fallback)") },
    @{ Name = "SpawnerComponent"; Source = $spawnerComponent; Required = @("EffectiveSpawnInterval", "EffectiveMaxSpawned", "EffectiveSpawnOffset", "EffectiveSpawnRandomRange", "DeltaSeconds(delta)", "FiniteVectorOr(SpawnOffset", "NonNegativeAbs(SpawnRandomRange") },
    @{ Name = "PickupComponent"; Source = $pickupComponent; Required = @("EffectiveQuantity", "EffectiveFloatAmplitude", "EffectiveFloatSpeed", "EffectiveRespawnSeconds", "EffectiveScoreValue", "DeltaSeconds(delta)", "Engine.IsEditorHint() || !IsActive") },
    @{ Name = "MovingPlatformComponent"; Source = $movingPlatformComponent; Required = @("EffectiveSpeed", "EffectivePauseDuration", "DeltaSeconds(delta)", "IsFinite(_body.GlobalPosition)", "if (EffectiveSpeed <= 0f) return;") },
    @{ Name = "TurretComponent"; Source = $turretComponent; Required = @("EffectiveFireRate", "EffectiveProjectileDamage", "EffectiveProjectileSpeed", "EffectiveRange", "EffectiveRotationSpeed", "DeltaSeconds(delta)", "1.0 / EffectiveFireRate", "IsFinite(_target.GlobalPosition)") },
    @{ Name = "WorkComponent"; Source = $workComponent; Required = @("EffectiveAvailableWork", "EffectiveWorkSpeed", "EffectiveOutputQuantity", "EffectiveTotalWorkRequired", "DeltaSeconds(delta)", "Mathf.Clamp(1f - EffectiveAvailableWork / EffectiveTotalWorkRequired") },
    @{ Name = "GameStateManagerComponent"; Source = $gameStateManagerComponent; Required = @("EffectiveMaxSaveSlots", "EffectiveAutosaveIntervalSeconds", "DeltaSeconds(delta)", "slot >= EffectiveMaxSaveSlots") },
    @{ Name = "GameApp"; Source = $gameAppComponent; Required = @("EffectiveDifficultyMultiplier", "DeltaSeconds(delta)", "double.IsFinite(SessionPlaytimeSeconds)", "Mathf.Max(0, SessionScore + scaledAmount)") },
    @{ Name = "CityEconomyComponent"; Source = $cityEconomyComponent; Required = @("EffectiveStartingTreasury", "EffectiveSecondsPerMonth", "EffectiveTaxPerResident", "DeltaSeconds", "Find(id) != null && count > 0", "ReadInt(Variant value, int fallback)", "b.VariantType == Variant.Type.Dictionary", "double.IsFinite(raw)") },
    @{ Name = "SurvivalVitalsComponent"; Source = $survivalVitalsComponent; Required = @("ReadFloat(Variant value, float fallback)", "double.IsFinite(raw)", "float.TryParse(value.AsString()", "ClampFinite(ReadFloat(h, EffectiveMaxHealth)") },
    @{ Name = "RaceStateComponent"; Source = $raceStateComponent; Required = @("ReadInt(Variant value, int fallback)", "ReadBool(Variant value, bool fallback)", "VariantFloat(Variant value, float fallback)", "double.IsFinite(raw)", "float.TryParse(value.AsString()", "Mathf.Clamp(ReadInt(l, Lap), 1, EffectiveTotalLaps)") },
    @{ Name = "ShooterCombatComponent"; Source = $shooterCombatComponent; Required = @("ReadInt(Variant value, int fallback)", "double.IsFinite(raw)", "int.TryParse(value.AsString()", "Mathf.Clamp(ReadInt(m, Magazine), 0, EffectiveMagazineSize)") }
)) {
    foreach ($required in $entry.Required) {
        if ($entry.Source -notmatch [regex]::Escape($required)) {
            Fail "$($entry.Name) must bound non-finite gameplay values: $required."
        }
    }
}
if ($statusEffectComponent -notmatch 'NormalizeEffectId' -or
    $statusEffectComponent -notmatch 'float\.IsFinite\(tickInterval\)' -or
    $statusEffectComponent -notmatch 'float\.IsFinite\(duration\)' -or
    $statusEffectComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $statusEffectComponent -notmatch 'maxStacks = Mathf\.Max\(1, maxStacks\)' -or
    $statusEffectComponent -notmatch 'if \(string\.IsNullOrEmpty\(id\)\) return') {
    Fail "StatusEffectComponent must normalize ids and clamp invalid effect timing/stack inputs."
}
if ($healthComponent -notmatch 'EffectiveMaxHealth' -or
    $healthComponent -notmatch 'NormalizeHealth\(\)' -or
    $healthComponent -notmatch '!float\.IsFinite\(damage\.Amount\)' -or
    $healthComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $healthComponent -notmatch 'float\.IsFinite\(state\.Combat\.Health\)') {
    Fail "HealthComponent must normalize invalid health state and reject non-positive damage."
}
if ($cameraZoomComponent -notmatch 'ResolveCamera\(\)' -or
    $cameraZoomComponent -notmatch 'EffectiveZoomStep\(\)' -or
    $cameraZoomComponent -notmatch 'ClampZoom\(Vector2 value\)' -or
    $cameraZoomComponent -notmatch 'SanitizeZoom\(MinZoom\)' -or
    $cameraZoomComponent -notmatch 'Mathf\.Min\(minZoom\.X, maxZoom\.X\)') {
    Fail "CameraZoomComponent must normalize bounds, step, and cached camera references."
}
if ($attackComponent -notmatch 'EffectiveCooldown => NonNegative\(Cooldown\)' -or
    $attackComponent -notmatch 'EffectiveRange => NonNegative\(Range\)' -or
    $attackComponent -notmatch 'EffectiveProjectileSpeed => NonNegative\(ProjectileSpeed\)' -or
    $attackComponent -notmatch 'float\.IsFinite\(value\)' -or
    $attackComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $attackComponent -notmatch '!IsFinite\(target\)' -or
    $attackComponent -notmatch 'private bool SpawnProjectile' -or
    $attackComponent -notmatch 'proj\.QueueFree\(\);[\s\S]*return false;' -or
    $attackComponent -notmatch 'CooldownRemaining = EffectiveCooldown;[\s\S]*EmitSignal\(SignalName\.Attacked') {
    Fail "AttackComponent must clamp authored combat values, free invalid projectile instances, and only cooldown/signal after a real attack."
}
if ($projectileComponent -notmatch 'GetSiblingComponent<ProjectileModifierComponent>\(\) is \{ IsActive: true \}' -or
    $projectileComponent -notmatch 'EffectiveSpeed => NonNegative\(Speed\)' -or
    $projectileComponent -notmatch 'EffectiveMaxLifetime => NonNegative\(MaxLifetime\)' -or
    $projectileComponent -notmatch 'EffectiveDamage => NonNegative\(Damage\)' -or
    $projectileComponent -notmatch 'EffectiveGravityStrength => NonNegative\(GravityStrength\)' -or
    $projectileComponent -notmatch 'EffectiveArcGravity' -or
    $projectileComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $projectileComponent -notmatch 'IsFinite\(direction\)') {
    Fail "ProjectileComponent must ignore inactive modifiers and clamp invalid speed/lifetime/gravity values."
}
if ($projectileModifierComponent -notmatch 'Speed = NonNegative\(speed\)' -or
    $projectileModifierComponent -notmatch 'IsFinite\(direction\)' -or
    $projectileModifierComponent -notmatch 'EffectiveHomingStrength \* dt' -or
    $projectileModifierComponent -notmatch 'int maxBounces = EffectiveMaxBounces' -or
    $projectileModifierComponent -notmatch 'double\.IsFinite\(delta\)') {
    Fail "ProjectileModifierComponent must clamp speed, homing interpolation, and bounce counts."
}
if ($hazardComponent -notmatch 'EffectiveDamage => NonNegative\(Damage\)' -or
    $hazardComponent -notmatch 'float\.IsFinite\(TickInterval\)' -or
    $hazardComponent -notmatch 'EffectiveHazardHeight' -or
    $hazardComponent -notmatch 'EffectiveHazardHalfThickness => NonNegative\(HazardHalfThickness\)' -or
    $hazardComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $hazardComponent -notmatch 'if \(amount <= 0f\) return;' -or
    $hazardComponent -notmatch 'EffectiveHazardHalfThickness \+ 16f') {
    Fail "HazardComponent must clamp damage, tick interval, and height thickness."
}
if ($aggroComponent -notmatch 'EffectiveDeaggroRange => NonNegative\(DeaggroRange\)' -or
    $aggroComponent -notmatch 'EffectiveThreatDecayRate => NonNegative\(ThreatDecayRate\)' -or
    $aggroComponent -notmatch '!float\.IsFinite\(amount\)' -or
    $aggroComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $aggroComponent -notmatch 'new List<KeyValuePair<Node2D, float>>\(ThreatTable\)' -or
    $aggroComponent -notmatch 'float currentThreat = float\.IsFinite\(kv\.Value\)') {
    Fail "AggroComponent must reject invalid threat, clamp tuning, and avoid mutating during dictionary enumeration."
}
if ($aiController -notmatch 'else\s+_moveDir = Vector2\.Zero;' -or
    $aiController -notmatch 'EffectiveSpeed => NonNegative\(Speed\)' -or
    $aiController -notmatch 'EffectiveDetectionRange => NonNegative\(DetectionRange\)' -or
    $aiController -notmatch 'EffectiveWanderChangeRate' -or
    $aiController -notmatch 'double\.IsFinite\(delta\)' -or
    $aiController -notmatch '_stats\?\.GetValue\("move_speed", EffectiveSpeed\)' -or
    $aiController -notmatch '_waypointIndex < 0 \|\| _waypointIndex >= Waypoints\.Length' -or
    $aiController -notmatch 'wp == null \|\| !GodotObject\.IsInstanceValid\(wp\)' -or
    $aiController -notmatch 'EffectiveWanderChangeRate') {
    Fail "AIController must stop while stunned, clamp speed/wander chance, and clear movement for invalid patrol waypoints."
}
if ($knockbackComponent -notmatch 'EffectiveStrength => NonNegative\(Strength\)' -or
    $knockbackComponent -notmatch 'EffectiveFriction => NonNegative\(Friction\)' -or
    $knockbackComponent -notmatch 'EffectiveDuration => NonNegative\(Duration\)' -or
    $knockbackComponent -notmatch 'EffectiveMaxKnockbackMagnitude => NonNegative\(MaxKnockbackMagnitude\)' -or
    $knockbackComponent -notmatch '!IsFinite\(fromPosition\)' -or
    $knockbackComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $knockbackComponent -notmatch 'if \(strength <= 0f\) return;' -or
    $knockbackComponent -notmatch 'MoveToward\(Vector2\.Zero, EffectiveFriction') {
    Fail "KnockbackComponent must clamp invalid force and timing values."
}
if ($movementComponent -notmatch 'EffectiveSpeed => NonNegative\(Speed\)' -or
    $movementComponent -notmatch 'EffectiveAcceleration => NonNegative\(Acceleration\)' -or
    $movementComponent -notmatch 'EffectiveFriction => NonNegative\(Friction\)' -or
    $movementComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $movementComponent -notmatch '!IsFinite\(direction\)' -or
    $movementComponent -notmatch 'InputActionsAvailable\("move_left", "move_right", "move_up", "move_down"\)' -or
    $movementComponent -notmatch 'normalized \* speed') {
    Fail "MovementComponent must clamp movement tuning, gate raw Input reads, and normalize direct Move directions."
}
if ($topDownController -notmatch 'EffectiveSpeed => NonNegative\(Speed\)' -or
    $topDownController -notmatch 'EffectiveAcceleration => NonNegative\(Acceleration\)' -or
    $topDownController -notmatch 'EffectiveFriction => NonNegative\(Friction\)' -or
    $topDownController -notmatch 'double\.IsFinite\(delta\)' -or
    $topDownController -notmatch 'InputActionsAvailable[\s\S]*MoveToward\(Vector2\.Zero, EffectiveFriction' -or
    $topDownController -notmatch 'NonNegative\(_stats\?\.GetValue\("move_speed", EffectiveSpeed\) \?\? EffectiveSpeed\)') {
    Fail "TopDownController must clamp movement tuning, stats output, and decelerate when input actions are missing."
}
if ($platformerController -notmatch 'EffectiveSpeed => NonNegative\(Speed\)' -or
    $platformerController -notmatch 'EffectiveGravity => NonNegative\(Gravity\)' -or
    $platformerController -notmatch 'float\.IsFinite\(JumpVelocity\)' -or
    $platformerController -notmatch 'EffectiveCoyoteTime => NonNegative\(CoyoteTime\)' -or
    $platformerController -notmatch 'EffectiveJumpBufferTime => NonNegative\(JumpBufferTime\)' -or
    $platformerController -notmatch 'double\.IsFinite\(delta\)' -or
    $platformerController -notmatch 'if \(!isStunned && Input\.IsActionJustPressed\("jump"\)\)' -or
    $platformerController -notmatch 'NonNegative\(_stats\?\.GetValue\("move_speed", EffectiveSpeed\) \?\? EffectiveSpeed\)') {
    Fail "PlatformerController must clamp movement/jump tuning, stats output, and avoid buffering jumps while stunned."
}
if ($shooterController -notmatch 'EffectiveMoveSpeed => NonNegative\(MoveSpeed\)' -or
    $shooterController -notmatch 'float\.IsFinite\(FireRate\)' -or
    $shooterController -notmatch 'EffectiveProjectileDamage => NonNegative\(ProjectileDamage\)' -or
    $shooterController -notmatch 'EffectiveProjectileSpeed => NonNegative\(ProjectileSpeed\)' -or
    $shooterController -notmatch 'double\.IsFinite\(delta\)' -or
    $shooterController -notmatch '1\.0 / EffectiveFireRate' -or
    $shooterController -notmatch 'proj\.QueueFree\(\);') {
    Fail "ShooterController must clamp movement/fire/projectile tuning and free invalid projectile scene roots."
}
if ($dashComponent -notmatch 'EffectiveDashSpeed => NonNegative\(DashSpeed\)' -or
    $dashComponent -notmatch 'EffectiveDashDuration => NonNegative\(DashDuration\)' -or
    $dashComponent -notmatch 'EffectiveDashCooldown => NonNegative\(DashCooldown\)' -or
    $dashComponent -notmatch 'EffectiveStaminaCost => NonNegative\(StaminaCost\)' -or
    $dashComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $dashComponent -notmatch 'if \(EffectiveDashDuration <= 0f \|\| EffectiveDashSpeed <= 0f\)[\s\S]*return;') {
    Fail "DashComponent must clamp invalid dash tuning and refuse zero/negative dash activation before spending stamina."
}
if ($jumpComponent -notmatch 'float\.IsFinite\(JumpForce\)' -or
    $jumpComponent -notmatch 'EffectiveMaxJumps => Mathf\.Max\(0, MaxJumps\)' -or
    $jumpComponent -notmatch 'float\.IsFinite\(VariableJumpMultiplier\)' -or
    $jumpComponent -notmatch 'float\.IsFinite\(ApexThreshold\)' -or
    $jumpComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $jumpComponent -notmatch 'if \(isStunned\)[\s\S]*_bufferTimer = 0f;' -or
    $jumpComponent -notmatch 'ForceJump[\s\S]*float\.IsFinite\(force\)') {
    Fail "JumpComponent must clamp jump/apex tuning, clear buffered jumps while stunned, and normalize forced jump direction."
}
if ($slideComponent -notmatch 'EffectiveSlideSpeed => NonNegative\(SlideSpeed\)' -or
    $slideComponent -notmatch 'EffectiveSlideDuration => NonNegative\(SlideDuration\)' -or
    $slideComponent -notmatch 'EffectiveSlideDeceleration => NonNegative\(SlideDeceleration\)' -or
    $slideComponent -notmatch 'float\.IsFinite\(HeightMultiplier\)' -or
    $slideComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $slideComponent -notmatch 'HasEffect\("stun"\)[\s\S]*EndSlide\(\)' -or
    $slideComponent -notmatch 'if \(EffectiveSlideDuration <= 0f \|\| EffectiveSlideSpeed <= 0f\)') {
    Fail "SlideComponent must clamp slide tuning and restore active slide state when stunned."
}
if ($wallJumpComponent -notmatch 'EffectiveRayDistance => NonNegative\(RayDistance\)' -or
    $wallJumpComponent -notmatch 'EffectiveWallSlideSpeed => NonNegative\(WallSlideSpeed\)' -or
    $wallJumpComponent -notmatch 'float\.IsFinite\(WallJumpForceY\)' -or
    $wallJumpComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $wallJumpComponent -notmatch 'HasEffect\("stun"\)[\s\S]*_isWallSliding = false' -or
    $wallJumpComponent -notmatch 'EffectiveWallJumpLockTime') {
    Fail "WallJumpComponent must clamp ray/slide/jump tuning and clear slide state when stunned."
}
if ($glideComponent -notmatch 'EffectiveGlideFallSpeed => NonNegative\(GlideFallSpeed\)' -or
    $glideComponent -notmatch 'EffectiveGlideAirSpeed => NonNegative\(GlideAirSpeed\)' -or
    $glideComponent -notmatch 'EffectiveGlideAccel => NonNegative\(GlideAccel\)' -or
    $glideComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $glideComponent -notmatch 'HasEffect\("stun"\)[\s\S]*GlideEnded') {
    Fail "GlideComponent must clamp glide tuning and end active glide when stunned."
}
if ($hoverComponent -notmatch 'EffectiveHoverGravity => NonNegative\(HoverGravity\)' -or
    $hoverComponent -notmatch 'EffectiveMaxHoverTime => NonNegative\(MaxHoverTime\)' -or
    $hoverComponent -notmatch 'EffectiveHoverCooldown => NonNegative\(HoverCooldown\)' -or
    $hoverComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $hoverComponent -notmatch 'HasEffect\("stun"\)[\s\S]*HoverEnded') {
    Fail "HoverComponent must clamp hover tuning and end active hover when stunned."
}
if ($flyComponent -notmatch 'EffectiveMaxSpeed => NonNegative\(MaxSpeed\)' -or
    $flyComponent -notmatch 'float\.IsFinite\(BoostMultiplier\)' -or
    $flyComponent -notmatch 'double\.IsFinite\(delta\)' -or
    $flyComponent -notmatch 'EnableBoost[\s\S]*InputActionsAvailable\("move_left", "move_right", "move_up", "move_down", BoostAction\)' -or
    $flyComponent -notmatch 'if \(stunned\)[\s\S]*_boostTimer = 0f;' -or
    $flyComponent -notmatch 'Mathf\.Clamp\(EffectiveTurnSpeed \* dt, 0f, 1f\)' -or
    $flyComponent -notmatch 'Mathf\.Clamp\(EffectiveBankSpeed \* dt, 0f, 1f\)') {
    Fail "FlyComponent must clamp flight/boost/banking tuning, guard boost input, and cancel boost while stunned."
}
if ($statResource -notmatch 'if \(!float\.IsFinite\(mod\.Amount\)\)\s*\r?\n\s*return;' -or
    $statResource -notmatch '!float\.IsFinite\(mod\.Duration\)' -or
    $statResource -notmatch '!float\.IsFinite\(amount\)' -or
    $statResource -notmatch 'float baseValue = float\.IsFinite\(BaseValue\)' -or
    $statResource -notmatch '!float\.IsFinite\(_cached\)') {
    Fail "Stat must reject non-finite modifiers, expire non-finite durations, and keep computed values finite."
}
if ($statsComponent -notmatch 'double\.IsFinite\(delta\)' -or $statsComponent -notmatch 'Mathf\.Max\(0f, \(float\)delta\)') {
    Fail "StatsComponent must tick durations with a finite non-negative delta."
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/AIController.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/DashComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/FlyComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/FootstepComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| _player == null \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/GlideComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/HoverComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/KnockbackComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| _remaining <= 0 \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/JumpComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/PlatformerController.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/SlideComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/TopDownController.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/WallJumpComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| _body == null \|\| !GodotObject\.IsInstanceValid\(_body\) \|\| !IsActive' },
    @{ Path = "addons/beep_game_builder_cs/ecs/WindFieldComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive \|\| _area == null \|\| !GodotObject\.IsInstanceValid\(_area\)' },
    @{ Path = "addons/beep_game_builder_cs/ecs/grid/GridPathFollowerComponent.cs"; Pattern = 'Engine\.IsEditorHint\(\) \|\| !IsActive' }
)) {
    $source = Read $entry.Path
    if ($source -notmatch $entry.Pattern) {
        Fail "$($entry.Path) must guard physics simulation against editor-time and inactive execution."
    }
}
$ninePatchFrame = Read "addons/beep_game_builder_cs/ecs/ui/NinePatchFrameComponent.cs"
if ($ninePatchFrame -notmatch 'FramePath' -or $ninePatchFrame -notmatch 'BuildInEditor\s*\{\s*get;\s*set;\s*\}\s*=\s*true' -or $ninePatchFrame -notmatch 'FindFrame' -or $ninePatchFrame -notmatch 'UsesSceneControls' -or $ninePatchFrame -notmatch '_rect\.Texture\s*=\s*null' -or $ninePatchFrame -notmatch '_rect\.Visible\s*=\s*false') {
    Fail "NinePatchFrameComponent must suppress legacy authored 9-patch UI chrome instead of drawing texture-backed frames."
}
foreach ($required in @('FindChild\("BeepFrame"', 'GetParent\(\)\?\.FindChild')) {
    if ($ninePatchFrame -notmatch $required) {
        Fail "NinePatchFrameComponent must auto-bind conventional BeepFrame controls before suppressing them: $required."
    }
}
if ($ninePatchFrame -match 'OverrideTexture|PatchMargin|BuildGeneratedFrame|StyleFrame|SetEditedOwner|AddChild\(_rect\)') {
    Fail "NinePatchFrameComponent must not expose texture overrides, patch margins, or generated frame creation."
}
foreach ($scenePath in @(
    "addons/beep_game_builder_cs/templates/scenes/topdown_main.tscn",
    "addons/beep_game_builder_cs/templates/scenes/survival_main.tscn",
    "addons/beep_game_builder_cs/templates/scenes/shooter_main.tscn",
    "addons/beep_game_builder_cs/templates/scenes/racing_main.tscn",
    "addons/beep_game_builder_cs/templates/scenes/platformer_main.tscn"
)) {
    $weatherScene = Read $scenePath
    foreach ($required in @(
        "RootPath = NodePath(`"WeatherRoot`")",
        "SlidePath = NodePath(`"WeatherRoot/Slide`")",
        "ForecastContainerPath = NodePath(`"WeatherRoot/Slide/ForecastContainer`")",
        "ToggleButtonPath = NodePath(`"WeatherRoot/WeatherToggle`")",
        "GenerateControlsWhenPathsEmpty = false",
        "KitPushButton.cs",
        "[node name=`"WeatherRoot`" type=`"VBoxContainer`" parent=`"HUD/Root/WeatherForecast`"]",
        "[node name=`"Slide`" type=`"Control`" parent=`"HUD/Root/WeatherForecast/WeatherRoot`"]",
        "[node name=`"ForecastContainer`" type=`"VBoxContainer`" parent=`"HUD/Root/WeatherForecast/WeatherRoot/Slide`"]",
        "[node name=`"WeatherToggle`" type=`"Button`" parent=`"HUD/Root/WeatherForecast/WeatherRoot`"]"
    )) {
        if ($weatherScene -notmatch [regex]::Escape($required)) { Fail "$scenePath must author WeatherForecastUI shell node $required." }
    }
}
$cityBuilderTemplate = Read "addons/beep_game_builder_cs/templates/scenes/citybuilder_main.tscn"
foreach ($required in @(
    "BoundButtonPaths = Array[NodePath]([NodePath(`"../Pause`"), NodePath(`"../Normal`"), NodePath(`"../Fast`"), NodePath(`"../Fastest`")])",
    "[node name=`"Pause`" type=`"Button`" parent=`"HUD/Root/SpeedBar`"]",
    "[node name=`"Normal`" type=`"Button`" parent=`"HUD/Root/SpeedBar`"]",
    "[node name=`"Fast`" type=`"Button`" parent=`"HUD/Root/SpeedBar`"]",
    "[node name=`"Fastest`" type=`"Button`" parent=`"HUD/Root/SpeedBar`"]",
    "CategoryRowPath = NodePath(`"../ToolbarSurface/Categories`")",
    "PaletteContainerPath = NodePath(`"../ToolbarSurface/PaletteScroll/Palette`")",
    "GenerateControlsWhenPathsEmpty = false",
    "[node name=`"ToolbarSurface`" type=`"VBoxContainer`" parent=`"HUD/Root/BottomDock/BuildMargin`"]",
    "[node name=`"Categories`" type=`"HBoxContainer`" parent=`"HUD/Root/BottomDock/BuildMargin/ToolbarSurface`"]",
    "[node name=`"PaletteScroll`" type=`"ScrollContainer`" parent=`"HUD/Root/BottomDock/BuildMargin/ToolbarSurface`"]",
    "[node name=`"Palette`" type=`"HBoxContainer`" parent=`"HUD/Root/BottomDock/BuildMargin/ToolbarSurface/PaletteScroll`"]"
)) {
    if ($cityBuilderTemplate -notmatch [regex]::Escape($required)) { Fail "citybuilder_main.tscn is missing design-time BuildToolbar container $required." }
}

$runtimeSmoke = Read "tests/runtime_smoke.ps1"
if ($runtimeSmoke -notmatch 'SCRIPT ERROR\|ERROR:\|Exception\|C# backtrace') { Fail "runtime_smoke.ps1 does not scan Godot output for script/runtime errors." }
$headlessSmoke = Read "tests/headless_runtime_smoke.gd"
if ($headlessSmoke -notmatch 'GridPlacementSmoke\.cs') { Fail "headless runtime smoke does not run GridPlacementSmoke." }
$gridVariantReader = Read "addons/beep_game_builder_cs/ecs/grid/GridVariantReader.cs"
foreach ($required in @(
    "TryDictionary(Variant value",
    "Array(Godot.Collections.Dictionary data, string key)",
    "Int(Variant value, int fallback",
    "Float(Variant value, float fallback",
    "Bool(Variant value, bool fallback",
    "Vector2I(Variant value, Vector2I fallback)",
    "double.IsFinite(raw)",
    "int.TryParse(text",
    "float.TryParse(text",
    "bool.TryParse(value.AsString()"
)) {
    if ($gridVariantReader -notmatch [regex]::Escape($required)) {
        Fail "GridVariantReader must provide tolerant shared parsing for malformed grid authored/save data: $required."
    }
}
$gridDefinitionReader = Read "addons/beep_game_builder_cs/ecs/grid/GridDefinitionReader.cs"
foreach ($required in @(
    "ReadString(Godot.Collections.Dictionary data, string pascal, string snake, string fallback)",
    "ReadString(Resource resource, string pascal, string snake, string fallback)",
    "GridVariantReader.Int(ReadVariant(data, pascal, snake), fallback)",
    "GridVariantReader.Float(ReadVariant(resource, pascal, snake), fallback)",
    "GridVariantReader.Bool(ReadVariant(data, pascal, snake), fallback)",
    "data.ContainsKey(snake)",
    "resource.Get(snake)"
)) {
    if ($gridDefinitionReader -notmatch [regex]::Escape($required)) {
        Fail "GridDefinitionReader must read dual pascal/snake keys from dictionaries and duck-typed Resources through GridVariantReader: $required."
    }
}
$gridProjection = Read "addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs"
if ($gridProjection -notmatch 'class\s+GridProjectionComponent' -or $gridProjection -notmatch 'CellToWorld' -or $gridProjection -notmatch 'WorldToCell' -or $gridProjection -notmatch 'CellCorners') {
    Fail "GridProjectionComponent is missing the expected reusable grid math surface."
}
foreach ($required in @("EffectiveTileSize", "EffectiveOrigin", "float.IsFinite(worldPosition.X)", "float.IsFinite(TileSize.X)", "float.IsFinite(Origin.X)")) {
    if ($gridProjection -notmatch [regex]::Escape($required)) {
        Fail "GridProjectionComponent must bound invalid grid tile/origin values before map math uses them: $required."
    }
}
$gridObject = Read "addons/beep_game_builder_cs/ecs/grid/GridObjectComponent.cs"
if ($gridObject -notmatch 'class\s+GridObjectComponent' -or $gridObject -notmatch 'Configure' -or $gridObject -notmatch 'CaptureState' -or $gridObject -notmatch 'ApplyParentMetadata' -or $gridObject -notmatch 'GameplayComponent') {
    Fail "GridObjectComponent is missing the expected inspectable grid object surface."
}
foreach ($required in @("ObjectKind", "Description", "EffectiveCategory", "PlacementPath", "NavigationPath", "ReserveFootprintOnReady", "ReservePlacementFootprint", "ReserveNavigationFootprint", "ReleaseReservedFootprintOnExit", "ReserveFootprint", "ReleaseFootprint", "FootprintCells", "grid_object_blocks_navigation", "grid_object_description")) {
    if ($gridObject -notmatch [regex]::Escape($required)) {
        Fail "GridObjectComponent must optionally reserve authored object footprints in placement and navigation: $required."
    }
}
$gridObjectInspector = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridObjectInspectorComponent.cs"
if ($gridObjectInspector -notmatch 'class\s+GridObjectInspectorComponent' -or $gridObjectInspector -notmatch 'RebuildInspector' -or $gridObjectInspector -notmatch 'SelectedObject' -or $gridObjectInspector -notmatch 'GridSelectionComponent' -or $gridObjectInspector -notmatch 'GridObjectComponent') {
    Fail "GridObjectInspectorComponent is missing the expected selection-to-HUD inspection surface."
}
foreach ($required in @("EffectiveCategory", "Description")) {
    if ($gridObjectInspector -notmatch [regex]::Escape($required)) {
        Fail "GridObjectInspectorComponent must render authored grid object kind/description fields: $required."
    }
}
if ($gridObjectInspector -notmatch 'PanelPath' -or $gridObjectInspector -notmatch 'TitleLabelPath' -or $gridObjectInspector -notmatch 'DetailsLabelPath' -or $gridObjectInspector -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridObjectInspector -notmatch 'BindExistingControls' -or $gridObjectInspector -notmatch 'UsesSceneControls' -or $gridObjectInspector -notmatch 'BuildGeneratedControls') {
    Fail "GridObjectInspectorComponent must bind authored inspector labels by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindPanel", "FindTitleLabel", "FindDetailsLabel", 'FindChild\("Panel"', 'FindChild\("Title"', 'FindChild\("Details"', 'GetParent\(\)\?\.FindChild')) {
    if ($gridObjectInspector -notmatch $required) {
        Fail "GridObjectInspectorComponent must auto-bind conventional Panel/Title/Details controls before generated fallback: $required."
    }
}
$gridMinimap = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridMinimapComponent.cs"
if ($gridMinimap -notmatch 'class\s+GridMinimapComponent' -or $gridMinimap -notmatch 'RebuildMinimap' -or $gridMinimap -notmatch 'CellToMinimap' -or $gridMinimap -notmatch 'GridRoadComponent' -or $gridMinimap -notmatch 'GridJobQueueComponent') {
    Fail "GridMinimapComponent is missing the expected grid overview HUD surface."
}
foreach ($required in @("EffectiveBoundsSize", "EffectiveCameraZoom", "float.IsFinite(worldPosition.X)", "float.IsFinite(size.X)")) {
    if ($gridMinimap -notmatch [regex]::Escape($required)) {
        Fail "GridMinimapComponent must bound invalid bounds, camera zoom, and layout values before drawing: $required."
    }
}
$gridNavigation = Read "addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs"
if ($gridNavigation -notmatch 'class\s+GridNavigationComponent' -or $gridNavigation -notmatch 'FindCellPath' -or $gridNavigation -notmatch 'PriorityQueue' -or $gridNavigation -notmatch 'RoadPath' -or $gridNavigation -notmatch 'CellDataPath' -or $gridNavigation -notmatch 'TraversalCost') {
    Fail "GridNavigationComponent is missing the expected reusable A* pathfinding surface."
}
foreach ($required in @("!GodotObject.IsInstanceValid(_placement)", "!GodotObject.IsInstanceValid(_roads)", "!GodotObject.IsInstanceValid(_cellData)", "TreatCellDataBlockedAsBlocked", "BlockedTerrainKinds", "TerrainCostMultipliers", "MinimumTerrainCostMultiplier")) {
    if ($gridNavigation -notmatch [regex]::Escape($required)) {
        Fail "GridNavigationComponent must integrate placement, roads, and cell terrain data: $required."
    }
}
foreach ($required in @("DataLayersPath", "DataLayers.TerrainAt(cell)")) {
    if ($gridNavigation -notmatch [regex]::Escape($required)) {
        Fail "GridNavigationComponent must optionally read terrain kinds from TerrainDataLayersComponent when DataLayersPath is wired: $required."
    }
}
$gridRoad = Read "addons/beep_game_builder_cs/ecs/grid/GridRoadComponent.cs"
if ($gridRoad -notmatch 'class\s+GridRoadComponent' -or $gridRoad -notmatch 'SetRoad' -or $gridRoad -notmatch 'TrySetRoad' -or $gridRoad -notmatch 'CanBuildRoad' -or $gridRoad -notmatch 'GetTraversalCostMultiplier' -or $gridRoad -notmatch 'CaptureState' -or $gridRoad -notmatch 'ISaveable') {
    Fail "GridRoadComponent is missing the expected reusable road/path surface."
}
foreach ($required in @("EffectiveDefaultRoadCostMultiplier", "EffectiveRoadWidthRatio", "EffectiveOutlineWidth", "float.IsFinite(costMultiplier)", "CellDataPath", "TreatCellDataBlockedAsUnroadable", "TreatBlockedTerrainKindsAsUnroadable", "RoadRejected")) {
    if ($gridRoad -notmatch [regex]::Escape($required)) {
        Fail "GridRoadComponent must bound invalid road cost and draw tuning values: $required."
    }
}
$gridFollower = Read "addons/beep_game_builder_cs/ecs/grid/GridPathFollowerComponent.cs"
if ($gridFollower -notmatch 'class\s+GridPathFollowerComponent' -or $gridFollower -notmatch 'MoveToCell' -or $gridFollower -notmatch 'AdvancePath') {
    Fail "GridPathFollowerComponent is missing the expected reusable grid movement surface."
}
foreach ($required in @("CancelMove();", "!GodotObject.IsInstanceValid(_grid)", "!GodotObject.IsInstanceValid(_navigation)")) {
    if ($gridFollower -notmatch [regex]::Escape($required)) {
        Fail "GridPathFollowerComponent must clear stuck moves and refresh stale grid/navigation references: $required."
    }
}
foreach ($required in @("EffectiveSpeed", "EffectiveStopDistance", "float.IsFinite(point.X)", "double.IsFinite(delta)")) {
    if ($gridFollower -notmatch [regex]::Escape($required)) {
        Fail "GridPathFollowerComponent must bound invalid speed, stop distance, path point, and delta values: $required."
    }
}
foreach ($required in @(
    "SetCellPath(Godot.Collections.Array cells)",
    "SetWorldPath(Godot.Collections.Array points)",
    "foreach (Variant value in cells)",
    "foreach (Variant value in points)",
    "GridVariantReader.TryReadCell(value",
    "GridVariantReader.TryReadWorldPoint(value"
)) {
    if ($gridFollower -notmatch [regex]::Escape($required)) {
        Fail "GridPathFollowerComponent must accept loose authored/GDScript path arrays without typed-array casts: $required."
    }
}
$gridSelection = Read "addons/beep_game_builder_cs/ecs/grid/GridSelectionComponent.cs"
if ($gridSelection -notmatch 'class\s+GridSelectionComponent' -or $gridSelection -notmatch 'SelectCell' -or $gridSelection -notmatch 'FinishDrag' -or $gridSelection -notmatch 'CellsInRect') {
    Fail "GridSelectionComponent is missing the expected reusable grid selection surface."
}
$gridCamera = Read "addons/beep_game_builder_cs/ecs/grid/GridCameraControllerComponent.cs"
if ($gridCamera -notmatch 'class\s+GridCameraControllerComponent' -or $gridCamera -notmatch 'FocusWorld' -or $gridCamera -notmatch 'ZoomAtWorldPoint' -or $gridCamera -notmatch 'ClampPosition') {
    Fail "GridCameraControllerComponent is missing the expected reusable map camera surface."
}
foreach ($required in @("EffectivePanSpeed", "EffectiveZoomStep", "EffectivePositionSmoothing", "EffectiveZoomSmoothing", "EffectiveBoundsSize", "EffectiveZoomRange", "DeltaSeconds(double delta)", "FiniteVector(Vector2 value", "float.IsFinite(value.X)", "double.IsFinite(delta)")) {
    if ($gridCamera -notmatch [regex]::Escape($required)) {
        Fail "GridCameraControllerComponent must bound invalid camera speed, zoom, smoothing, bounds, vectors, and frame deltas: $required."
    }
}
$gridJobQueue = Read "addons/beep_game_builder_cs/ecs/grid/GridJobQueueComponent.cs"
if ($gridJobQueue -notmatch 'class\s+GridJobQueueComponent' -or $gridJobQueue -notmatch 'AddJob' -or $gridJobQueue -notmatch 'ClaimNextJob' -or $gridJobQueue -notmatch 'CompleteJob' -or $gridJobQueue -notmatch 'LoadJobs') {
    Fail "GridJobQueueComponent is missing the expected reusable cell-job queue surface."
}
if ($gridJobQueue -notmatch 'GetJobClaimedBy') {
    Fail "GridJobQueueComponent must expose GetJobClaimedBy so worker assignment can enforce claim ownership."
}
foreach ($required in @("EffectiveDefaultWorkSeconds", "ClampWorkSeconds", "float.IsFinite(workSeconds)")) {
    if ($gridJobQueue -notmatch [regex]::Escape($required)) {
        Fail "GridJobQueueComponent must bound invalid authored and loaded work durations: $required."
    }
}
foreach ($required in @("GridVariantReader.TryDictionary(value", "GridVariantReader.Int(dict, key, fallback)", "GridVariantReader.Float(dict, key, fallback)", "GridVariantReader.Vector2I(dict, key, fallback)")) {
    if ($gridJobQueue -notmatch [regex]::Escape($required)) {
        Fail "GridJobQueueComponent must parse loose/malformed saved jobs through GridVariantReader: $required."
    }
}
$gridJobBoard = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridJobBoardComponent.cs"
if ($gridJobBoard -notmatch 'class\s+GridJobBoardComponent' -or $gridJobBoard -notmatch 'RebuildBoard' -or $gridJobBoard -notmatch 'RefreshBoard' -or $gridJobBoard -notmatch 'CancelJob' -or $gridJobBoard -notmatch 'GridJobQueueComponent') {
    Fail "GridJobBoardComponent is missing the expected reusable job HUD surface."
}
if ($gridJobBoard -notmatch 'TitleLabelPath' -or $gridJobBoard -notmatch 'SummaryLabelPath' -or $gridJobBoard -notmatch 'RowsContainerPath' -or $gridJobBoard -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridJobBoard -notmatch 'BindExistingControls' -or $gridJobBoard -notmatch 'UsesSceneControls') {
    Fail "GridJobBoardComponent must bind authored panel controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindTitleLabel", "FindSummaryLabel", "FindRowsContainer", 'FindChild\("Title"', 'FindChild\("Summary"', 'FindChild\("Rows"')) {
    if ($gridJobBoard -notmatch $required) {
        Fail "GridJobBoardComponent must auto-bind conventional design-time children before generated fallback: $required."
    }
}
$gridJobEffect = Read "addons/beep_game_builder_cs/ecs/grid/GridJobEffectComponent.cs"
if ($gridJobEffect -notmatch 'class\s+GridJobEffectComponent' -or $gridJobEffect -notmatch 'ApplyJobEffect' -or $gridJobEffect -notmatch 'JobEffectApplied' -or $gridJobEffect -notmatch 'clear_land' -or $gridJobEffect -notmatch 'harvest' -or $gridJobEffect -notmatch 'ResourceNodesRootPath' -or $gridJobEffect -notmatch 'ApplyGather') {
    Fail "GridJobEffectComponent is missing the expected job-to-cell-effect surface."
}
foreach ($required in @("_connectedQueue", "_resolvedJobQueuePath", "explicitQueuePathChanged", "DisconnectQueue();")) {
    if ($gridJobEffect -notmatch [regex]::Escape($required)) {
        Fail "GridJobEffectComponent must reconnect cleanly when the authored job queue changes: $required."
    }
}
$gridWorker = Read "addons/beep_game_builder_cs/ecs/grid/GridWorkerComponent.cs"
if ($gridWorker -notmatch 'class\s+GridWorkerComponent' -or $gridWorker -notmatch 'ClaimNextJob' -or $gridWorker -notmatch 'GridPathFollowerComponent' -or $gridWorker -notmatch 'CompleteCurrentJob') {
    Fail "GridWorkerComponent is missing the expected reusable worker/job execution surface."
}
foreach ($required in @("EffectiveClaimInterval", "EffectiveWorkSpeed", "GetJobClaimedBy(jobId) != WorkerId", "claimed_by_another_worker", "complete_rejected")) {
    if ($gridWorker -notmatch [regex]::Escape($required)) {
        Fail "GridWorkerComponent must enforce job ownership and bound invalid worker tuning: $required."
    }
}
foreach ($required in @("double.IsFinite(delta)", "!GodotObject.IsInstanceValid(_body)", "float.IsFinite(ClaimIntervalSeconds)", "float.IsFinite(WorkSpeedMultiplier)")) {
    if ($gridWorker -notmatch [regex]::Escape($required)) {
        Fail "GridWorkerComponent must ignore invalid frame deltas and refresh stale body references: $required."
    }
}
foreach ($required in @("!GodotObject.IsInstanceValid(_queue)", "!GodotObject.IsInstanceValid(_grid)", "!GodotObject.IsInstanceValid(_follower)")) {
    if ($gridWorker -notmatch [regex]::Escape($required)) {
        Fail "GridWorkerComponent must refresh stale queue/grid/follower references: $required."
    }
}
$gridSmoke = Read "tests/GridPlacementSmoke.cs"
if ($gridSmoke -notmatch 'VerifyGridWorkerRejectsClaimedJob' -or
    $gridSmoke -notmatch 'claimedBy=' -or
    $gridSmoke -notmatch 'Cancelling the owning worker did not release the claimed job') {
    Fail "GridPlacementSmoke must cover claimed-job ownership and worker release behavior."
}
foreach ($required in @("VerifyPathFollowerBoundsInvalidTuning", "VerifyGridJobQueueBoundsInvalidWorkSeconds", "VerifyGridWorkerBoundsInvalidTuning")) {
    if ($gridSmoke -notmatch $required) {
        Fail "GridPlacementSmoke must cover invalid job, worker, and path tuning regression cases: $required."
    }
}
$gridWorkerStatusPanel = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridWorkerStatusPanelComponent.cs"
if ($gridWorkerStatusPanel -notmatch 'class\s+GridWorkerStatusPanelComponent' -or $gridWorkerStatusPanel -notmatch 'RebuildPanel' -or $gridWorkerStatusPanel -notmatch 'RefreshPanel' -or $gridWorkerStatusPanel -notmatch 'CancelWorkerJob' -or $gridWorkerStatusPanel -notmatch 'GridWorkerComponent') {
    Fail "GridWorkerStatusPanelComponent is missing the expected reusable worker status HUD surface."
}
if ($gridWorkerStatusPanel -notmatch 'TitleLabelPath' -or $gridWorkerStatusPanel -notmatch 'SummaryLabelPath' -or $gridWorkerStatusPanel -notmatch 'RowsContainerPath' -or $gridWorkerStatusPanel -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridWorkerStatusPanel -notmatch 'BindExistingControls' -or $gridWorkerStatusPanel -notmatch 'UsesSceneControls') {
    Fail "GridWorkerStatusPanelComponent must bind authored panel controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindTitleLabel", "FindSummaryLabel", "FindRowsContainer", 'FindChild\("Title"', 'FindChild\("Summary"', 'FindChild\("Rows"')) {
    if ($gridWorkerStatusPanel -notmatch $required) {
        Fail "GridWorkerStatusPanelComponent must auto-bind conventional design-time children before generated fallback: $required."
    }
}
$gridWorkerSpawner = Read "addons/beep_game_builder_cs/ecs/grid/GridWorkerSpawnerComponent.cs"
if ($gridWorkerSpawner -notmatch 'class\s+GridWorkerSpawnerComponent' -or $gridWorkerSpawner -notmatch 'SpawnWorker' -or $gridWorkerSpawner -notmatch 'UnitSpawned' -or $gridWorkerSpawner -notmatch 'GridPathFollowerComponent' -or $gridWorkerSpawner -notmatch 'GridWorkerComponent') {
    Fail "GridWorkerSpawnerComponent is missing the expected base-to-worker spawning surface."
}
foreach ($required in @("!GodotObject.IsInstanceValid(_unitsRoot)", "!GodotObject.IsInstanceValid(_grid)", "!GodotObject.IsInstanceValid(_navigation)", "!GodotObject.IsInstanceValid(_jobs)", "!GodotObject.IsInstanceValid(_cellData)", "!GodotObject.IsInstanceValid(_placement)")) {
    if ($gridWorkerSpawner -notmatch [regex]::Escape($required)) {
        Fail "GridWorkerSpawnerComponent must refresh stale root/grid/navigation/job/cell-data/placement references: $required."
    }
}
foreach ($required in @("EffectiveMaxWorkers", "EffectiveInitialWorkers", "EffectiveDefaultUnitSpeed", "SafeName(WorkerIdPrefix)", "float.IsFinite(DefaultUnitSpeed)")) {
    if ($gridWorkerSpawner -notmatch [regex]::Escape($required)) {
        Fail "GridWorkerSpawnerComponent must bound invalid spawn limits/speed and sanitize generated worker ids: $required."
    }
}
foreach ($required in @("CellDataPath", "PlacementPath", "CanSpawnAt", "SpawnBlockReason", "TreatCellDataBlockedAsUnspawnable", "TreatBlockedTerrainKindsAsUnspawnable", "TreatPlacementOccupiedAsUnspawnable", "AllowedTerrainKinds", "BlockedTerrainKinds")) {
    if ($gridWorkerSpawner -notmatch [regex]::Escape($required)) {
        Fail "GridWorkerSpawnerComponent must validate spawn cells against terrain/cell data and placement occupancy: $required."
    }
}
$gridWorkerSpawnerPanel = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridWorkerSpawnerPanelComponent.cs"
if ($gridWorkerSpawnerPanel -notmatch 'class\s+GridWorkerSpawnerPanelComponent' -or $gridWorkerSpawnerPanel -notmatch 'RequestSpawn' -or $gridWorkerSpawnerPanel -notmatch 'RefreshPanel' -or $gridWorkerSpawnerPanel -notmatch 'GridWorkerSpawnerComponent' -or $gridWorkerSpawnerPanel -notmatch 'TitleLabelPath' -or $gridWorkerSpawnerPanel -notmatch 'CountLabelPath' -or $gridWorkerSpawnerPanel -notmatch 'SpawnButtonPath' -or $gridWorkerSpawnerPanel -notmatch 'GenerateControlsWhenPathsEmpty') {
    Fail "GridWorkerSpawnerPanelComponent is missing the expected base spawn HUD surface."
}
if ($gridWorkerSpawnerPanel -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false') {
    Fail "GridWorkerSpawnerPanelComponent must bind authored controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindTitleLabel", "FindCountLabel", "FindSpawnButton", 'FindChild\("Title"', 'FindChild\("Count"', 'FindChild\("SpawnButton"')) {
    if ($gridWorkerSpawnerPanel -notmatch $required) {
        Fail "GridWorkerSpawnerPanelComponent must auto-bind conventional design-time children before generated fallback: $required."
    }
}
$gridSelectionJobCommand = Read "addons/beep_game_builder_cs/ecs/grid/GridSelectionJobCommandComponent.cs"
if ($gridSelectionJobCommand -notmatch 'class\s+GridSelectionJobCommandComponent' -or $gridSelectionJobCommand -notmatch 'QueueSelectedCells' -or $gridSelectionJobCommand -notmatch 'QueueRectangle' -or $gridSelectionJobCommand -notmatch 'GridJobQueueComponent') {
    Fail "GridSelectionJobCommandComponent is missing the expected selection-to-job command surface."
}
foreach ($required in @("EffectiveWorkSeconds", "float.IsFinite(WorkSeconds)", "float.IsFinite(workSeconds)")) {
    if ($gridSelectionJobCommand -notmatch [regex]::Escape($required)) {
        Fail "GridSelectionJobCommandComponent must bound invalid authored and override work durations: $required."
    }
}
foreach ($required in @(
    "QueueCells(Godot.Collections.Array cells",
    "foreach (Variant value in cells)",
    "GridVariantReader.TryReadCell(value"
)) {
    if ($gridSelectionJobCommand -notmatch [regex]::Escape($required)) {
        Fail "GridSelectionJobCommandComponent must accept loose authored/GDScript cell arrays without typed-array casts: $required."
    }
}
foreach ($required in @("CellDataPath", "NavigationPath", "CanQueueJobAt", "QueueBlockReason", "UseNavigationBounds", "RejectNavigationBlockedCells", "TreatCellDataBlockedAsUnqueueable", "TreatBlockedTerrainKindsAsUnqueueable", "BlockedTerrainKinds", "AllowedTerrainKinds", "no_valid_cells")) {
    if ($gridSelectionJobCommand -notmatch [regex]::Escape($required)) {
        Fail "GridSelectionJobCommandComponent must validate queued work against terrain/cell data and navigation bounds: $required."
    }
}
$gridCellData = Read "addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs"
if ($gridCellData -notmatch 'class\s+GridCellDataComponent' -or $gridCellData -notmatch 'PlantCrop' -or $gridCellData -notmatch 'AdvanceDay' -or $gridCellData -notmatch 'HarvestReady' -or $gridCellData -notmatch 'LoadCells') {
    Fail "GridCellDataComponent is missing the expected Stardew-style cell state surface."
}
foreach ($required in @("GridVariantReader.TryDictionary(value", "GridVariantReader.Int(dict, key, fallback)", "GridVariantReader.Vector2I(dict, key, fallback)")) {
    if ($gridCellData -notmatch [regex]::Escape($required)) {
        Fail "GridCellDataComponent must parse loose/malformed saved cells through GridVariantReader: $required."
    }
}
foreach ($required in @("int regrowDays = -1", "CropRegrowDays", "crop_regrow_days", "RemoveCrop")) {
    if ($gridCellData -notmatch [regex]::Escape($required)) {
        Fail "GridCellDataComponent must support regrowing crops (harvest re-arms the growth clock, interval survives saves, RemoveCrop uproots): $required."
    }
}
$gridToolAction = Read "addons/beep_game_builder_cs/ecs/grid/GridToolActionComponent.cs"
if ($gridToolAction -notmatch 'class\s+GridToolActionComponent' -or $gridToolAction -notmatch 'ToolAction' -or $gridToolAction -notmatch 'ApplyToCell' -or $gridToolAction -notmatch 'Plant' -or $gridToolAction -notmatch 'Harvest' -or $gridToolAction -notmatch 'QueueJob' -or $gridToolAction -notmatch 'RoadPath' -or $gridToolAction -notmatch 'RemoveRoad' -or $gridToolAction -notmatch 'GridRoadComponent' -or $gridToolAction -notmatch 'CropCatalogPath' -or $gridToolAction -notmatch 'CalendarPath' -or $gridToolAction -notmatch 'ResourceWalletPath' -or $gridToolAction -notmatch 'AddHarvestYieldToWallet') {
    Fail "GridToolActionComponent is missing the expected Stardew-style tool action surface."
}
foreach ($required in @("EffectiveRoadCostMultiplier", "EffectiveCropDaysToMature", "EffectiveJobWorkSeconds", "EffectiveCropId", "EffectiveJobKind", "EffectiveRoadKind")) {
    if ($gridToolAction -notmatch [regex]::Escape($required)) {
        Fail "GridToolActionComponent must bound invalid crop, road, and queued-job tuning: $required."
    }
}
foreach ($required in @(
    "ApplyToCells(Godot.Collections.Array cells",
    "foreach (Variant value in cells)",
    "GridVariantReader.TryReadCell(value"
)) {
    if ($gridToolAction -notmatch [regex]::Escape($required)) {
        Fail "GridToolActionComponent must accept loose authored/GDScript cell arrays without typed-array casts: $required."
    }
}
foreach ($required in @("NavigationPath", "UseNavigationBounds", "RejectNavigationBlockedCellsForJobs", "TreatBlockedTerrainKindsAsUnworkable", "BlockedTerrainKinds", "AllowedTerrainKinds", "CanWorkTerrain", "IsBlockedTerrainKind", "WorkJobBlockReason", "unworkable_terrain", "cell_out_of_bounds")) {
    if ($gridToolAction -notmatch [regex]::Escape($required)) {
        Fail "GridToolActionComponent must reject direct tools/jobs on unworkable terrain and out-of-bounds cells: $required."
    }
}
foreach ($required in @("ConsumeSeedsFromWallet", "missing_seeds", "TrySpendAmount(seedId, 1)", "RegrowDays(cropId)", "DataLayersPath")) {
    if ($gridToolAction -notmatch [regex]::Escape($required)) {
        Fail "GridToolActionComponent must charge authored seed costs on plant and pass crop regrowth through to cell data: $required."
    }
}
$gridToolPalette = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridToolPaletteComponent.cs"
if ($gridToolPalette -notmatch 'class\s+GridToolPaletteComponent' -or $gridToolPalette -notmatch 'SelectTool' -or $gridToolPalette -notmatch 'ApplySelectedTool' -or $gridToolPalette -notmatch 'VisibleToolButtonCount' -or $gridToolPalette -notmatch 'SelectedActionName' -or $gridToolPalette -notmatch 'ShowRoad' -or $gridToolPalette -notmatch 'ShowRemoveRoad' -or $gridToolPalette -notmatch 'InteractionModePath' -or $gridToolPalette -notmatch 'AutoSwitchInteractionMode' -or $gridToolPalette -notmatch 'BoundActionNames' -or $gridToolPalette -notmatch 'BoundButtonPaths' -or $gridToolPalette -notmatch 'GenerateControlsWhenPathsEmpty') {
    Fail "GridToolPaletteComponent is missing the expected reusable tool palette surface."
}
if ($gridToolPalette -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false') {
    Fail "GridToolPaletteComponent must bind authored buttons by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasConventionalToolButtons", "FindToolButton", "BindToolButton", 'Name = \$"Tool_\{action\}"', 'FindChild\(name', 'GetParent\(\)\?\.FindChild')) {
    if ($gridToolPalette -notmatch $required) {
        Fail "GridToolPaletteComponent must auto-bind conventional Tool_* buttons before generated fallback: $required."
    }
}
$gridCropDefinition = Read "addons/beep_game_builder_cs/ecs/grid/GridCropDefinition.cs"
if ($gridCropDefinition -notmatch 'class\s+GridCropDefinition' -or $gridCropDefinition -notmatch 'DaysToMature' -or $gridCropDefinition -notmatch 'RegrowDays' -or $gridCropDefinition -notmatch 'CanPlantIn') {
    Fail "GridCropDefinition is missing the expected reusable crop data surface."
}
if ($gridCropDefinition -notmatch 'EffectiveDaysToMature' -or
    $gridCropDefinition -notmatch 'EffectiveYieldCount' -or
    $gridCropDefinition -notmatch 'TryRead\(Variant entry') {
    Fail "GridCropDefinition must expose bounded values and tolerant authored-data parsing."
}
foreach ($required in @("GridVariantReader.TryDictionary(entry", "using static Beep.ECS.GridDefinitionReader;", 'ReadInt(data, "DaysToMature", "days_to_mature"', 'ReadBool(resource, "Spring", "spring"')) {
    if ($gridCropDefinition -notmatch [regex]::Escape($required)) {
        Fail "GridCropDefinition must parse loose/malformed authored data through the shared GridDefinitionReader: $required."
    }
}
$gridCropCatalog = Read "addons/beep_game_builder_cs/ecs/grid/GridCropCatalogComponent.cs"
if ($gridCropCatalog -notmatch 'class\s+GridCropCatalogComponent' -or $gridCropCatalog -notmatch 'FindCrop' -or $gridCropCatalog -notmatch 'CanPlant' -or $gridCropCatalog -notmatch 'CropIdsForSeason' -or $gridCropCatalog -notmatch 'YieldItem') {
    Fail "GridCropCatalogComponent is missing the expected seasonal crop lookup surface."
}
if ($gridCropCatalog -match 'Array\s*<\s*GridCropDefinition\s*>' -or
    $gridCropCatalog -match 'foreach\s*\(\s*GridCropDefinition\s+\w+\s+in\s+Crops\)' -or
    $gridCropCatalog -notmatch 'GridCropDefinition\.Enumerate\(Crops\)') {
    Fail "GridCropCatalogComponent must use untyped crop definitions to avoid managed cast failures."
}
$gridCellOverlay = Read "addons/beep_game_builder_cs/ecs/grid/GridCellOverlayComponent.cs"
if ($gridCellOverlay -notmatch 'class\s+GridCellOverlayComponent' -or $gridCellOverlay -notmatch 'ColorForCell' -or $gridCellOverlay -notmatch 'VisibleCellCount' -or $gridCellOverlay -notmatch 'DrawColoredPolygon') {
    Fail "GridCellOverlayComponent is missing the expected reusable cell visual feedback surface."
}
if ($gridCellOverlay -notmatch 'EffectiveOutlineWidth' -or $gridCellOverlay -notmatch 'float\.IsFinite\(OutlineWidth\)') {
    Fail "GridCellOverlayComponent must bound invalid outline width before drawing."
}
# The overlay reads the typed EnumerateFlags view now - no Variant snapshot is
# marshalled at all, which supersedes the old "go through GridVariantReader"
# requirement this check used to enforce.
if ($gridCellOverlay -notmatch 'EnumerateFlags\(\)') {
    Fail "GridCellOverlayComponent must read cells through GridCellDataComponent.EnumerateFlags, not per-cell Variant snapshots."
}
$gridTileMapBridge = Read "addons/beep_game_builder_cs/ecs/grid/GridTileMapLayerBridgeComponent.cs"
if ($gridTileMapBridge -notmatch 'class\s+GridTileMapLayerBridgeComponent' -or $gridTileMapBridge -notmatch 'TileMapLayerPath' -or $gridTileMapBridge -notmatch 'GridCellDataComponent' -or $gridTileMapBridge -notmatch 'GridRoadComponent' -or $gridTileMapBridge -notmatch 'AtlasForCell' -or $gridTileMapBridge -notmatch 'SetCell') {
    Fail "GridTileMapLayerBridgeComponent is missing the expected Godot TileMapLayer sync surface."
}
if ($gridTileMapBridge -notmatch 'GridVariantReader.Vector2I\(cellData, "cell"') {
    Fail "GridTileMapLayerBridgeComponent must read cell snapshots through GridVariantReader instead of direct Variant casts."
}
# The painted view's bridge is gone with the painter it fed: the surface is now
# drawn by a shader that reads the generated grid directly, so there is no second
# component translating cells into paint samples.
$gridSplatRenderer = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainPaintedRendererComponent.cs"
if ($gridSplatRenderer -notmatch 'class\s+TerrainPaintedRendererComponent' -or
    $gridSplatRenderer -notmatch 'TerrainGeneratorPath' -or
    $gridSplatRenderer -notmatch 'public void Rebuild\(\)' -or
    $gridSplatRenderer -notmatch 'BuildIdMap\(field, size, out ImageTexture shadeMap, out ImageTexture coastMap\)') {
    Fail "TerrainPaintedRendererComponent must draw the generated grid as one shader surface fed by an uploaded id map."
}
# A fragment shader cannot read a TileMapLayer, so the grid is uploaded as a
# texture; neighbour lookups are then one-texel samples, which is what makes edge
# blending possible at all.
foreach ($required in @("id_map", "shade_map", "coast_map", "map_size", "blend_width", "beach_tiles")) {
    if ($gridSplatRenderer -notmatch [regex]::Escape($required)) {
        Fail "TerrainPaintedRendererComponent must upload the terrain grid and its blending inputs to the shader: $required."
    }
}
if ($gridSplatRenderer -notmatch 'RefreshOnReady && !Engine\.IsEditorHint\(\)' -or
    $gridSplatRenderer -notmatch 'CallDeferred\(nameof\(Rebuild\)\)') {
    Fail "TerrainPaintedRendererComponent must not repaint while opening editor scenes, and runtime ready rebuild must be deferred."
}
# Where a view sits in the stack belongs to TerrainLayers. A per-renderer z dial
# beside it is a second owner of one fact, and the views then disagree.
if ($gridSplatRenderer -notmatch '_surface\.ZIndex = TerrainLayers\.ZForFloor\(\)' -or
    $gridSplatRenderer -match '\[Export\][^\r\n]*ZIndex') {
    Fail "TerrainPaintedRendererComponent must take its z index from TerrainLayers rather than exporting a second one."
}
$gridTerrainGenerator = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs"
if ($gridTerrainGenerator -notmatch 'class\s+TerrainGeneratorComponent' -or
    $gridTerrainGenerator -notmatch 'GenerateTerrain' -or
    $gridTerrainGenerator -notmatch 'GridCellDataComponent' -or
    $gridTerrainGenerator -notmatch 'LoadGeneratedCells|LoadCells') {
    Fail "TerrainGeneratorComponent must generate seeded terrain kinds into GridCellDataComponent."
}
# ONE field per settings, written in one pass. The failure this replaced was a
# terrain kind decided by an isolated secondary mask, which is how an island came
# to contain arbitrary water cuts.
foreach ($required in @("TerrainGenerationSettings settings = CurrentSettings()", "GeneratedTerrainField field = FieldFor(settings)", "field.TerrainAtCell(", "GetGenerationDiagnostics()", "ApplyMapSetup(")) {
    if ($gridTerrainGenerator -notmatch [regex]::Escape($required)) {
        Fail "TerrainGeneratorComponent must build one terrain field per settings and write it in a single pass: $required."
    }
}
$terrainDataLayers = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainDataLayersComponent.cs"
foreach ($required in @("TerrainAt(", "ResourceAt(", "FeatureAt(", "ReliefAt(", "IsWaterAt(", "PassableAt(", "ContinentAt(", "IsStartPositionAt(", "StartCells()", "DescribeContinent", "DescribeStart")) {
    if ($terrainDataLayers -notmatch [regex]::Escape($required)) {
        Fail "TerrainDataLayersComponent must publish terrain, resource, feature, relief, water, passability, continent, and start-position data layers: $required."
    }
}
# One noise set per run, each channel on its own seed offset, so changing one
# stage's frequency cannot shift another stage's pattern.
# ApplyMapSetup OVERWRITES exported generator settings, so a value typed into the
# Inspector for any of them is discarded before it is read. That is only
# acceptable while it is written down: the doc comment names all eleven, and this
# keeps the two in step, so a twelfth derived write cannot be added silently.
$applyMapSetup = [regex]::Match($gridTerrainGenerator, 'public void ApplyMapSetup\([\s\S]*?
        \}')
if (-not $applyMapSetup.Success) { Fail "TerrainGeneratorComponent.ApplyMapSetup not found." }
$assigned = [regex]::Matches($applyMapSetup.Value, '(?m)^\s{12}([A-Z][A-Za-z]*)\s*=') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$documented = @(
    "ArchipelagoIslandCount", "ClimateLatitudeCentre", "FeatureDensity", "HillsFraction",
    "LakeCoverage", "LandmassScale", "Landform", "MountainsFraction", "ResourceDensity",
    "RiverDensity", "StartPositionCount"
) | Sort-Object
$undocumented = @($assigned | Where-Object { $_ -notin $documented })
if ($undocumented.Count -gt 0) {
    Fail "ApplyMapSetup overwrites $($undocumented -join ', ') without naming them as derived; a setting silently discarded is worse than one rejected."
}
foreach ($name in $documented) {
    if ($gridTerrainGenerator -notmatch [regex]::Escape($name)) {
        Fail "TerrainGeneratorComponent no longer has the derived setting $name; update the ApplyMapSetup contract."
    }
}

# TerrainWorldComponent.Build overwrites five MORE generator settings outside
# ApplyMapSetup entirely (BoundsSize, Seed, ResourceSet, and the two booleans
# that matter: UseClimateBiomeMaps and UseScaleRules, forced true
# unconditionally). Same rule as above, same reason: undocumented here means
# ClimateLatitudeSpan/MinBiomeRegionFraction can be typed into the Inspector
# and silently discarded for every TerrainWorldComponent-built world.
$terrainWorldForBuild = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs"
$buildMethod = [regex]::Match($terrainWorldForBuild, 'public void Build\(\)[\s\S]*?
        \}')
if (-not $buildMethod.Success) { Fail "TerrainWorldComponent.Build not found." }
$buildAssigned = [regex]::Matches($buildMethod.Value, '(?m)^\s{12}_generator\.([A-Z][A-Za-z]*)\s*=') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$buildDocumented = @("BoundsSize", "Seed", "ResourceSet", "UseClimateBiomeMaps", "UseScaleRules") | Sort-Object
$buildUndocumented = @($buildAssigned | Where-Object { $_ -notin $buildDocumented })
if ($buildUndocumented.Count -gt 0) {
    Fail "TerrainWorldComponent.Build overwrites $($buildUndocumented -join ', ') on the generator without naming them as derived in Build's doc comment."
}

$terrainNoiseSet = Read "addons/beep_game_builder_cs/ecs/terrain/TerrainNoiseSet.cs"
if ($terrainNoiseSet -notmatch 'internal sealed class TerrainNoiseSet' -or
    $terrainNoiseSet -notmatch 'FastNoiseLite Shape' -or
    $terrainNoiseSet -notmatch 'FastNoiseLite Moisture' -or
    $terrainNoiseSet -notmatch 'FastNoiseLite Temperature') {
    Fail "TerrainNoiseSet must own every noise channel a generation run needs, rather than each stage allocating its own."
}
$gridCalendar = Read "addons/beep_game_builder_cs/ecs/grid/GridCalendarComponent.cs"
if ($gridCalendar -notmatch 'class\s+GridCalendarComponent' -or $gridCalendar -notmatch 'AdvanceDay' -or $gridCalendar -notmatch 'GridSeason' -or $gridCalendar -notmatch 'CaptureState' -or $gridCalendar -notmatch 'GridCellDataComponent') {
    Fail "GridCalendarComponent is missing the expected Stardew-style calendar/crop advancement surface."
}
foreach ($required in @("EffectiveSecondsPerDay", "EffectiveDaysPerSeason", "DeltaSeconds(double delta)", "PositiveFinite(SecondsPerDay", "NonNegativeFinite(DictFloat", "float.IsFinite(_dayClock)", "double.IsFinite(delta)")) {
    if ($gridCalendar -notmatch [regex]::Escape($required)) {
        Fail "GridCalendarComponent must bound invalid day length, season length, saved clocks, and frame deltas: $required."
    }
}
foreach ($required in @("GridVariantReader.TryDictionary(value", "GridVariantReader.Int(dict, key, fallback)", "GridVariantReader.Float(dict, key, fallback)")) {
    if ($gridCalendar -notmatch [regex]::Escape($required)) {
        Fail "GridCalendarComponent must parse loose/malformed saved calendar state through GridVariantReader: $required."
    }
}
$gridCalendarHud = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridCalendarHudComponent.cs"
if ($gridCalendarHud -notmatch 'class\s+GridCalendarHudComponent' -or $gridCalendarHud -notmatch 'RebuildHud' -or $gridCalendarHud -notmatch 'RefreshHud' -or $gridCalendarHud -notmatch 'RequestAdvanceDay' -or $gridCalendarHud -notmatch 'GridCalendarComponent') {
    Fail "GridCalendarHudComponent is missing the expected reusable calendar HUD surface."
}
if ($gridCalendarHud -notmatch 'DateLabelPath' -or $gridCalendarHud -notmatch 'DayProgressPath' -or $gridCalendarHud -notmatch 'AdvanceButtonPath' -or $gridCalendarHud -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridCalendarHud -notmatch 'BindExistingControls' -or $gridCalendarHud -notmatch 'UsesSceneControls') {
    Fail "GridCalendarHudComponent must bind authored scene controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindDateLabel", "FindDayProgress", "FindAdvanceButton", 'FindChild\("Date"', 'FindChild\("DayProgress"', 'FindChild\("AdvanceDay"')) {
    if ($gridCalendarHud -notmatch $required) {
        Fail "GridCalendarHudComponent must auto-bind conventional design-time children before generated fallback: $required."
    }
}
$gridWorldState = Read "addons/beep_game_builder_cs/ecs/grid/GridWorldStateComponent.cs"
if ($gridWorldState -notmatch 'class\s+GridWorldStateComponent' -or $gridWorldState -notmatch 'CaptureState' -or $gridWorldState -notmatch 'RestoreState' -or $gridWorldState -notmatch 'ISaveable' -or $gridWorldState -notmatch 'SaveableHelper\.Group' -or $gridWorldState -notmatch 'cell_data') {
    Fail "GridWorldStateComponent is missing the expected grid snapshot/save surface."
}
foreach ($required in @("RoadPath", "ObjectsRootPath", "CaptureGridObjects", "grid_objects", "CaptureGridObjectStates", "RestoreGridObjectStates", "ReleaseGridObjectFootprints", "GridObjectComponent")) {
    if ($gridWorldState -notmatch $required) {
        Fail "GridWorldStateComponent must capture and restore roads plus authored grid object state: $required."
    }
}
foreach ($required in @("GridVariantReader.TryDictionary(value", "GridVariantReader.Array(state, key)", "GridVariantReader.Vector2I(value")) {
    if ($gridWorldState -notmatch [regex]::Escape($required)) {
        Fail "GridWorldStateComponent must restore loose/malformed saved arrays through GridVariantReader: $required."
    }
}
$gridPlacement = Read "addons/beep_game_builder_cs/ecs/grid/GridPlacementComponent.cs"
if ($gridPlacement -notmatch 'bool\s+IsOccupied\(Vector2I cell\)') { Fail "GridPlacementComponent does not expose IsOccupied for navigation integration." }
if ($gridPlacement -notmatch 'BeginPlacement\(GridBuildDefinition' -or $gridPlacement -notmatch 'ResourceWalletPath' -or $gridPlacement -notmatch 'MovePreviewToCell' -or $gridPlacement -notmatch 'ConfigurePlacedObject' -or $gridPlacement -notmatch 'GridObjectComponent' -or $gridPlacement -notmatch 'NavigationPath') {
    Fail "GridPlacementComponent is missing catalog-driven build placement support."
}
foreach ($required in @("!GodotObject.IsInstanceValid(_grid)", "!GodotObject.IsInstanceValid(_placementRoot)", "!GodotObject.IsInstanceValid(_resourceWallet)", "!GodotObject.IsInstanceValid(_cellData)", "!GodotObject.IsInstanceValid(_navigation)", "IsInsideTree() ? EntityComponent.FindComponent", "TreatCellDataBlockedAsUnplaceable", "TreatBlockedTerrainKindsAsUnplaceable", "AllowedTerrainKinds", "BlockedTerrainKinds", "MarkPlacedCellsBlockedInNavigation", "SetFootprintNavigationBlocked")) {
    if ($gridPlacement -notmatch [regex]::Escape($required)) {
        Fail "GridPlacementComponent must refresh cached references safely without clobbering valid nodes: $required."
    }
}
$gridInteractionMode = Read "addons/beep_game_builder_cs/ecs/grid/GridInteractionModeComponent.cs"
if ($gridInteractionMode -notmatch 'class\s+GridInteractionModeComponent' -or $gridInteractionMode -notmatch 'InteractionMode' -or $gridInteractionMode -notmatch 'HandlePrimaryCell' -or $gridInteractionMode -notmatch 'ApplyToolAtCell' -or $gridInteractionMode -notmatch 'ConfirmBuildAtCell' -or $gridInteractionMode -notmatch 'ManageChildMouseInput') {
    Fail "GridInteractionModeComponent is missing the expected map input coordination surface."
}
$gridInteractionModeBar = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridInteractionModeBarComponent.cs"
if ($gridInteractionModeBar -notmatch 'class\s+GridInteractionModeBarComponent' -or $gridInteractionModeBar -notmatch 'RebuildBar' -or $gridInteractionModeBar -notmatch 'SelectMode' -or $gridInteractionModeBar -notmatch 'VisibleModeButtonCount' -or $gridInteractionModeBar -notmatch 'GridInteractionModeComponent') {
    Fail "GridInteractionModeBarComponent is missing the expected mode-switching HUD surface."
}
if ($gridInteractionModeBar -notmatch 'BoundModeNames' -or $gridInteractionModeBar -notmatch 'BoundButtonPaths' -or $gridInteractionModeBar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridInteractionModeBar -notmatch 'BindExistingButtons' -or $gridInteractionModeBar -notmatch 'UsesSceneButtons' -or $gridInteractionModeBar -notmatch 'DisconnectButtons') {
    Fail "GridInteractionModeBarComponent must bind authored mode buttons by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasConventionalModeButtons", "FindModeButton", "BindModeButton", 'Name = \$"Mode_\{mode\}"', 'FindChild\(name', 'GetParent\(\)\?\.FindChild')) {
    if ($gridInteractionModeBar -notmatch $required) {
        Fail "GridInteractionModeBarComponent must auto-bind conventional Mode_* buttons before generated fallback: $required."
    }
}
$gridInteractionStatus = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridInteractionStatusComponent.cs"
if ($gridInteractionStatus -notmatch 'class\s+GridInteractionStatusComponent' -or $gridInteractionStatus -notmatch 'RebuildStatus' -or $gridInteractionStatus -notmatch 'StatusText' -or $gridInteractionStatus -notmatch 'LastFeedback' -or $gridInteractionStatus -notmatch 'GridInteractionModeComponent') {
    Fail "GridInteractionStatusComponent is missing the expected interaction status HUD surface."
}
if ($gridInteractionStatus -notmatch 'StatusLabelPath' -or $gridInteractionStatus -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridInteractionStatus -notmatch 'BindExistingControls' -or $gridInteractionStatus -notmatch 'UsesSceneControls') {
    Fail "GridInteractionStatusComponent must bind an authored status label by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindStatusLabel", 'FindChild\("Status"')) {
    if ($gridInteractionStatus -notmatch $required) {
        Fail "GridInteractionStatusComponent must auto-bind a conventional design-time Status label before generated fallback: $required."
    }
}
$gridInteractionCursor = Read "addons/beep_game_builder_cs/ecs/grid/GridInteractionCursorComponent.cs"
if ($gridInteractionCursor -notmatch 'class\s+GridInteractionCursorComponent' -or $gridInteractionCursor -notmatch 'CurrentCell' -or $gridInteractionCursor -notmatch 'CurrentOutlineColor' -or $gridInteractionCursor -notmatch 'CellCorners' -or $gridInteractionCursor -notmatch 'BuildInvalidColor') {
    Fail "GridInteractionCursorComponent is missing the expected interaction cursor drawing surface."
}
$gridResourceAmount = Read "addons/beep_game_builder_cs/ecs/grid/GridResourceAmount.cs"
if ($gridResourceAmount -notmatch 'class\s+GridResourceAmount' -or $gridResourceAmount -notmatch 'ResourceId' -or $gridResourceAmount -notmatch 'Amount') {
    Fail "GridResourceAmount is missing the expected reusable resource amount surface."
}
if ($gridResourceAmount -notmatch '\[Tool\]' -or $gridResourceAmount -notmatch '\[GlobalClass\]') {
    Fail "GridResourceAmount must be a Tool GlobalClass so editor-authored build costs do not deserialize as plain Resource."
}
foreach ($required in @("Enumerate(Godot.Collections.Array amounts)", "TryRead(Variant entry", "Variant.Type.Dictionary", "GridVariantReader.TryDictionary(entry", 'GridDefinitionReader.ReadString(dictionary, "ResourceId", "resource_id"', 'GridDefinitionReader.ReadInt(resource, "Amount", "amount"')) {
    if ($gridResourceAmount -notmatch [regex]::Escape($required)) {
        Fail "GridResourceAmount must parse typed resources, dictionaries, and plain Resource-like entries without forcing typed-array casts: $required."
    }
}
$gridResourceWallet = Read "addons/beep_game_builder_cs/ecs/grid/GridResourceWalletComponent.cs"
if ($gridResourceWallet -notmatch 'class\s+GridResourceWalletComponent' -or $gridResourceWallet -notmatch 'CanAfford' -or $gridResourceWallet -notmatch 'Spend' -or $gridResourceWallet -notmatch 'Refund' -or $gridResourceWallet -notmatch 'CaptureState') {
    Fail "GridResourceWalletComponent is missing the expected saveable settlement resource surface."
}
if ($gridResourceWallet -notmatch 'StartingResourceAmounts' -or $gridResourceWallet -notmatch 'LoadStartingResourceAmounts' -or $gridResourceWallet -match 'StartingResources\s*\{') {
    Fail "GridResourceWalletComponent must author startup balances as primitive dictionary data, not C# Resource subresources."
}
if ($gridResourceWallet -match 'LoadAmounts\s*\(\s*Godot\.Collections\.Array\s*<\s*GridResourceAmount\s*>') {
    Fail "GridResourceWalletComponent.LoadAmounts must not expose Array<GridResourceAmount>; Godot can populate it with plain Resource instances before managed script binding."
}
if ($gridResourceWallet -match '(CanAfford|Spend|Refund)\s*\(\s*Godot\.Collections\.Array\s*<\s*GridResourceAmount\s*>' -or
    $gridResourceWallet -match 'foreach\s*\(\s*GridResourceAmount') {
    Fail "GridResourceWalletComponent affordability/spend/refund must use untyped arrays plus GridResourceAmount parsing to avoid managed cast failures."
}
foreach ($required in @("TrySpendAmount(string resourceId, int amount)", "EmitSignal(SignalName.ResourceSpendRejected, id, required, available)")) {
    if ($gridResourceWallet -notmatch [regex]::Escape($required)) {
        Fail "GridResourceWalletComponent must offer a single-resource spend that keeps the ResourceSpendRejected contract: $required."
    }
}
foreach ($required in @("GridVariantReader.TryDictionary(value", "GridVariantReader.Int(value, 0)")) {
    if ($gridResourceWallet -notmatch [regex]::Escape($required)) {
        Fail "GridResourceWalletComponent must parse loose/malformed saved wallet state through GridVariantReader: $required."
    }
}
$gridWorldTemplate = Read "addons/beep_game_builder_cs/templates/scenes/grid_world_2d_iso.tscn"
if ($gridWorldTemplate -notmatch 'StartingResourceAmounts\s*=\s*\{' -or $gridWorldTemplate -match 'InitialAmounts' -or $gridWorldTemplate -match 'GridResourceAmount') {
    Fail "grid_world_2d_iso.tscn must author wallet starting balances as a primitive dictionary, not GridResourceAmount subresources."
}
$gridResourceNode = Read "addons/beep_game_builder_cs/ecs/grid/GridResourceNodeComponent.cs"
if ($gridResourceNode -notmatch 'class\s+GridResourceNodeComponent' -or $gridResourceNode -notmatch 'QueueGatherJob' -or $gridResourceNode -notmatch 'Gather' -or $gridResourceNode -notmatch 'ResourceWalletPath' -or $gridResourceNode -notmatch 'PlacementPath' -or $gridResourceNode -notmatch 'GatherJobKind') {
    Fail "GridResourceNodeComponent is missing the expected gatherable map resource surface."
}
if ($gridResourceNode -notmatch 'ActiveGatherJobId' -or
    $gridResourceNode -notmatch 'ClearStaleActiveGatherJob' -or
    $gridResourceNode -notmatch 'GatherAllForJob\(string jobId\)' -or
    $gridResourceNode -notmatch 'GatherForJob\(string jobId\)' -or
    $gridResourceNode -notmatch 'MarkCellOccupiedOnReady' -or
    $gridResourceNode -notmatch 'ReleaseReservedCell') {
    Fail "GridResourceNodeComponent must track one active gather job and reserve/release placement cells when work resolves."
}
foreach ($required in @("GridVariantReader.Vector2I(state, `"cell`"", "GridVariantReader.Int(state, `"amount`"", "GridVariantReader.Bool(state, `"depleted`"")) {
    if ($gridResourceNode -notmatch [regex]::Escape($required)) {
        Fail "GridResourceNodeComponent must parse loose/malformed saved node state through GridVariantReader: $required."
    }
}
$gridJobEffect = Read "addons/beep_game_builder_cs/ecs/grid/GridJobEffectComponent.cs"
if ($gridJobEffect -notmatch 'resource\.GatherForJob\(jobId\)' -or
    $gridJobEffect -notmatch 'ClearLandGathersResourceNode' -or
    $gridJobEffect -notmatch 'resource\.GatherAllForJob\(jobId\)') {
    Fail "GridJobEffectComponent must tell resource nodes which gather/clear job completed."
}
$gridResourceScatter = Read "addons/beep_game_builder_cs/ecs/grid/GridResourceScatterComponent.cs"
if ($gridResourceScatter -notmatch 'class\s+GridResourceScatterComponent' -or $gridResourceScatter -notmatch 'RebuildScatter' -or $gridResourceScatter -notmatch 'PreviewCells' -or $gridResourceScatter -notmatch 'AvoidOccupiedCells' -or $gridResourceScatter -notmatch 'CellDataPath' -or $gridResourceScatter -notmatch 'GridResourceNodeComponent') {
    Fail "GridResourceScatterComponent is missing the expected seeded resource population surface."
}
foreach ($required in @("EffectiveBoundsSize", "EffectiveDensity", "EffectiveMaxNodes", "EffectiveAmountPerGather", "EffectiveGatherSeconds", "EffectiveResourceId", "EffectiveGatherJobKind", "AvoidCellDataBlocked", "AvoidBlockedTerrainKinds", "MarkGeneratedCellsOccupied", "AllowedTerrainKinds", "CanSpawnResourceAt", "ReserveGeneratedCell")) {
    if ($gridResourceScatter -notmatch [regex]::Escape($required)) {
        Fail "GridResourceScatterComponent must bound invalid scatter tuning before generating resource nodes: $required."
    }
}
$gridProductionRecipe = Read "addons/beep_game_builder_cs/ecs/grid/GridProductionRecipe.cs"
if ($gridProductionRecipe -notmatch 'class\s+GridProductionRecipe' -or $gridProductionRecipe -notmatch 'RecipeId' -or $gridProductionRecipe -notmatch 'Inputs' -or $gridProductionRecipe -notmatch 'Outputs' -or $gridProductionRecipe -notmatch 'DurationSeconds') {
    Fail "GridProductionRecipe is missing the expected reusable production recipe surface."
}
if ($gridProductionRecipe -notmatch '\[Tool\]' -or
    $gridProductionRecipe -match 'Array\s*<\s*GridResourceAmount\s*>' -or
    $gridProductionRecipe -notmatch 'GridResourceAmount\.Enumerate\(Outputs\)' -or
    $gridProductionRecipe -notmatch 'EffectiveDurationSeconds') {
    Fail "GridProductionRecipe must be a Tool GlobalClass and expose untyped Inputs/Outputs parsed through GridResourceAmount."
}
if ($gridProductionRecipe -notmatch [regex]::Escape('GridVariantReader.TryDictionary(entry') -or
    $gridProductionRecipe -notmatch [regex]::Escape('using static Beep.ECS.GridDefinitionReader;') -or
    $gridProductionRecipe -notmatch [regex]::Escape('ReadFloat(data, "DurationSeconds", "duration_seconds"')) {
    Fail "GridProductionRecipe must parse loose/malformed authored recipe data through the shared GridDefinitionReader."
}
$gridProduction = Read "addons/beep_game_builder_cs/ecs/grid/GridProductionComponent.cs"
if ($gridProduction -notmatch 'class\s+GridProductionComponent' -or $gridProduction -notmatch 'StartProduction' -or $gridProduction -notmatch 'CompleteProduction' -or $gridProduction -notmatch 'ProductionCompleted' -or $gridProduction -notmatch 'GridResourceWalletComponent') {
    Fail "GridProductionComponent is missing the expected reusable production building surface."
}
if ($gridProduction -match 'foreach\s*\(\s*GridResourceAmount' -or
    $gridProduction -notmatch 'GridResourceAmount\.Enumerate\(recipe\.Outputs\)') {
    Fail "GridProductionComponent must consume recipe outputs through GridResourceAmount.Enumerate instead of typed foreach casts."
}
if ($gridProduction -notmatch 'State != ProductionState\.Idle' -or
    $gridProduction -notmatch 'already_producing' -or
    $gridProduction -notmatch 'recipe\.EffectiveDurationSeconds' -or
    $gridProduction -notmatch 'GridProductionRecipe\.Enumerate\(Recipes\)') {
    Fail "GridProductionComponent must reject duplicate starts and use a consistent effective duration."
}
foreach ($required in @("EffectiveRemainingSeconds", "DeltaSeconds(double delta)", "double.IsFinite(delta)", "Mathf.Min(delta, 86400.0)")) {
    if ($gridProduction -notmatch [regex]::Escape($required)) {
        Fail "GridProductionComponent must ignore invalid frame deltas and keep remaining production time finite: $required."
    }
}
$gridProductionPanel = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridProductionPanelComponent.cs"
if ($gridProductionPanel -notmatch 'class\s+GridProductionPanelComponent' -or $gridProductionPanel -notmatch 'RebuildPanel' -or $gridProductionPanel -notmatch 'StartMachine' -or $gridProductionPanel -notmatch 'PauseMachine' -or $gridProductionPanel -notmatch 'GridProductionComponent') {
    Fail "GridProductionPanelComponent is missing the expected reusable production HUD surface."
}
if ($gridProductionPanel -notmatch 'TitleLabelPath' -or $gridProductionPanel -notmatch 'SummaryLabelPath' -or $gridProductionPanel -notmatch 'RowsContainerPath' -or $gridProductionPanel -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridProductionPanel -notmatch 'BindExistingControls' -or $gridProductionPanel -notmatch 'UsesSceneControls') {
    Fail "GridProductionPanelComponent must bind authored panel controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindTitleLabel", "FindSummaryLabel", "FindRowsContainer", 'FindChild\("Title"', 'FindChild\("Summary"', 'FindChild\("Rows"')) {
    if ($gridProductionPanel -notmatch $required) {
        Fail "GridProductionPanelComponent must auto-bind conventional design-time children before generated fallback: $required."
    }
}
$gridObjectiveDefinition = Read "addons/beep_game_builder_cs/ecs/grid/GridObjectiveDefinition.cs"
if ($gridObjectiveDefinition -notmatch 'class\s+GridObjectiveDefinition' -or $gridObjectiveDefinition -notmatch 'ObjectiveId' -or $gridObjectiveDefinition -notmatch 'TargetCount' -or $gridObjectiveDefinition -notmatch 'ActiveOnStart') {
    Fail "GridObjectiveDefinition is missing the expected reusable objective data surface."
}
foreach ($required in @("GridVariantReader.TryDictionary(entry", "using static Beep.ECS.GridDefinitionReader;", 'ReadInt(data, "TargetCount", "target_count"', 'ReadBool(resource, "ActiveOnStart", "active_on_start"')) {
    if ($gridObjectiveDefinition -notmatch [regex]::Escape($required)) {
        Fail "GridObjectiveDefinition must parse loose/malformed authored objective data through the shared GridDefinitionReader: $required."
    }
}
$gridObjectiveTracker = Read "addons/beep_game_builder_cs/ecs/grid/GridObjectiveTrackerComponent.cs"
if ($gridObjectiveTracker -notmatch 'class\s+GridObjectiveTrackerComponent' -or $gridObjectiveTracker -notmatch 'AddProgress' -or $gridObjectiveTracker -notmatch 'CompleteObjective' -or $gridObjectiveTracker -notmatch 'CaptureState' -or $gridObjectiveTracker -notmatch 'ISaveable') {
    Fail "GridObjectiveTrackerComponent is missing the expected objective tracking/save surface."
}
if ($gridObjectiveTracker -match 'Array\s*<\s*GridObjectiveDefinition\s*>' -or
    $gridObjectiveTracker -match 'foreach\s*\(\s*GridObjectiveDefinition\s+\w+\s+in\s+Objectives\)' -or
    $gridObjectiveTracker -notmatch 'GridObjectiveDefinition\.Enumerate\(Objectives\)' -or
    $gridObjectiveTracker -notmatch 'EffectiveTargetCount') {
    Fail "GridObjectiveTrackerComponent must use untyped objective definitions and bounded targets to avoid managed cast failures."
}
foreach ($required in @("GridVariantReader.TryDictionary(value", "GridVariantReader.Array(state, `"objectives`")", "GridVariantReader.Int(data, key, 0)", "GridVariantReader.Bool(data, key, fallback)")) {
    if ($gridObjectiveTracker -notmatch [regex]::Escape($required)) {
        Fail "GridObjectiveTrackerComponent must parse loose/malformed saved objective state through GridVariantReader: $required."
    }
}
$gridObjectivePanel = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridObjectivePanelComponent.cs"
if ($gridObjectivePanel -notmatch 'class\s+GridObjectivePanelComponent' -or $gridObjectivePanel -notmatch 'RebuildPanel' -or $gridObjectivePanel -notmatch 'TextForObjective' -or $gridObjectivePanel -notmatch 'GridObjectiveTrackerComponent') {
    Fail "GridObjectivePanelComponent is missing the expected reusable objective HUD surface."
}
if ($gridObjectivePanel -notmatch 'TitleLabelPath' -or $gridObjectivePanel -notmatch 'SummaryLabelPath' -or $gridObjectivePanel -notmatch 'RowsContainerPath' -or $gridObjectivePanel -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridObjectivePanel -notmatch 'BindExistingControls' -or $gridObjectivePanel -notmatch 'UsesSceneControls') {
    Fail "GridObjectivePanelComponent must bind authored panel controls by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("HasAuthoredControls", "FindTitleLabel", "FindSummaryLabel", "FindRowsContainer", 'FindChild\("Title"', 'FindChild\("Summary"', 'FindChild\("Rows"')) {
    if ($gridObjectivePanel -notmatch $required) {
        Fail "GridObjectivePanelComponent must auto-bind conventional design-time children before generated fallback: $required."
    }
}
$parentAwareTerrainFinders = @{
    "GridCalendarHudComponent" = $gridCalendarHud
    "GridInteractionStatusComponent" = $gridInteractionStatus
    "GridJobBoardComponent" = $gridJobBoard
    "GridObjectivePanelComponent" = $gridObjectivePanel
    "GridProductionPanelComponent" = $gridProductionPanel
    "GridWorkerSpawnerPanelComponent" = $gridWorkerSpawnerPanel
    "GridWorkerStatusPanelComponent" = $gridWorkerStatusPanel
}
foreach ($entry in $parentAwareTerrainFinders.GetEnumerator()) {
    if ($entry.Value -notmatch 'GetParent\(\)\?\.FindChild') {
        Fail "$($entry.Key) conventional control lookup must search parent/sibling authored controls, not only component children."
    }
}
$gridObjectiveEventBinder = Read "addons/beep_game_builder_cs/ecs/grid/GridObjectiveEventBinderComponent.cs"
if ($gridObjectiveEventBinder -notmatch 'class\s+GridObjectiveEventBinderComponent' -or $gridObjectiveEventBinder -notmatch 'ConnectSystems' -or $gridObjectiveEventBinder -notmatch 'ObjectiveIdForJob' -or $gridObjectiveEventBinder -notmatch 'ObjectiveIdForBuild' -or $gridObjectiveEventBinder -notmatch 'ObjectiveIdForResource' -or $gridObjectiveEventBinder -notmatch 'ObjectiveIdForProduction') {
    Fail "GridObjectiveEventBinderComponent is missing the expected gameplay-to-objective event bridge."
}
if ($gridObjectiveEventBinder -notmatch 'PruneInvalidTrackedNodes' -or
    $gridObjectiveEventBinder -notmatch '_jobsConnected = false' -or
    $gridObjectiveEventBinder -notmatch '_buildSitesConnected = false' -or
    $gridObjectiveEventBinder -notmatch '_resourceNodes\.RemoveWhere' -or
    $gridObjectiveEventBinder -notmatch '_productionNodes\.RemoveWhere') {
    Fail "GridObjectiveEventBinderComponent must recover cleanly when connected gameplay nodes are freed or replaced."
}
$gridResourceBar = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridResourceBarComponent.cs"
if ($gridResourceBar -notmatch 'class\s+GridResourceBarComponent' -or $gridResourceBar -notmatch 'RebuildBar' -or $gridResourceBar -notmatch 'VisibleResourceCount' -or $gridResourceBar -notmatch 'TextForResource' -or $gridResourceBar -notmatch 'BoundResourceIds' -or $gridResourceBar -notmatch 'BoundLabelPaths') {
    Fail "GridResourceBarComponent is missing the expected reusable resource HUD surface."
}
if ($gridResourceBar -notmatch 'RowPath' -or $gridResourceBar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridResourceBar -notmatch 'BindExistingLabels' -or $gridResourceBar -notmatch 'BindExistingRow' -or $gridResourceBar -notmatch 'UsesSceneControls' -or $gridResourceBar -notmatch 'BuildGeneratedRow') {
    Fail "GridResourceBarComponent must bind authored resource labels/row by default and only generate fallback UI when explicitly enabled."
}
foreach ($required in @("FindResourceRow", "FindResourceLabel", 'FindChild\("ResourceBar"', 'FindChild\(nodeName', 'GetParent\(\)\?\.FindChild')) {
    if ($gridResourceBar -notmatch $required) {
        Fail "GridResourceBarComponent must auto-bind conventional ResourceBar/Resource_* controls before generated fallback: $required."
    }
}
$gridBuildDefinition = Read "addons/beep_game_builder_cs/ecs/grid/GridBuildDefinition.cs"
if ($gridBuildDefinition -notmatch 'class\s+GridBuildDefinition' -or $gridBuildDefinition -notmatch 'BuildId' -or $gridBuildDefinition -notmatch 'Footprint' -or $gridBuildDefinition -notmatch 'Costs' -or $gridBuildDefinition -notmatch 'BlocksNavigation') {
    Fail "GridBuildDefinition is missing the expected reusable placeable build data surface."
}
if ($gridBuildDefinition -notmatch '\[Tool\]' -or
    $gridBuildDefinition -match 'Array\s*<\s*GridResourceAmount\s*>' -or
    $gridBuildDefinition -notmatch 'Godot\.Collections\.Array Costs' -or
    $gridBuildDefinition -notmatch 'EffectiveFootprint' -or
    $gridBuildDefinition -notmatch 'EffectiveBuildSeconds') {
    Fail "GridBuildDefinition must be a Tool GlobalClass and expose untyped Costs so scene-authored costs cannot crash on managed casts."
}
$gridBuildCatalog = Read "addons/beep_game_builder_cs/ecs/grid/GridBuildCatalogComponent.cs"
if ($gridBuildCatalog -notmatch 'class\s+GridBuildCatalogComponent' -or $gridBuildCatalog -notmatch 'FindBuild' -or $gridBuildCatalog -notmatch 'BeginPlacement' -or $gridBuildCatalog -notmatch 'BuildIdsForCategory' -or $gridBuildCatalog -notmatch 'CostSummary') {
    Fail "GridBuildCatalogComponent is missing the expected build menu/catalog surface."
}
if ($gridBuildCatalog -match 'foreach\s*\(\s*GridResourceAmount' -or
    $gridBuildCatalog -notmatch 'GridResourceAmount\.Enumerate\(build\.Costs\)' -or
    $gridBuildCatalog -match 'Array\s*<\s*GridBuildDefinition\s*>' -or
    $gridBuildCatalog -notmatch 'GridBuildDefinition\.Enumerate\(Builds\)') {
    Fail "GridBuildCatalogComponent must summarize costs through GridResourceAmount.Enumerate instead of typed foreach casts."
}
$gridBuildToolbar = Read "addons/beep_game_builder_cs/ecs/grid/ui/GridBuildToolbarComponent.cs"
if ($gridBuildToolbar -notmatch 'class\s+GridBuildToolbarComponent' -or $gridBuildToolbar -notmatch 'RebuildToolbar' -or $gridBuildToolbar -notmatch 'SelectBuild' -or $gridBuildToolbar -notmatch 'SelectCategory' -or $gridBuildToolbar -notmatch 'VisibleBuildButtonCount' -or $gridBuildToolbar -notmatch 'InteractionModePath' -or $gridBuildToolbar -notmatch 'AutoSwitchInteractionMode') {
    Fail "GridBuildToolbarComponent is missing the expected reusable build toolbar surface."
}
if ($gridBuildToolbar -notmatch 'CategoryRowPath' -or $gridBuildToolbar -notmatch 'BuildGridPath' -or $gridBuildToolbar -notmatch 'GenerateControlsWhenPathsEmpty\s*\{\s*get;\s*set;\s*\}\s*=\s*false' -or $gridBuildToolbar -notmatch 'BindExistingControls' -or $gridBuildToolbar -notmatch 'UsesSceneControls' -or $gridBuildToolbar -notmatch 'BuildGeneratedSurface') {
    Fail "GridBuildToolbarComponent must bind authored toolbar containers by default and only generate fallback UI when explicitly enabled."
}
if ($gridBuildToolbar -match 'foreach\s*\(\s*GridResourceAmount' -or
    $gridBuildToolbar -notmatch 'GridResourceAmount\.Enumerate\(build\.Costs\)' -or
    $gridBuildToolbar -notmatch 'GridBuildDefinition\.Enumerate\(_catalog\.Builds\)' -or
    $gridBuildToolbar -notmatch 'EffectiveButtonMinimumSize') {
    Fail "GridBuildToolbarComponent must render cost text through GridResourceAmount.Enumerate instead of typed foreach casts."
}
foreach ($required in @("FindCategoryRow", "FindBuildGrid", 'Name = "Categories"', 'Name = "Builds"', 'FindChild\("Categories"', 'FindChild\("Builds"', 'GetParent\(\)\?\.FindChild')) {
    if ($gridBuildToolbar -notmatch $required) {
        Fail "GridBuildToolbarComponent must auto-bind conventional Categories/Builds controls before generated fallback: $required."
    }
}
$gridBuildSite = Read "addons/beep_game_builder_cs/ecs/grid/GridBuildSiteComponent.cs"
if ($gridBuildSite -notmatch 'class\s+GridBuildSiteComponent' -or $gridBuildSite -notmatch 'RegisterPlacedBuild' -or $gridBuildSite -notmatch 'BuildSiteCreated' -or $gridBuildSite -notmatch 'BuildSiteCompleted' -or $gridBuildSite -notmatch 'JobCompleted') {
    Fail "GridBuildSiteComponent is missing the expected blueprint/build-job lifecycle surface."
}
if ($gridBuildSite -notmatch '_placementConnected = false' -or
    $gridBuildSite -notmatch '_jobsConnected = false') {
    Fail "GridBuildSiteComponent must reset connection flags when cached signal sources are freed or replaced."
}
$gridSmoke = Read "tests/GridPlacementSmoke.cs"
if ($gridSmoke -notmatch 'VerifyGridBuildCatalogAndResources') {
    Fail "GridPlacementSmoke does not verify GridBuildCatalog/GridResourceWallet behavior."
}
if ($gridSmoke -notmatch 'VerifyGridInteractionMode') {
    Fail "GridPlacementSmoke does not verify GridInteractionMode behavior."
}
if ($gridSmoke -notmatch 'VerifyGridInteractionModeBar') {
    Fail "GridPlacementSmoke does not verify GridInteractionModeBar behavior."
}
if ($gridSmoke -notmatch 'VerifyGridInteractionStatus') {
    Fail "GridPlacementSmoke does not verify GridInteractionStatus behavior."
}
if ($gridSmoke -notmatch 'VerifyGridInteractionCursor') {
    Fail "GridPlacementSmoke does not verify GridInteractionCursor behavior."
}
if ($gridSmoke -notmatch 'VerifyGridObjectComponent') {
    Fail "GridPlacementSmoke does not verify GridObject behavior."
}
if ($gridSmoke -notmatch 'VerifyGridObjectInspector') {
    Fail "GridPlacementSmoke does not verify GridObjectInspector behavior."
}
if ($gridSmoke -notmatch 'VerifyGridBuildSites') {
    Fail "GridPlacementSmoke does not verify GridBuildSite behavior."
}
if ($gridSmoke -notmatch 'VerifyGridResourceBar') {
    Fail "GridPlacementSmoke does not verify GridResourceBar behavior."
}
if ($gridSmoke -notmatch 'VerifyGridResourceBarSceneLabels') {
    Fail "GridPlacementSmoke does not verify GridResourceBar scene-authored labels."
}
if ($gridSmoke -notmatch 'VerifyGridBuildToolbar') {
    Fail "GridPlacementSmoke does not verify GridBuildToolbar behavior."
}
if ($gridSmoke -notmatch 'VerifyNavigationAvoidsBlockedCells' -or $gridSmoke -notmatch 'VerifyNavigationUsesPlacementOccupancy') {
    Fail "GridPlacementSmoke does not verify grid navigation and placement occupancy integration."
}
if ($gridSmoke -notmatch 'VerifyGridRoads') {
    Fail "GridPlacementSmoke does not verify GridRoad behavior."
}
if ($gridSmoke -notmatch 'VerifyGridRoadsUseCellDataTerrain') {
    Fail "GridPlacementSmoke does not verify GridRoad integration with GridCellData terrain."
}
if ($gridSmoke -notmatch 'VerifyPathFollowerMovesBody') {
    Fail "GridPlacementSmoke does not verify GridPathFollower movement."
}
if ($gridSmoke -notmatch 'VerifyProjectionBoundsInvalidTuning') {
    Fail "GridPlacementSmoke does not verify invalid GridProjection tuning behavior."
}
if ($gridSmoke -notmatch 'VerifyGridSelectionState') {
    Fail "GridPlacementSmoke does not verify GridSelection state and rectangle selection."
}
if ($gridSmoke -notmatch 'VerifyGridCameraController') {
    Fail "GridPlacementSmoke does not verify GridCameraController behavior."
}
if ($gridSmoke -notmatch 'VerifyGridJobQueueAndWorker') {
    Fail "GridPlacementSmoke does not verify GridJobQueue/GridWorker behavior."
}
if ($gridSmoke -notmatch 'VerifyGridWorkerStatusPanel') {
    Fail "GridPlacementSmoke does not verify GridWorkerStatusPanel behavior."
}
if ($gridSmoke -notmatch 'VerifyGridJobBoard') {
    Fail "GridPlacementSmoke does not verify GridJobBoard behavior."
}
if ($gridSmoke -notmatch 'VerifyGridJobEffects') {
    Fail "GridPlacementSmoke does not verify GridJobEffect behavior."
}
if ($gridSmoke -notmatch 'VerifyGridResourceNodes') {
    Fail "GridPlacementSmoke does not verify GridResourceNode behavior."
}
if ($gridSmoke -notmatch 'VerifyGridResourceScatter') {
    Fail "GridPlacementSmoke does not verify GridResourceScatter behavior."
}
if ($gridSmoke -notmatch 'VerifyGridResourceScatterBoundsInvalidTuning') {
    Fail "GridPlacementSmoke does not verify invalid GridResourceScatter tuning behavior."
}
if ($gridSmoke -notmatch 'VerifyPlacementUsesCellDataTerrain') {
    Fail "GridPlacementSmoke does not verify GridPlacement integration with GridCellData terrain."
}
if ($gridSmoke -notmatch 'VerifyGridProduction') {
    Fail "GridPlacementSmoke does not verify GridProduction behavior."
}
if ($gridSmoke -notmatch 'VerifyGridProductionPanel') {
    Fail "GridPlacementSmoke does not verify GridProductionPanel behavior."
}
if ($gridSmoke -notmatch 'VerifyGridObjectiveTracker') {
    Fail "GridPlacementSmoke does not verify GridObjectiveTracker behavior."
}
if ($gridSmoke -notmatch 'VerifyGridObjectivePanel') {
    Fail "GridPlacementSmoke does not verify GridObjectivePanel behavior."
}
if ($gridSmoke -notmatch 'VerifyGridObjectiveEventBinder') {
    Fail "GridPlacementSmoke does not verify GridObjectiveEventBinder behavior."
}
if ($gridSmoke -notmatch 'VerifyPlacementMarksNavigationFootprint') {
    Fail "GridPlacementSmoke does not verify placement writes blocking build footprints into navigation."
}
if ($gridSmoke -notmatch 'VerifyGridWorkerSpawner') {
    Fail "GridPlacementSmoke does not verify GridWorkerSpawner behavior."
}
if ($gridSmoke -notmatch 'VerifyGridWorkerSpawnerUsesCellDataTerrain') {
    Fail "GridPlacementSmoke does not verify GridWorkerSpawner terrain and occupancy integration."
}
if ($gridSmoke -notmatch 'VerifyGridWorkerSpawnerBoundsInvalidTuning') {
    Fail "GridPlacementSmoke does not verify invalid GridWorkerSpawner tuning behavior."
}
if ($gridSmoke -notmatch 'VerifyGridWorkerSpawnerPanel') {
    Fail "GridPlacementSmoke does not verify GridWorkerSpawnerPanel behavior."
}
if ($gridSmoke -notmatch 'VerifyGridWorkerSpawnerPanelSceneControls') {
    Fail "GridPlacementSmoke does not verify GridWorkerSpawnerPanel scene-authored controls."
}
if ($gridSmoke -notmatch 'VerifySelectionJobCommand') {
    Fail "GridPlacementSmoke does not verify GridSelectionJobCommand behavior."
}
if ($gridSmoke -notmatch 'VerifySelectionJobCommandUsesTerrainRules') {
    Fail "GridPlacementSmoke does not verify GridSelectionJobCommand terrain/navigation filtering."
}
if ($gridSmoke -notmatch 'VerifySelectionJobCommandBoundsInvalidTuning') {
    Fail "GridPlacementSmoke does not verify invalid GridSelectionJobCommand tuning behavior."
}
if ($gridSmoke -notmatch 'VerifyGridCellData') {
    Fail "GridPlacementSmoke does not verify GridCellData farming/cell-state behavior."
}
if ($gridSmoke -notmatch 'VerifyGridToolActions') {
    Fail "GridPlacementSmoke does not verify GridToolAction farming/tool behavior."
}
if ($gridSmoke -notmatch 'VerifyGridToolActionsBoundInvalidTuning') {
    Fail "GridPlacementSmoke does not verify invalid GridToolAction tuning behavior."
}
if ($gridSmoke -notmatch 'VerifyGridLooseArrayInputs' -or
    $gridSmoke -notmatch 'new Godot.Collections.Array' -or
    $gridSmoke -notmatch 'new Resource()' -or
    $gridSmoke -notmatch 'loose authored cell arrays') {
    Fail "GridPlacementSmoke must verify loose authored/GDScript grid arrays do not trigger typed-array casts."
}
if ($gridSmoke -notmatch 'VerifyGridMalformedStateInputs' -or
    $gridSmoke -notmatch 'malformed grid saved state values' -or
    $gridSmoke -notmatch 'Grid definition resources did not ignore malformed grid authored data values') {
    Fail "GridPlacementSmoke must verify malformed saved/authored grid data does not trigger direct Variant cast failures."
}
if ($gridSmoke -notmatch 'VerifyGridToolPalette') {
    Fail "GridPlacementSmoke does not verify GridToolPalette behavior."
}
if ($gridSmoke -notmatch 'VerifyGridToolPaletteSceneButtons') {
    Fail "GridPlacementSmoke does not verify GridToolPalette scene-authored buttons."
}
if ($gridSmoke -notmatch 'VerifyGridMinimap') {
    Fail "GridPlacementSmoke does not verify GridMinimap behavior."
}
if ($gridSmoke -notmatch 'VerifyGridCropCatalog') {
    Fail "GridPlacementSmoke does not verify GridCropCatalog seasonal planting behavior."
}
if ($gridSmoke -notmatch 'harvestedWithYield') {
    Fail "GridPlacementSmoke does not verify crop harvest yield payout."
}
if ($gridSmoke -notmatch 'VerifyGridCellOverlay') {
    Fail "GridPlacementSmoke does not verify GridCellOverlay visual-state behavior."
}
if ($gridSmoke -notmatch 'VerifyGridVisualHelpersBoundInvalidTuning') {
    Fail "GridPlacementSmoke does not verify invalid minimap/overlay visual tuning behavior."
}
if ($gridSmoke -notmatch 'VerifyGridTileMapLayerBridge') {
    Fail "GridPlacementSmoke does not verify GridTileMapLayerBridge behavior."
}
if ($gridSmoke -notmatch 'VerifyNavigationUsesCellDataTerrain') {
    Fail "GridPlacementSmoke does not verify GridNavigation integration with GridCellData terrain."
}
if ($gridSmoke -notmatch 'VerifyGridTerrainGenerator') {
    Fail "GridPlacementSmoke does not verify GridTerrainGenerator output into GridCellData."
}
if ($gridSmoke -notmatch 'VerifyGridCalendar') {
    Fail "GridPlacementSmoke does not verify GridCalendar date/crop advancement behavior."
}
if ($gridSmoke -notmatch 'VerifyGridCalendarHud') {
    Fail "GridPlacementSmoke does not verify GridCalendarHud behavior."
}
if ($gridSmoke -notmatch 'VerifyGridWorldStateRoundTrip') {
    Fail "GridPlacementSmoke does not verify GridWorldState snapshot round-tripping."
}
if ($gridSmoke -notmatch 'objectRestored' -or $gridSmoke -notmatch 'GridWorldState did not restore grid object state and footprint reservations') {
    Fail "GridPlacementSmoke must verify GridWorldState restores authored grid object state and footprint reservations."
}
$gridGuide = Read "docs/2D_ISO_TOOLKIT.md"
foreach ($required in @(
    "GridProjectionComponent",
    "GridMinimapComponent",
    "GridObjectComponent",
    "GridObjectInspectorComponent",
    "GridPlacementComponent",
    "GridInteractionModeComponent",
    "GridInteractionModeBarComponent",
    "GridInteractionStatusComponent",
    "GridInteractionCursorComponent",
    "GridBuildDefinition",
    "GridBuildCatalogComponent",
    "GridBuildToolbarComponent",
    "GridBuildSiteComponent",
    "GridResourceWalletComponent",
    "GridResourceNodeComponent",
    "GridResourceScatterComponent",
    "GridProductionRecipe",
    "GridProductionComponent",
    "GridProductionPanelComponent",
    "GridObjectiveDefinition",
    "GridObjectiveTrackerComponent",
    "GridObjectivePanelComponent",
    "GridObjectiveEventBinderComponent",
    "GridResourceBarComponent",
    "GridNavigationComponent",
    "GridRoadComponent",
    "GridPathFollowerComponent",
    "GridSelectionComponent",
    "GridCameraControllerComponent",
    "GridJobBoardComponent",
    "GridJobQueueComponent",
    "GridJobEffectComponent",
    "GridWorkerComponent",
    "GridWorkerStatusPanelComponent",
    "GridWorkerSpawnerComponent",
    "GridWorkerSpawnerPanelComponent",
    "GridSelectionJobCommandComponent",
    "GridCellDataComponent",
    "GridToolActionComponent",
    "GridToolPaletteComponent",
    "GridCropDefinition",
    "GridCropCatalogComponent",
    "GridCellOverlayComponent",
    "GridTileMapLayerBridgeComponent",
    "TerrainGeneratorComponent",
    "TerrainDataLayersComponent",
    "TerrainWorldComponent",
    "GridCalendarComponent",
    "GridCalendarHudComponent",
    "GridWorldStateComponent",
    "TerrainPaintedRendererComponent"
)) {
    if ($gridGuide -notmatch $required) { Fail "2D_ISO_TOOLKIT.md is missing $required." }
}
foreach ($required in @("Who owns which spatial fact", "MarkPlacedCellsBlockedInNavigation", "SetFootprintNavigationBlocked", "TreatPlacementOccupiedAsBlocked")) {
    if ($gridGuide -notmatch [regex]::Escape($required)) {
        Fail "2D_ISO_TOOLKIT.md must document the occupancy-ownership matrix (who owns blocked/occupied facts and how placement links them to navigation): $required."
    }
}
$gridHelpHtml = Read "docs/2d-iso-toolkit.html"
if ($gridHelpHtml -notmatch 'Beep 2D And Isometric Toolkit' -or
    $gridHelpHtml -notmatch 'GridSelectionComponent' -or
    $gridHelpHtml -notmatch 'TerrainPaintedRendererComponent') {
    Fail "2d-iso-toolkit.html does not document the 2D/isometric toolkit."
}
$gridTemplate = Read "addons/beep_game_builder_cs/templates/scenes/grid_world_2d_iso.tscn"
$gridWorkerTemplate = Read "addons/beep_game_builder_cs/templates/scenes/grid_worker_unit.tscn"
$gridBaseTemplate = Read "addons/beep_game_builder_cs/templates/scenes/grid_base_depot.tscn"
foreach ($required in @(
    "TerrainPaintedRendererComponent.cs",
    "GridProjectionComponent.cs",
    "GridMinimapComponent.cs",
    "GridObjectComponent.cs",
    "GridObjectInspectorComponent.cs",
    "GridPlacementComponent.cs",
    "GridInteractionModeComponent.cs",
    "GridInteractionModeBarComponent.cs",
    "GridInteractionStatusComponent.cs",
    "GridInteractionCursorComponent.cs",
    "GridResourceWalletComponent.cs",
    "GridResourceNodeComponent.cs",
    "GridResourceScatterComponent.cs",
    "GridProductionComponent.cs",
    "GridProductionPanelComponent.cs",
    "GridObjectiveTrackerComponent.cs",
    "GridObjectivePanelComponent.cs",
    "GridObjectiveEventBinderComponent.cs",
    "GridResourceBarComponent.cs",
    "GridBuildCatalogComponent.cs",
    "GridBuildToolbarComponent.cs",
    "GridBuildSiteComponent.cs",
    "GridNavigationComponent.cs",
    "GridRoadComponent.cs",
    "GridPathFollowerComponent.cs",
    "GridSelectionComponent.cs",
    "GridCameraControllerComponent.cs",
    "GridJobBoardComponent.cs",
    "GridJobQueueComponent.cs",
    "GridJobEffectComponent.cs",
    "GridWorkerComponent.cs",
    "GridWorkerStatusPanelComponent.cs",
    "GridWorkerSpawnerComponent.cs",
    "GridWorkerSpawnerPanelComponent.cs",
    "GridSelectionJobCommandComponent.cs",
    "GridCellDataComponent.cs",
    "GridToolActionComponent.cs",
    "GridToolPaletteComponent.cs",
    "GridCropCatalogComponent.cs",
    "GridCellOverlayComponent.cs",
    "GridTileMapLayerBridgeComponent.cs",
    "TerrainGeneratorComponent.cs",
    "GridCalendarComponent.cs",
    "GridCalendarHudComponent.cs",
    "GridWorldStateComponent.cs"
)) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing $required." }
}
foreach ($required in @("CharacterBody2D", "Sprite2D", "CollisionShape2D", "GridPathFollowerComponent.cs", "GridWorkerComponent.cs")) {
    if ($gridWorkerTemplate -notmatch [regex]::Escape($required)) { Fail "grid_worker_unit.tscn is missing $required." }
}
foreach ($required in @("Sprite2D", "Marker2D", "GridObjectComponent.cs", "GridWorkerSpawnerComponent.cs")) {
    if ($gridBaseTemplate -notmatch [regex]::Escape($required)) { Fail "grid_base_depot.tscn is missing $required." }
}
foreach ($required in @("PlaceholderTexture2D", "Sprite2D", "grid_worker_unit.tscn")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing $required." }
}
# The painted terrain reads the generated grid straight from the generator, so
# the wiring to check is the renderer's generator path - not a bridge translating
# cells into paint, which no longer exists. Cell and road state still reach the
# visible TileMapLayer through the tilemap bridge.
foreach ($required in @("Splat", "TerrainGeneratorPath = NodePath(`"../TerrainGenerator`")", "TileMapBridge", "CellDataPath = NodePath(`"../Cells`")", "RoadPath = NodePath(`"../Roads`")", "ClearBeforeRebuild = true")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing integrated grid/terrain wiring $required." }
}
# One owner for the first pass. The generator writes the cells that gameplay
# reads; the painted renderer samples the generator's own field rather than the
# cells, so it cannot draw a half-generated world whichever _Ready runs first.
foreach ($required in @("GenerateOnReady = true", "GenerateInEditor = false", "TerrainGeneratorPath = NodePath(`"../TerrainGenerator`")")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn must let TerrainGeneratorComponent own the first terrain pass: $required." }
}
foreach ($required in @("RoadPath = NodePath(`"../Roads`")", "ObjectsRootPath = NodePath(`".`")")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn must wire GridWorldState to roads and authored grid objects $required." }
}
foreach ($required in @("TerrainGenerator", "TerrainGeneratorComponent.cs", "ClearExistingCells = true", "BoundsSize = Vector2i(64, 64)")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time terrain generator wiring $required." }
}
# The generator's inputs are the generator's. They were once read off the
# renderer whenever UsePainterSettings was set, which made a RENDERER the owner
# of the generator's settings - two owners for one fact, with a flag deciding
# which won, and a pipeline that could not run without a renderer present.
if ($gridTemplate -match 'UsePainterSettings' -or $gridTerrainGenerator -match 'UsePainterSettings\s*\{\s*get;') {
    Fail "A renderer must not own TerrainGeneratorComponent's settings; UsePainterSettings is the two-owner flag that was removed."
}
foreach ($required in @("CellDataPath = NodePath(`"../Cells`")", "NavigationPath = NodePath(`"../Navigation`")", "TreatPlacementOccupiedAsBlocked = true", "MarkPlacedCellsOccupied = true", "MarkPlacedCellsBlockedInNavigation = true")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn must wire terrain/grid placement and navigation integration $required." }
}
foreach ($required in @("ReserveFootprintOnReady = true", "PlacementPath = NodePath(`"../../Placement`")", "NavigationPath = NodePath(`"../../Navigation`")")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn must wire authored GridObject footprints into placement/navigation integration $required." }
}
foreach ($required in @("CellDataPath = NodePath(`"../../Cells`")", "PlacementPath = NodePath(`"../../Placement`")")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn must wire worker spawning to terrain/cell-data and placement integration $required." }
}
foreach ($required in @("TitleLabelPath", "CountLabelPath", "SpawnButtonPath", "[node name=`"SpawnButton`" type=`"Button`" parent=`"HUD/BasePanel/Panel/Content`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time BasePanel control $required." }
}
foreach ($required in @("BoundResourceIds", "BoundLabelPaths", "[node name=`"Wood`" type=`"Label`" parent=`"HUD/ResourceBar/Row`"]", "[node name=`"Stone`" type=`"Label`" parent=`"HUD/ResourceBar/Row`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time ResourceBar control $required." }
}
foreach ($required in @("PanelPath = NodePath(`"Panel`")", "DetailsLabelPath = NodePath(`"Panel/Content/Details`")", "[node name=`"Panel`" type=`"PanelContainer`" parent=`"HUD/ObjectInspector`"]", "[node name=`"Title`" type=`"Label`" parent=`"HUD/ObjectInspector/Panel/Content`"]", "[node name=`"Details`" type=`"Label`" parent=`"HUD/ObjectInspector/Panel/Content`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time ObjectInspector control $required." }
}
foreach ($required in @("ThemePresetComponent.cs", "KitPanelContainer.cs", "KitPushButton.cs", "KitLabel.cs", "BoundActionNames", "BoundButtonPaths", "GenerateControlsWhenPathsEmpty = false", "[node name=`"Hoe`" type=`"Button`" parent=`"HUD/ToolPalette/Row`"]", "[node name=`"NoRoad`" type=`"Button`" parent=`"HUD/ToolPalette/Row`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing kit-authored HUD control $required." }
}
foreach ($required in @("BoundModeNames", "[node name=`"Select`" type=`"Button`" parent=`"HUD/ModeBar/Row`"]", "[node name=`"Inspect`" type=`"Button`" parent=`"HUD/ModeBar/Row`"]", "[node name=`"Tools`" type=`"Button`" parent=`"HUD/ModeBar/Row`"]", "[node name=`"Build`" type=`"Button`" parent=`"HUD/ModeBar/Row`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time ModeBar control $required." }
}
foreach ($required in @("DateLabelPath", "[node name=`"Date`" type=`"Label`" parent=`"HUD/CalendarHud/Panel`"]", "StatusLabelPath", "[node name=`"Status`" type=`"Label`" parent=`"HUD/InteractionStatus/Panel`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time calendar/status HUD control $required." }
}
foreach ($required in @(
    "TitleLabelPath = NodePath(`"Panel/Content/Title`")",
    "SummaryLabelPath = NodePath(`"Panel/Content/Summary`")",
    "RowsContainerPath = NodePath(`"Panel/Content/Rows`")",
    "[node name=`"Rows`" type=`"VBoxContainer`" parent=`"HUD/JobBoard/Panel/Content`"]",
    "[node name=`"Rows`" type=`"VBoxContainer`" parent=`"HUD/WorkerStatus/Panel/Content`"]",
    "[node name=`"Rows`" type=`"VBoxContainer`" parent=`"HUD/ObjectivesPanel/Panel/Content`"]",
    "[node name=`"Rows`" type=`"VBoxContainer`" parent=`"HUD/ProductionPanel/Panel/Content`"]"
)) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time grid HUD panel shell $required." }
}
foreach ($required in @("CategoryRowPath", "BuildGridPath", "[node name=`"Categories`" type=`"HBoxContainer`" parent=`"HUD/BuildToolbar/Panel/Content`"]", "[node name=`"BuildScroll`" type=`"ScrollContainer`" parent=`"HUD/BuildToolbar/Panel/Content`"]", "[node name=`"Builds`" type=`"GridContainer`" parent=`"HUD/BuildToolbar/Panel/Content/BuildScroll`"]")) {
    if ($gridTemplate -notmatch [regex]::Escape($required)) { Fail "grid_world_2d_iso.tscn is missing design-time BuildToolbar container $required." }
}
$templateGuide = Read "docs/ADDON_GUIDE.md"
if ($templateGuide -notmatch 'grid_world_2d_iso\.tscn') { Fail "ADDON_GUIDE.md does not list grid_world_2d_iso.tscn." }
if ($templateGuide -notmatch 'grid_worker_unit\.tscn' -or $templateGuide -notmatch 'grid_base_depot\.tscn') { Fail "ADDON_GUIDE.md does not list the grid worker/base templates." }
if ($headlessSmoke -notmatch 'grid_world_2d_iso\.tscn') { Fail "headless runtime smoke does not load the grid world template." }
if ($headlessSmoke -notmatch 'grid_worker_unit\.tscn' -or $headlessSmoke -notmatch 'grid_base_depot\.tscn') { Fail "headless runtime smoke does not load the grid worker/base templates." }

$themeGalleryScene = Read "addons/beep_game_builder_cs/templates/scenes/theme_gallery.tscn"
$themeGallery = Read "addons/beep_game_builder_cs/ecs/scenes/ThemeGallery.cs"
$hudScene = Read "addons/beep_game_builder_cs/templates/scenes/hud.tscn"
$hudComponent = Read "addons/beep_game_builder_cs/ecs/ui/HudComponent.cs"
$gameInfoBinder = Read "addons/beep_game_builder_cs/ecs/ui/GameInfoBinder.cs"
$templateSceneRoot = Join-Path $root "addons/beep_game_builder_cs/templates/scenes"
foreach ($sceneFile in Get-ChildItem -Path $templateSceneRoot -Filter "*.tscn" -Recurse) {
    $scene = Get-Content -LiteralPath $sceneFile.FullName -Raw
    $scripts = @{}
    foreach ($match in [regex]::Matches($scene, '(?m)^\[ext_resource type="Script" path="([^"]+)" id="([^"]+)"\]')) {
        $scripts[$match.Groups[2].Value] = $match.Groups[1].Value
    }

    foreach ($match in [regex]::Matches($scene, '(?ms)^\[node name="(?<name>[^"]+)"[^\]]*\]\r?\n(?<body>.*?)(?=^\[node |\z)')) {
        $body = $match.Groups["body"].Value
        $scriptMatch = [regex]::Match($body, '(?m)^script = ExtResource\("([^"]+)"\)')
        if (-not $scriptMatch.Success) { continue }

        $scriptPath = $scripts[$scriptMatch.Groups[1].Value]
        if ($scriptPath -notmatch 'ecs/ui/kit/KitPanel(Container)?\.cs$') { continue }
        if ($body -match '(?m)^Banner = ') {
            $relativeScene = $sceneFile.FullName.Substring($templateSceneRoot.Length + 1)
            Fail "$relativeScene node '$($match.Groups["name"].Value)' serializes legacy Banner on $scriptPath; use KitPanel.HeaderStyle or KitPanelContainer.TitleStyle instead."
        }
    }
}
foreach ($required in @("SampleOption", "SampleItemList", "SampleTree", "KitItemList.cs", "KitGodotTree.cs", "KitTabPanel.cs")) {
    if ($themeGalleryScene -notmatch [regex]::Escape($required)) { Fail "theme_gallery.tscn is missing authored UI kit sample control $required." }
}
foreach ($required in @('[node name="Margin" type="Control" parent="."]', '[node name="TitleLabel" type="Label" parent="Margin/VBox"]', '[node name="Header" type="HFlowContainer" parent="Margin/VBox"]', '[node name="ScrollFrame" type="Control" parent="Margin/VBox"]', '[node name="Content" type="VBoxContainer" parent="Margin/VBox/ScrollFrame/Scroll"]', '[node name="ButtonRow" type="HFlowContainer"', '[node name="InputRow" type="HFlowContainer"', '[node name="ListRow" type="HFlowContainer"', 'horizontal_scroll_mode = 1')) {
    if ($themeGalleryScene -notmatch [regex]::Escape($required)) { Fail "theme_gallery.tscn must keep its responsive authored gallery layout: $required." }
}
foreach ($forbidden in @('BeepTitle — screen titles', 'Hover and press these', 'Full width — a 9-patch stretches here')) {
    if ($themeGalleryScene -match [regex]::Escape($forbidden)) { Fail "theme_gallery.tscn must not use instructional copy as visible UI sample text: $forbidden." }
}
foreach ($required in @("TypeSectionLabel", "ButtonSectionLabel", "InputSectionLabel", "RangeSectionLabel", "ListSectionLabel", "AutoRole = false", "Role = 3", "Accent = 1")) {
    if ($themeGalleryScene -notmatch [regex]::Escape($required)) { Fail "theme_gallery.tscn must author compact panel section labels instead of hero-sized headings: $required." }
}
foreach ($forbidden in @("TypeHeading", "ButtonHeading", "InputHeading", "RangeHeading", "ListHeading")) {
    if ($themeGalleryScene -match [regex]::Escape($forbidden)) { Fail "theme_gallery.tscn must not use '$forbidden'; heading names trigger large inferred typography inside compact panels." }
}
foreach ($required in @("PopulateControlSamples", "SampleOption", "SampleItemList", "SampleTree", "AddItem(`"Gather wood`")", "CreateItem(root)", "SetSelected(workshop, 0)")) {
    if ($themeGallery -notmatch [regex]::Escape($required)) { Fail "ThemeGallery.cs must populate existing list/tree/option samples for $required." }
}
if ($themeGallery -notmatch 'IndexOf\(_themePick,\s*_theme\?\.PresetName \?\? ""\)' -or $themeGallery -notmatch 'authored >= 0 \? authored') {
    Fail "ThemeGallery.cs must honor the scene-authored ThemePresetComponent.PresetName before falling back to the genre default."
}
if ($themeGallery -match 'UpdateTextureToggle|TexturesCheck|_textures|UseTextures = on') {
    Fail "ThemeGallery.cs must not expose the removed UI chrome texture toggle."
}
foreach ($required in @('PresetName = "oilfield_days"', 'GenreName = "citybuilder"')) {
    if ($themeGalleryScene -notmatch [regex]::Escape($required)) { Fail "theme_gallery.tscn must default to the Oilfield Days citybuilder skin: $required." }
}
if ($themeGalleryScene -match 'TexturesCheck|text = "Textures"') {
    Fail "theme_gallery.tscn must not expose the removed UI chrome texture toggle."
}
foreach ($required in @('PresetName = "oilfield_days"', 'GenreName = "citybuilder"', 'HudMode = true')) {
    if ($hudScene -notmatch [regex]::Escape($required)) { Fail "hud.tscn must default to the Oilfield Days citybuilder HUD skin: $required." }
}
if ($hudComponent -notmatch 'WarnMissingSources \{ get; set; \} = false' -or
    $hudComponent -notmatch 'private void WarnMissing\(string message\)' -or
    $hudComponent -notmatch 'if \(WarnMissingSources\)') {
    Fail "HudComponent must keep missing optional runtime sources quiet by default for standalone templates, with an explicit strict warning opt-in."
}
if ($gameInfoBinder -notmatch 'WarnMissingGameInfo \{ get; set; \} = false' -or
    $gameInfoBinder -notmatch 'if \(WarnMissingGameInfo\)[\s\S]*No GameApp\.Info resource found') {
    Fail "GameInfoBinder must keep missing GameInfo quiet by default for standalone templates, with an explicit warning opt-in."
}

# The example SCENES ship WITH the addon now, in templates/scenes/terrain, so a
# consumer who copies the addon gets working wiring instead of scenes left behind
# in tests/. tests/examples holds the guards that drive them.
$examplesReadme = Read "tests/examples/README.md"
$gridWorldExample = Read "addons/beep_game_builder_cs/templates/scenes/terrain/grid_world_kit_hud_example.tscn"
$baseWorkerExample = Read "addons/beep_game_builder_cs/templates/scenes/terrain/base_worker_templates_example.tscn"
if ($examplesReadme -notmatch 'grid_world_kit_hud_example\.tscn' -or
    $examplesReadme -notmatch 'base_worker_templates_example\.tscn' -or
    $examplesReadme -notmatch 'terrain_generator_lab\.tscn' -or
    $examplesReadme -notmatch 'terrain_guards\.ps1') {
    Fail "tests/examples/README.md must document the shipped example scenes and the guards that drive them."
}
foreach ($required in @("OilfieldSettlersShowcase", "WorldArt", "ClearedYard", "RoadMain", "PreparedPlots", "DepotRoof", "Truck_Clear", "Camera2D", "StartingResourceAmounts", "HideZeroAmounts = false", "GridDispatchBoardComponent.cs", "StatusLabelPath", "KitPanelContainer.cs", "KitPushButton.cs", "KitLabel.cs", "GenerateControlsWhenPathsEmpty = false")) {
    if ($gridWorldExample -notmatch [regex]::Escape($required)) { Fail "grid_world_kit_hud_example.tscn is missing authored showcase element $required." }
}
# The wallet's startup amounts are a plain dictionary, not authored C# Resource
# subresources. Deliberately NOT a bare Array[Resource] match: the dispatch board
# carries its tasks as exactly that, and legitimately - it is the
# Definition : Resource pattern this folder uses.
if ($gridWorldExample -match 'StartingResources\s*=|resource_amount|SubResource\("resource_') {
    Fail "grid_world_kit_hud_example.tscn must not create startup wallet entries as C# Resource subresources."
}
# The showcase's behaviour is a data-driven component now, not a controller script
# sitting beside the scene: a switch over button names, with screen coordinates as
# literals in its cases, became a board of task definitions.
$dispatchBoard = Read "addons/beep_game_builder_cs/ecs/grid/GridDispatchBoardComponent.cs"
$dispatchTask = Read "addons/beep_game_builder_cs/ecs/grid/GridDispatchTaskDefinition.cs"
if ($dispatchBoard -notmatch 'class\s+GridDispatchBoardComponent' -or
    $dispatchBoard -notmatch 'Array<GridDispatchTaskDefinition> Tasks') {
    Fail "GridDispatchBoardComponent must carry the showcase's dispatch tasks as data rather than as cases in a switch."
}
if ($dispatchTask -notmatch 'class\s+GridDispatchTaskDefinition\s*:\s*Resource') {
    Fail "GridDispatchTaskDefinition must follow the Definition : Resource pattern this folder already uses."
}
if ($baseWorkerExample -notmatch 'grid_base_depot\.tscn' -or $baseWorkerExample -notmatch 'grid_worker_unit\.tscn') {
    Fail "base_worker_templates_example.tscn does not instance the base and worker templates."
}
if ($headlessSmoke -notmatch 'terrain/grid_world_kit_hud_example\.tscn' -or
    $headlessSmoke -notmatch 'terrain/base_worker_templates_example\.tscn') {
    Fail "headless runtime smoke must load the example scenes from where the addon ships them."
}
$renderProbe = Read "tests/render_scene_probe.gd"
if ($renderProbe -notmatch 'terrain/grid_world_kit_hud_example\.tscn' -or $renderProbe -notmatch 'save_png' -or $renderProbe -notmatch 'non_empty_pixels') {
    Fail "render_scene_probe.gd does not verify that the showcase renders visible content."
}
$renderProbeRunner = Read "tests/render_scene_probe.ps1"
if ($renderProbeRunner -notmatch 'render_scene_probe\.gd' -or $renderProbeRunner -notmatch '\[render-probe\] OK:') {
    Fail "render_scene_probe.ps1 does not run the visual render probe."
}
$renderCapture = Read "tests/render_scene_capture.gd"
if ($renderCapture -notmatch 'root\.content_scale_size\s*=\s*Vector2i\(width,\s*height\)') {
    Fail "render_scene_capture.gd must set root.content_scale_size so viewport captures exercise responsive Control layout rather than only project stretch scaling."
}
$kitGalleryScene = Read "addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn"
foreach ($required in @('[node name="Scroll" type="ScrollContainer" parent="Margin"]', '[node name="Root" type="VBoxContainer" parent="Margin/Scroll"]', '[node name="Row1" type="HFlowContainer"', '[node name="Actions" type="HFlowContainer" parent="Margin/Scroll/Root/Row1"]', '[node name="Row2" type="HFlowContainer"', '[node name="Footer" type="HFlowContainer"', 'custom_minimum_size = Vector2(260, 286)')) {
    if ($kitGalleryScene -notmatch [regex]::Escape($required)) { Fail "kit_gallery.tscn must keep its responsive scroll/wrap showcase layout: $required." }
}
if ($kitGalleryScene -notmatch '\[node name="Actions" type="HFlowContainer"[\s\S]*?size_flags_horizontal = 3') {
    Fail "kit_gallery.tscn Actions flow must expand horizontally so desktop does not collapse the action widgets into one column."
}
$kitBrowserScene = Read "addons/beep_game_builder_cs/templates/scenes/kit_browser.tscn"
foreach ($required in @('[node name="Background" type="ColorRect" parent="."]', '[node name="Root" type="VBoxContainer" parent="Margin"]', '[node name="Header" type="HFlowContainer" parent="Margin/Root"]', '[node name="GenrePicker" type="OptionButton" parent="Margin/Root/Header"]', '[node name="ScrollFrame" type="Control" parent="Margin/Root"]', '[node name="Content" type="VBoxContainer" parent="Margin/Root/ScrollFrame/Scroll"]', '[node name="Theme" type="Node" parent="Margin/Root"]', 'horizontal_scroll_mode = 0')) {
    if ($kitBrowserScene -notmatch [regex]::Escape($required)) { Fail "kit_browser.tscn must keep its authored responsive browser shell: $required." }
}
$kitBrowserCode = Read "addons/beep_game_builder_cs/ecs/scenes/KitBrowser.cs"
foreach ($forbidden in @('new MarginContainer', 'new ScrollContainer', 'new OptionButton { CustomMinimumSize', 'new ThemePresetComponent { Name = "Theme" }')) {
    if ($kitBrowserCode -match [regex]::Escape($forbidden)) { Fail "KitBrowser.cs must bind the authored kit_browser.tscn shell instead of rebuilding browser chrome in code: $forbidden." }
}
$showcaseInteractionProbe = Read "tests/showcase_interaction_probe.gd"
foreach ($required in @("HUD/HudRoot/ToolPalette/Panel/Row/Clear", "HUD/HudRoot/ToolPalette/Panel/Row/Road", "HUD/HudRoot/ToolPalette/Panel/Row/Plant", 'call("GetAmount"', "[showcase-interaction] OK:")) {
    if ($showcaseInteractionProbe -notmatch [regex]::Escape($required)) { Fail "showcase_interaction_probe.gd does not exercise the authored HUD workflow: $required." }
}
$showcaseInteractionRunner = Read "tests/showcase_interaction_probe.ps1"
if ($showcaseInteractionRunner -notmatch 'showcase_interaction_probe\.gd' -or $showcaseInteractionRunner -notmatch '\[showcase-interaction\] OK:') {
    Fail "showcase_interaction_probe.ps1 does not run the authored HUD interaction probe."
}
$addonChecks = Read "tests/run_addon_checks.ps1"
if ($addonChecks -notmatch 'showcase_interaction_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the authored HUD interaction probe."
}
if ($addonChecks -notmatch 'theme_gallery_layout_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the theme gallery desktop/mobile geometry probe."
}
if ($addonChecks -notmatch 'kit_gallery_desktop\.png' -or $addonChecks -notmatch 'kit_gallery_mobile\.png') {
    Fail "run_addon_checks.ps1 must capture kit_gallery at desktop and mobile viewport sizes."
}
if ($addonChecks -notmatch 'kit_gallery_layout_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit gallery desktop/mobile geometry probe."
}
if ($addonChecks -notmatch 'kit_browser_desktop\.png' -or $addonChecks -notmatch 'kit_browser_mobile\.png') {
    Fail "run_addon_checks.ps1 must capture kit_browser at desktop and mobile viewport sizes."
}
if ($addonChecks -notmatch 'kit_browser_layout_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit browser desktop/mobile geometry probe."
}
$kitGalleryLayoutProbe = Read "tests/kit_gallery_layout_probe.gd"
foreach ($required in @("desktop", "mobile", "Margin/Scroll/Root/Row1/Actions/Dial", "Mobile Weather card spills outside Equipment panel", "Mobile Bag overlaps Equipment panel", "[kit-gallery-layout] OK:")) {
    if ($kitGalleryLayoutProbe -notmatch [regex]::Escape($required)) { Fail "kit_gallery_layout_probe.gd does not assert the expected gallery geometry: $required." }
}
$kitBrowserLayoutProbe = Read "tests/kit_browser_layout_probe.gd"
foreach ($required in @("desktop", "mobile", "Margin/Root/Header/GenrePicker", "Mobile browser toolbar overlaps the wrapped title.", "Mobile browser content starts outside the viewport", "[kit-browser-layout] OK:")) {
    if ($kitBrowserLayoutProbe -notmatch [regex]::Escape($required)) { Fail "kit_browser_layout_probe.gd does not assert the expected browser geometry: $required." }
}
$themeGalleryLayoutProbe = Read "tests/theme_gallery_layout_probe.gd"
foreach ($required in @("desktop", "mobile", "Margin/VBox/Header/GenreOption", "Margin/VBox/ScrollFrame/Scroll/Content/TypeSection", "theme gallery scroll body overlaps picker header", "must not expose a UI chrome texture toggle", "[theme-gallery-layout] OK:")) {
    if ($themeGalleryLayoutProbe -notmatch [regex]::Escape($required)) { Fail "theme_gallery_layout_probe.gd does not assert the expected theme gallery geometry: $required." }
}

$cityBuilderGenre = Read "addons/beep_game_builder_cs/catalogs/skins/citybuilder/genre.json"
$oilfieldTheme = Read "addons/beep_game_builder_cs/catalogs/skins/citybuilder/themes/oilfield_days/theme.json"
$skinCatalog = Read "addons/beep_game_builder_cs/ecs/ui/SkinCatalog.cs"
$themePreset = Read "addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.cs"
$themeNodeTheming = Read "addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.NodeTheming.cs"
$themeInterface = Read "addons/beep_game_builder_cs/ecs/ui/IThemePreset.cs"
$fileThemePreset = Read "addons/beep_game_builder_cs/ecs/ui/FileThemePreset.cs"
$paletteTintedPreset = Read "addons/beep_game_builder_cs/ecs/ui/PaletteTintedPreset.cs"
$gameInfo = Read "addons/beep_game_builder_cs/core/GameInfo.cs"
$gameApp = Read "addons/beep_game_builder_cs/ecs/GameApp.cs"
$builderDock = Read "addons/beep_game_builder_cs/ui/BeepGameBuilderDock.cs"
$kitChrome = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitChrome.cs"
$projectDefaults = Read "addons/beep_game_builder_cs/core/BeepProjectDefaults.cs"
$mcpCommands = Read "addons/beep_game_builder_cs/mcp/BeepMcpCommands.cs"
$mcpSceneCommands = Read "addons/beep_game_builder_cs/mcp/BeepMcpSceneCommands.cs"
$mcpBridgeAuthoring = Read "addons/godot_mcp/GodotMcpBridgeController.Authoring.cs"
$mcpServerAuthoring = Read "tools/beep-mcp-server/src/authoring.ts"
$mcpServerProtocol = Read "tools/beep-mcp-server/src/protocol.ts"
$mcpServerTools = Read "tools/beep-mcp-server/src/tools.ts"
if ($cityBuilderGenre -notmatch '"oilfield_days"') { Fail "City Builder skin catalog does not list the Oilfield Days theme." }
foreach ($required in @(
    '"id": "oilfield_days"',
    '"register": "technical"',
    '"frame_mode": "hairline"',
    '"shadow": "none"',
    '"studs": 0',
    '"upper_case": false',
    '"rim_brightness"',
    '"select_slot": "border\|glow"'
)) {
    if ($oilfieldTheme -notmatch $required) { Fail "Oilfield Days theme is missing required mockup-derived token: $required." }
}
if ($skinCatalog -match 'SettingHudArt|HudTextures|beep/ui/hud_textures|SourceMode|texture_source|texture_custom_root|BuildStyleBox\(|ThemeTextureSlots|TextureSlotDef|ParseTextures|ParseTextureSlot|\.Textures') {
    Fail "SkinCatalog must not expose or load UI chrome texture settings."
}
$themeJsonRoot = Join-Path $Root "addons/beep_game_builder_cs/catalogs/skins"
foreach ($themeJson in Get-ChildItem -Path $themeJsonRoot -Filter "theme.json" -Recurse) {
    $themeJsonText = Get-Content -LiteralPath $themeJson.FullName -Raw
    if ($themeJsonText -match '"textures"\s*:|"texture_path"\s*:') {
        Fail "$($themeJson.FullName.Substring($Root.Length + 1)) must not declare UI chrome texture slots."
    }
    if ($themeJsonText -match '"grain"\s*:|"grain_amount"\s*:|"grain_tiles"\s*:') {
        Fail "$($themeJson.FullName.Substring($Root.Length + 1)) must not declare kit grain texture settings."
    }
}
if ($projectDefaults -match 'SettingHudArt|hud_textures') {
    Fail "Project defaults must not register HUD texture settings."
}
foreach ($removedTextureApi in @(
    "addons/beep_game_builder_cs/ecs/ui/UISkin.cs",
    "addons/beep_game_builder_cs/ecs/ui/UISkin.cs.uid",
    "addons/beep_game_builder_cs/core/BeepTextureBaker.cs",
    "addons/beep_game_builder_cs/core/BeepTextureBaker.cs.uid",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGrain.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGrain.cs.uid",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGrainTable.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGrainTable.cs.uid"
)) {
    if (Test-Path (Join-Path $Root $removedTextureApi)) {
        Fail "$removedTextureApi must not exist; texture-backed UI chrome has been removed."
    }
}
foreach ($removedTextureFolder in @(
    "addons/beep_game_builder_cs/textures/hud",
    "addons/beep_game_builder_cs/textures/cardgame",
    "addons/beep_game_builder_cs/textures/cartoon",
    "addons/beep_game_builder_cs/textures/platformer",
    "addons/beep_game_builder_cs/textures/puzzle",
    "addons/beep_game_builder_cs/textures/racing",
    "addons/beep_game_builder_cs/textures/rpg",
    "addons/beep_game_builder_cs/textures/scifi",
    "addons/beep_game_builder_cs/textures/sea",
    "addons/beep_game_builder_cs/textures/shooter",
    "addons/beep_game_builder_cs/textures/strategy",
    "addons/beep_game_builder_cs/textures/survival",
    "addons/beep_game_builder_cs/textures/topdown",
    "addons/beep_game_builder_cs/textures/citybuilder/blueprint",
    "addons/beep_game_builder_cs/textures/citybuilder/eco",
    "addons/beep_game_builder_cs/textures/citybuilder/future",
    "addons/beep_game_builder_cs/textures/citybuilder/industrial",
    "addons/beep_game_builder_cs/textures/citybuilder/urban",
    "addons/beep_game_builder_cs/textures/grain"
)) {
    if (Test-Path (Join-Path $Root $removedTextureFolder)) {
        Fail "$removedTextureFolder must not exist; UI kit chrome uses procedural theme drawing only."
    }
}
if (($mcpCommands + $mcpSceneCommands + $mcpBridgeAuthoring + $mcpServerAuthoring + $mcpServerProtocol + $mcpServerTools) -match 'UISkin|beep\.bake_textures|BakeTextures|BeepTextureBaker|texture baking|Bake the textures') {
    Fail "MCP command surfaces must not advertise or route removed UI chrome texture features."
}
if ($themePreset -match '\bUseTextures\b|\bUse(Button|Panel|Input|ProgressBar|Dialog|Slider|ScrollBar|Separator)Textures\b|UISkin\?\s+Skin|_skin|_useTextures|TextureSlotDef|ThemeTextureSlots|SkinCatalog\.HudTextures|StyleBoxTexture') {
    Fail "ThemePresetComponent must not expose or key off UI chrome texture settings."
}
if ($themeInterface -match 'UsesTextures|TexturePath|Get(Button|Panel|Dialog|Input|Progress|Slider|Scroll|Separator|Hud).*Texture|IHudTexturePreset|UsesHudTextures|GetHudTexture') {
    Fail "IThemePreset must not expose UI chrome texture APIs."
}
if ($fileThemePreset -match 'UsesTextures|TexturePath|Get(Button|Panel|Dialog|Input|Progress|Slider|Scroll|Separator|Hud).*Texture|IHudTexturePreset|UsesHudTextures|GetHudTexture|BuildStyleBox\(') {
    Fail "FileThemePreset must build procedural styleboxes only."
}
if ($paletteTintedPreset -match 'UsesTextures|TexturePath|Get(Button|Panel|Dialog|Input|Progress|Slider|Scroll|Separator|Hud).*Texture|IHudTexturePreset|UsesHudTextures|GetHudTexture') {
    Fail "PaletteTintedPreset must not forward UI chrome texture APIs."
}
if ($gameInfo -match 'UISkin\?\s+Skin|StyleBoxTexture \(9-patch\)|texture-based UI skin') {
    Fail "GameInfo must not expose UI chrome texture skin settings."
}
if ($gameApp -match 'UISkin\?\s+Skin|Skin\s*=|info\.Skin|texture-based UI skin') {
    Fail "GameApp must not expose or copy UI chrome texture skin settings."
}
foreach ($geometryFile in Get-ChildItem -LiteralPath (Join-Path $Root "addons/beep_game_builder_cs/catalogs/skins") -Recurse -Filter "geometry.json") {
    $geometryJson = Get-Content -LiteralPath $geometryFile.FullName -Raw
    if ($geometryJson -match '"background_image"\s*:|"background_mode"\s*:') {
        $relativeGeometry = $geometryFile.FullName.Substring($Root.Length + 1)
        Fail "$relativeGeometry must not declare UI texture backgrounds; skin geometry should stay procedural."
    }
}
if ($builderDock -match 'info\.Skin|\.Skin\s*=') {
    Fail "BeepGameBuilderDock must not write UI chrome texture skin settings."
}
if ($themeNodeTheming -match 'SkinOr\(|WithThemePadding\(|TextureRegister\(|SurfaceForSlot\(|Get(Button|Panel|Dialog|Input|Progress|Slider|Scroll|Separator|Hud).*Texture\(|_skin\?\.') {
    Fail "ThemePresetComponent.NodeTheming must not request or process chrome texture resources."
}
if ($themeNodeTheming -notmatch 'case StyleBoxFlat flat: FlatRegister\(flat, c\); break;' -or $themeNodeTheming -match 'case StyleBoxTexture') {
    Fail "ThemePresetComponent.NodeTheming game-art register must handle flat procedural boxes only."
}

$kitCore = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCore.cs"
$kitControl = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitControl.cs"
$kitLayer = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLayer.cs"
$kitStyleJson = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitStyleJson.cs"
$uiSurface = Read "addons/beep_game_builder_cs/ecs/ui/UiSurface.cs"
$kitPanel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanel.cs"
$kitPanelContainer = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanelContainer.cs"
$kitCollapsible = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs"
if ($kitCore -notmatch 'enum\s+KitPanelHeaderStyle') { Fail "KitPanelHeaderStyle enum is missing." }
if ($kitControl -match 'DebugOutline|TEMPORARY diagnostic') { Fail "KitControl must not expose temporary diagnostic switches in production kit code." }
if ($kitControl -match 'KitArt\.|TryDrawArt|ArtName|ArtModulate|DrawAfterArt|new StyleBoxTexture \{ Texture = tex \}') {
    Fail "KitControl must not draw texture-backed UI chrome; kit widgets should use procedural layers."
}
if ($uiSurface -match 'StyleBoxTexture|ArtNominalLuminance') {
    Fail "UiSurface must not compensate for removed texture-backed chrome."
}
if (Test-Path (Join-Path $Root "addons/beep_game_builder_cs/ecs/ui/kit/KitArt.cs")) {
    Fail "KitArt.cs must not exist; the UI kit must not expose a texture-backed chrome resolver."
}
if (($kitCore + $kitControl + $kitChrome + $kitLayer + $kitStyleJson) -match 'KitLayerKind\.Grain|KitGrain\.|GrainPattern|GrainAmount|GrainTiles|textures/grain|\"grain_amount\"|\"grain_tiles\"') {
    Fail "UI kit must not keep the removed grain texture layer, parser fields, or grain asset path."
}
if (($kitCore + $kitControl + $kitChrome + $kitLayer + $kitStyleJson + $kitPanel + $kitPanelContainer + $kitCollapsible) -match 'atlas-style|texture-backed UI chrome|UI chrome texture|StyleBoxTexture|KitArt\.|TryDrawArt|ArtName|ArtModulate|DrawAfterArt') {
    Fail "UI kit chrome must stay procedural and must not reintroduce atlas/textured styling hooks."
}
$kitEdgeRun = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitEdgeRun.cs"
if ($kitEdgeRun -match 'KitSegFill\.(Hatch|Ticks)|case\s+KitSegFill\.(Hatch|Ticks)|Diagonal hatching|ruler-like') {
    Fail "UI kit edge runs must not draw texture-like hatch or tick fill patterns."
}
foreach ($themeJson in Get-ChildItem -Path $themeJsonRoot -Filter "theme.json" -Recurse) {
    $themeJsonText = Get-Content -LiteralPath $themeJson.FullName -Raw
    if ($themeJsonText -match '"fill"\s*:\s*"(hatch|ticks)"') {
        Fail "$($themeJson.FullName.Substring($Root.Length + 1)) must not declare texture-like hatch or tick edge fills."
    }
}
if ($kitControl -notmatch 'AutoInputDefaults[\s\S]*get => _autoInputDefaults[\s\S]*if \(_autoInputDefaults == value\) return;[\s\S]*private bool _autoInputDefaults = true' -or
    $kitControl -notmatch 'protected void ApplyInputDefaults\(MouseFilterEnum\? mouseFilter = null,\s*FocusModeEnum\? focusMode = null\)' -or
    $kitControl -notmatch 'KitChrome\.ApplyInputDefaults\(this,\s*AutoInputDefaults,\s*mouseFilter,\s*focusMode\)') {
    Fail "KitControl must expose AutoInputDefaults and ApplyInputDefaults so authored scene mouse/focus settings can opt out of kit startup defaults."
}
$kitFocusDefaultPattern = '(FocusMode\s*=\s*FocusModeEnum\.All|ApplyInputDefaults\([^;\r\n]*FocusModeEnum\.All|KitChrome\.ApplyInputDefaults\([^;\r\n]*FocusModeEnum\.All)'
$nativeInputDefaultFiles = @(
    "KitButton.cs",
    "KitBuildTile.cs",
    "KitCheckBox.cs",
    "KitCheckButton.cs",
    "KitColorOverlay.cs",
    "KitIconButton.cs",
    "KitKnob.cs",
    "KitModalShade.cs",
    "KitOptionButton.cs",
    "KitPushButton.cs",
    "KitRemovableChip.cs",
    "KitSlider.cs",
    "KitSliderBar.cs",
    "KitStarRating.cs",
    "KitSwitchVisual.cs",
    "KitTabStrip.cs",
    "KitToggle.cs"
)
if ($kitChrome -notmatch 'public static void ApplyInputDefaults\(' `
    -or $kitChrome -notmatch 'if \(!autoInputDefaults\) return;' `
    -or $kitChrome -notmatch 'if \(mouseFilter\.HasValue && ctl\.MouseFilter != mouseFilter\.Value\)[\s\S]*ctl\.MouseFilter = mouseFilter\.Value' `
    -or $kitChrome -notmatch 'if \(focusMode\.HasValue && ctl\.FocusMode != focusMode\.Value\)[\s\S]*ctl\.FocusMode = focusMode\.Value') {
    Fail "KitChrome must expose shared change-only ApplyInputDefaults for native Godot-derived kit wrappers."
}
foreach ($fileName in $nativeInputDefaultFiles) {
    $source = Read "addons/beep_game_builder_cs/ecs/ui/kit/$fileName"
    if ($source -notmatch 'AutoInputDefaults[\s\S]*get => _autoInputDefaults[\s\S]*if \(_autoInputDefaults == value\) return;[\s\S]*private bool _autoInputDefaults = true' -or
        $source -notmatch 'KitChrome\.ApplyInputDefaults\(this,\s*AutoInputDefaults') {
        Fail "$fileName must expose AutoInputDefaults and apply startup mouse/focus defaults through KitChrome.ApplyInputDefaults."
    }
    if ($source -match '(?m)^\s*(MouseFilter|FocusMode)\s*=') {
        Fail "$fileName must not directly overwrite authored scene mouse/focus settings in _Ready."
    }
}
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    if ($file.Name -eq "KitControl.cs") { continue }
    $source = Get-Content -LiteralPath $file.FullName -Raw
    if ($source -match ':\s*KitControl' -and $source -match '(?m)^\s*(MouseFilter|FocusMode)\s*=') {
        Fail "$($file.Name) derives from KitControl and must use ApplyInputDefaults instead of overriding authored mouse/focus settings directly."
    }
}
foreach ($required in @("frame_mode", "studs", "rim_brightness", "height_ratio", "pad_ratio", "well_shade")) {
    if ($kitStyleJson -notmatch ('"' + [regex]::Escape($required) + '"')) { Fail "KitStyleJson does not accept kit.$required from theme.json." }
}
if ($kitChrome -notmatch 'DrawPanelHeader' -or $kitChrome -notmatch 'PanelHeaderRoom' -or $kitChrome -notmatch 'PanelHeaderOverhang') {
    Fail "KitChrome does not expose the shared panel header helpers."
}
if ($kitChrome -notmatch 'DrawPanelHeader\(ctl,\s*genre,\s*host,\s*text,\s*KitPanelHeaderStyle\.Banner,\s*shape,\s*shade,[\s\r\n ]*0\.90f' -or
    $kitChrome -notmatch 'float h = Mathf\.Max\(titleFs \* 1\.35f,\s*14f\)' -or
    $kitChrome -notmatch 'new Vector2\(r\.Size\.X - padX \* 2f,\s*h \* 0\.84f\)' -or
    $kitChrome -notmatch '0\.82f,\s*text,\s*font,\s*min:\s*8') {
    Fail "KitChrome utility panel headers must use readable header sizing, not tiny caption-style fit bounds."
}
if ($kitChrome -notmatch 'DrawFocusRing' -or $kitChrome -notmatch 'IsConfirmKey' -or $kitChrome -notmatch 'DirectionFromKey') {
    Fail "KitChrome does not expose shared keyboard/focus helpers for custom controls."
}
if ($kitChrome -notmatch 'ShouldClearPointerState' -or $kitChrome -notmatch 'NotificationVisibilityChanged' -or $kitChrome -notmatch 'IsVisibleInTree') {
    Fail "KitChrome must expose a shared hidden-state reset helper for custom hover/drag visuals."
}
if ($kitChrome -notmatch 'ActivateOnClickOrConfirm') {
    Fail "KitChrome must expose shared click/confirm activation handling for simple custom controls."
}
if ($kitChrome -notmatch 'WrapLines' -or $kitChrome -notmatch 'DrawWrappedText') {
    Fail "KitChrome does not expose shared wrapped text helpers."
}
foreach ($required in @(
    "SetStyleboxOverrideIfChanged",
    "SetEmptyStyleboxOverride",
    "SetBlankIconOverride",
    "SetColorOverrideIfChanged",
    "RemoveColorOverrideIfPresent",
    "SetFontSizeOverrideIfChanged",
    "SetConstantOverrideIfChanged",
    "SetFontOverrideIfChanged"
)) {
    if ($kitChrome -notmatch $required) { Fail "KitChrome is missing shared idempotent theme override helper $required." }
}
if ($kitChrome -notmatch '(?s)HasThemeStyleboxOverride\(name\).*new StyleBoxEmpty') {
    Fail "KitChrome.SetEmptyStyleboxOverride must inspect the existing override before creating a replacement StyleBoxEmpty."
}
if ($kitChrome -notmatch 'public static bool SetEmptyStyleboxOverride' -or $kitChrome -notmatch 'SameMargins\(existing,\s*left,\s*right,\s*top,\s*bottom\)\)[\s\S]*return false;[\s\S]*AddThemeStyleboxOverride[\s\S]*return true;') {
    Fail "KitChrome.SetEmptyStyleboxOverride must report whether it changed a stylebox so callers can skip no-op layout invalidation."
}
foreach ($required in @(
    '(?s)GetThemeColor\(name\).*return',
    'GetThemeFontSize\(name\) == value',
    'GetThemeConstant\(name\) == value',
    'GetThemeFont\(name\) == value'
)) {
    if ($kitChrome -notmatch $required) {
        Fail "KitChrome idempotent override helpers must compare the inherited effective theme value before creating a per-node override: $required."
    }
}
if ($kitChrome -notmatch 'ThemeWriteMetaPrefix' -or
    $kitChrome -notmatch 'BeginThemeOverrideWrite' -or
    $kitChrome -notmatch 'EndThemeOverrideWrite' -or
    $kitChrome -notmatch 'SetFontSizeOverrideIfChanged[\s\S]*BeginThemeOverrideWrite\(ctl,\s*"font_size"' -or
    $kitChrome -notmatch 'SetFontOverrideIfChanged[\s\S]*BeginThemeOverrideWrite\(ctl,\s*"font"' -or
    $kitChrome -notmatch 'SetColorOverrideIfChanged[\s\S]*BeginThemeOverrideWrite\(ctl,\s*"color"' -or
    $kitChrome -notmatch 'SetStyleboxOverrideIfChanged[\s\S]*BeginThemeOverrideWrite\(ctl,\s*"stylebox"' -or
    $kitChrome -notmatch 'SetConstantOverrideIfChanged[\s\S]*BeginThemeOverrideWrite\(ctl,\s*"constant"') {
    Fail "KitChrome theme override helpers must mark in-progress writes; Godot sends synchronous theme notifications before new overrides are visible."
}
$directThemeOverrideFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    if ($file.Name -eq "KitChrome.cs") { continue }
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match 'AddTheme(Stylebox|Icon|Color|FontSize|Constant|Font)Override\s*\(') {
        $directThemeOverrideFiles += $file.Name
    }
}
if ($directThemeOverrideFiles.Count -gt 0) {
    Fail "Kit controls must use KitChrome idempotent theme override helpers instead of direct AddTheme*Override calls: $($directThemeOverrideFiles -join ', ')."
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs"; Reset = "ClearHandleHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs"; Reset = "ClearPointerState" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs"; Reset = "ClearPointerState" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs"; Reset = "ClearDragState" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSliderBar.cs"; Reset = "ClearDragState" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs"; Reset = "ClearHover" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"; Reset = "ClearHover" }
)) {
    $source = Read $entry.Path
    if ($source -notmatch 'public override void _Notification\(int what\)' -or
        $source -notmatch ('ShouldClearPointerState\(this,\s*what\)[\s\S]*' + [regex]::Escape($entry.Reset) + '\(\)')) {
        Fail "$($entry.Path) must clear custom pointer state when the control leaves the visible tree."
    }
}
$wrapperThemeOverrideFiles = @()
foreach ($relativePath in @(
    "addons/beep_game_builder_cs/ecs/ui/BeepDialogLayout.cs",
    "addons/beep_game_builder_cs/ecs/ui/BossHealthBarComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/BuffBarComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/BuildToolbarComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/CollapsiblePanelComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/GameSpeedComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/InteractionPromptComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/LoadGameMenuComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/MatchTimerComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/MeterBarComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/PanelFrameComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/RatingComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/SaveGameMenuComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/SearchBarComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/StepperComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/TabGroupComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/TableComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/ToggleSwitchComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/WeatherForecastUI.cs",
    "addons/beep_game_builder_cs/ecs/ui/WeatherHUDComponent.cs",
    "addons/beep_game_builder_cs/ecs/ui/hud/GenreHudComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridBuildToolbarComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridCalendarHudComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridInteractionModeBarComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridInteractionStatusComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridJobBoardComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridObjectivePanelComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridObjectInspectorComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridProductionPanelComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridResourceBarComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridToolPaletteComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridWorkerSpawnerPanelComponent.cs",
    "addons/beep_game_builder_cs/ecs/grid/ui/GridWorkerStatusPanelComponent.cs"
)) {
    $source = Read $relativePath
    if ($source -match '(Add|Remove)Theme(Stylebox|Icon|Color|FontSize|Constant|Font)Override\s*\(') {
        $wrapperThemeOverrideFiles += $relativePath
    }
}
if ($wrapperThemeOverrideFiles.Count -gt 0) {
    Fail "Authored-scene UI wrappers must use KitChrome idempotent theme override helpers instead of direct AddTheme*/RemoveTheme* calls: $($wrapperThemeOverrideFiles -join ', ')."
}
$panelFrame = Read "addons/beep_game_builder_cs/ecs/ui/PanelFrameComponent.cs"
if ($panelFrame -notmatch 'Title\s*\{[^\r\n]*string next = value \?\? ""[\s\S]*if \(_title == next\) return[\s\S]*RefreshFrameChrome\(\)' -or
    $panelFrame -notmatch 'TitleIcon\s*\{[^\r\n]*if \(_titleIcon == value\) return[\s\S]*RefreshFrameChrome\(\)' -or
    $panelFrame -notmatch 'OutlineWidth[\s\S]*Mathf\.Max\(0,\s*value\)[\s\S]*if \(_outlineWidth == next\) return[\s\S]*RefreshFrameChrome\(\)' -or
    $panelFrame -notmatch 'TitleFontScale[\s\S]*Mathf\.Clamp\(value,\s*0\.5f,\s*3\.0f\)[\s\S]*RefreshFrameChrome\(\)' -or
    $panelFrame -notmatch 'FramePadding[\s\S]*Mathf\.Max\(0,\s*value\)[\s\S]*RefreshFrameChrome\(\)' -or
    $panelFrame -notmatch 'DrawWell[\s\S]*if \(_drawWell == value\) return[\s\S]*RefreshFrameChrome\(\)' -or
    $panelFrame -notmatch 'RefreshFrameChrome[\s\S]*IsInsideTree\(\)[\s\S]*DriveSiblingMargin\(\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or
    $panelFrame -notmatch 'TargetPath[\s\S]*if \(_targetPath == value\) return[\s\S]*_target = null[\s\S]*UpdateProcessing\(\)' -or
    $panelFrame -notmatch 'private void UpdateProcessing\(\)\s*=>\s*SetProcess\(!TargetPath\.IsEmpty\)' -or
    $panelFrame -notmatch 'SetConstantOverrideIfChanged\(mc,\s*"margin_top"' -or
    $panelFrame -notmatch 'SetConstantOverrideIfChanged\(mc,\s*"margin_left"') {
    Fail "PanelFrameComponent exported header/layout setters and driven margins must be no-op guarded, layout-aware, and change-aware."
}
$resourceBadge = Read "addons/beep_game_builder_cs/ecs/ui/ResourceBadgeComponent.cs"
if ($resourceBadge -notmatch 'Icon\s*\{[^\r\n]*if \(_icon == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or
    $resourceBadge -notmatch 'Value\s*\{[^\r\n]*string next = value \?\? ""[\s\S]*if \(_value == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or
    $resourceBadge -notmatch 'Fill[\s\S]*Mathf\.IsEqualApprox\(_fill,\s*value\)[\s\S]*RefreshVisualAndRedraw\(\)' -or
    $resourceBadge -notmatch 'FontScale[\s\S]*Mathf\.Clamp\(value,\s*0\.5f,\s*3\.0f\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or
    $resourceBadge -notmatch 'IconScale[\s\S]*Mathf\.Clamp\(value,\s*1\.0f,\s*4\.0f\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or
    $resourceBadge -notmatch 'override\s+Vector2\s+_GetMinimumSize\(\)\s*=>\s*NaturalMinimumSize\(\)' -or
    $resourceBadge -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*NaturalMinimumSize\(\),\s*force\)[\s\S]*QueueRedraw\(\)' -or
    $resourceBadge -notmatch 'NotificationThemeChanged[\s\S]*RefreshMinimumAndRedraw\(force:\s*true\)') {
    Fail "ResourceBadgeComponent authored value/font/icon edits must be no-op guarded and refresh natural minimum size when layout can change."
}
$directFontFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    if ($file.Name -eq "KitFonts.cs") { continue }
    $source = Get-Content -Path $file.FullName -Raw
    $source = [regex]::Replace($source, '(?m)^\s*///.*$', '')
    $source = [regex]::Replace($source, '(?m)^\s*//.*$', '')
    if ($source -match 'KitFonts\.Resolve\s*\(' -or $source -match 'GetThemeDefaultFont\s*\(') {
        $directFontFiles += $file.Name
    }
}
if ($directFontFiles.Count -gt 0) {
    Fail "Kit controls must use KitChrome.Font or KitFonts.Fallback instead of direct font fallback calls: $($directFontFiles -join ', ')."
}
$kitFonts = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitFonts.cs"
$fontLicense = Read "addons/beep_game_builder_cs/fonts/LICENSE.txt"
$notoLicense = Read "addons/beep_game_builder_cs/fonts/NotoSans-OFL.txt"
$audexImport = Read "addons/beep_game_builder_cs/fonts/Audex-Regular.ttf.import"
$notoImport = Read "addons/beep_game_builder_cs/fonts/NotoSans-Variable.ttf.import"
if ($kitFonts -notmatch '\[KitFontRole\.Sans\]\s*=\s*"NotoSans-Variable\.ttf"' -or
    $kitFonts -notmatch '\[KitFontRole\.Condensed\]\s*=\s*"Audex-Regular\.ttf"' -or
    $kitFonts -notmatch 'ResourceLoader\.Exists\(path\)\s*\|\|\s*FileAccess\.FileExists\(path\)') {
    Fail "KitFonts must map Sans to Noto, Condensed to Audex, and accept real resource files with generated imports."
}
if ($fontLicense -notmatch 'Sans\s+NotoSans-Variable\.ttf') {
    Fail "fonts/LICENSE.txt must document the Sans role's Noto mapping."
}
if ($fontLicense -notmatch 'NotoSans-OFL\.txt' -or $notoLicense -notmatch 'SIL OPEN FONT LICENSE Version 1\.1') {
    Fail "Noto Sans must ship with its OFL text beside the bundled font."
}
if ($fontLicense -notmatch 'Condensed\s+Audex-Regular\.ttf') {
    Fail "fonts/LICENSE.txt must document the Condensed role's Audex mapping."
}
if ($audexImport -notmatch 'importer="font_data_dynamic"' -or $audexImport -notmatch 'source_file="res://addons/beep_game_builder_cs/fonts/Audex-Regular\.ttf"') {
    Fail "Audex-Regular.ttf must have a Godot font import sidecar so the Condensed role loads in headless and exported projects."
}
if ($notoImport -notmatch 'importer="font_data_dynamic"' -or $notoImport -notmatch 'source_file="res://addons/beep_game_builder_cs/fonts/NotoSans-Variable\.ttf"') {
    Fail "NotoSans-Variable.ttf must have a Godot font import sidecar so the Sans role loads in headless and exported projects."
}
$kitLabel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLabel.cs"
if ($kitLabel -notmatch 'KitChrome\.Font\(this,\s*_genre\)' -or $kitLabel -notmatch 'SetFontOverrideIfChanged\(this,\s*"font"') {
    Fail "KitLabel must apply the active kit font through the centralized font override helper."
}
foreach ($relativePath in @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs"
)) {
    $source = Read $relativePath
    if ($source -notmatch 'ActivateOnClickOrConfirm') {
        Fail "$relativePath must use KitChrome.ActivateOnClickOrConfirm for simple click/confirm activation."
    }
}
$kitNodeCard = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs"
if ($kitNodeCard -notmatch 'Locked\s*\{\s*get\s*=>\s*_locked;\s*set\s*\{[^}]*RefreshVisualAndRedraw\(\)') {
    Fail "KitNodeCard.Locked must redraw immediately when lock state changes."
}
$liveExportCoreKitFiles = @(
    "KitLabel.cs",
    "KitButton.cs",
    "KitPushButton.cs",
    "KitIconButton.cs",
    "KitPanel.cs"
)
foreach ($fileName in $liveExportCoreKitFiles) {
    $source = Read "addons/beep_game_builder_cs/ecs/ui/kit/$fileName"
    $passiveExports = [regex]::Matches($source, '\[Export(?:\([^\)]*\))?\]\s+public\s+[^\r\n]+\{\s*get;\s*set;\s*\}')
    if ($passiveExports.Count -gt 0) {
        Fail "$fileName has passive exported kit appearance/layout properties. Core design-time kit controls must redraw, reflow, or reapply when inspector values change."
    }
}
$liveExportReadoutKitFiles = @(
    "KitMeter.cs",
    "KitOrbMeter.cs",
    "KitRadialMeter.cs",
    "KitSlider.cs",
    "KitSliderBar.cs",
    "KitToggle.cs",
    "KitCheckBox.cs",
    "KitCheckButton.cs"
)
foreach ($fileName in $liveExportReadoutKitFiles) {
    $source = Read "addons/beep_game_builder_cs/ecs/ui/kit/$fileName"
    $passiveExports = [regex]::Matches($source, '\[Export(?:\([^\)]*\))?\]\s+public\s+[^\r\n]+\{\s*get;\s*set;\s*\}')
    if ($passiveExports.Count -gt 0) {
        Fail "$fileName has passive exported readout/input appearance properties. Design-time edits must redraw, rebuild, or reflow immediately."
    }
}
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    $source = Get-Content -Path $file.FullName -Raw
    $passiveExports = [regex]::Matches($source, '\[Export(?:\([^\)]*\))?\]\s+public\s+[^\r\n]+\{\s*get;\s*set;\s*\}')
    if ($passiveExports.Count -gt 0) {
        Fail "$($file.Name) has passive exported kit properties. Inspector edits must update drawing, theme overrides, layout, or backing state immediately."
    }
}
$kitButton = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitButton.cs"
if ($kitButton -notmatch 'BadgeText[\s\S]*Suppress\(\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitButton -notmatch 'public UiSurface\.Role Accent[\s\S]*if \(_accent == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitButton -notmatch 'public UiSurface\.Role BadgeRole[\s\S]*if \(_badgeRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitButton -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitButton -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitButton exported appearance changes must use guarded visual redraw and BadgeText/theme changes must refresh native Button margins."
}
if ($kitButton -notmatch 'DrawLabel\(body,\s*state,\s*face\)' -or $kitButton -notmatch 'UiSurface\.Ink\(face\)') {
    Fail "KitButton must draw label text against the actual accent plate face, not generic surface text."
}
foreach ($required in @("EllipsizeText(font, text", "string badge = KitChrome.Case(_badge, _genre)", "EllipsizeText(font, badge")) {
    if ($kitButton -notmatch [regex]::Escape($required)) {
        Fail "KitButton must ellipsize label and badge text inside their actual draw bounds: $required."
    }
}
$kitPushButton = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs"
if ($kitPushButton -notmatch 'public UiSurface\.Role Accent[\s\S]*QueueRedraw\(\)') {
    Fail "KitPushButton.Accent must redraw immediately for design-time edits."
}
if ($kitPushButton -notmatch 'DrawLabel\(state,\s*face\)' -or $kitPushButton -notmatch 'UiSurface\.Ink\(face\)') {
    Fail "KitPushButton must draw label text against the actual accent plate face, not generic surface text."
}
if ($kitPushButton -notmatch 'string\[\]\s+lines\s*=\s*KitChrome\.Case\(Text,\s*_genre\)\.Split' -or $kitPushButton -notmatch 'EllipsizeText\(font,\s*lines\[i\]') {
    Fail "KitPushButton must case and ellipsize each rendered label line inside the button draw bounds."
}
if ($kitPushButton -notmatch $kitFocusDefaultPattern -or $kitPushButton -notmatch 'DrawFocusRing') {
    Fail "KitPushButton must draw a visible kit focus ring after suppressing native button focus chrome."
}
if ($kitPushButton -notmatch 'private void SuppressBaseChrome\(\)[\s\S]*SetEmptyStyleboxOverride[\s\S]*UpdateMinimumSize\(\)') {
    Fail "KitPushButton.SuppressBaseChrome must invalidate Button minimum size after replacing native stylebox margins."
}
if ($kitPushButton -notmatch 'override\s+Vector2\s+_GetMinimumSize' -or $kitPushButton -notmatch 'SetAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)' -or $kitPushButton -notmatch 'RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)' -or $kitPushButton -notmatch 'KitChrome\.Case\(Text,\s*_genre\)\.Split' -or $kitPushButton -notmatch 'base\._GetMinimumSize\(\)') {
    Fail "KitPushButton must publish a natural minimum size that matches its custom cased/multiline label while preserving native Button sizing."
}
$kitPanel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanel.cs"
if ($kitPanel -notmatch 'RefreshPanelChrome' -or $kitPanel -notmatch 'public bool ShowClose[\s\S]*ApplyMouseFilterDefault\(\)' -or $kitPanel -notmatch 'public NodePath TargetPath[\s\S]*_target = null') {
    Fail "KitPanel exported chrome/target options must refresh drawing, input filtering and target resolution when edited."
}
if ($kitPanel -notmatch 'Title\s*\{[^\r\n]*SetText\(ref\s+_title' -or $kitPanel -notmatch 'HeaderStyle\s*\{[^\r\n]*RefreshPanelChrome\(\)' -or $kitPanel -notmatch 'TitleFontScale[\s\S]*RefreshPanelChrome\(\)' -or $kitPanel -notmatch 'RefreshPanelChrome[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitPanel title/header edits must refresh auto minimum size, notify layout, and redraw through RefreshPanelChrome."
}
if ($kitPanel -notmatch 'private float _titleFontScale = 0\.90f') {
    Fail "KitPanel default header title scale must stay readable; do not regress it to tiny utility text."
}
if ($kitPanel -notmatch 'ApplyCloseFocusDefault' -or $kitPanel -notmatch 'ShowClose\s*\?\s*FocusModeEnum\.All\s*:\s*FocusModeEnum\.None' -or $kitPanel -notmatch 'InputEventKey[\s\S]*IsConfirmKey[\s\S]*IsCancelKey[\s\S]*CloseRequested' -or $kitPanel -notmatch 'GrabFocus\(\)[\s\S]*CloseRequested' -or $kitPanel -notmatch 'DrawFocusRing\(this,\s*_genre,\s*CloseRect\(\)') {
    Fail "KitPanel.ShowClose draws an interactive close affordance and must support focus, keyboard close, mouse focus, and a close-button focus ring."
}
if ($kitPanel -notmatch 'AutoMouseFilter[\s\S]*_autoMouseFilter\s*=\s*true') {
    Fail "KitPanel must let scenes opt out of automatic mouse filtering so authored MouseFilter values are not overwritten at startup."
}
$applyMouseFilterDefault = [regex]::Match($kitPanel, 'private void ApplyMouseFilterDefault\(\)\s*\{(?<body>[\s\S]*?)\n\s*\}')
$applyMouseFilterDefaultBody = $applyMouseFilterDefault.Groups["body"].Value
$applyMouseFilterInvalid = -not $applyMouseFilterDefault.Success `
    -or $applyMouseFilterDefaultBody -notmatch 'if \(!AutoMouseFilter\)[\s\S]*return;' `
    -or $applyMouseFilterDefaultBody -notmatch 'MouseFilter = ShowClose \? MouseFilterEnum\.Stop : MouseFilterEnum\.Ignore' `
    -or $applyMouseFilterDefaultBody -match 'ApplyMouseFilterDefault\(\)'
if ($applyMouseFilterInvalid) {
    Fail "KitPanel.ApplyMouseFilterDefault must directly apply Stop/Ignore mouse filtering and must not recurse."
}
$kitPanelReady = [regex]::Match($kitPanel, 'public override void _Ready\(\)[\s\S]*?KitChrome\.SetAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\);')
$kitPanelReadyBody = if ($kitPanelReady.Success) { $kitPanelReady.Value } else { "" }
$kitPanelReadyInvalid = -not $kitPanelReady.Success `
    -or $kitPanelReadyBody -notmatch 'ApplyMouseFilterDefault\(\)' `
    -or $kitPanelReadyBody -match 'MouseFilter\s*=\s*ShowClose'
if ($kitPanelReadyInvalid) {
    Fail "KitPanel._Ready must apply mouse filtering through ApplyMouseFilterDefault so AutoMouseFilter=false preserves authored scene values."
}
if ($kitPanel -notmatch 'private bool _eventsHooked;' `
    -or $kitPanel -notmatch 'if \(!_eventsHooked\)[\s\S]*Resized \+= ApplyArchetypeOrnaments;[\s\S]*_eventsHooked = true;') {
    Fail "KitPanel._Ready must guard its resize event subscription so repeated ready cycles do not stack ornament refresh handlers."
}
if ($kitPanel -notmatch 'private bool ShouldFitTarget\(\)[\s\S]*!_targetPath\.IsEmpty && IsVisibleInTree\(\)' `
    -or $kitPanel -notmatch 'private void UpdateTargetFitProcessing\(\)[\s\S]*SetProcess\(ShouldFitTarget\(\)\)' `
    -or $kitPanel -notmatch 'TargetPath[\s\S]*_target = null;[\s\S]*UpdateTargetFitProcessing\(\)' `
    -or $kitPanel -notmatch 'public override void _Ready\(\)[\s\S]*UpdateTargetFitProcessing\(\)' `
    -or $kitPanel -notmatch 'NotificationVisibilityChanged[\s\S]*UpdateTargetFitProcessing\(\)' `
    -or $kitPanel -notmatch 'public override void _Process\(double delta\)[\s\S]*if \(!ShouldFitTarget\(\)\)[\s\S]*UpdateTargetFitProcessing\(\)') {
    Fail "KitPanel must not run per-frame target-fit processing unless TargetPath is configured and visible."
}
if ($kitPanel -notmatch 'GenerateOrnamentsWhenMissing[\s\S]*_generateOrnamentsWhenMissing' -or $kitPanel -notmatch 'ApplyArchetypeOrnaments' -or $kitPanel -notmatch 'KitArchetypes\.Apply\(this,\s*_archetype,\s*GenerateOrnamentsWhenMissing\)') {
    Fail "KitPanel archetype ornaments must bind authored KitOrnament children by default and only generate fallback ornaments when explicitly enabled."
}
$kitOrnament = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitOrnament.cs"
if ($kitOrnament -notmatch 'Kind[\s\S]*if \(_kind == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitOrnament -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitOrnament -notmatch 'OrnamentScale[\s\S]*Mathf\.IsEqualApprox\(_ornamentScale,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitOrnament -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitOrnament authored kind, role, and scale must use guarded visual-only redraw."
}
$kitPanelHanger = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanelHanger.cs"
if ($kitPanelHanger -notmatch 'Kind[\s\S]*if \(_kind == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanelHanger -notmatch 'Inset[\s\S]*Mathf\.IsEqualApprox\(_inset,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanelHanger -notmatch 'Accent[\s\S]*if \(_accent == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanelHanger -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitPanelHanger authored kind, inset, and accent must use guarded visual-only redraw."
}
$kitArchetypes = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitArchetypes.cs"
if ($kitArchetypes -notmatch 'generateWhenMissing\s*=\s*false' -or $kitArchetypes -notmatch 'FindAuthoredOrnament' -or $kitArchetypes -notmatch 'BuildGeneratedOrnament' -or $kitArchetypes -notmatch 'RemoveGeneratedOrnaments' -or $kitArchetypes -notmatch 'if \(!generateWhenMissing\)\s*\r?\n\s*continue;') {
    Fail "KitArchetypes.Apply must style authored KitOrnament children first and keep generated ornaments as explicit fallback only."
}
if ($kitArchetypes -match 'if \(!generateWhenMissing\)\s*\r?\n\s*RemoveGeneratedOrnaments\(host\)' `
    -or $kitArchetypes -notmatch 'if \(specs\.Length == 0\)[\s\S]*if \(generateWhenMissing\)[\s\S]*RemoveGeneratedOrnaments\(host\)[\s\S]*return;' `
    -or $kitArchetypes -notmatch 'Legacy generation and cleanup[\s\S]*only when explicitly requested') {
    Fail "KitArchetypes must not create or delete KitOrnament children while GenerateOrnamentsWhenMissing is false."
}
if ($kitArchetypes -notmatch 'FindGeneratedOrnament' `
    -or $kitArchetypes -notmatch 'RemoveUnclaimedGeneratedOrnaments' `
    -or $kitArchetypes -notmatch 'FindGeneratedOrnament\(host,\s*spec,\s*claimed\)[\s\S]*\?\?\s*BuildGeneratedOrnament\(host,\s*spec\)' `
    -or $kitArchetypes -notmatch 'if \(generateWhenMissing\)\s*\r?\n\s*RemoveUnclaimedGeneratedOrnaments\(host,\s*claimed\)') {
    Fail "KitArchetypes generated ornament fallback must reuse existing generated children and remove only stale generated nodes."
}
$kitMeter = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitMeter.cs"
if ($kitMeter -notmatch 'public UiSurface\.Role Fill[\s\S]*Rebuild\(\)') {
    Fail "KitMeter.Fill must rebuild immediately because end-cap attachment roles depend on it."
}
if ($kitMeter -notmatch 'override void _Notification\(int what\)[\s\S]*NotificationThemeChanged[\s\S]*SuppressNativeStyles\(\)[\s\S]*Rebuild\(\)' -or $kitMeter -notmatch 'private void SuppressNativeStyles\(\)[\s\S]*SetEmptyStyleboxOverride' -or $kitMeter -notmatch 'Rebuild[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitMeter -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)') {
    Fail "KitMeter is a native ProgressBar with custom kit drawing; it must suppress native styles, refresh minimum size, and rebuild attachments on theme changes."
}
if ($kitMeter -notmatch 'RefreshMinimumAndRedraw' -or $kitMeter -notmatch 'TextWidth' -or $kitMeter -notmatch 'Segments[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitMeter -notmatch 'Readout[\s\S]*if \(_readout == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitMeter -notmatch 'UpdateMinimumSize\(\)' -or $kitMeter -notmatch '_segments \* Mathf\.Max' -or $kitMeter -notmatch '_cap != null[\s\S]*w \+=' -or $kitMeter -notmatch '_caps[\s\S]*w \+=' -or $kitMeter -notmatch 'EllipsizeText\(font,\s*readout,\s*fs,\s*bar\.Size\.X') {
    Fail "KitMeter must refresh and derive its minimum size from segment count, readout text, and cap overhangs, and ellipsize readout inside the bar."
}
$kitSlider = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs"
$kitKnob = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs"
foreach ($entry in @(
    @{ Name = "KitSlider"; Source = $kitSlider },
    @{ Name = "KitKnob"; Source = $kitKnob },
    @{ Name = "KitMeter"; Source = $kitMeter }
)) {
    if ($entry.Source -match 'public override void _Ready\(\)[\s\S]*?(MinValue|MaxValue|Step)\s*=') {
        Fail "$($entry.Name) must not overwrite authored Range values in _Ready(); default range belongs in the constructor."
    }
    if ($entry.Source -notmatch "public $($entry.Name)\(\)[\s\S]*MinValue\s*=[\s\S]*MaxValue\s*=[\s\S]*Step\s*=") {
        Fail "$($entry.Name) must set its default 0..1 range in the constructor so scene-authored values can override it."
    }
}
foreach ($entry in @(
    @{ Name = "KitSlider"; Source = $kitSlider },
    @{ Name = "KitKnob"; Source = $kitKnob }
)) {
    if ($entry.Source -notmatch 'NormalizedValue\(\)' -or $entry.Source -notmatch 'Value - MinValue') {
        Fail "$($entry.Name) custom drawing must normalize Value against MinValue/MaxValue instead of assuming 0..1."
    }
}
if ($kitKnob -notmatch 'if \(!Editable\)[\s\S]*_drag = false[\s\S]*return;' -or $kitKnob -notmatch 'KitState state = Editable \? KitState\.Normal : KitState\.Disabled' -or $kitKnob -notmatch 'StateFace\(face,\s*state\)' -or $kitKnob -notmatch 'StateFace\(acc,\s*state\)') {
    Fail "KitKnob owns custom keyboard/drag input and drawing, so Editable=false must block input and render the disabled state."
}
if ($kitKnob -notmatch 'private void SuppressNativeChrome\(\)[\s\S]*SetEmptyStyleboxOverride\(this,\s*sb\)[\s\S]*SetBlankIconOverride\(this,\s*ic\)' -or $kitKnob -notmatch 'public override void _Ready\(\)[\s\S]*SuppressNativeChrome\(\)' -or $kitKnob -notmatch 'NotificationThemeChanged[\s\S]*SuppressNativeChrome\(\)[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)') {
    Fail "KitKnob derives from HSlider and must suppress native slider chrome both in _Ready and after theme changes."
}
$kitStarRating = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs"
if ($kitStarRating -notmatch 'if \(!Editable\)[\s\S]*ClearHover\(\)[\s\S]*return;' -or $kitStarRating -notmatch 'KitState state = Editable \? KitState\.Normal : KitState\.Disabled' -or $kitStarRating -notmatch 'StateFace\(UiSurface\.SemanticOrDerived\(this,\s*Role\),\s*state\)' -or $kitStarRating -notmatch 'if \(Editable\)[\s\S]*DrawFocusRing') {
    Fail "KitStarRating owns custom Range input and drawing, so Editable=false must block changes, clear hover, and render as read-only."
}
$kitRadarChart = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs"
if ($kitRadarChart -notmatch 'if \(!_editable\) _activeAxis = -1' -or $kitRadarChart -notmatch 'if \(!Editable\)[\s\S]*_activeAxis = -1[\s\S]*return;' -or $kitRadarChart -notmatch 'KitState state = Editable \? KitState\.Normal : KitState\.Disabled' -or $kitRadarChart -notmatch 'if \(Editable\)[\s\S]*DrawFocusRing') {
    Fail "KitRadarChart owns custom editable input and drawing, so Editable=false must clear active drag state and render as read-only."
}
$kitSlider = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs"
if ($kitSlider -notmatch 'KitState state = Editable \? KitState\.Normal : KitState\.Disabled' -or $kitSlider -notmatch 'bool dragging = Editable && _dragging' -or $kitSlider -notmatch 'StateFace\(UiSurface\.SemanticOrDerived\(this,\s*Fill\),\s*state\)' -or $kitSlider -notmatch 'StateFace\(UiSurface\.Ink\(UiSurface\.Of\(this\)\),\s*state\)') {
    Fail "KitSlider relies on native Editable input but custom drawing must still render disabled/read-only state."
}
if ($kitSlider -notmatch $kitFocusDefaultPattern -or $kitSlider -notmatch 'DrawFocusRing') {
    Fail "KitSlider blanks native slider focus chrome, so it must opt into keyboard focus and draw a kit focus ring."
}
$kitRadialMeter = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRadialMeter.cs"
foreach ($required in @('public UiSurface\.Role Fill[\s\S]*QueueRedraw\(\)', 'public float GapDegrees[\s\S]*Mathf\.Clamp[\s\S]*QueueRedraw\(\)', 'public float Thickness[\s\S]*Mathf\.Clamp[\s\S]*QueueRedraw\(\)')) {
    if ($kitRadialMeter -notmatch $required) { Fail "KitRadialMeter exported ring appearance changes must clamp and redraw: $required." }
}
if ($kitRadialMeter -notmatch 'string\s+text\s*=\s*KitCase\(_centre\)' -or $kitRadialMeter -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*inner\)') {
    Fail "KitRadialMeter centre text must be cased and ellipsized inside the ring."
}
$kitSliderBar = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSliderBar.cs"
if ($kitSliderBar -notmatch 'public UiSurface\.Role Accent[\s\S]*QueueRedraw\(\)') {
    Fail "KitSliderBar.Accent must redraw immediately for design-time edits."
}
if ($kitSliderBar -notmatch 'KitState state = Editable \? KitState\.Normal : KitState\.Disabled' -or $kitSliderBar -notmatch 'bool dragging = Editable && _dragging' -or $kitSliderBar -notmatch 'Color ink = KitChrome\.StateFace\(UiSurface\.Ink\(surface\),\s*state\)' -or $kitSliderBar -notmatch 'ink with \{ A = Editable \? 0\.50f : 0\.30f \}') {
    Fail "KitSliderBar relies on native Editable input but custom drawing must not keep drag/high-contrast affordances when read-only."
}
if ($kitSliderBar -notmatch $kitFocusDefaultPattern -or $kitSliderBar -notmatch 'DrawFocusRing') {
    Fail "KitSliderBar blanks native slider focus chrome, so it must opt into keyboard focus and draw a kit focus ring."
}
$nativeThemeMinimumFiles = @(
    "KitCheckBox.cs",
    "KitCheckButton.cs",
    "KitOptionButton.cs",
    "KitSlider.cs",
    "KitSliderBar.cs",
    "KitTabStrip.cs"
)
foreach ($fileName in $nativeThemeMinimumFiles) {
    $source = Read "addons/beep_game_builder_cs/ecs/ui/kit/$fileName"
    if ($source -notmatch 'override\s+Vector2\s+_GetMinimumSize\s*\(' -or $source -notmatch 'SetAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)') {
        Fail "$fileName blanks native theme art and must publish a kit-owned _GetMinimumSize in _Ready()."
    }
    if ($source -notmatch 'override void _Notification\(int what\)[\s\S]*NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)') {
        Fail "$fileName blanks native theme art and computes minimum size from the active kit font; theme changes must refresh container layout, not only redraw."
    }
}
$kitColorRect = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitColorRect.cs"
if ($kitColorRect -notmatch 'public bool AutoFallback' -or
    $kitColorRect -notmatch 'if \(!AutoFallback\) return' -or
    $kitColorRect -notmatch 'if \(_appliedFallback && SameColor\(Color,\s*c\)\) return') {
    Fail "KitColorRect must expose an AutoFallback opt-out and skip no-op fallback colour writes."
}
$startupSafeSemanticFiles = @(
    "KitButton.cs",
    "KitBuildTile.cs",
    "KitCheckBox.cs",
    "KitCheckButton.cs",
    "KitColorRect.cs",
    "KitGodotTree.cs",
    "KitIconButton.cs",
    "KitItemList.cs",
    "KitKnob.cs",
    "KitLabel.cs",
    "KitMeter.cs",
    "KitModalShade.cs",
    "KitOptionButton.cs",
    "KitPanel.cs",
    "KitPushButton.cs",
    "KitRemovableChip.cs",
    "KitSlider.cs",
    "KitSliderBar.cs",
    "KitStarRating.cs",
    "KitSwitchVisual.cs",
    "KitTabPanel.cs",
    "KitTabStrip.cs",
    "KitToggle.cs"
)
foreach ($fileName in $startupSafeSemanticFiles) {
    $source = Read "addons/beep_game_builder_cs/ecs/ui/kit/$fileName"
    if ($source -notmatch 'UiSurface\.SemanticOrDerived\(this') {
        Fail "$fileName is a native-wrapper kit control that draws semantic colours and must use UiSurface.SemanticOrDerived during startup/theme notifications."
    }
    if ($source -match 'UiSurface\.Semantic\(this') {
        Fail "$fileName must not call strict UiSurface.Semantic from native-wrapper drawing; early Godot theme notifications can run before ThemePresetComponent assigns BeepSemantic colours."
    }
}
$kitToggle = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitToggle.cs"
if ($kitToggle -notmatch 'Style[\s\S]*if \(_style == value\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitToggle -notmatch 'OnRole[\s\S]*if \(_onRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitToggle -notmatch 'Toggled \+= _ => RefreshVisualAndRedraw\(\)' -or $kitToggle -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitToggle -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitToggle -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitToggle authored style must relayout once, while OnRole and theme changes use guarded visual redraw."
}
$kitModalShade = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitModalShade.cs"
if ($kitModalShade -notmatch 'override void _Notification\(int what\)[\s\S]*NotificationThemeChanged[\s\S]*QueueRedraw\(\)' -or $kitModalShade -notmatch 'UiSurface\.SemanticOrDerived\(this,\s*UiSurface\.Role\.Accent\)') {
    Fail "KitModalShade draws theme accent lines and must redraw when the active kit theme changes."
}
if ($kitModalShade -notmatch 'OverlayColor[\s\S]*if \(_overlayColor == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitModalShade -notmatch 'NotificationThemeChanged[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitModalShade -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitModalShade authored overlay and theme changes must use guarded visual-only redraw."
}
if ($kitModalShade -notmatch $kitFocusDefaultPattern -or $kitModalShade -notmatch 'NotificationVisibilityChanged[\s\S]*Visible[\s\S]*GrabFocus\(\)' -or $kitModalShade -notmatch 'InputEventKey[\s\S]*IsCancelKey[\s\S]*ShadePressed' -or $kitModalShade -notmatch 'InputEventMouseButton \{ Pressed: true, ButtonIndex: MouseButton\.Left \}[\s\S]*GrabFocus\(\)[\s\S]*ShadePressed') {
    Fail "KitModalShade must capture keyboard focus and dismiss on Escape as well as left-click backdrop presses."
}
$kitSwitchVisual = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSwitchVisual.cs"
if ($kitSwitchVisual -notmatch 'IsOn[\s\S]*if \(_isOn == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitSwitchVisual -notmatch 'OnRole[\s\S]*if \(_onRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitSwitchVisual -notmatch 'NotificationThemeChanged[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitSwitchVisual -notmatch 'override Vector2 _GetMinimumSize\(\)' -or $kitSwitchVisual -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitSwitchVisual -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitSwitchVisual authored on state, role, theme, and direct scene use must use guarded redraw and expose a natural minimum size."
}
$themePreset = Read "addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.cs"
$themePresetNodeTheming = Read "addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.NodeTheming.cs"
$passiveThemeExports = [regex]::Matches($themePreset, '\[Export(?:\([^\)]*\))?\]\s+public\s+(bool|float|AudioStream\?|UISkin\?)\s+\w+\s*\{\s*get;\s*set;')
if ($passiveThemeExports.Count -gt 0) {
    Fail "ThemePresetComponent exported runtime/theme options must use real setters that call RequestThemeReapply instead of passive auto-properties."
}
if ($themePreset -notmatch 'RequestThemeReapply' -or $themePreset -notmatch '_suspendAutoApply[\s\S]*ApplyTheme\(\)') {
    Fail "ThemePresetComponent must centralize exported option reapply behavior through RequestThemeReapply."
}
if ($themePreset -match 'AddTheme(Constant|Stylebox|Color|FontSize|Icon|Font)Override\s*\(' -or
    $themePreset -match 'RemoveTheme(Constant|Stylebox|Color|FontSize|Icon|Font)Override\s*\(' -or
    $themePreset -notmatch 'SetStyleboxOverrideIfChanged\(btn,\s*state' -or
    $themePreset -notmatch 'SetStyleboxOverrideIfChanged\(btn,\s*"normal"' -or
    $themePreset -notmatch 'SetColorOverrideIfChanged\(btn,\s*"font_color"' -or
    $themePreset -notmatch 'SetFontSizeOverrideIfChanged\(btn,\s*"font_size"') {
    Fail "ThemePresetComponent must use KitChrome change-aware override helpers for per-control theme writes."
}
if ($themePreset -notmatch '_lastThemeBuildKey' -or $themePreset -notmatch 'BuildThemeKey' -or $themePreset -notmatch '_targetControl\.Theme == _generatedTheme') {
    Fail "ThemePresetComponent must skip no-op theme builds instead of assigning a fresh Theme resource for the same resolved skin."
}
if ($themePreset -notmatch 'public const string DefaultGenre = "citybuilder"' -or
    $themePreset -notmatch 'private string _presetName = "oilfield_days"') {
    Fail "ThemePresetComponent must default new UI kit scenes to the Oilfield Days citybuilder skin, not the old platformer/modern register."
}
foreach ($required in @(
    'EnableAnimations\.ToString\(\)',
    'EnableRippleOnClick\.ToString\(\)',
    'EnableButtonSounds\.ToString\(\)',
    'ButtonSoundVolumeDb\.ToString',
    'HoverSound\?\.GetInstanceId\(\)',
    'PressSound\?\.GetInstanceId\(\)'
)) {
    if ($themePreset -notmatch $required) {
        Fail "ThemePresetComponent.BuildThemeKey must include button chrome option '$required' so no-op theme skips do not hide real interaction changes."
    }
}
if ($themePreset -notmatch 'ResetInjectedButtons\(root\)' -or $themePreset -notmatch 'ResetInjectedButtons\(btn\)' -or $themePreset -notmatch 'RippleOwnedMeta' -or $themePreset -notmatch 'RippleNodeName') {
    Fail "ThemePresetComponent must reset owned button animation/ripple injection before real theme rebuilds so buttons do not keep stale handlers or ripple colors."
}
if ($themePreset -notmatch '_themeFont\s*=\s*Kit\.KitFonts\.Fallback' -or $themePreset -notmatch 'SetFont\("font",\s*type,\s*_themeFont\)' -or $themePreset -notmatch 'SetFont\("font",\s*role,\s*_themeFont\)') {
    Fail "ThemePresetComponent must publish a non-null kit font through the generated Theme so KitLabel does not create unnecessary per-label font overrides."
}
if ($themePreset -notmatch 'if \(node is Kit\.KitLabel\)[\s\r\n]*return;' -or
    $themePreset -notmatch 'Kit\.KitChrome\.SetFontSizeOverrideIfChanged\(label,\s*"font_size",\s*SizeFor\(role\)\)' -or
    $themePreset -notmatch 'Kit\.KitChrome\.SetColorOverrideIfChanged\(label,\s*"font_color",\s*ColorFor\(role,\s*c\)\)') {
    Fail "ThemePresetComponent compatibility typography must skip KitLabel and use change-only writes for ordinary Label nodes."
}
if ($themePreset -notmatch 'SetMeta\(Kit\.KitChrome\.GenreMeta,\s*gGenre\)') {
    Fail "ThemePresetComponent must stamp the applied kit genre onto the themed root so drawn kit controls resolve the same genre as the generated Theme."
}
$kitIconButton = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitIconButton.cs"
if ($kitIconButton -match 'Disabled\s*=\s*value\s*\|\|\s*Disabled' -or $kitIconButton -notmatch '_lockedAppliedDisabled') {
    Fail "KitIconButton.Locked must not leave Disabled stuck true after unlocking."
}
if ($kitIconButton -notmatch $kitFocusDefaultPattern -or $kitIconButton -notmatch 'DrawFocusRing') {
    Fail "KitIconButton must draw a visible kit focus ring after suppressing native button focus chrome."
}
if ($kitIconButton -notmatch 'string\s+glyph\s*=\s*KitChrome\.Case\(_glyph,\s*_genre\)' -or $kitIconButton -notmatch 'EllipsizeText\(font,\s*glyph,\s*size,\s*gs\)' -or $kitIconButton -notmatch 'string\s+req\s*=\s*KitChrome\.Case\(_req,\s*_genre\)' -or $kitIconButton -notmatch 'EllipsizeText\(font,\s*req,\s*small,\s*reqWidth\)') {
    Fail "KitIconButton must case and ellipsize fallback glyph and locked requirement text inside the square button."
}
foreach ($property in @("Glyph", "Requirement")) {
    $pattern = 'public\s+[^{}]*\s+' + [regex]::Escape($property) + '\s*\{.*?SetText\(ref\s+_.*?\}'
    if (-not [regex]::IsMatch($kitIconButton, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Fail "KitIconButton $property setter must use the shared text refresh path so inspector edits notify Godot layout."
    }
}
if ($kitIconButton -notmatch 'ButtonIcon\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitIconButton -notmatch 'Locked[\s\S]*RefreshContentAndRedraw\(\)' -or $kitIconButton -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitIconButton -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitIconButton texture swaps must redraw without relayout, while lock and text edits refresh its auto minimum size before redraw."
}
$nativeChromeFocusControls = @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitBuildTile.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitCheckBox.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitCheckButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitIconButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitOptionButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRemovableChip.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitToggle.cs"
)
foreach ($relativePath in $nativeChromeFocusControls) {
    $source = Read $relativePath
    if ($source -notmatch '"focus"' -or $source -notmatch $kitFocusDefaultPattern -or $source -notmatch 'DrawFocusRing') {
        Fail "$relativePath suppresses native focus chrome and must explicitly draw the kit focus ring."
    }
    if ($source -notmatch 'HookButtonChromeRedraw\(this,\s*RefreshVisualAndRedraw,\s*ref _eventsHooked\)') {
        Fail "$relativePath custom-draws native button state and must hook hover/press/focus redraws through KitChrome."
    }
}
if ($kitChrome -notmatch 'SetAutoMinimumSize' -or $kitChrome -notmatch '_beep_kit_auto_minimum') {
    Fail "KitChrome must expose an owned auto-minimum-size helper for drop-in controls that suppress native chrome."
}
foreach ($required in @("HookButtonChromeRedraw", "MouseEntered += redraw", "MouseExited += redraw", "FocusEntered += redraw", "FocusExited += redraw", "ButtonDown += redraw", "ButtonUp += redraw", "Pressed += redraw")) {
    if ($kitChrome -notmatch [regex]::Escape($required)) {
        Fail "KitChrome.HookButtonChromeRedraw must subscribe custom-drawn native buttons to $required."
    }
}
if ($kitChrome -notmatch 'public const string GenreMeta' -or $kitChrome -notmatch 'HasMeta\(GenreMeta\)' -or $kitChrome -notmatch 'GetMeta\(GenreMeta' -or $kitChrome -notmatch 'SkinCatalog\.HasActiveSkin \? SkinCatalog\.ActiveGenre : ""') {
    Fail "KitChrome.GenreOf must resolve the nearest themed root metadata before falling back to SkinCatalog.ActiveGenre."
}
if ($kitChrome -notmatch 'RefreshAutoMinimumSize' -or $kitChrome -notmatch 'ctl\.UpdateMinimumSize\(\)') {
    Fail "KitChrome must expose RefreshAutoMinimumSize so footprint-changing exported setters relayout without taking over scene-authored sizes."
}
if ($kitChrome -notmatch 'public static bool SetStyleboxOverrideIfChanged' -or $kitChrome -notmatch 'public static bool SetColorOverrideIfChanged' -or $kitChrome -notmatch 'public static bool SetFontSizeOverrideIfChanged' -or $kitChrome -notmatch 'public static bool SetConstantOverrideIfChanged' -or $kitChrome -notmatch 'return false;[\s\S]*AddThemeStyleboxOverride[\s\S]*return true;' -or $kitChrome -notmatch 'AddThemeColorOverride[\s\S]*return true;' -or $kitChrome -notmatch 'AddThemeFontSizeOverride[\s\S]*return true;' -or $kitChrome -notmatch 'AddThemeConstantOverride[\s\S]*return true;') {
    Fail "KitChrome theme override helpers must report whether they changed anything so native wrappers can avoid no-op layout/redraw churn."
}
if ($kitChrome -notmatch 'HasThemeColorOverride\(name\) && SameColor\(ctl\.GetThemeColor\(name\),\s*value\)' -or
    $kitChrome -notmatch 'HasThemeFontSizeOverride\(name\) && ctl\.GetThemeFontSize\(name\) == value' -or
    $kitChrome -notmatch 'HasThemeConstantOverride\(name\) && ctl\.GetThemeConstant\(name\) == value' -or
    $kitChrome -notmatch 'HasThemeFontOverride\(name\) && ctl\.GetThemeFont\(name\) == value') {
    Fail "KitChrome theme override helpers must create a local override even when the inherited theme already matches the requested value."
}
if ($kitChrome -notmatch 'public\s+static\s+bool\s+SetAutoMinimumSize' -or $kitChrome -notmatch 'SameVector' -or $kitChrome -notmatch 'if\s*\(\s*SetAutoMinimumSize\(ctl,\s*wanted\)\s*\)\s*\r?\n\s*ctl\.UpdateMinimumSize\(\)') {
    Fail "KitChrome auto-minimum helpers must be change-aware so theme notifications do not force redundant layout invalidation."
}
if ($kitChrome -notmatch 'public static Color WellFace\(Color surface\)' -or
    $kitChrome -notmatch 'if \(lum < 0\.20f\)' -or
    $kitChrome -notmatch 'Mathf\.Lerp\(surface\.R,\s*1f,\s*t\)') {
    Fail "KitChrome must expose a readable recessed well face for checkbox and switch off-states on dark skins."
}
if ($uiSurface -notmatch 'public static Color ControlFace\(Color surface\)' -or
    $uiSurface -notmatch 'if \(lum >= 0\.145f\) return surface;' -or
    $uiSurface -notmatch 'Mathf\.Lerp\(surface\.R,\s*1f,\s*t\)' -or
    $kitChrome -notmatch 'if \(st != KitState\.Disabled\)\s*\r?\n\s*s = UiSurface\.ControlFace\(s\);' -or
    $kitControl -notmatch 'Color s = UiSurface\.ControlFace\(UiSurface\.Of\(this\)\);') {
    Fail "Neutral kit control faces must be lifted through UiSurface.ControlFace before state shading so dark themes stay readable."
}
if ($kitChrome -notmatch 'EllipsizeText') {
    Fail "KitChrome must expose EllipsizeText for single-line kit text that can exceed its draw box."
}
if ($kitChrome -notmatch 'EllipsizeText\(font,\s*label,\s*fit,\s*r\.Size\.X - fit \* 0\.95f\)' -or $kitChrome -notmatch 'EllipsizeText\(font,\s*text,\s*fit,\s*r\.Size\.X - padX \* 2f\)' -or $kitChrome -notmatch 'string\s+line\s*=\s*EllipsizeText\(font,\s*lines\[i\],\s*fs,\s*box\.Size\.X\)') {
    Fail "KitChrome shared header and label helpers must ellipsize text inside their final draw rectangles."
}
if ($kitChrome -notmatch 'private static void ClipInto[\s\S]*Geometry2D\.IntersectPolygons\(host,\s*band\)[\s\S]*Geometry2D\.TriangulatePolygon\(piece\)[\s\S]*DrawColoredPolygon\(piece,\s*c\)' -or
    $kitControl -notmatch 'protected void ClipFill[\s\S]*Geometry2D\.IntersectPolygons\(host,\s*band\)[\s\S]*Geometry2D\.TriangulatePolygon\(piece\)[\s\S]*DrawColoredPolygon\(piece,\s*c\)') {
    Fail "Kit chrome material bands must be clipped to the host silhouette before drawing."
}
$ratchetingMinimumFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match 'CustomMinimumSize\s*=\s*new\s+Vector2\s*\(\s*Mathf\.Max\s*\(\s*CustomMinimumSize\.') {
        $ratchetingMinimumFiles += $file.Name
    }
}
if ($ratchetingMinimumFiles.Count -gt 0) {
    Fail "Kit controls must use KitChrome.SetAutoMinimumSize instead of ratcheting CustomMinimumSize upward: $($ratchetingMinimumFiles -join ', ')."
}
$allowedDirectMinimumFiles = @(
    "KitArchetypes.cs",   # factory-created ornaments declare their initial size with the node
    "KitBuildTile.cs",    # FixedSize is an explicit exported contract for build grids
    "KitChrome.cs",       # the central owner-tracked helper writes CustomMinimumSize
    "KitContextMenu.cs"   # popup menus resize to the live item list
)
$directMinimumFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    if ($allowedDirectMinimumFiles -contains $file.Name) { continue }
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match 'CustomMinimumSize\s*=') {
        $directMinimumFiles += $file.Name
    }
}
if ($directMinimumFiles.Count -gt 0) {
    Fail "Kit controls must use KitChrome.SetAutoMinimumSize for kit-owned defaults, preserving scene-authored CustomMinimumSize: $($directMinimumFiles -join ', ')."
}
$autoMinimumRefreshRequirements = @{
    "KitChip.cs" = "Kind"
    "KitHeartRow.cs" = "MaxHearts"
    "KitInputHint.cs" = "Keys"
    "KitItemCard.cs" = "Layout"
    "KitLevelPath.cs" = "PerRow"
    "KitPager.cs" = "ShowJump"
    "KitPanel.cs" = "HeaderStyle"
    "KitSlotGrid.cs" = "Columns"
    "KitSpinner.cs" = "Kind"
    "KitStarRating.cs" = "Total"
    "KitToggle.cs" = "Style"
    "KitTree.cs" = "Columns"
}
foreach ($entry in $autoMinimumRefreshRequirements.GetEnumerator()) {
    $source = Read "addons/beep_game_builder_cs/ecs/ui/kit/$($entry.Key)"
    if ($source -notmatch 'RefreshAutoMinimumSize') {
        Fail "$($entry.Key) must refresh kit-owned minimum size when '$($entry.Value)' changes."
    }
}
$kitInputHint = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitInputHint.cs"
if ($kitInputHint -notmatch 'EllipsizeText\(font,\s*k' -or $kitInputHint -notmatch 'string\s+action\s*=\s*KitCase\(_action\)' -or $kitInputHint -notmatch 'FitRole\(this,\s*UiSurface\.TextRole\.Caption,[\s\S]*action,\s*font' -or $kitInputHint -notmatch 'EllipsizeText\(font,\s*action,\s*afs,\s*remaining\)') {
    Fail "KitInputHint must ellipsize key/action text so long chords and actions stay inside the control."
}
if ($kitInputHint -notmatch 'TextWidth' -or $kitInputHint -notmatch 'RefreshMinimumAndRedraw' -or $kitInputHint -notmatch 'Keys[\s\S]*NormalizeKeys\(value\)[\s\S]*SameKeys\(_keys,\s*next\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitInputHint -notmatch 'Action[\s\S]*if \(_action == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitInputHint -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitInputHint -notmatch 'SameKeys' -or $kitInputHint -notmatch 'NormalizeKeys[\s\S]*next\[i\] = keys\[i\] \?\? ""') {
    Fail "KitInputHint must derive its minimum width from normalized key chords and action text, not a fixed constant."
}
if ($kitInputHint -notmatch 'TextWidth\(font,\s*KitCase\(_action\),\s*UiSurface\.FontSize\(this,\s*UiSurface\.TextRole\.Caption\)\)') {
    Fail "KitInputHint natural width must measure the cased action text it draws."
}
if ($kitInputHint -notmatch 'SetKeys' -or $kitInputHint -notmatch 'AddKey' -or $kitInputHint -notmatch 'ClearKeys' -or $kitInputHint -notmatch 'WithAdded') {
    Fail "KitInputHint must expose key collection helpers so runtime chord changes refresh minimum size and redraw."
}
$directDrawStringFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    if ($file.Name -eq "KitChrome.cs") { continue }
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match 'DrawString\s*\(') {
        $directDrawStringFiles += $file.Name
    }
}
if ($directDrawStringFiles.Count -gt 0) {
    Fail "Kit controls must draw text through KitControl.DrawText or KitChrome.DrawText so text treatment stays consistent: $($directDrawStringFiles -join ', ')."
}
$kitCheckBox = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCheckBox.cs"
if ($kitCheckBox -notmatch 'KitChrome\.DrawText\(this,\s*_genre' -or $kitCheckBox -notmatch 'EllipsizeText\(font,\s*caption') {
    Fail "KitCheckBox captions must use centralized text treatment and ellipsis."
}
if ($kitCheckBox -notmatch 'KitChrome\.WellFace\(surface\)' -or $kitCheckBox -match 'surface\.R \* 0\.42f') {
    Fail "KitCheckBox off-state must use KitChrome.WellFace instead of hard-coded dark surface multiplication."
}
if ($kitCheckBox -notmatch 'OnRole[\s\S]*if \(_onRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitCheckBox -notmatch 'Toggled \+= _ => RefreshVisualAndRedraw\(\)' -or $kitCheckBox -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitCheckBox -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitCheckBox authored on role and theme changes must use guarded visual redraw after native chrome suppression."
}
$kitCheckButton = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCheckButton.cs"
if ($kitCheckButton -notmatch 'DrawCaption\(font,\s*track,\s*state\)' -or $kitCheckButton -notmatch 'SetColorOverrideIfChanged\(this,\s*c,\s*new Color\(0,\s*0,\s*0,\s*0\)\)' -or $kitCheckButton -notmatch 'EllipsizeText\(font,\s*caption,\s*fs,\s*available\)' -or $kitCheckButton -notmatch 'KitChrome\.DrawText\(this,\s*_genre') {
    Fail "KitCheckButton captions must suppress native text and use centralized kit text treatment with ellipsis."
}
if ($kitCheckButton -notmatch 'KitChrome\.WellFace\(surface\)' -or $kitCheckButton -match 'surface\.R \* 0\.42f') {
    Fail "KitCheckButton off-state must use KitChrome.WellFace instead of hard-coded dark surface multiplication."
}
if ($kitCheckButton -notmatch 'OnRole[\s\S]*if \(_onRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitCheckButton -notmatch 'Toggled \+= _ => RefreshVisualAndRedraw\(\)' -or $kitCheckButton -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitCheckButton -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitCheckButton authored on role and theme changes must use guarded visual redraw after native chrome suppression."
}
$checkControlsProbe = Read "tests/kit_check_controls_contrast_probe.gd"
$runAddonChecks = Read "tests/run_addon_checks.ps1"
if ($checkControlsProbe -notmatch 'KIT_CHECK_BOX_SCRIPT' -or
    $checkControlsProbe -notmatch 'KIT_CHECK_BUTTON_SCRIPT' -or
    $checkControlsProbe -notmatch '_checkbox_box_luminance' -or
    $checkControlsProbe -notmatch '_switch_track_luminance' -or
    $checkControlsProbe -notmatch 'luminance=') {
    Fail "kit_check_controls_contrast_probe.gd must render and sample both unchecked checkbox and switch controls."
}
if ($runAddonChecks -notmatch 'kit_check_controls_contrast_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit check-controls contrast probe."
}
$kitOptionButton = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitOptionButton.cs"
if ($kitOptionButton -match 'KitChrome\.DrawLabel\(this,\s*this,\s*Text,\s*textBox' -or $kitOptionButton -notmatch 'DrawSelectedText' -or $kitOptionButton -notmatch 'EllipsizeText\(font,\s*label' -or $kitOptionButton -notmatch 'KitChrome\.DrawText\(this,\s*_genre') {
    Fail "KitOptionButton selected text must be fitted, ellipsized, and drawn through the centralized kit text treatment."
}
if ($kitOptionButton -notmatch 'ItemSelected \+= _ => RefreshMinimumAndRedraw\(\)' -or $kitOptionButton -notmatch 'RefreshMinimumAndRedraw' -or $kitOptionButton -notmatch 'DrawFocusRing') {
    Fail "KitOptionButton must refresh its custom minimum size when selection text changes and draw a visible kit focus ring."
}
$kitArrowSelector = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs"
if ($kitArrowSelector -notmatch 'string\s+txt\s*=\s*KitCase\(Options\[Mathf\.Clamp' -or $kitArrowSelector -notmatch 'FitRole\(this,\s*UiSurface\.TextRole\.Value,[\s\S]*txt,\s*font' -or $kitArrowSelector -notmatch 'EllipsizeText\(font,\s*txt,\s*tf,\s*textWidth\)' -or $kitArrowSelector -notmatch 'textWidth' -or $kitArrowSelector -notmatch 'DrawText\(font') {
    Fail "KitArrowSelector selected text must be cased before fitting and ellipsized between the arrow buttons."
}
if ($kitArrowSelector -notmatch 'public bool RemoveOption\(int index\)' -or
    $kitArrowSelector -notmatch 'Options\.RemoveAt\(index\)' -or
    $kitArrowSelector -notmatch 'if \(index <= _current\)\s*\r?\n\s*_current = Mathf\.Max\(0,\s*_current - 1\)' -or
    $kitArrowSelector -notmatch 'public void ClearOptions\(\)' -or
    $kitArrowSelector -notmatch 'Options\.Clear\(\)[\s\S]*RefreshOptions\(\)') {
    Fail "KitArrowSelector must expose refresh-safe remove and clear helpers for runtime option lists."
}
$kitTabStrip = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs"
if ($kitTabStrip -notmatch 'EllipsizeText\(font,\s*text,\s*tf,\s*r\.Size\.X \* 0\.86f\)') {
    Fail "KitTabStrip tab titles must be ellipsized inside their divided tab bounds."
}
if ($kitTabStrip -notmatch 'EllipsizeText\(font,\s*b,\s*small,\s*r\.Size\.X \* 0\.45f\)') {
    Fail "KitTabStrip corner badges must be ellipsized inside each tab's badge bound."
}
if ($kitTabStrip -notmatch 'FindEnabledTab' -or $kitTabStrip -notmatch 'SelectKeyboardTab' -or $kitTabStrip -notmatch 'IsTabDisabled\(index\)' -or $kitTabStrip -notmatch 'IsTabDisabled\(i\)' -or $kitTabStrip -notmatch 'disabled[\s\S]*A = 0\.38f') {
    Fail "KitTabStrip keyboard navigation and drawing must skip and visually mute disabled tabs."
}
if ($kitTabStrip -notmatch 'Selection[\s\S]*if \(_selection == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitTabStrip -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)' -or $kitTabStrip -notmatch 'RebuildTabsFromList[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitTabStrip -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)') {
    Fail "KitTabStrip authored selection style must use guarded visual redraw, while tab/theme rebuilds update minimum size."
}
$kitSegmentedIconGroup = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs"
if ($kitSegmentedIconGroup -notmatch 'string\s+glyph\s*=\s*KitCase\(Segments\[i\]\.Glyph\)' -or $kitSegmentedIconGroup -notmatch 'EllipsizeText\(font,\s*glyph,\s*gf,\s*textWidth\)') {
    Fail "KitSegmentedIconGroup text segments must be cased and ellipsized inside each segment."
}
$missingReadyBaseFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    $source = Get-Content -Path $file.FullName -Raw
    $matches = [regex]::Matches($source, 'public\s+override\s+void\s+_Ready\s*\(\)\s*(?:=>\s*(?<expr>[^;]+);|\{(?<body>[\s\S]*?)\n\s*\})')
    foreach ($match in $matches) {
        $readyBody = if ($match.Groups["expr"].Success) { $match.Groups["expr"].Value } else { $match.Groups["body"].Value }
        if ($readyBody -notmatch 'base\._Ready\s*\(') {
            $missingReadyBaseFiles += $file.Name
        }
    }
}
if ($missingReadyBaseFiles.Count -gt 0) {
    $missingReadyBaseList = ($missingReadyBaseFiles | Sort-Object -Unique) -join ', '
    Fail "Kit _Ready overrides must call base._Ready() before custom setup: $missingReadyBaseList."
}
$missingNotificationBaseFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    $source = Get-Content -Path $file.FullName -Raw
    $matches = [regex]::Matches($source, 'public\s+override\s+void\s+_Notification\s*\([^)]*\)\s*\{(?<body>[\s\S]*?)\n\s*\}')
    foreach ($match in $matches) {
        if ($match.Groups["body"].Value -notmatch 'base\._Notification\s*\(') {
            $missingNotificationBaseFiles += $file.Name
        }
    }
}
if ($missingNotificationBaseFiles.Count -gt 0) {
    $missingNotificationBaseList = ($missingNotificationBaseFiles | Sort-Object -Unique) -join ', '
    Fail "Kit _Notification overrides must call base._Notification() before custom handling: $missingNotificationBaseList."
}
$kitControl = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitControl.cs"
if ($kitControl -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitControl -notmatch 'OverrideShape[\s\S]*if \(_overrideShape == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitControl -notmatch 'Shape[\s\S]*if \(_shape == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitControl -notmatch 'Elevation[\s\S]*if \(_elevation == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitControl -notmatch 'CornerOverride[\s\S]*Mathf\.IsEqualApprox\(_cornerOverride,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitControl -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitControl must refresh kit-owned minimum size on theme changes so font/genre changes relayout design-time controls."
}
$kitBuildTile = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitBuildTile.cs"
if ($kitBuildTile -notmatch 'FixedSize == Vector2\.Zero[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)') {
    Fail "KitBuildTile must refresh auto minimum size on theme changes when FixedSize is not explicitly authored."
}
$kitLabelValue = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLabelValue.cs"
if ($kitLabelValue -notmatch 'string\s+label\s*=\s*KitCase\(_label\)' -or $kitLabelValue -notmatch 'string\s+value\s*=\s*KitCase\(_value\)') {
    Fail "KitLabelValue must case its label/value before fitting and drawing."
}
if ($kitLabelValue -notmatch 'DrawTextIn[\s\S]*EllipsizeText\(font,\s*text,\s*fs,\s*maxWidth\)[\s\S]*DrawText\(font') {
    Fail "KitLabelValue must ellipsize bounded label/value text after fitting and before drawing."
}
if ($kitLabelValue -notmatch 'Label[\s\S]*if \(_label == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitLabelValue -notmatch 'Value[\s\S]*if \(_value == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitLabelValue -notmatch 'Accent[\s\S]*if \(_accent == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitLabelValue -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitLabelValue -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitLabelValue authored label/value edits must refresh layout once, while accent uses guarded visual redraw."
}
$kitRow = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs"
foreach ($required in @("KitCase(_rank)", "KitCase(_title)", "KitCase(_sub)", "KitCase(_value)", "KitCase(_state)")) {
    if ($kitRow -notmatch [regex]::Escape($required)) { Fail "KitRow must case row text before fitting and drawing: $required." }
}
foreach ($required in @("EllipsizeText(font, rank", "EllipsizeText(font, value", "EllipsizeText(font, stateText", "EllipsizeText(font, title", "EllipsizeText(font, subtitle")) {
    if ($kitRow -notmatch [regex]::Escape($required)) { Fail "KitRow must ellipsize each row text region before drawing: $required." }
}
$kitDialogBox = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs"
if ($kitDialogBox -notmatch '\[Export\]\s*public string\[\]\s+Choices') {
    Fail "KitDialogBox.Choices must be exported so dialog choices can be authored at design time."
}
if ($kitDialogBox -notmatch 'RefreshChoiceLayout' -or $kitDialogBox -notmatch 'RefreshMinimumAndRedraw' -or $kitDialogBox -notmatch 'ChoiceRowHeight' -or $kitDialogBox -notmatch 'TextWidth' -or $kitDialogBox -notmatch 'LongestLineWidth' -or $kitDialogBox -notmatch 'EstimateWrappedLineCount' -or $kitDialogBox -notmatch 'Speaker\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitDialogBox -notmatch 'Body\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitDialogBox -notmatch 'Choices\s*\{[^\r\n]*SetStringArray\(ref\s+_choices' -or $kitDialogBox -notmatch 'ChoicesVisible\s*\{[^\r\n]*RefreshChoiceLayout\(\)' -or $kitDialogBox -notmatch 'UpdateMinimumSize\(\)' -or $kitDialogBox -notmatch 'EllipsizeText\(font,\s*choice') {
    Fail "KitDialogBox must refresh and derive its minimum size from speaker/body/choice text and ellipsize choice text before drawing."
}
if ($kitDialogBox -notmatch 'Speaker[\s\S]*if \(_speaker == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitDialogBox -notmatch 'Body[\s\S]*if \(_body == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitDialogBox -notmatch 'Choices[\s\S]*if \(SetStringArray\(ref _choices, value\)\) RefreshChoiceLayout\(\)' -or $kitDialogBox -notmatch 'NormalizeStrings[\s\S]*next\[i\] = values\[i\] \?\? ""' -or $kitDialogBox -notmatch 'SameStrings[\s\S]*a\.Length != b\.Length' -or $kitDialogBox -notmatch 'VisibleCharacters[\s\S]*if \(_visibleCharacters == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitDialogBox -notmatch 'ContinueVisible[\s\S]*if \(_continueVisible == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitDialogBox -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitDialogBox authored text and choices must avoid duplicate layout refreshes, normalize null entries, while typewriter and continue state use visual-only redraw."
}
$kitBookSpread = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitBookSpread.cs"
if ($kitBookSpread -notmatch 'string\s+text\s*=\s*KitCase\(_tabs\[i\]' -or $kitBookSpread -notmatch 'FitRole\(this,\s*UiSurface\.TextRole\.Small,[\s\S]*text,\s*font' -or $kitBookSpread -notmatch 'EllipsizeText\(font,\s*text,\s*tf,\s*textWidth\)') {
    Fail "KitBookSpread tab labels must be cased before fitting and ellipsized inside each side-tab draw bound."
}
if ($kitBookSpread -notmatch 'string\s+text\s*=\s*KitCase\(t\)' -or $kitBookSpread -notmatch 'EllipsizeText\(font,\s*text,\s*tf,\s*textWidth\)') {
    Fail "KitBookSpread page titles must be cased and ellipsized inside each page title bound."
}
if ($kitBookSpread -notmatch 'string\s+label\s*=\s*\$"\{_selectedTab \+ 1\}/\{pages\}"' -or $kitBookSpread -notmatch 'EllipsizeText\(font,\s*label,\s*fs,\s*labelWidth\)') {
    Fail "KitBookSpread page counter must be ellipsized inside its footer label bound."
}
if ($kitBookSpread -notmatch 'RefreshPageList' -or $kitBookSpread -notmatch 'RefreshMinimumAndRedraw' -or $kitBookSpread -notmatch 'UpdateMinimumSize\(\)' -or $kitBookSpread -notmatch 'TabOutset \* 0\.78f' -or $kitBookSpread -notmatch 'TabHeight' -or $kitBookSpread -notmatch 'LeftTitle\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitBookSpread -notmatch 'RightTitle\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitBookSpread -notmatch 'LeftPageTitles\s*\{[^\r\n]*SetStringArray\(ref\s+_leftPages' -or $kitBookSpread -notmatch 'RightPageTitles\s*\{[^\r\n]*SetStringArray\(ref\s+_rightPages' -or $kitBookSpread -notmatch 'Tabs\s*\{[^\r\n]*SetStringArray\(ref\s+_tabs' -or $kitBookSpread -notmatch 'ShowTabs\s*\{[^\r\n]*RefreshPageList\(\)' -or $kitBookSpread -notmatch 'SelectedTab[\s\S]*int next = Mathf\.Max\(0,\s*value\)[\s\S]*TurnTo\(next\)' -or $kitBookSpread -notmatch '_selectedTab\s*=\s*next' -or $kitBookSpread -notmatch '_turnFrom\s*=\s*Mathf\.Clamp\(_turnFrom' -or $kitBookSpread -notmatch 'UpdateProcessing\(\)') {
    Fail "KitBookSpread page/tab collection setters must normalize selected and animated page indices when the page count changes."
}
if ($kitBookSpread -notmatch 'NotificationVisibilityChanged[\s\S]*UpdateProcessing\(\)' -or $kitBookSpread -notmatch 'private bool ShouldAnimate\(\)[\s\S]*_turnTime < 1f' -or $kitBookSpread -notmatch 'SetProcess\(IsVisibleInTree\(\) && ShouldAnimate\(\)\)' -or $kitBookSpread -match 'SetProcess\(true\)') {
    Fail "KitBookSpread page-turn animation must process only while visible and actively turning."
}
if ($kitBookSpread -notmatch 'LeftTitle[\s\S]*if \(_lt == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitBookSpread -notmatch 'RightTitle[\s\S]*if \(_rt == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitBookSpread -notmatch 'LeftPageTitles[\s\S]*if \(SetStringArray\(ref _leftPages, value\)\) RefreshPageList\(\)' -or $kitBookSpread -notmatch 'RightPageTitles[\s\S]*if \(SetStringArray\(ref _rightPages, value\)\) RefreshPageList\(\)' -or $kitBookSpread -notmatch 'Tabs[\s\S]*if \(SetStringArray\(ref _tabs, value\)\) RefreshPageList\(\)' -or $kitBookSpread -notmatch 'NormalizeStrings[\s\S]*next\[i\] = values\[i\] \?\? ""' -or $kitBookSpread -notmatch 'SameStrings[\s\S]*a\.Length != b\.Length' -or $kitBookSpread -notmatch 'ShowPageCorners[\s\S]*if \(_showPageCorners == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitBookSpread -notmatch 'ShowRibbon[\s\S]*if \(_showRibbon == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitBookSpread -notmatch 'ShowCover[\s\S]*if \(_showCover == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitBookSpread -notmatch 'TurnTo[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitBookSpread -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitBookSpread authored titles/page lists must avoid duplicate layout refreshes and normalize null entries, while page decoration and turns use guarded visual redraw."
}
foreach ($required in @("SetLeftPageTitles", "AddLeftPageTitle", "ClearLeftPageTitles", "SetRightPageTitles", "AddRightPageTitle", "ClearRightPageTitles", "SetTabs", "AddTab", "ClearTabs", "WithAdded")) {
    if ($kitBookSpread -notmatch [regex]::Escape($required)) {
        Fail "KitBookSpread must expose collection helper $required so runtime page/tab changes normalize and redraw."
    }
}
$kitLevelButton = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs"
if ($kitLevelButton -notmatch 'string\s+text\s*=\s*KitCase\(_locked\s*\?\s*"LOCK"\s*:\s*_levelText\)' -or $kitLevelButton -notmatch 'EllipsizeText\(font,\s*text,\s*tf,\s*textWidth\)') {
    Fail "KitLevelButton level text must be cased before fitting and ellipsized inside the inner button badge."
}
if ($kitLevelButton -notmatch 'LevelText\s*\{[^\r\n]*SetText\(ref\s+_levelText' -or $kitLevelButton -notmatch 'Stars\s*\{[^\r\n]*RefreshContentAndRedraw\(\)' -or $kitLevelButton -notmatch 'Locked\s*\{[^\r\n]*RefreshContentAndRedraw\(\)' -or $kitLevelButton -notmatch 'Accent\s*\{[^\r\n]*RefreshContentAndRedraw\(\)' -or $kitLevelButton -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitLevelButton authored level text, stars, lock, and accent edits must refresh its auto minimum size before redraw."
}
$kitGemSlot = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs"
if ($kitGemSlot -notmatch 'string\s+req\s*=\s*KitCase\(_req\)' -or $kitGemSlot -notmatch 'EllipsizeText\(font,\s*req,\s*s,\s*textWidth\)') {
    Fail "KitGemSlot locked requirement text must be cased and ellipsized inside the socket."
}
if ($kitGemSlot -notmatch 'Requirement\s*\{[^\r\n]*SetText\(ref\s+_req' -or $kitGemSlot -notmatch 'State_\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitGemSlot -notmatch 'Gem\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitGemSlot -notmatch 'Role\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitGemSlot -notmatch 'RefreshKitMinimumContract[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)' -or $kitGemSlot -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshKitMinimumContract\(\)[\s\S]*QueueRedraw\(\)' -or $kitGemSlot -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitGemSlot authored requirement edits must refresh layout; state, gem, and role edits must use visual-only redraw."
}
$kitInventorySlot = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs"
if ($kitInventorySlot -notmatch 'string\s+req\s*=\s*KitCase\(_requirement\)' -or $kitInventorySlot -notmatch 'EllipsizeText\(font,\s*req,\s*fs,\s*textWidth\)' -or $kitInventorySlot -match 'm\.X <= Size\.X') {
    Fail "KitInventorySlot locked requirement text must be cased and ellipsized inside the slot instead of overflowing or disappearing."
}
if ($kitNodeCard -notmatch 'text\s*=\s*KitCase\(text\)[\s\S]*FitRole\(this,\s*role,\s*r\.Size,\s*text,\s*font' -or $kitNodeCard -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*r\.Size\.X\)') {
    Fail "KitNodeCard fitted text helper must case before measuring and ellipsize title/requirement text inside card bands."
}
foreach ($property in @("Title", "FooterText", "Requirement")) {
    $pattern = 'public\s+[^{}]*\s+' + [regex]::Escape($property) + '\s*\{.*?SetText\(ref\s+_.*?\}'
    if (-not [regex]::IsMatch($kitNodeCard, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Fail "KitNodeCard $property setter must use the shared text refresh path so inspector edits notify Godot layout."
    }
}
if ($kitNodeCard -notmatch 'Footer\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitNodeCard -notmatch 'Art\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitNodeCard -notmatch 'FooterRole[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitNodeCard -notmatch 'Locked[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitNodeCard -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitNodeCard -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitNodeCard text edits must refresh layout; art, footer, role, and lock edits must use visual-only redraw."
}
$kitOrbMeter = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitOrbMeter.cs"
if ($kitOrbMeter -notmatch 'string\s+text\s*=\s*KitCase\(string\.IsNullOrWhiteSpace\(_centre\)' -or $kitOrbMeter -notmatch 'FitRole\(this,\s*UiSurface\.TextRole\.Value,[\s\S]*text,\s*font' -or $kitOrbMeter -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*textWidth\)') {
    Fail "KitOrbMeter centre text must be cased before fitting and ellipsized inside the orb."
}
if ($kitOrbMeter -notmatch 'CentreText\s*\{[^\r\n]*SetText\(ref\s+_centre' -or $kitOrbMeter -notmatch 'Symbol\s*\{[^\r\n]*SetText\(ref\s+_symbol' -or $kitOrbMeter -notmatch 'Value\s*\{[^\r\n]*RefreshContentAndRedraw\(\)' -or $kitOrbMeter -notmatch 'Fill[\s\S]*RefreshContentAndRedraw\(\)' -or $kitOrbMeter -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitOrbMeter authored value, fill, centre text, and symbol edits must refresh its auto minimum size before redraw."
}
$kitAvatarFrame = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitAvatarFrame.cs"
if ($kitAvatarFrame -notmatch 'string\s+badge\s*=\s*KitCase\(_badge\)' -or $kitAvatarFrame -notmatch 'EllipsizeText\(font,\s*badge,\s*bs,\s*badgeWidth\)') {
    Fail "KitAvatarFrame badge text must be cased and ellipsized inside the overhanging badge."
}
if ($kitAvatarFrame -notmatch 'BadgeText\s*\{[^\r\n]*SetText\(ref\s+_badge' -or $kitAvatarFrame -notmatch 'Portrait\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $kitAvatarFrame -notmatch 'BadgeRole[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitAvatarFrame -notmatch 'Round[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitAvatarFrame -notmatch 'RimRole[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitAvatarFrame -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitAvatarFrame -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitAvatarFrame badge text must refresh layout; portrait, roundness, and role edits must use visual-only redraw."
}
$kitRadialMeter = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRadialMeter.cs"
if ($kitRadialMeter -notmatch 'string\s+text\s*=\s*KitCase\(_centre\)' -or $kitRadialMeter -notmatch 'FitRole\(this,\s*UiSurface\.TextRole\.Value,[\s\S]*text,\s*font' -or $kitRadialMeter -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*inner\)') {
    Fail "KitRadialMeter centre text must be cased before fitting and ellipsized inside the inner ring."
}
if ($kitRadialMeter -notmatch 'CentreText\s*\{[^\r\n]*SetText\(ref\s+_centre' -or $kitRadialMeter -notmatch 'Value\s*\{[^\r\n]*RefreshContentAndRedraw\(\)' -or $kitRadialMeter -notmatch 'Segments\s*\{[^\r\n]*RefreshContentAndRedraw\(\)' -or $kitRadialMeter -notmatch 'Fill[\s\S]*RefreshContentAndRedraw\(\)' -or $kitRadialMeter -notmatch 'GapDegrees[\s\S]*RefreshContentAndRedraw\(\)' -or $kitRadialMeter -notmatch 'Thickness[\s\S]*RefreshContentAndRedraw\(\)' -or $kitRadialMeter -notmatch 'RefreshContentAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitRadialMeter authored gauge and centre text edits must refresh its auto minimum size before redraw."
}
$speechBubble = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSpeechBubble.cs"
if ($speechBubble -notmatch 'RefreshMinimumAndRedraw' -or $speechBubble -notmatch 'LongestLineWidth' -or $speechBubble -notmatch 'EstimateWrappedLineCount' -or $speechBubble -notmatch 'TailSizeFor' -or $speechBubble -notmatch 'Text\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $speechBubble -notmatch 'Padding\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $speechBubble -notmatch 'Tail\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)') {
    Fail "KitSpeechBubble must refresh and derive its minimum size from wrapped text, padding, and tail orientation."
}
if ($speechBubble -notmatch 'Text[\s\S]*if \(_text == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $speechBubble -notmatch 'TailOffset[\s\S]*Mathf\.IsEqualApprox\(_tailOffset,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $speechBubble -notmatch 'Accent[\s\S]*if \(_accent == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $speechBubble -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $speechBubble -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitSpeechBubble authored text, tail offset, and accent edits must use the correct layout or visual refresh path."
}
foreach ($panelSource in @($kitPanel, $kitPanelContainer, $kitCollapsible)) {
    if ($panelSource -notmatch 'KitChrome\.DrawPanelHeader') { Fail "A panel control does not draw through KitChrome.DrawPanelHeader." }
}
if ($kitPanelContainer -match 'private void DrawUtilityHeader') { Fail "KitPanelContainer reintroduced a private utility header renderer." }
if ($kitPanelContainer -notmatch 'KitChrome\.DrawPlate\(this,\s*_genre,\s*body,\s*face,\s*KitState\.Normal,\s*fs / 14f,[\s\r\n ]*KitWidgetClass\.Panel\)' -or $kitPanelContainer -match 'private void Cut\(' -or $kitPanelContainer -match 'KitControl\.Outline') {
    Fail "KitPanelContainer must draw its body through KitChrome.DrawPlate/DrawShape instead of a private panel material renderer."
}
if ($kitPanelContainer -notmatch 'private void Refresh\(\)[\s\S]*bool marginsChanged = KitChrome\.SetEmptyStyleboxOverride[\s\S]*if \(marginsChanged\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitPanelContainer.Refresh must invalidate container layout only when rebuilt panel stylebox content margins actually change."
}
if ($kitPanelContainer -notmatch 'Title\s*\{[^\r\n]*string next = value \?\? ""[\s\S]*if \(_title == next\) return[\s\S]*Refresh\(\)' -or $kitPanelContainer -notmatch 'BannerShade[\s\S]*Mathf\.Abs\(_bannerShade - value\) < 0\.001f[\s\S]*Refresh\(\)' -or $kitPanelContainer -notmatch 'TitleFontScale[\s\S]*Mathf\.Abs\(_titleFontScale - value\) < 0\.001f[\s\S]*Refresh\(\)' -or $kitPanelContainer -notmatch 'TitleStyle[\s\S]*if \(_titleStyle == value\) return[\s\S]*Refresh\(\)' -or $kitPanelContainer -notmatch 'Intent[\s\S]*if \(_intent == value\) return[\s\S]*Refresh\(\)' -or $kitPanelContainer -notmatch 'ShowWell[\s\S]*if \(_showWell == value\) return[\s\S]*Refresh\(\)' -or $kitPanelContainer -notmatch 'ExtraPadding[\s\S]*if \(_extraPadding == value\) return[\s\S]*Refresh\(\)') {
    Fail "KitPanelContainer exported chrome setters must ignore no-op assignments before rebuilding stylebox margins."
}
if ($kitPanelContainer -notmatch 'private float _titleFontScale = 0\.90f') {
    Fail "KitPanelContainer default header title scale must stay readable."
}
$kitTabPanel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTabPanel.cs"
if ($kitTabPanel -notmatch 'private void Apply\(\)[\s\S]*SetStyleboxOverrideIfChanged[\s\S]*SetFontSizeOverrideIfChanged[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitTabPanel.Apply must invalidate TabContainer layout and redraw after rebuilding tab styleboxes and font metrics."
}
if ($kitTabPanel -notmatch 'UiSurface\.SemanticOrDerived\(this,\s*Accent\)') {
    Fail "KitTabPanel must not use strict UiSurface.Semantic during startup styling; early theme notifications are valid while ThemePresetComponent is assigning the generated theme."
}
if ($kitTabPanel -notmatch 'bool changed = false[\s\S]*changed \|= KitChrome\.SetStyleboxOverrideIfChanged[\s\S]*changed \|= KitChrome\.SetFontSizeOverrideIfChanged[\s\S]*if \(!changed\) return;[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitTabPanel.Apply must skip layout/redraw work when all theme overrides are already current."
}
if ($kitTabPanel -notmatch 'set[\s\S]*RequestApply\(\)' -or $kitTabPanel -notmatch 'private void RequestApply\(\)[\s\S]*if \(!IsInsideTree\(\)\) return[\s\S]*Apply\(\)' -or $kitTabPanel -notmatch 'if \(_applying \|\| !IsInsideTree\(\)\) return') {
    Fail "KitTabPanel exported theme edits must defer Apply until the control is inside the scene tree."
}
$kitItemList = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitItemList.cs"
if ($kitItemList -notmatch 'private void Apply\(\)[\s\S]*SetStyleboxOverrideIfChanged[\s\S]*SetConstantOverrideIfChanged[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitItemList.Apply must invalidate ItemList layout and redraw after rebuilding styleboxes, spacing, and font metrics."
}
if ($kitItemList -notmatch 'UiSurface\.SemanticOrDerived\(this,\s*Accent\)') {
    Fail "KitItemList must not use strict UiSurface.Semantic during startup styling; early theme notifications are valid while ThemePresetComponent is assigning the generated theme."
}
if ($kitItemList -notmatch 'bool changed = false[\s\S]*changed \|= KitChrome\.SetStyleboxOverrideIfChanged[\s\S]*changed \|= KitChrome\.SetConstantOverrideIfChanged[\s\S]*if \(!changed\) return;[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitItemList.Apply must skip layout/redraw work when all theme overrides are already current."
}
if ($kitItemList -notmatch 'set[\s\S]*RequestApply\(\)' -or $kitItemList -notmatch 'private void RequestApply\(\)[\s\S]*if \(!IsInsideTree\(\)\) return[\s\S]*Apply\(\)' -or $kitItemList -notmatch 'if \(_applying \|\| !IsInsideTree\(\)\) return') {
    Fail "KitItemList exported theme edits must defer Apply until the control is inside the scene tree."
}
$kitGodotTree = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitGodotTree.cs"
if ($kitGodotTree -notmatch 'private void Apply\(\)[\s\S]*SetStyleboxOverrideIfChanged[\s\S]*SetConstantOverrideIfChanged[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitGodotTree.Apply must invalidate Tree layout and redraw after rebuilding styleboxes, spacing, and font metrics."
}
if ($kitGodotTree -notmatch 'UiSurface\.SemanticOrDerived\(this,\s*Accent\)') {
    Fail "KitGodotTree must not use strict UiSurface.Semantic during startup styling; early theme notifications are valid while ThemePresetComponent is assigning the generated theme."
}
if ($kitGodotTree -notmatch 'bool changed = false[\s\S]*changed \|= KitChrome\.SetStyleboxOverrideIfChanged[\s\S]*changed \|= KitChrome\.SetConstantOverrideIfChanged[\s\S]*if \(!changed\) return;[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitGodotTree.Apply must skip layout/redraw work when all theme overrides are already current."
}
if ($kitGodotTree -notmatch 'set[\s\S]*RequestApply\(\)' -or $kitGodotTree -notmatch 'private void RequestApply\(\)[\s\S]*if \(!IsInsideTree\(\)\) return[\s\S]*Apply\(\)' -or $kitGodotTree -notmatch 'if \(_applying \|\| !IsInsideTree\(\)\) return') {
    Fail "KitGodotTree exported theme edits must defer Apply until the control is inside the scene tree."
}
if ($kitCollapsible -match 'body\.Position\.X \+ \(body\.Size\.X - m\.X\)') { Fail "KitCollapsiblePanel reintroduced direct centered title text drawing." }
if ($kitCollapsible -notmatch 'RefreshMinimumAndRedraw' -or $kitCollapsible -notmatch 'TextWidth' -or $kitCollapsible -notmatch 'PanelHeaderRoom' -or $kitCollapsible -notmatch 'HandleEdge\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitCollapsible -notmatch 'Title[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitCollapsible -notmatch 'HeaderStyle\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitCollapsible -notmatch 'TitleFontScale[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitCollapsible -notmatch 'UpdateMinimumSize\(\)') {
    Fail "KitCollapsiblePanel must refresh and derive its minimum size from handle edge and shared panel header/title metrics."
}
if ($kitCollapsible -notmatch 'private float _titleFontScale = 0\.90f') {
    Fail "KitCollapsiblePanel default header title scale must stay readable."
}

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
    if ($source -notmatch 'FocusMode\s*=\s*FocusModeEnum\.All' -and $source -notmatch 'ApplyInputDefaults\([^;\r\n]*FocusModeEnum\.All') { Fail "$relativePath is interactive but does not opt into keyboard focus." }
    if ($source -notmatch 'InputEventKey' -and $source -notmatch 'ActivateOnClickOrConfirm') { Fail "$relativePath is interactive but does not handle keyboard input." }
    if ($source -notmatch 'DrawFocusRing') { Fail "$relativePath is interactive but does not draw a visible focus ring." }
}

$pureHoverControls = @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs"; Reset = '_hoverSide\s*=\s*0' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs"; Reset = '_hoverHandle\s*=\s*false' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs"; Reset = '_hover\s*=\s*-1' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs"; Reset = '_hoverChoice\s*=\s*-1' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs"; Reset = '_hover\s*=\s*-1' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs"; Reset = '_hoverButton\s*=\s*0' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs"; Reset = '_hover\s*=\s*-1' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"; Reset = '_hover\s*=\s*-1' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"; Reset = '_hover\s*=\s*-1' }
)
foreach ($entry in $pureHoverControls) {
    $source = Read $entry.Path
    if ($source -notmatch 'MouseExited\s*\+=' -or $source -notmatch 'private void Clear' -or $source -notmatch $entry.Reset -or $source -notmatch 'QueueRedraw\(\)') {
        Fail "$($entry.Path) tracks visual hover independently and must clear it on MouseExited."
    }
}

$readySignalControls = @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitMeter.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitOptionButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSliderBar.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"
)
foreach ($relativePath in $readySignalControls) {
    $source = Read $relativePath
    $hasLocalGuard = $source -match 'if \(!_eventsHooked\)[\s\S]*(MouseEntered|MouseExited|ValueChanged|DragStarted|DragEnded|ItemSelected|TabChanged)\s*\+=[\s\S]*_eventsHooked = true'
    $hasSharedButtonGuard = $source -match 'if \(!_eventsHooked\)[\s\S]*HookButtonChromeRedraw\(this,\s*RefreshVisualAndRedraw,\s*ref _eventsHooked\)'
    if ($source -notmatch 'private bool _eventsHooked' -or (-not $hasLocalGuard -and -not $hasSharedButtonGuard)) {
        Fail "$relativePath subscribes to Godot signals in _Ready without a one-time guard."
    }
}

$editorCollectionSeedFiles = @()
foreach ($file in Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File) {
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match 'Engine\.IsEditorHint\(\)[\s\S]{0,240}(AddRange|Add\(|Set[A-Za-z0-9_]*\(\s*new\[\]|SeedDemo)') {
        $editorCollectionSeedFiles += $file.Name
    }
}
if ($editorCollectionSeedFiles.Count -gt 0) {
    Fail "Kit controls must not mutate exported/demo collections from _Ready just for editor preview: $($editorCollectionSeedFiles -join ', ')."
}
if ($kitChrome -notmatch 'DrawEmptyPreview' -or
    $kitChrome -notmatch 'Engine\.IsEditorHint\(\)' -or
    $kitChrome -notmatch 'DrawShape\(ctl,\s*genre,\s*r,\s*shape') {
    Fail "KitChrome must expose an editor-only empty preview helper so collection widgets can be visible without seeding authored data."
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs"; Empty = 'Options\.Count == 0'; Label = 'Options' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitCurrencyBar.cs"; Empty = 'Entries\.Count == 0'; Label = 'Entries' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs"; Empty = 'Levels\.Count == 0'; Label = 'Levels' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs"; Empty = 'n < 3'; Label = 'Axes' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs"; Empty = 'Segments\.Count == 0'; Label = 'Segments' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"; Empty = 'i < Slots\.Count \? Slots\[i\] : new Slot\(\)'; Label = $null },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs"; Empty = 'n < 2'; Label = 'Wedges' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs"; Empty = 'count == 0'; Label = 'Tabs' },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"; Empty = 'Nodes\.Count == 0'; Label = 'Nodes' }
)) {
    $source = Read $entry.Path
    if ($source -notmatch $entry.Empty) {
        Fail "$($entry.Path) must keep an explicit empty-data drawing path."
    }
    if ($entry.Label -ne $null -and
        ($source -notmatch 'KitChrome\.DrawEmptyPreview' -or $source -notmatch ('"' + [regex]::Escape($entry.Label) + '"'))) {
        Fail "$($entry.Path) must draw a non-mutating editor preview for empty authored data."
    }
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
if ($contextMenu -notmatch '\[Export\]\s*public string\[\]\s+Items') {
    Fail "KitContextMenu.Items must be exported so menu items can be authored at design time."
}
if ($contextMenu -notmatch 'GrabFocus\(\)' -or $contextMenu -notmatch 'IsCancelKey' -or $contextMenu -notmatch 'InputEventKey') {
    Fail "KitContextMenu does not implement focus, Escape dismissal, and keyboard selection."
}
if ($contextMenu -notmatch 'ClampedPopupPosition' -or $contextMenu -notmatch 'GetVisibleRect' -or $contextMenu -match 'Position\s*=\s*globalPosition') {
    Fail "KitContextMenu does not clamp popup placement to the visible viewport."
}
if ($contextMenu -notmatch 'PopupSizeForViewport' -or
    $contextMenu -notmatch 'override Vector2 _GetMinimumSize\(\)\s*\r?\n\s*=> PopupSizeForViewport\(NaturalMinimumSize\(\)\)' -or
    $contextMenu -notmatch 'private Vector2 NaturalMinimumSize\(\)' -or
    $contextMenu -notmatch 'visible\.Size\.X - margin \* 2f' -or
    $contextMenu -notmatch 'Mathf\.Min\(natural\.X,\s*maxWidth\)') {
    Fail "KitContextMenu must cap popup width to the visible viewport before clamping its position."
}
if ($contextMenu -notmatch 'PopupVisibleRect' -or
    $contextMenu -notmatch 'if \(TopLevel\)\s*\r?\n\s*return visible' -or
    $contextMenu -notmatch 'GetCanvasTransform\(\)\.AffineInverse\(\)') {
    Fail "KitContextMenu must use viewport coordinates for top-level popups and canvas conversion only for non-top-level cases."
}
if ($contextMenu -notmatch 'EllipsizeText\(font,\s*text,\s*fit,\s*row\.Size\.X') {
    Fail "KitContextMenu must ellipsize item text inside the actual row draw bounds."
}
if ($contextMenu -notmatch 'GetStringSize\(KitCase\(item\)' -or $contextMenu -notmatch 'NormalizeHover\(\)' -or $contextMenu -notmatch 'public void AddItem' -or $contextMenu -notmatch 'public void ClearItems' -or $contextMenu -notmatch 'NormalizeStrings[\s\S]*next\[i\] = values\[i\] \?\? ""' -or $contextMenu -notmatch 'SameStrings[\s\S]*a\.Length != b\.Length') {
    Fail "KitContextMenu must measure the same cased item labels it draws, normalize null item labels, and normalize hover state when items change."
}
if ($contextMenu -notmatch 'Items[\s\S]*string\[\] next = NormalizeStrings\(value\)[\s\S]*if \(SameStrings\(_items,\s*next\)\) return[\s\S]*ResizeToItems\(\)') {
    Fail "KitContextMenu item assignments must skip equivalent arrays before resizing popup layout."
}
if ($contextMenu -notmatch 'ResizeToItems[\s\S]*Vector2 wanted = _GetMinimumSize\(\)[\s\S]*Size = CustomMinimumSize = wanted[\s\S]*UpdateMinimumSize\(\)') {
    Fail "KitContextMenu item changes must notify Godot layout after resizing to its dynamic item list."
}
if ($contextMenu -notmatch 'override void _Input\(InputEvent @event\)[\s\S]*SetInputAsHandled\(\)') {
    Fail "KitContextMenu outside-click dismissal must consume the click so it cannot activate controls behind the popup."
}
$contextMenuViewportProbe = Read "tests/kit_context_menu_viewport_probe.gd"
if ($contextMenuViewportProbe -notmatch 'PopupAt' -or
    $contextMenuViewportProbe -notmatch 'extremely long generated building configuration label' -or
    $contextMenuViewportProbe -notmatch 'rect\.end\.x > viewport\.end\.x' -or
    $contextMenuViewportProbe -notmatch 'width was not capped') {
    Fail "kit_context_menu_viewport_probe.gd must assert long context menus stay within a small viewport."
}
$contextMenuViewportRunner = Read "tests/kit_context_menu_viewport_probe.ps1"
if ($contextMenuViewportRunner -notmatch '--resolution 320x240') {
    Fail "kit_context_menu_viewport_probe.ps1 must run Godot at a real small viewport resolution."
}
if ($runAddonChecks -notmatch 'kit_context_menu_viewport_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit context menu viewport probe."
}
$spinWheel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs"
if ($spinWheel -notmatch 'public bool RemoveWedge\(int index\)' -or
    $spinWheel -notmatch 'Wedges\.RemoveAt\(index\)' -or
    $spinWheel -notmatch 'public void ClearWedges\(\)' -or
    $spinWheel -notmatch 'Wedges\.Clear\(\)[\s\S]*RefreshWedges\(\)') {
    Fail "KitSpinWheel must expose refresh-safe remove and clear helpers for runtime wedge lists."
}
$currencyBar = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCurrencyBar.cs"
if ($currencyBar -notmatch 'public bool RemoveEntry\(int index\)' -or
    $currencyBar -notmatch 'Entries\.RemoveAt\(index\)' -or
    $currencyBar -notmatch 'public void ClearEntries\(\)' -or
    $currencyBar -notmatch 'Entries\.Clear\(\)[\s\S]*RefreshEntries\(\)') {
    Fail "KitCurrencyBar must expose refresh-safe remove and clear helpers for runtime resource readouts."
}
$segmentedIconGroup = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs"
if ($segmentedIconGroup -notmatch 'public bool RemoveSegment\(int index\)' -or
    $segmentedIconGroup -notmatch 'Segments\.RemoveAt\(index\)' -or
    $segmentedIconGroup -notmatch 'if \(index <= _current\)\s*\r?\n\s*_current = Mathf\.Max\(0,\s*_current - 1\)' -or
    $segmentedIconGroup -notmatch 'public void ClearSegments\(\)' -or
    $segmentedIconGroup -notmatch 'Segments\.Clear\(\)[\s\S]*RefreshSegments\(\)') {
    Fail "KitSegmentedIconGroup must expose refresh-safe remove and clear helpers for runtime segment lists."
}
$tabStripApi = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs"
if ($tabStripApi -notmatch 'public bool RemoveKitTab\(int index\)' -or
    $tabStripApi -notmatch 'Tabs\.RemoveAt\(index\)' -or
    $tabStripApi -notmatch 'public void ClearKitTabs\(\)' -or
    $tabStripApi -notmatch 'Tabs\.Clear\(\)[\s\S]*RebuildTabsFromList\(\)') {
    Fail "KitTabStrip must expose refresh-safe RemoveKitTab/ClearKitTabs helpers without hiding TabBar.ClearTabs."
}
$slotGridApi = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"
if ($slotGridApi -notmatch 'public bool RemoveSlot\(int index\)' -or
    $slotGridApi -notmatch 'Slots\.RemoveAt\(index\)' -or
    $slotGridApi -notmatch 'public void ClearSlots\(\)' -or
    $slotGridApi -notmatch 'Slots\.Clear\(\)[\s\S]*RefreshSlots\(\)') {
    Fail "KitSlotGrid must expose refresh-safe remove and clear helpers for runtime slot data."
}
$radarChart = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs"
if ($radarChart -notmatch 'public bool RemoveAxis\(int index\)' -or
    $radarChart -notmatch 'Axes\.RemoveAt\(index\)' -or
    $radarChart -notmatch 'Values\.RemoveAt\(index\)' -or
    $radarChart -notmatch 'public void ClearAxes\(\)' -or
    $radarChart -notmatch 'Axes\.Clear\(\)[\s\S]*Values\.Clear\(\)[\s\S]*RefreshData\(\)') {
    Fail "KitRadarChart must expose refresh-safe remove and clear helpers for runtime axis/value lists."
}
$treeApi = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"
if ($treeApi -notmatch 'public bool RemoveNode\(int index,\s*bool expandBounds = true\)' -or
    $treeApi -notmatch 'Nodes\.RemoveAt\(index\)' -or
    $treeApi -notmatch 'RemapParentReferencesAfterRemove\(index\)' -or
    $treeApi -notmatch 'node\.Parents\[i\] = parent - 1' -or
    $treeApi -notmatch 'public void ClearNodes\(\)' -or
    $treeApi -notmatch 'Nodes\.Clear\(\)[\s\S]*RefreshNodes\(expandBounds: false\)') {
    Fail "KitTree must expose refresh-safe remove/clear helpers and remap parent references when nodes are removed."
}
$collectionApiProbe = Read "tests/kit_collection_api_probe.gd"
if ($collectionApiProbe -notmatch 'RemoveOption' -or
    $collectionApiProbe -notmatch 'ClearOptions' -or
    $collectionApiProbe -notmatch 'RemoveEntry' -or
    $collectionApiProbe -notmatch 'ClearEntries' -or
    $collectionApiProbe -notmatch 'RemoveSegment' -or
    $collectionApiProbe -notmatch 'ClearSegments' -or
    $collectionApiProbe -notmatch 'RemoveKitTab' -or
    $collectionApiProbe -notmatch 'ClearKitTabs' -or
    $collectionApiProbe -notmatch 'RemoveSlot' -or
    $collectionApiProbe -notmatch 'ClearSlots' -or
    $collectionApiProbe -notmatch 'RemoveWedge' -or
    $collectionApiProbe -notmatch 'ClearWedges' -or
    $collectionApiProbe -notmatch 'RemoveAxis' -or
    $collectionApiProbe -notmatch 'ClearAxes' -or
    $collectionApiProbe -notmatch 'RemoveNode' -or
    $collectionApiProbe -notmatch 'ClearNodes' -or
    $collectionApiProbe -notmatch 'did not remap parent references') {
    Fail "kit_collection_api_probe.gd must exercise refresh-safe collection helper APIs across the collection-backed kit widgets."
}
if ($runAddonChecks -notmatch 'kit_collection_api_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit collection API probe."
}
$emptyCollectionProbe = Read "tests/kit_empty_collection_probe.gd"
foreach ($required in @(
    "KitArrowSelector",
    "KitCurrencyBar",
    "KitLevelPath",
    "KitRadarChart",
    "KitSegmentedIconGroup",
    "KitSlotGrid",
    "KitSpinWheel",
    "KitTabStrip",
    "KitTree",
    "get_combined_minimum_size",
    "seeded",
    "[kit-empty-collection] OK:"
)) {
    if ($emptyCollectionProbe -notmatch [regex]::Escape($required)) {
        Fail "kit_empty_collection_probe.gd must verify empty startup data and natural minimums for collection-backed kit widgets."
    }
}
if ($runAddonChecks -notmatch 'kit_empty_collection_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit empty collection probe."
}
$kitUpdateMinimumCollisions = rg '^\s*(private|public|protected|internal)\s+void\s+UpdateMinimumSize\s*\(' 'addons/beep_game_builder_cs/ecs/ui/kit'
if ($LASTEXITCODE -eq 0 -and $kitUpdateMinimumCollisions) {
    Fail "Kit controls must not define helper methods named UpdateMinimumSize; that name collides with Godot.Control.UpdateMinimumSize().`n$kitUpdateMinimumCollisions"
}
$ballComponentApi = Read "addons/beep_game_builder_cs/ecs/algorithms/BallComponent.cs"
if ($ballComponentApi -match 'public\s+Node2D\?\s+Owner\s*\{' -or
    $ballComponentApi -notmatch 'public\s+Node2D\?\s+Possessor\s*\{' -or
    $ballComponentApi -notmatch 'public bool IsOwned => Possessor != null') {
    Fail "BallComponent must expose Possessor, not Owner, so it does not hide Godot.Node.Owner."
}
$statApi = Read "addons/beep_game_builder_cs/ecs/stats/Stat.cs"
if ($statApi -match '\bChangedEventHandler' -or
    $statApi -match 'SignalName\.Changed' -or
    $statApi -notmatch 'ValueChangedEventHandler' -or
    $statApi -notmatch 'SignalName\.ValueChanged') {
    Fail "Stat must emit ValueChanged, not Changed, so it does not hide Godot.Resource.Changed."
}
$encryptionApi = Read "addons/beep_game_builder_cs/core/BeepEncryptionPathfinding.cs"
if ($encryptionApi -match 'new Rfc2898DeriveBytes' -or
    $encryptionApi -notmatch 'Rfc2898DeriveBytes\.Pbkdf2' -or
    $encryptionApi -notmatch 'outputLength:\s*48') {
    Fail "BeepEncryptionHelper must use the non-obsolete PBKDF2 API and derive 32 bytes of key plus 16 bytes of IV."
}
$weatherHudApi = Read "addons/beep_game_builder_cs/ecs/ui/WeatherHUDComponent.cs"
if ($weatherHudApi -match '\[Export\]\s*public\s+NodePath\?\s+WeatherSystemPath' -or
    $weatherHudApi -match '\[Export\]\s*public\s+Texture2D\?\[\]\s+WeatherIcons' -or
    $weatherHudApi -notmatch '\[Export\]\s*public\s+NodePath\s+WeatherSystemPath\s*\{\s*get;\s*set;\s*\}\s*=\s*new\(""\)' -or
    $weatherHudApi -notmatch '\[Export\]\s*public\s+Texture2D\[\]\s+WeatherIcons') {
    Fail "WeatherHUDComponent exported properties must avoid nullable annotations so Godot source generation stays warning-free."
}
$nullableUiNodePathExports = @()
Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui") -Filter "*.cs" -File -Recurse | ForEach-Object {
    $source = Get-Content -Path $_.FullName -Raw
    if ($source -match '\[Export\]\s*public\s+NodePath\?') {
        $nullableUiNodePathExports += $_.FullName
    }
}
if ($nullableUiNodePathExports.Count -gt 0) {
    Fail "UI components must use empty NodePath exports instead of nullable NodePath exports: $($nullableUiNodePathExports -join ', ')."
}
$segmentedGroup = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs"
if ($segmentedGroup -notmatch '\[Export\]\s*public string\[\]\s+SegmentGlyphs' -or $segmentedGroup -notmatch '\[Export\]\s*public string\[\]\s+SegmentTips' -or $segmentedGroup -notmatch '\[Export\]\s*public Texture2D\[\]\s+SegmentIcons') {
    Fail "KitSegmentedIconGroup must export SegmentGlyphs, SegmentTips, and SegmentIcons so segments can be authored at design time."
}
if ($segmentedGroup -notmatch 'SetSegments' -or $segmentedGroup -notmatch 'AddSegment' -or $segmentedGroup -notmatch 'RefreshSegments' -or $segmentedGroup -notmatch 'RefreshAutoMinimumSize') {
    Fail "KitSegmentedIconGroup must expose collection refresh APIs so runtime segment changes relayout and redraw."
}
if ($segmentedGroup -notmatch 'SetSegments[\s\S]*List<Segment> next = NormalizeSegments\(segments\)' -or $segmentedGroup -notmatch 'SetSegments[\s\S]*if \(SameSegments\(Segments,\s*next\)\) return' -or $segmentedGroup -notmatch 'NormalizeSegments[\s\S]*Glyph = segment\?\.Glyph \?\? ""' -or $segmentedGroup -notmatch 'NormalizeSegments[\s\S]*Tip = segment\?\.Tip \?\? ""' -or $segmentedGroup -notmatch 'SameSegments[\s\S]*ReferenceEquals\(left\[i\]\.Icon,\s*right\[i\]\.Icon\)') {
    Fail "KitSegmentedIconGroup.SetSegments must clone and normalize segment data before mutating the live authored collection."
}
if ($segmentedGroup -notmatch 'SetSegmentGlyphs[\s\S]*bool changed = Segments\.Count != count[\s\S]*string next = glyphs!\[i\] \?\? ""[\s\S]*if \(!changed\) return' -or $segmentedGroup -notmatch 'SetSegmentTips[\s\S]*if \(tips == null\)[\s\S]*if \(!changed\) return[\s\S]*string next = tips\[i\] \?\? ""[\s\S]*if \(!updated\) return') {
    Fail "KitSegmentedIconGroup design-time glyph and tip updates must normalize null labels and skip no-op refreshes."
}
if ($segmentedGroup -notmatch 'for \(int i = tips\.Length; i < Segments\.Count; i\+\+\)[\s\S]*Segments\[i\]\.Tip = "";' -or $segmentedGroup -notmatch 'SetSegmentIcons[\s\S]*if \(Segments\[i\]\.Icon == icons\[i\]\) continue[\s\S]*for \(int i = icons\.Length; i < Segments\.Count; i\+\+\)[\s\S]*Segments\[i\]\.Icon = null;') {
    Fail "KitSegmentedIconGroup partial tip/icon arrays must clear omitted trailing segment metadata to defaults."
}
if ($segmentedGroup -notmatch 'RefreshSegments[\s\S]*RefreshMinimumAndRedraw\(\)' -or $segmentedGroup -notmatch 'Current[\s\S]*if \(v == _current\) return[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*EmitSignal\(SignalName\.SegmentChanged,\s*v\)' -or $segmentedGroup -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $segmentedGroup -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitSegmentedIconGroup segment list edits must relayout, while current selection uses guarded visual redraw."
}
if ($segmentedGroup -notmatch 'Segments\.Count > 0 && _current >= 0 && _current < Segments\.Count') {
    Fail "KitSegmentedIconGroup must not emit a segment activation when the segment list is empty."
}
$tabStrip = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs"
if ($tabStrip -notmatch '\[Export\]\s*public string\[\]\s+TabLabels' -or $tabStrip -notmatch '\[Export\]\s*public Texture2D\[\]\s+TabIcons' -or $tabStrip -notmatch '\[Export\]\s*public int\[\]\s+TabBadges') {
    Fail "KitTabStrip must export TabLabels, TabIcons, and TabBadges so kit tabs can be authored at design time."
}
if ($tabStrip -notmatch 'SetTabs' -or $tabStrip -notmatch 'AddKitTab' -or $tabStrip -notmatch 'RefreshTabs' -or $tabStrip -notmatch 'RebuildTabsFromList' -or $tabStrip -notmatch 'ClearTabs\(\)' -or $tabStrip -notmatch 'AddTabsToNative' -or $tabStrip -notmatch 'RefreshAutoMinimumSize') {
    Fail "KitTabStrip must expose collection refresh APIs and rebuild the native TabBar tabs when the kit tab list changes."
}
if ($tabStrip -notmatch 'SetTabs[\s\S]*List<Tab> next = NormalizeTabs\(tabs\)' -or $tabStrip -notmatch 'SetTabs[\s\S]*if \(SameTabs\(Tabs,\s*next\)\) return' -or $tabStrip -notmatch 'NormalizeTabs[\s\S]*Text = tab\?\.Text \?\? ""' -or $tabStrip -notmatch 'NormalizeTabs[\s\S]*Icon = tab\?\.Icon' -or $tabStrip -notmatch 'NormalizeTabs[\s\S]*Badge = Mathf\.Max\(0,\s*tab\?\.Badge \?\? 0\)') {
    Fail "KitTabStrip.SetTabs must clone and normalize tab data before mutating the live authored collection."
}
if ($tabStrip -notmatch 'SetTabLabels[\s\S]*bool changed = Tabs\.Count != count[\s\S]*string next = labels!\[i\] \?\? ""[\s\S]*if \(!changed\) return' -or $tabStrip -notmatch 'SetTabIcons[\s\S]*if \(icons == null\)[\s\S]*if \(!changed\) return[\s\S]*if \(Tabs\[i\]\.Icon == icons\[i\]\) continue[\s\S]*if \(!updated\) return' -or $tabStrip -notmatch 'SetTabBadges[\s\S]*if \(badges == null\)[\s\S]*if \(!changed\) return[\s\S]*int next = Mathf\.Max\(0,\s*badges\[i\]\)[\s\S]*if \(!updated\) return') {
    Fail "KitTabStrip design-time tab arrays must normalize values and skip no-op native TabBar rebuilds."
}
if ($tabStrip -notmatch 'for \(int i = icons\.Length; i < Tabs\.Count; i\+\+\)[\s\S]*Tabs\[i\]\.Icon = null;' -or $tabStrip -notmatch 'for \(int i = badges\.Length; i < Tabs\.Count; i\+\+\)[\s\S]*Tabs\[i\]\.Badge = 0;') {
    Fail "KitTabStrip partial icon/badge arrays must clear omitted trailing tab metadata to defaults."
}
$arrowSelector = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs"
if ($arrowSelector -notmatch '\[Export\]\s*public string\[\]\s+OptionLabels') {
    Fail "KitArrowSelector.OptionLabels must be exported so selector options can be authored at design time."
}
if ($arrowSelector -notmatch 'SetOptions' -or $arrowSelector -notmatch 'AddOption' -or $arrowSelector -notmatch 'RefreshOptions' -or $arrowSelector -notmatch 'RefreshAutoMinimumSize' -or $arrowSelector -notmatch 'NormalizeStrings' -or $arrowSelector -notmatch 'SameStrings') {
    Fail "KitArrowSelector must expose option refresh APIs so runtime option changes normalize labels, selection, and redraw."
}
if ($arrowSelector -notmatch 'SetOptions[\s\S]*string\[\] next = NormalizeStrings\(options\)' -or $arrowSelector -notmatch 'SetOptions[\s\S]*if \(SameStrings\(Options,\s*next\) && _current == normalizedCurrent\) return' -or $arrowSelector -notmatch 'NormalizeStrings[\s\S]*next\.Add\(value \?\? ""\)' -or $arrowSelector -notmatch 'RefreshOptions[\s\S]*RefreshMinimumAndRedraw\(\)' -or $arrowSelector -notmatch 'Current[\s\S]*if \(v == _current\) return[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*EmitSignal\(SignalName\.OptionChanged,\s*v\)' -or $arrowSelector -notmatch 'Clamp[\s\S]*if \(_clamp == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $arrowSelector -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $arrowSelector -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitArrowSelector option list edits must normalize null labels and skip equivalent relayouts, while current/clamp state uses guarded visual redraw."
}
$arrowSetOptions = [regex]::Match($arrowSelector, 'public void SetOptions\(IEnumerable<string>\? options,\s*int current = 0\)[\s\S]*?RefreshOptions\(\);')
if (-not $arrowSetOptions.Success `
    -or $arrowSetOptions.Value -notmatch '_current = normalizedCurrent;' `
    -or $arrowSetOptions.Value -match 'Current\s*=\s*current;') {
    Fail "KitArrowSelector.SetOptions must bulk-rebind options without routing through the signal-emitting Current setter."
}
if ($arrowSelector -notmatch 'Clamp\s*\?\s*Mathf\.Clamp\(value,\s*0,\s*Options\.Count - 1\)\s*:\s*Mathf\.PosMod\(value,\s*Options\.Count\)') {
    Fail "KitArrowSelector.Current must clamp instead of wrap when Clamp is enabled."
}
$currencyBar = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCurrencyBar.cs"
if ($currencyBar -notmatch '\[Export\]\s*public string\[\]\s+EntryValues' -or $currencyBar -notmatch '\[Export\]\s*public string\[\]\s+EntryGlyphs' -or $currencyBar -notmatch '\[Export\]\s*public Texture2D\[\]\s+EntryIcons' -or $currencyBar -notmatch '\[Export\]\s*public int\[\]\s+EntryAccentRoles') {
    Fail "KitCurrencyBar must export EntryValues, EntryGlyphs, EntryIcons, and EntryAccentRoles so HUD resource capsules can be authored at design time."
}
if ($currencyBar -notmatch 'SetEntries' -or $currencyBar -notmatch 'AddEntry' -or $currencyBar -notmatch 'RefreshEntries' -or $currencyBar -notmatch 'RefreshAutoMinimumSize') {
    Fail "KitCurrencyBar must expose entry refresh APIs so runtime resource changes relayout the capsule row."
}
if ($currencyBar -notmatch 'SetEntries[\s\S]*List<Entry> next = NormalizeEntries\(entries\)' -or $currencyBar -notmatch 'SetEntries[\s\S]*if \(SameEntries\(Entries,\s*next\)\) return' -or $currencyBar -notmatch 'NormalizeEntries[\s\S]*Value = entry\?\.Value \?\? ""' -or $currencyBar -notmatch 'NormalizeEntries[\s\S]*Glyph = entry\?\.Glyph \?\? ""' -or $currencyBar -notmatch 'NormalizeEntries[\s\S]*Accent = RoleFromOrdinal\(\(int\)\(entry\?\.Accent \?\? UiSurface\.Role\.Warning\)\)') {
    Fail "KitCurrencyBar.SetEntries must clone and normalize resource entries before mutating the live authored collection."
}
if ($currencyBar -notmatch 'SetEntryValues[\s\S]*bool changed = Entries\.Count != count[\s\S]*string next = values!\[i\] \?\? ""[\s\S]*if \(!changed\) return' -or $currencyBar -notmatch 'SetEntryGlyphs[\s\S]*if \(glyphs == null\)[\s\S]*if \(!changed\) return[\s\S]*string next = glyphs\[i\] \?\? ""[\s\S]*if \(!updated\) return' -or $currencyBar -notmatch 'SetEntryIcons[\s\S]*if \(icons == null\)[\s\S]*if \(!changed\) return[\s\S]*if \(Entries\[i\]\.Icon == icons\[i\]\) continue[\s\S]*if \(!updated\) return' -or $currencyBar -notmatch 'SetEntryAccentRoles[\s\S]*if \(accents == null\)[\s\S]*if \(!changed\) return[\s\S]*UiSurface\.Role next = RoleFromOrdinal\(accents\[i\]\)[\s\S]*if \(!updated\) return') {
    Fail "KitCurrencyBar design-time entry arrays must normalize values and skip no-op refreshes."
}
if ($currencyBar -notmatch 'for \(int i = glyphs\.Length; i < Entries\.Count; i\+\+\)[\s\S]*Entries\[i\]\.Glyph = "";' `
    -or $currencyBar -notmatch 'for \(int i = icons\.Length; i < Entries\.Count; i\+\+\)[\s\S]*Entries\[i\]\.Icon = null;' `
    -or $currencyBar -notmatch 'for \(int i = accents\.Length; i < Entries\.Count; i\+\+\)[\s\S]*Entries\[i\]\.Accent = UiSurface\.Role\.Warning;') {
    Fail "KitCurrencyBar partial glyph/icon/accent arrays must clear omitted trailing entry metadata to defaults."
}
if ($currencyBar -notmatch 'TextWidth\(font,\s*entry\.Value' -or $currencyBar -notmatch 'capR \+ padX \* 2f \+ valueWidth' -or $currencyBar -notmatch 'public void SetValue\(int index,\s*string value\)[\s\S]*RefreshFootprint\(\)') {
    Fail "KitCurrencyBar minimum width and SetValue must account for live value text so HUD resource changes do not clip."
}
if ($currencyBar -notmatch 'EllipsizeText\(font,\s*e\.Value,\s*vf,\s*avail\)' -or $currencyBar -notmatch 'string\s+glyph\s*=\s*KitCase\(e\.Glyph\)' -or $currencyBar -notmatch 'EllipsizeText\(font,\s*glyph,\s*gs,\s*glyphWidth\)') {
    Fail "KitCurrencyBar must ellipsize long resource values and fallback glyphs inside their capsule bounds."
}
if ($currencyBar -match 'KitWidgetClass\.Chip' -or $currencyBar -match 'DrawShape\(iconCell') {
    Fail "KitCurrencyBar must render entries as one HUD strip with inline icons, not nested chip controls."
}
$kitToggleChrome = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitToggle.cs"
if ($kitToggleChrome -match 'DrawPlate\(this,\s*_genre,\s*knob') {
    Fail "KitToggle switch thumbs must not draw a nested button plate inside the track."
}
$kitCheckButtonChrome = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCheckButton.cs"
if ($kitCheckButtonChrome -match 'DrawPlate\(this,\s*_genre,\s*knob') {
    Fail "KitCheckButton switch thumbs must not draw a nested button plate inside the track."
}
$kitSwitchVisualChrome = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSwitchVisual.cs"
if ($kitSwitchVisualChrome -match 'DrawPlate\(this,\s*_genre,\s*knob') {
    Fail "KitSwitchVisual thumbs must not draw a nested button plate inside the track."
}
$kitSliderBarChrome = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSliderBar.cs"
if ($kitSliderBarChrome -match 'DrawPlate\(this,\s*_genre,\s*(knob|fill)') {
    Fail "KitSliderBar must render fill and grabber as one slider surface, not nested plates inside the track."
}
$radarChart = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs"
if ($radarChart -notmatch '\[Export\]\s*public string\[\]\s+AxisLabels' -or $radarChart -notmatch '\[Export\]\s*public float\[\]\s+AxisValues') {
    Fail "KitRadarChart must export AxisLabels and AxisValues so chart data can be authored at design time."
}
if ($radarChart -notmatch 'SetData' -or $radarChart -notmatch 'AddAxis' -or $radarChart -notmatch 'RefreshData') {
    Fail "KitRadarChart must expose data refresh APIs so axes and values stay normalized after runtime changes."
}
if ($radarChart -notmatch 'SetData[\s\S]*List<string> nextAxes = NormalizeAxes\(axes\)' -or $radarChart -notmatch 'SetData[\s\S]*List<float> nextValues = NormalizeValues\(values\)' -or $radarChart -notmatch 'SetData[\s\S]*NormalizeParallelData\(nextAxes,\s*nextValues\)[\s\S]*if \(SameStrings\(Axes,\s*nextAxes\) && SameFloats\(Values,\s*nextValues\)\)' -or $radarChart -notmatch 'NormalizeAxes[\s\S]*next\.Add\(axis \?\? ""\)' -or $radarChart -notmatch 'NormalizeValues[\s\S]*Mathf\.Clamp\(value,\s*0f,\s*1f\)' -or $radarChart -notmatch 'NormalizeParallelData[\s\S]*int count = Mathf\.Max\(axes\.Count,\s*values\.Count\)[\s\S]*axes\.Add\(""\)[\s\S]*values\.Add\(0f\)') {
    Fail "KitRadarChart.SetData must normalize chart labels and values before mutating the live data lists."
}
if ($radarChart -notmatch 'SetAxisLabels[\s\S]*bool changed = Axes\.Count != count \|\| Values\.Count != count[\s\S]*string next = labels!\[i\] \?\? ""[\s\S]*if \(!changed\) return' -or $radarChart -notmatch 'SetAxisValues[\s\S]*bool changed = Values\.Count != count \|\| Axes\.Count < count[\s\S]*float next = Mathf\.Clamp\(values!\[i\],\s*0f,\s*1f\)[\s\S]*if \(!changed\) return') {
    Fail "KitRadarChart design-time axis arrays must normalize values and skip no-op refreshes."
}
if ($radarChart -notmatch 'string\s+t\s*=\s*KitCase\(Axes\[i\]' -or $radarChart -notmatch 'EllipsizeText\(font,\s*t,\s*tf,\s*labelWidth\)') {
    Fail "KitRadarChart axis labels must be cased and ellipsized inside their badge bounds."
}
$slotGrid = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs"
if ($slotGrid -notmatch '\[Export\]\s*public int\[\]\s+SlotKinds' -or $slotGrid -notmatch '\[Export\]\s*public int\[\]\s+SlotCounts' -or $slotGrid -notmatch '\[Export\]\s*public Texture2D\[\]\s+SlotIcons' -or $slotGrid -notmatch '\[Export\]\s*public string\[\]\s+SlotRequirements' -or $slotGrid -notmatch '\[Export\]\s*public int\[\]\s+SlotTintRoles') {
    Fail "KitSlotGrid must export SlotKinds, SlotCounts, SlotIcons, SlotRequirements, and SlotTintRoles so slots can be authored at design time."
}
if ($slotGrid -notmatch 'SetSlots' -or $slotGrid -notmatch 'AddSlot' -or $slotGrid -notmatch 'RefreshSlots' -or $slotGrid -notmatch 'RefreshAutoMinimumSize') {
    Fail "KitSlotGrid must expose slot refresh APIs so runtime slot changes normalize selection and redraw."
}
if ($slotGrid -notmatch 'SetSlots[\s\S]*List<Slot> next = NormalizeSlots\(slots\)' -or $slotGrid -notmatch 'SetSlots[\s\S]*if \(SameSlots\(Slots,\s*next\)\) return' -or $slotGrid -notmatch 'NormalizeSlots[\s\S]*Kind = SlotKindFromOrdinal\(\(int\)\(slot\?\.Kind \?\? SlotKind\.Blank\)\)' -or $slotGrid -notmatch 'NormalizeSlots[\s\S]*Count = Mathf\.Max\(0,\s*slot\?\.Count \?\? 0\)' -or $slotGrid -notmatch 'NormalizeSlots[\s\S]*Requirement = slot\?\.Requirement \?\? ""' -or $slotGrid -notmatch 'NormalizeSlots[\s\S]*Tint = RoleFromOrdinal\(\(int\)\(slot\?\.Tint \?\? UiSurface\.Role\.Neutral\)\)') {
    Fail "KitSlotGrid.SetSlots must clone and normalize slot data before mutating the live authored collection."
}
if ($slotGrid -notmatch 'SetSlotKinds[\s\S]*bool changed = Slots\.Count != count[\s\S]*SlotKind next = SlotKindFromOrdinal\(kinds!\[i\]\)[\s\S]*if \(!changed\) return' -or $slotGrid -notmatch 'SetSlotCounts[\s\S]*if \(counts == null\)[\s\S]*if \(!changed\) return[\s\S]*int next = Mathf\.Max\(0,\s*counts\[i\]\)[\s\S]*if \(!updated\) return' -or $slotGrid -notmatch 'SetSlotIcons[\s\S]*if \(icons == null\)[\s\S]*if \(!changed\) return[\s\S]*if \(Slots\[i\]\.Icon == icons\[i\]\) continue[\s\S]*if \(!updated\) return' -or $slotGrid -notmatch 'SetSlotRequirements[\s\S]*if \(requirements == null\)[\s\S]*if \(!changed\) return[\s\S]*string next = requirements\[i\] \?\? ""[\s\S]*if \(!updated\) return' -or $slotGrid -notmatch 'SetSlotTintRoles[\s\S]*if \(tints == null\)[\s\S]*if \(!changed\) return[\s\S]*UiSurface\.Role next = RoleFromOrdinal\(tints\[i\]\)[\s\S]*if \(!updated\) return') {
    Fail "KitSlotGrid design-time slot arrays must normalize values and skip no-op refreshes."
}
if ($slotGrid -notmatch 'for \(int i = counts\.Length; i < Slots\.Count; i\+\+\)[\s\S]*Slots\[i\]\.Count = 0;' `
    -or $slotGrid -notmatch 'for \(int i = icons\.Length; i < Slots\.Count; i\+\+\)[\s\S]*Slots\[i\]\.Icon = null;' `
    -or $slotGrid -notmatch 'for \(int i = requirements\.Length; i < Slots\.Count; i\+\+\)[\s\S]*Slots\[i\]\.Requirement = "";' `
    -or $slotGrid -notmatch 'for \(int i = tints\.Length; i < Slots\.Count; i\+\+\)[\s\S]*Slots\[i\]\.Tint = UiSurface\.Role\.Neutral;') {
    Fail "KitSlotGrid partial metadata arrays must clear omitted trailing slot values to defaults."
}
if ($slotGrid -notmatch 'Columns[\s\S]*NormalizeSelectionToGrid\(\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or $slotGrid -notmatch 'Rows[\s\S]*NormalizeSelectionToGrid\(\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or $slotGrid -notmatch 'Selected[\s\S]*if \(_sel == next\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $slotGrid -notmatch 'InteriorRatio[\s\S]*Mathf\.IsEqualApprox\(_interiorRatio,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $slotGrid -notmatch 'RefreshSlots[\s\S]*NormalizeSelectionToGrid\(\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or $slotGrid -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $slotGrid -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitSlotGrid authored grid dimensions must relayout, while selection/interior state uses guarded visual redraw."
}
if ($slotGrid -notmatch 'NormalizeSelectionToGrid' -or $slotGrid -notmatch 'Selected[\s\S]*Mathf\.Clamp\(value,\s*-1,\s*TotalSlots - 1\)' -or $slotGrid -notmatch 'KitChrome\.IsConfirmKey\(key\) && _sel >= 0 && _sel < TotalSlots') {
    Fail "KitSlotGrid must clamp selected indices when grid bounds change and never emit out-of-range slot activations."
}
if ($slotGrid -notmatch 'string\s+req\s*=\s*KitCase\(s\.Requirement\)' -or $slotGrid -notmatch 'EllipsizeText\(font,\s*req,\s*small,\s*textWidth\)' -or $slotGrid -match 'm\.X <= r\.Size\.X') {
    Fail "KitSlotGrid locked requirement text must be cased and ellipsized instead of overflowing or disappearing."
}
if ($slotGrid -notmatch 'DrawCountBadge[\s\S]*EllipsizeText\(font,\s*txt,\s*small,\s*r\.Size\.X \* 0\.55f\)[\s\S]*DrawText\(font') {
    Fail "KitSlotGrid count badges must be ellipsized inside their badge bounds before drawing."
}
$levelPath = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs"
if ($levelPath -notmatch '\[Export\]\s*public string\[\]\s+LevelLabels' -or $levelPath -notmatch '\[Export\]\s*public int\[\]\s+LevelStates' -or $levelPath -notmatch '\[Export\]\s*public int\[\]\s+LevelStars') {
    Fail "KitLevelPath must export LevelLabels, LevelStates, and LevelStars so level maps can be authored at design time."
}
if ($levelPath -notmatch 'SetLevels' -or $levelPath -notmatch 'AddLevel' -or $levelPath -notmatch 'RefreshLevels' -or $levelPath -notmatch 'RefreshAutoMinimumSize' -or $levelPath -notmatch 'RefreshLevels[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitLevelPath must expose level refresh APIs so runtime level changes normalize focus/current state and redraw."
}
if ($levelPath -notmatch 'SetLevels[\s\S]*List<Level> next = NormalizeLevels\(levels\)' -or $levelPath -notmatch 'SetLevels[\s\S]*int normalizedCurrent = NormalizeCurrent\(current,\s*next\.Count\)' -or $levelPath -notmatch 'SetLevels[\s\S]*if \(SameLevels\(Levels,\s*next\) && _cur == normalizedCurrent\)' -or $levelPath -notmatch 'NormalizeLevels[\s\S]*Label = level\?\.Label \?\? ""' -or $levelPath -notmatch 'NormalizeLevels[\s\S]*State = StateFromOrdinal\(\(int\)\(level\?\.State \?\? LevelState\.Locked\)\)' -or $levelPath -notmatch 'NormalizeLevels[\s\S]*Stars = Mathf\.Clamp\(level\?\.Stars \?\? 0,\s*0,\s*3\)') {
    Fail "KitLevelPath.SetLevels must clone and normalize level data before mutating the live authored collection."
}
if ($levelPath -notmatch 'SetLevelLabels[\s\S]*bool changed = Levels\.Count != count[\s\S]*string next = labels!\[i\] \?\? ""[\s\S]*if \(!changed\) return' -or $levelPath -notmatch 'SetLevelStates[\s\S]*if \(states == null\)[\s\S]*if \(!changed\) return[\s\S]*LevelState next = StateFromOrdinal\(states\[i\]\)[\s\S]*if \(!updated\) return' -or $levelPath -notmatch 'SetLevelStars[\s\S]*if \(stars == null\)[\s\S]*if \(!changed\) return[\s\S]*int next = Mathf\.Clamp\(stars\[i\],\s*0,\s*3\)[\s\S]*if \(!updated\) return') {
    Fail "KitLevelPath design-time level arrays must normalize values and skip no-op refreshes."
}
if ($levelPath -notmatch 'for \(int i = states\.Length; i < Levels\.Count; i\+\+\)[\s\S]*Levels\[i\]\.State = LevelState\.Locked;' `
    -or $levelPath -notmatch 'for \(int i = stars\.Length; i < Levels\.Count; i\+\+\)[\s\S]*Levels\[i\]\.Stars = 0;') {
    Fail "KitLevelPath partial state/star arrays must clear omitted trailing level values to defaults."
}
if ($levelPath -notmatch 'AddLevel[\s\S]*State = StateFromOrdinal\(\(int\)state\)' -or $levelPath -notmatch 'NormalizeCurrent\(int value,\s*int levelCount\)[\s\S]*levelCount == 0[\s\S]*Mathf\.Clamp\(value,\s*-1,\s*levelCount - 1\)') {
    Fail "KitLevelPath level creation and current-index normalization must clamp invalid authored data."
}
if ($levelPath -notmatch 'string\s+label\s*=\s*KitCase\(lv\.Label\)' -or $levelPath -notmatch 'EllipsizeText\(font,\s*label,\s*lf,\s*labelWidth\)') {
    Fail "KitLevelPath node labels must be cased and ellipsized inside each level marker."
}
$kitTree = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs"
if ($kitTree -notmatch '\[Export\]\s*public int\[\]\s+BranchRoleOrdinals' -or $kitTree -notmatch '\[Export\]\s*public int\[\]\s+NodeColumns' -or $kitTree -notmatch '\[Export\]\s*public int\[\]\s+NodeTiers' -or $kitTree -notmatch '\[Export\]\s*public int\[\]\s+NodeBranches' -or $kitTree -notmatch '\[Export\]\s*public int\[\]\s+NodeStates' -or $kitTree -notmatch '\[Export\]\s*public Texture2D\[\]\s+NodeIcons' -or $kitTree -notmatch '\[Export\]\s*public int\[\]\s+NodeCosts' -or $kitTree -notmatch '\[Export\]\s*public string\[\]\s+NodeParentIndices') {
    Fail "KitTree must export BranchRoleOrdinals, NodeColumns, NodeTiers, NodeBranches, NodeStates, NodeIcons, NodeCosts, and NodeParentIndices so tree nodes can be authored at design time."
}
if ($kitTree -notmatch 'SetBranchRoleOrdinals') {
    Fail "KitTree must expose SetBranchRoleOrdinals so branch palette role changes redraw authored trees."
}
if ($kitTree -notmatch 'SetNodes' -or $kitTree -notmatch 'AddNode' -or $kitTree -notmatch 'RefreshNodes' -or $kitTree -notmatch 'ExpandBoundsToNodes' -or $kitTree -notmatch 'RefreshAutoMinimumSize') {
    Fail "KitTree must expose node refresh APIs so runtime node changes relayout branches and redraw."
}
if ($kitTree -notmatch 'SetNodes[\s\S]*List<Node> next = NormalizeNodes\(nodes\)' -or $kitTree -notmatch 'SetNodes[\s\S]*if \(SameNodes\(Nodes,\s*next\)\)[\s\S]*return' -or $kitTree -notmatch 'NormalizeNodes[\s\S]*Column = Mathf\.Max\(0,\s*node\?\.Column \?\? 0\)' -or $kitTree -notmatch 'NormalizeNodes[\s\S]*State = StateFromOrdinal\(\(int\)\(node\?\.State \?\? NodeState\.Locked\)\)' -or $kitTree -notmatch 'NormalizeNodes[\s\S]*foreach \(int parent in parentSources\[i\]\)[\s\S]*if \(parent >= 0 && parent < next\.Count\)[\s\S]*next\[i\]\.Parents\.Add\(parent\)') {
    Fail "KitTree.SetNodes must clone and normalize node data and parent lists before mutating the live authored collection."
}
if ($kitTree -notmatch 'SetNodeColumns[\s\S]*bool changed = Nodes\.Count != count[\s\S]*int next = Mathf\.Max\(0,\s*columns!\[i\]\)[\s\S]*if \(!changed\) return' -or $kitTree -notmatch 'SetNodeTiers[\s\S]*if \(tiers == null\)[\s\S]*if \(!changed\) return[\s\S]*int next = Mathf\.Max\(0,\s*tiers\[i\]\)[\s\S]*if \(!updated\) return' -or $kitTree -notmatch 'SetNodeBranches[\s\S]*if \(branches == null\)[\s\S]*if \(!changed\) return[\s\S]*int next = Mathf\.Max\(0,\s*branches\[i\]\)[\s\S]*if \(!updated\) return' -or $kitTree -notmatch 'SetNodeStates[\s\S]*if \(states == null\)[\s\S]*if \(!changed\) return[\s\S]*NodeState next = StateFromOrdinal\(states\[i\]\)[\s\S]*if \(!updated\) return') {
    Fail "KitTree design-time node placement/state arrays must normalize values and skip no-op refreshes."
}
if ($kitTree -notmatch 'SetNodeIcons[\s\S]*if \(icons == null\)[\s\S]*if \(!changed\) return[\s\S]*if \(Nodes\[i\]\.Icon == icons\[i\]\) continue[\s\S]*if \(!updated\) return' -or $kitTree -notmatch 'SetNodeCosts[\s\S]*if \(costs == null\)[\s\S]*if \(!changed\) return[\s\S]*int next = Mathf\.Max\(0,\s*costs\[i\]\)[\s\S]*if \(!updated\) return' -or $kitTree -notmatch 'SetNodeParentIndices[\s\S]*if \(parents == null\)[\s\S]*if \(!changed\) return[\s\S]*List<int> next = ParseParentList\(parents\[i\],\s*Nodes\.Count\)[\s\S]*if \(SameParents\(Nodes\[i\]\.Parents,\s*next\)\) continue[\s\S]*if \(!updated\) return') {
    Fail "KitTree design-time icon, cost, and parent arrays must normalize values and skip no-op refreshes."
}
if ($kitTree -notmatch 'for \(int i = tiers\.Length; i < Nodes\.Count; i\+\+\)[\s\S]*Nodes\[i\]\.Tier = 0;' `
    -or $kitTree -notmatch 'for \(int i = branches\.Length; i < Nodes\.Count; i\+\+\)[\s\S]*Nodes\[i\]\.Branch = 0;' `
    -or $kitTree -notmatch 'for \(int i = states\.Length; i < Nodes\.Count; i\+\+\)[\s\S]*Nodes\[i\]\.State = NodeState\.Locked;' `
    -or $kitTree -notmatch 'for \(int i = icons\.Length; i < Nodes\.Count; i\+\+\)[\s\S]*Nodes\[i\]\.Icon = null;' `
    -or $kitTree -notmatch 'for \(int i = costs\.Length; i < Nodes\.Count; i\+\+\)[\s\S]*Nodes\[i\]\.Cost = 0;' `
    -or $kitTree -notmatch 'for \(int i = parents\.Length; i < Nodes\.Count; i\+\+\)[\s\S]*Nodes\[i\]\.Parents\.Clear\(\);') {
    Fail "KitTree partial metadata arrays must clear omitted trailing node values to defaults."
}
if ($kitTree -notmatch 'SetBranchRoleOrdinals[\s\S]*if \(BranchRoles\.Length == 0\) return' -or $kitTree -notmatch 'UiSurface\.Role\[\] next = new UiSurface\.Role\[roles\.Length\]' -or $kitTree -notmatch 'if \(SameRoles\(BranchRoles,\s*next\)\) return') {
    Fail "KitTree branch role updates must normalize role ordinals and skip no-op redraws."
}
if ($kitTree -notmatch 'Selected[\s\S]*Nodes\.Count == 0 \? -1 : Mathf\.Clamp\(value,\s*-1,\s*Nodes\.Count - 1\)') {
    Fail "KitTree.Selected must normalize authored selected indices against the node list."
}
if ($kitTree -notmatch 'Columns[\s\S]*if \(_cols == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitTree -notmatch 'Tiers[\s\S]*if \(_tiers == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitTree -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitTree authored grid dimensions must use the guarded minimum-size refresh path."
}
if ($kitTree -notmatch 'CycleStateOnClick[\s\S]*if \(_cycleStateOnClick == value\) return[\s\S]*_cycleStateOnClick = value') {
    Fail "KitTree.CycleStateOnClick must guard repeated authored behavior-only writes."
}
if ($kitTree -notmatch 'ColourCarries[\s\S]*if \(_colourCarries == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitTree -notmatch 'Selected[\s\S]*if \(_sel == next\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitTree -notmatch 'SetBranchRoleOrdinals[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitTree -notmatch 'RefreshNodes[\s\S]*NormalizeParentReferences\(\)[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitTree -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitTree authored colour/selection state must use guarded visual redraw, while node refreshes update minimum size."
}
if ($kitTree -notmatch 'private void NormalizeParentReferences\(\)[\s\S]*parent < 0 \|\| parent >= count[\s\S]*RemoveAt\(i\)') {
    Fail "KitTree must remove stale parent references after exported node arrays shrink the node list."
}
if ($kitTree -notmatch 'EllipsizeText\(font,\s*txt,\s*small,\s*r\.Size\.X \* 0\.50f\)[\s\S]*DrawText\(font') {
    Fail "KitTree node cost badges must be ellipsized inside their badge bounds before drawing."
}
$spinWheel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs"
if ($spinWheel -notmatch '\[Export\]\s*public string\[\]\s+WedgeLabels') {
    Fail "KitSpinWheel.WedgeLabels must be exported so prize labels can be authored at design time."
}
if ($spinWheel -notmatch 'SetWedges' -or $spinWheel -notmatch 'AddWedge' -or $spinWheel -notmatch 'RefreshWedges' -or $spinWheel -notmatch 'UpdateProcessing\(\)') {
    Fail "KitSpinWheel must expose wedge refresh APIs so runtime prize changes normalize spin state and redraw."
}
if ($spinWheel -notmatch 'NotificationVisibilityChanged[\s\S]*UpdateProcessing\(\)' -or $spinWheel -notmatch 'private bool ShouldAnimate\(\)[\s\S]*_spinning && Wedges\.Count > 0' -or $spinWheel -notmatch 'SetProcess\(IsVisibleInTree\(\) && ShouldAnimate\(\)\)' -or $spinWheel -match 'SetProcess\(true\)') {
    Fail "KitSpinWheel spin animation must process only while visible and actively spinning."
}
if ($spinWheel -notmatch 'SetWedges[\s\S]*List<string> next = NormalizeStrings\(wedges\)' -or $spinWheel -notmatch 'SetWedges[\s\S]*if \(SameStrings\(Wedges,\s*next\)\) return' -or $spinWheel -notmatch 'NormalizeStrings[\s\S]*next\.Add\(value \?\? ""\)' -or $spinWheel -notmatch 'SameStrings[\s\S]*\(left\[i\] \?\? ""\) != right\[i\]') {
    Fail "KitSpinWheel.SetWedges must normalize wedge labels and skip equivalent redraws."
}
if ($spinWheel -notmatch 'string\s+wedge\s*=\s*KitCase\(Wedges\[i\]\)' -or $spinWheel -notmatch 'EllipsizeText\(font,\s*wedge,\s*wf,\s*labelWidth\)') {
    Fail "KitSpinWheel wedge labels must be cased and ellipsized inside each wedge label badge."
}
if ($spinWheel -notmatch 'RefreshWedges[\s\S]*RefreshVisualAndRedraw\(\)' -or $spinWheel -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $spinWheel -notmatch 'Rotation_[\s\S]*Mathf\.IsEqualApprox\(_rot,\s*value\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $spinWheel -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitSpinWheel authored wedge, role, and rotation changes must use guarded visual redraw."
}
$kitSpinner = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinner.cs"
if ($kitSpinner -notmatch 'Kind[\s\S]*if \(_kind == value\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitSpinner -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitSpinner -notmatch 'Progress[\s\S]*Mathf\.IsEqualApprox\(_progress,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitSpinner -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitSpinner -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitSpinner authored kind must relayout once, while role/progress changes use guarded visual redraw."
}
$kitToast = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitToast.cs"
if ($kitToast -notmatch 'override\s+Vector2\s+_GetMinimumSize' -or $kitToast -notmatch 'SetAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)') {
    Fail "KitToast must publish its drawn minimum size to Godot containers."
}
if ($kitToast -notmatch 'RefreshMinimumAndRedraw' -or $kitToast -notmatch 'ToastText' -or $kitToast -notmatch 'LongestLineWidth' -or $kitToast -notmatch 'EstimateWrappedLineCount' -or $kitToast -notmatch 'Message\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitToast -notmatch 'IconGlyph\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)') {
    Fail "KitToast must refresh and derive its minimum size from icon/message text instead of forcing all toasts into a fixed width."
}
if ($kitToast -notmatch 'string\s+text\s*=\s*KitCase\(ToastText\(\)\)') {
    Fail "KitToast must measure and fit the cased toast text it draws."
}
if ($kitToast -notmatch 'Message[\s\S]*if \(_message == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitToast -notmatch 'IconGlyph[\s\S]*if \(_icon == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitToast -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitToast -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitToast -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitToast authored message/icon edits must refresh layout once, while role changes use visual-only redraw."
}
$kitTooltip = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTooltip.cs"
$tooltipComponent = Read "addons/beep_game_builder_cs/ecs/ui/TooltipComponent.cs"
if ($tooltipComponent -notmatch 'public override void _Ready\(\)[\s\S]*SetProcess\(false\)[\s\S]*if \(Engine\.IsEditorHint\(\)\)[\s\S]*return;' -or
    $tooltipComponent -notmatch 'OnMouseEntered[\s\S]*string\.IsNullOrEmpty\(TooltipText\)[\s\S]*SetProcess\(true\)' -or
    $tooltipComponent -notmatch 'public override void _Process\(double delta\)[\s\S]*if \(_showing\)[\s\S]*!_control\.IsVisibleInTree\(\)[\s\S]*HideTooltip\(\)' -or
    $tooltipComponent -notmatch 'public override void _Process\(double delta\)[\s\S]*SetProcess\(false\)[\s\S]*ShowTooltip\(\)' -or
    $tooltipComponent -notmatch 'ShowTooltip\(\)[\s\S]*_showing = true;[\s\S]*SetProcess\(true\)' -or
    $tooltipComponent -notmatch 'HideTooltip\(\)[\s\S]*SetProcess\(false\)' -or
    $tooltipComponent -match 'NotificationVisibilityChanged') {
    Fail "TooltipComponent must be runtime-only and keep _Process enabled only while a tooltip hover delay or visible tooltip needs monitoring."
}
if ($kitTooltip -notmatch 'RefreshMinimumAndRedraw' -or $kitTooltip -notmatch 'LongestLineWidth' -or $kitTooltip -notmatch 'EstimateWrappedLineCount' -or $kitTooltip -notmatch 'TailSizeFor' -or $kitTooltip -notmatch 'Text[\s\S]*if \(_text == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitTooltip -notmatch 'Tail\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitTooltip -notmatch 'TailOffset[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitTooltip -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitTooltip -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitTooltip must refresh and derive its minimum size from wrapped text and tail orientation."
}
$kitPager = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs"
if ($kitPager -notmatch 'PageCount[\s\S]*previousPage = _page' -or $kitPager -notmatch '_page = Mathf\.Clamp\(_page,\s*0,\s*_count - 1\)' -or $kitPager -notmatch 'EmitSignal\(SignalName\.PageChanged,\s*_page\)') {
    Fail "KitPager.PageCount must clamp and emit PageChanged when the page count shrinks below the current page."
}
if ($kitPager -notmatch 'PageCount[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*EmitSignal\(SignalName\.PageChanged,\s*_page\)' -or $kitPager -notmatch 'Page[\s\S]*if \(v == _page\) return[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*EmitSignal\(SignalName\.PageChanged,\s*v\)' -or $kitPager -notmatch 'ShowJump[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitPager -notmatch 'MaxDots[\s\S]*if \(_maxDots == next\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPager -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitPager -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitPager page count/show-jump edits must use correct layout or visual refresh paths."
}
if ($kitPager -notmatch 'EllipsizeText\(font,\s*t,\s*tf,\s*mid\.Size\.X \* 0\.90f\)[\s\S]*DrawText\(font') {
    Fail "KitPager numeric readout must be ellipsized inside the middle pager bound."
}
$heartRow = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitHeartRow.cs"
if ($heartRow -notmatch 'MaxHearts[\s\S]*_value = Mathf\.Clamp\(_value,\s*0,\s*_max\)') {
    Fail "KitHeartRow.MaxHearts must clamp the current value when the maximum shrinks."
}
if ($heartRow -notmatch 'MaxHearts[\s\S]*RefreshMinimumAndRedraw\(\)' -or $heartRow -notmatch 'HeartSize[\s\S]*RefreshMinimumAndRedraw\(\)' -or $heartRow -notmatch 'Spacing[\s\S]*RefreshMinimumAndRedraw\(\)' -or $heartRow -notmatch 'Value[\s\S]*Mathf\.IsEqualApprox\(_value,\s*next\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $heartRow -notmatch 'FillRole[\s\S]*RefreshVisualAndRedraw\(\)' -or $heartRow -notmatch 'DrawBackplate[\s\S]*RefreshVisualAndRedraw\(\)' -or $heartRow -notmatch 'RefreshMinimumAndRedraw[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $heartRow -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitHeartRow authored health, size, role, and backplate edits must use the correct layout or visual refresh path."
}
$levelPath = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs"
if ($levelPath -notmatch 'NormalizeCurrent' -or $levelPath -notmatch 'Current[\s\S]*int next = NormalizeCurrent\(value\)' -or $levelPath -notmatch 'NormalizeCurrent\(int value,\s*int levelCount\)[\s\S]*levelCount == 0[\s\S]*\? -1') {
    Fail "KitLevelPath.Current must normalize authored current indices against the level list."
}
$kitChip = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitChip.cs"
if ($kitChip -notmatch 'TextWidth' -or $kitChip -notmatch 'DeltaText' -or $kitChip -notmatch 'RefreshMinimumAndRedraw' -or $kitChip -notmatch 'Text\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $kitChip -notmatch 'Delta\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)') {
    Fail "KitChip must relayout from visible text and delta changes instead of shrinking/clipping inside a fixed chip width."
}
if ($kitChip -notmatch 'Kind[\s\S]*if \(_kind == value\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitChip -notmatch 'Text[\s\S]*if \(_text == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitChip -notmatch 'Delta[\s\S]*Mathf\.IsEqualApprox\(_delta,\s*value\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitChip -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitChip -notmatch 'Positive[\s\S]*if \(_positive == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitChip -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitChip -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitChip authored kind/text/delta edits must refresh layout once, while role and positive state use guarded visual redraw."
}
if ($kitChip -notmatch 'string\s+draw\s*=\s*KitCase\(text\)' -or $kitChip -notmatch 'GetStringSize\(draw') {
    Fail "KitChip natural width must measure cased chip text."
}
if ($kitChip -notmatch 'EllipsizeText\(font,\s*txt,\s*dfs,\s*deltaTextWidth\)' -or $kitChip -notmatch 'EllipsizeText\(font,\s*text,\s*size,\s*textWidth\)') {
    Fail "KitChip must ellipsize both delta and regular chip text inside their actual draw boxes."
}
$buildTile = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitBuildTile.cs"
if ($buildTile -notmatch 'EllipsizeText\(font,\s*draw,\s*fs,\s*r\.Size\.X\)' -or $buildTile -notmatch 'EllipsizeText\(font,\s*owned,\s*bfs,\s*textWidth\)') {
    Fail "KitBuildTile must ellipsize caption/cost and owned badge text inside their draw boxes."
}
if ($buildTile -notmatch 'override\s+Vector2\s+_GetMinimumSize' -or $buildTile -notmatch 'RefreshMinimumAndRedraw' -or $buildTile -notmatch 'Caption\s*\{[\s\S]*RefreshMinimumAndRedraw\(\)' -or $buildTile -notmatch 'CostText\s*\{[\s\S]*RefreshMinimumAndRedraw\(\)' -or $buildTile -notmatch 'OwnedText\s*\{[\s\S]*RefreshMinimumAndRedraw\(\)') {
    Fail "KitBuildTile must publish and refresh a content-driven minimum size for its drawn caption, cost, and owned badge text."
}
if ($buildTile -notmatch 'Accent[\s\S]*if \(_accent == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $buildTile -notmatch 'TileIcon[\s\S]*if \(_tileIcon == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $buildTile -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $buildTile -notmatch 'ApplyFixedSize[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $buildTile -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $buildTile -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitBuildTile authored icon/accent/fixed-size/text/theme edits must use guarded visual or layout refresh paths."
}
$itemCard = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs"
if ($itemCard -notmatch 'text\s*=\s*KitCase\(text\)[\s\S]*FitRole\(this,\s*UiSurface\.TextRole\.Small,\s*r\.Size \* 0\.76f,\s*text,\s*font' -or $itemCard -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*r\.Size\.X \* 0\.76f\)' -or $itemCard -notmatch 'text\s*=\s*KitCase\(text\)[\s\S]*FitRole\(this,\s*role,\s*r\.Size,\s*text,\s*font' -or $itemCard -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*r\.Size\.X\)') {
    Fail "KitItemCard must case before measuring and ellipsize badge, title, and compact fitted text inside card bounds."
}
$removableChip = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRemovableChip.cs"
if ($removableChip -notmatch 'override\s+Vector2\s+_GetMinimumSize' -or $removableChip -notmatch 'ChipText\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $removableChip -notmatch 'Removable\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $removableChip -notmatch 'SetAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)') {
    Fail "KitRemovableChip must publish and refresh its content-driven minimum size for Godot containers."
}
if ($removableChip -notmatch 'EllipsizeText\(font,\s*text,\s*fs,\s*textBox\.Size\.X\)') {
    Fail "KitRemovableChip must ellipsize chip text inside the remove affordance reserve."
}
if ($removableChip -notmatch 'InputEventKey[\s\S]*RemovePressed[\s\S]*InputEventMouseButton \{ Pressed: true, ButtonIndex: MouseButton\.Left \}[\s\S]*RemovePressed[\s\S]*base\._GuiInput\(@event\)' -or $removableChip -match 'public override void _GuiInput\(InputEvent @event\)\s*\{\s*base\._GuiInput\(@event\)') {
    Fail "KitRemovableChip must handle keyboard/delete and close-X removal before delegating to Button base input."
}
if ($removableChip -notmatch 'ChipText[\s\S]*if \(_text == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $removableChip -notmatch 'Removable[\s\S]*if \(_removable == value\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $removableChip -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $removableChip -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $removableChip -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)' -or $removableChip -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitRemovableChip authored text/removable edits must refresh layout once, while role changes use visual-only redraw."
}
$hudText = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitHudText.cs"
if ($hudText -notmatch 'EllipsizeText\(font,\s*draw,\s*fs,\s*box\.Size\.X\)') {
    Fail "KitHudText must ellipsize bounded HUD text after fitting."
}
if ($hudText -notmatch 'Text[\s\S]*if \(_text == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $hudText -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $hudText -notmatch 'Accent[\s\S]*if \(_accent == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $hudText -notmatch 'ShowPlate[\s\S]*if \(_showPlate == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $hudText -notmatch 'Align[\s\S]*if \(_align == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $hudText -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $hudText -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitHudText authored text/role edits must refresh layout once, while accent/plate/alignment use guarded visual redraw."
}
$tableCell = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitTableCell.cs"
if ($tableCell -notmatch 'EllipsizeText\(font,\s*draw,\s*fs,\s*box\.Size\.X\)') {
    Fail "KitTableCell must ellipsize bounded cell text after fitting."
}
if ($tableCell -notmatch 'CellText[\s\S]*if \(_text == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $tableCell -notmatch 'Role[\s\S]*if \(_role == value\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $tableCell -notmatch 'Align[\s\S]*if \(_align == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $tableCell -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $tableCell -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitTableCell authored text/role edits must refresh layout once, while alignment uses guarded visual redraw."
}
$labelValue = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLabelValue.cs"
if ($labelValue -notmatch 'TextWidth' -or $labelValue -notmatch 'RefreshMinimumAndRedraw' -or $labelValue -notmatch 'Label\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $labelValue -notmatch 'Value\s*\{[^\r\n]*RefreshMinimumAndRedraw\(\)' -or $labelValue -notmatch 'LabelValueRatio[\s\S]*RefreshMinimumAndRedraw\(\)') {
    Fail "KitLabelValue must relayout from label/value text and ratio changes instead of forcing text into a fixed minimum width."
}
$kitRow = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs"
if ($kitRow -notmatch 'TextWidth' -or $kitRow -notmatch 'RefreshMinimumAndRedraw' -or $kitRow -notmatch 'Rank\s*\{[^\r\n]*SetText\(ref\s+_rank' -or $kitRow -notmatch 'Title\s*\{[^\r\n]*SetText\(ref\s+_title' -or $kitRow -notmatch 'Subtitle\s*\{[^\r\n]*SetText\(ref\s+_sub' -or $kitRow -notmatch 'Value\s*\{[^\r\n]*SetText\(ref\s+_value' -or $kitRow -notmatch 'State_\s*\{[^\r\n]*SetText\(ref\s+_state' -or $kitRow -notmatch 'SetText[\s\S]*if \(target == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)') {
    Fail "KitRow must publish a content-driven minimum size and guard no-op rank, title, subtitle, value, and state text edits."
}
if ($kitRow -notmatch 'StateRole[\s\S]*if \(_stateRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitRow -notmatch 'Selected[\s\S]*if \(_sel == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitRow -notmatch 'Alternate[\s\S]*if \(_alternate == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitRow -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitRow authored role, selection, and alternate visual state must use guarded visual-only redraw."
}
$colorOverlay = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitColorOverlay.cs"
if ($colorOverlay -notmatch 'Color[\s\S]*if \(_color == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $colorOverlay -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitColorOverlay color edits must avoid duplicate redraws and use the visual refresh path."
}
$inventorySlot = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs"
if ($inventorySlot -notmatch 'Icon[\s\S]*if \(_icon == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'Count[\s\S]*Mathf\.Max\(0,\s*value\)[\s\S]*if \(_count == next\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'Rarity[\s\S]*if \(_rarity == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'Locked[\s\S]*if \(_locked == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'Requirement[\s\S]*string next = value \?\? ""[\s\S]*if \(_requirement == next\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'GhostIcon[\s\S]*if \(_ghost == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'Selected[\s\S]*if \(_selected == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $inventorySlot -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitInventorySlot authored visual state must use guarded visual-only redraws."
}
$kitPanel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitPanel.cs"
if ($kitPanel -notmatch 'OverrideBannerShape[\s\S]*if \(_overrideBannerShape == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'BannerShape[\s\S]*if \(_bannerShape == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'BannerShade[\s\S]*Mathf\.Abs\(_bannerShade - value\) < 0\.001f[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'ShowWell[\s\S]*if \(_showWell == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'TargetPath[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'TargetPadding[\s\S]*if \(_targetPadding == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'TornEdge[\s\S]*if \(_tornEdge == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'ShowClose[\s\S]*ApplyMouseFilterDefault\(\)[\s\S]*ApplyCloseFocusDefault\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitPanel -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitPanel authored chrome and theme changes must use guarded visual or layout refresh paths."
}
if ($kitPanel -notmatch 'public override void _Process\(double delta\)[\s\S]*Size = size[\s\S]*RefreshAutoMinimumSize\(this,\s*size\)[\s\S]*QueueRedraw\(\)' -or $kitPanel -match 'public override void _Process\(double delta\)[\s\S]*SetAutoMinimumSize\(this,\s*size\)') {
    Fail "KitPanel target-fit resizing must notify Godot layout when it updates the kit-owned minimum size."
}
if ($kitPanel -notmatch 'private bool ShouldFitTarget\(\)[\s\S]*!_targetPath\.IsEmpty && IsVisibleInTree\(\)' -or
    $kitPanel -notmatch 'UpdateTargetFitProcessing[\s\S]*SetProcess\(ShouldFitTarget\(\)\)' -or
    $kitPanel -notmatch 'NotificationVisibilityChanged[\s\S]*UpdateTargetFitProcessing\(\)' -or
    $kitPanel -notmatch 'public override void _Process\(double delta\)[\s\S]*if \(!ShouldFitTarget\(\)\)[\s\S]*UpdateTargetFitProcessing\(\)') {
    Fail "KitPanel target-fit processing must stop while hidden and resume only when visible with a target path."
}
$kitCollapsiblePanel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs"
if ($kitCollapsiblePanel -notmatch 'Collapsed[\s\S]*if \(_collapsed == value\) return[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*EmitSignal\(SignalName\.Toggled,\s*value\)' -or $kitCollapsiblePanel -notmatch 'Title[\s\S]*string next = value \?\? ""[\s\S]*if \(_title == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitCollapsiblePanel -notmatch 'BannerShade[\s\S]*Mathf\.Abs\(_bannerShade - value\) < 0\.001f[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitCollapsiblePanel -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)' -or $kitCollapsiblePanel -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitCollapsiblePanel authored collapse/header edits must use guarded visual or layout refresh paths."
}
$exportSignalsAreSceneSafe =
    $arrowSelector -match 'Current[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*if \(IsInsideTree\(\)\)[\s\S]*EmitSignal\(SignalName\.OptionChanged,\s*v\)' -and
    $segmentedGroup -match 'Current[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*if \(IsInsideTree\(\)\)[\s\S]*EmitSignal\(SignalName\.SegmentChanged,\s*v\)' -and
    $kitPager -match 'PageCount[\s\S]*_page != previousPage && IsInsideTree\(\)[\s\S]*EmitSignal\(SignalName\.PageChanged,\s*_page\)' -and
    $kitPager -match 'Page[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*if \(IsInsideTree\(\)\)[\s\S]*EmitSignal\(SignalName\.PageChanged,\s*v\)' -and
    $kitCollapsiblePanel -match 'Collapsed[\s\S]*RefreshVisualAndRedraw\(\)[\s\S]*if \(IsInsideTree\(\)\)[\s\S]*EmitSignal\(SignalName\.Toggled,\s*value\)'
if (-not $exportSignalsAreSceneSafe) {
    Fail "Kit controls with exported selection/collapse/page properties must not emit gameplay signals during scene deserialization."
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs"; Name = "KitCollapsiblePanel" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs"; Name = "KitGemSlot" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitHeartRow.cs"; Name = "KitHeartRow" }
)) {
    $source = Read $entry.Path
    if ($source -match 'override void _Notification\(int what\)[\s\S]*NotificationThemeChanged') {
        Fail "$($entry.Name) must rely on KitControl's theme-change refresh instead of duplicating layout/redraw work."
    }
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitIconButton.cs"; Property = "Accent"; Field = "_accent" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs"; Property = "Role"; Field = "_role" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitOptionButton.cs"; Property = "Accent"; Field = "_accent" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs"; Property = "Accent"; Field = "_accent" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs"; Property = "Fill"; Field = "_fill" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitSliderBar.cs"; Property = "Accent"; Field = "_accent" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs"; Property = "Role"; Field = "_role" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs"; Property = "Role"; Field = "_role" }
)) {
    $source = Read $entry.Path
    $pattern = [regex]::Escape($entry.Property) + '[\s\S]*if \(' + [regex]::Escape($entry.Field) + ' == value\) return[\s\S]*RefreshVisualAndRedraw\(\)'
    if ($source -notmatch $pattern -or $source -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
        Fail "$($entry.Path) $($entry.Property) setter must use guarded visual-only redraw."
    }
}
foreach ($path in @(
    "addons/beep_game_builder_cs/ecs/ui/kit/KitIconButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitOptionButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitSliderBar.cs",
    "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs"
)) {
    $source = Read $path
    if ($source -notmatch 'NotificationThemeChanged[\s\S]*RefreshAutoMinimumSize\(this,\s*_GetMinimumSize\(\)\)[\s\S]*UpdateMinimumSize\(\)[\s\S]*(RefreshVisualAndRedraw|QueueRedraw)\(\)') {
        Fail "$path theme changes must refresh Godot minimum size before redraw."
    }
}
$starRating = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs"
if ($starRating -notmatch 'Earned[\s\S]*Mathf\.Clamp\(value,\s*0,\s*\(int\)MaxValue\)[\s\S]*if \(Mathf\.IsEqualApprox\(\(float\)Value,\s*next\)\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $starRating -notmatch 'Total[\s\S]*RefreshMinimumAndRedraw\(\)' -or $starRating -notmatch 'RefreshMinimumAndRedraw[\s\S]*UpdateMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitStarRating total changes must relayout and earned changes must use guarded visual redraw."
}
if ($starRating -notmatch 'get => Mathf\.Clamp\(\(int\)MaxValue,\s*1,\s*10\)' -or
    $starRating -notmatch 'int next = Mathf\.Clamp\(value,\s*1,\s*10\)' -or
    $starRating -notmatch '_totalExplicitlySet = true' -or
    $starRating -notmatch 'Mathf\.IsEqualApprox\(\(float\)MaxValue,\s*100f\)[\s\S]*MaxValue = 5' -or
    $starRating -notmatch 'MaxValue = Mathf\.Clamp\(\(float\)MaxValue,\s*1f,\s*10f\)') {
    Fail "KitStarRating must not inherit Range's 100-value default as a 100-star compact minimum."
}
$kitDialogBox = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs"
if ($kitDialogBox -notmatch 'Mathf\.Clamp\(fs \* 26f,\s*280f,\s*340f\)' -or
    $kitDialogBox -notmatch 'Mathf\.Clamp\(LongestLineWidth\(font,\s*body,\s*bodyFs\),\s*fs \* 14f,\s*fs \* 23f\)') {
    Fail "KitDialogBox default minimum width must stay compact-safe while explicit scene sizing can make it wider."
}
$kitBookSpread = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitBookSpread.cs"
if ($kitBookSpread -notmatch 'Mathf\.Clamp\(fs \* 24f \+ TabOutset \* 0\.78f,\s*300f,\s*340f\)' -or
    $kitBookSpread -notmatch 'Mathf\.Max\(fs \* 14f,\s*190f\)') {
    Fail "KitBookSpread default minimum width must stay compact-safe while explicit scene sizing can make it wider."
}
$compactMinimumProbe = Read "tests/kit_compact_minimum_probe.gd"
if ($compactMinimumProbe -notmatch 'MAX_COMPACT_WIDTH := 360\.0' -or
    $compactMinimumProbe -notmatch 'KitStarRating' -or
    $compactMinimumProbe -notmatch 'KitDialogBox' -or
    $compactMinimumProbe -notmatch 'KitBookSpread' -or
    $compactMinimumProbe -notmatch 'get_combined_minimum_size\(\)' -or
    $compactMinimumProbe -notmatch 'Default minimum width exceeds') {
    Fail "kit_compact_minimum_probe.gd must instantiate compact-relevant controls and reject oversized default minimum widths."
}
if ($runAddonChecks -notmatch 'kit_compact_minimum_probe\.ps1') {
    Fail "run_addon_checks.ps1 must include the kit compact minimum probe."
}
$kitSpinner = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitSpinner.cs"
if ($kitSpinner -notmatch 'Speed[\s\S]*Mathf\.Clamp\(value,\s*0\.1f,\s*4f\)[\s\S]*if \(Mathf\.IsEqualApprox\(_speed,\s*next\)\) return;[\s\S]*RefreshVisualAndRedraw\(\)') {
    Fail "KitSpinner.Speed must clamp, ignore no-op edits, and redraw through the shared visual refresh path."
}
if ($kitSpinner -notmatch 'Kind[\s\S]*UpdateProcessing\(\)[\s\S]*RefreshMinimumAndRedraw\(\)' -or $kitSpinner -notmatch 'Progress[\s\S]*_progress = next;[\s\S]*UpdateProcessing\(\)[\s\S]*RefreshVisualAndRedraw\(\)' -or $kitSpinner -notmatch 'private bool ShouldAnimate\(\)[\s\S]*Kind != SpinnerKind\.Bar \|\| Progress < 0f' -or $kitSpinner -notmatch '_Ready\(\)[\s\S]*UpdateProcessing\(\)' -or $kitSpinner -notmatch 'NotificationVisibilityChanged[\s\S]*UpdateProcessing\(\)' -or $kitSpinner -notmatch 'SetProcess\(IsVisibleInTree\(\) && ShouldAnimate\(\)\)') {
    Fail "KitSpinner must disable idle _Process for determinate bar progress and re-enable it only for animated spinner modes."
}
$settingsMenu = Read "addons/beep_game_builder_cs/ecs/scenes/SettingsMenu.cs"
$settingsScene = Read "addons/beep_game_builder_cs/templates/scenes/settings_menu.tscn"
if ($settingsMenu -match 'new\s+KitLabel' -or $settingsMenu -notmatch 'ControlsBindings') {
    Fail "SettingsMenu must bind the authored ControlsBindings label instead of creating KitLabel rows at startup."
}
if ($settingsMenu -match 'new\s+Godot\.Control' -or $settingsMenu -match 'AddScrollGutter' -or $settingsMenu -match 'row\.AddChild') {
    Fail "SettingsMenu must not create layout spacer controls at startup; row gutters must be authored in the scene."
}
if ($settingsMenu -match 'AddTheme(Constant|Stylebox|Color|FontSize|Icon|Font)Override\s*\(' -or
    $settingsMenu -notmatch 'RepairMissingLayoutDefaults' -or
    $settingsMenu -notmatch 'ApplyShellDefaults\(this\)' -or
    $settingsMenu -notmatch 'SetConstantIfUnset\(content,\s*"separation"' -or
    $settingsMenu -notmatch 'SetConstantIfUnset\(footer,\s*"separation"' -or
    $settingsMenu -notmatch 'SetConstantIfUnset\(row,\s*"separation"' -or
    $settingsMenu -notmatch 'ApplyMinimumIfUnset') {
    Fail "SettingsMenu layout repair must only fill missing design-time defaults using change-aware helpers."
}
if ($settingsScene -notmatch 'node name="ControlsBindings"' -or $settingsScene -notmatch 'script = ExtResource\("k_label"\)') {
    Fail "settings_menu.tscn must own the ControlsBindings KitLabel at design time."
}
foreach ($rowName in @("MasterRow", "SfxRow", "MusicRow", "FullscreenRow", "ResolutionRow", "LanguageRow", "SubtitlesRow", "ScreenShakeRow", "DamageNumbersRow")) {
    if ($settingsScene -notmatch ('node name="ScrollGutter" type="Control" parent="[^"]*/' + $rowName + '"')) {
        Fail "settings_menu.tscn must author a ScrollGutter control under $rowName."
    }
}
$oldSettingsGutters = [regex]::Matches($settingsScene, 'custom_minimum_size = Vector2\(12, 0\)')
$authoredSettingsGutters = [regex]::Matches($settingsScene, 'custom_minimum_size = Vector2\(14, 0\)')
if ($oldSettingsGutters.Count -gt 0 -or $authoredSettingsGutters.Count -lt 9) {
    Fail "settings_menu.tscn must author 14px scroll gutters instead of relying on SettingsMenu startup repair."
}
$kitBrowser = Read "addons/beep_game_builder_cs/ecs/scenes/KitBrowser.cs"
$floatingText = Read "addons/beep_game_builder_cs/ecs/FloatingTextComponent.cs"
$inventoryDisplay = Read "addons/beep_game_builder_cs/ecs/InventoryComponent.Display.cs"
if ($floatingText -match 'AddTheme(Constant|Stylebox|Color|FontSize|Icon|Font)Override\s*\(' -or
    $floatingText -notmatch 'SetColorOverrideIfChanged\(label,\s*"font_color"' -or
    $floatingText -notmatch 'SetFontSizeOverrideIfChanged\(label,\s*"font_size"' -or
    $floatingText -notmatch 'SetConstantOverrideIfChanged\(label,\s*"shadow_offset_x"') {
    Fail "FloatingTextComponent must use KitChrome change-aware theme helpers for generated label chrome."
}
if ($inventoryDisplay -match 'AddTheme(Constant|Stylebox|Color|FontSize|Icon|Font)Override\s*\(' -or
    $inventoryDisplay -notmatch 'SetConstantOverrideIfChanged\(_grid,\s*"h_separation"' -or
    $inventoryDisplay -notmatch 'SetConstantOverrideIfChanged\(_grid,\s*"v_separation"') {
    Fail "InventoryComponent.Display must use KitChrome change-aware theme helpers for generated grid spacing."
}
if ($kitBrowser -match 'sel\.Options\.AddRange' -or $kitBrowser -match 'cur\.Entries\.(Clear|AddRange)' -or $kitBrowser -match 'radar\.(Axes|Values)\.AddRange' -or $kitBrowser -match 'grid\.Slots\.AddRange' -or $kitBrowser -match 'tree\.Nodes\.AddRange' -or $kitBrowser -match 'path\.Levels\.AddRange' -or $kitBrowser -match 'wheel\.Wedges\.(Clear|Add|AddRange)' -or $kitBrowser -match 'tree\.Nodes\[\d+\]\.Parents\.Add') {
    Fail "KitBrowser examples must use the kit collection refresh APIs instead of direct list mutation."
}
if ($kitBrowser -notmatch 'HumanizeCaption' -or $kitBrowser -notmatch 'StartsWith\("Kit"' -or $kitBrowser -notmatch 'char\.IsUpper') {
    Fail "KitBrowser must humanize raw Kit* class captions so the showcase does not wrap labels mid-word."
}
if ($kitBrowser -notmatch 'ProjectSettings\.GlobalizePath' -or $kitBrowser -notmatch 'mono/temp/bin/Debug') {
    Fail "KitBrowser build stamp must fall back to Godot's compiled Mono DLL path instead of showing unknown in normal runs."
}
if ($kitBrowser -notmatch 'private bool CompactLayout\(\)' -or
    $kitBrowser -notmatch 'float width = Size\.X > 0f \? Size\.X : GetViewportRect\(\)\.Size\.X' -or
    $kitBrowser -notmatch 'width < 560f' -or
    $kitBrowser -notmatch 'CallDeferred\(nameof\(DeferredInitialRebuild\)\)' -or
    $kitBrowser -notmatch 'private Container Section\(string title\)' -or
    $kitBrowser -notmatch 'if \(CompactLayout\(\)\)[\s\S]*new VBoxContainer' -or
    $kitBrowser -notmatch 'new HFlowContainer' -or
    $kitBrowser -notmatch 'private static void Card\(Container row') {
    Fail "KitBrowser must use vertical section lists on compact/mobile viewports and flow sections on desktop."
}
if ($kitBrowser -match 'AddTheme(Constant|Stylebox|Color|FontSize|Icon|Font)Override\s*\(' -or
    $kitBrowser -notmatch 'RequireNode<OptionButton>\("GenrePicker"\)' -or
    $kitBrowser -notmatch 'RequireNode<VBoxContainer>\("Content"\)' -or
    $kitBrowser -notmatch 'SetFontSizeOverrideIfChanged\(title,' -or
    $kitBrowser -notmatch 'SetColorOverrideIfChanged\(cap,') {
    Fail "KitBrowser showcase chrome must bind design-time nodes and use KitChrome change-aware theme helpers instead of direct override writes."
}
$kitBrowserLayoutProbe = Read "tests/kit_browser_layout_probe.gd"
if ($kitBrowserLayoutProbe -notmatch 'Mobile browser content requires horizontal scrolling' -or
    $kitBrowserLayoutProbe -notmatch 'get_h_scroll_bar\(\)\.visible') {
    Fail "kit_browser_layout_probe.gd must reject mobile horizontal content overflow and visible horizontal scrollbars."
}
$kitGallery = Read "addons/beep_game_builder_cs/ecs/scenes/KitGallery.cs"
if ($kitGallery -match 'bag\.Slots\.(Clear|AddRange)' -or $kitGallery -match 'tree\.Nodes\.(Clear|AddRange)' -or $kitGallery -match 'tree\.Nodes\[\d+\]\.Parents\.Add') {
    Fail "KitGallery examples must use the kit collection refresh APIs instead of direct list mutation."
}
$kitGalleryScene = Read "addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn"
foreach ($required in @("EquipmentContent", "KitHudText.cs", "KitInputHint.cs", "KitWeatherForecastCard.cs", "StatusText", "Hint", "Weather")) {
    if ($kitGalleryScene -notmatch [regex]::Escape($required)) {
        Fail "kit_gallery.tscn must include authored design-time equipment panel content for $required."
    }
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
    $pattern = 'public\s+[^{}]*\s+' + [regex]::Escape($entry.Property) + '\s*\{.*?(UpdateMinimumSize\(\)|RefreshMinimumAndRedraw\(\)).*?\}'
    if (-not [regex]::IsMatch($source, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Fail "$($entry.Path) $($entry.Property) setter does not notify Godot containers of minimum-size changes."
    }
}

$itemCard = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs"
if ($itemCard -notmatch 'ApplyMinimumSize\(force:\s*true\)' -or $itemCard -notmatch 'UpdateMinimumSize\(\)') {
    Fail "KitItemCard layout changes do not force a smaller/larger minimum-size refresh."
}
foreach ($property in @("Title", "Description", "PriceText", "CountText", "BadgeText")) {
    $pattern = 'public\s+[^{}]*\s+' + [regex]::Escape($property) + '\s*\{.*?SetText\(ref\s+_.*?\}'
    if (-not [regex]::IsMatch($itemCard, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Fail "KitItemCard $property setter must use the shared text refresh path so inspector edits notify Godot layout."
    }
}
if ($itemCard -notmatch 'RefreshContentAndRedraw' -or $itemCard -notmatch 'ApplyMinimumSize\(\)[\s\S]*QueueRedraw\(\)') {
    Fail "KitItemCard text edits must refresh its auto minimum size before redraw."
}
if ($itemCard -notmatch 'Icon\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $itemCard -notmatch 'Accent\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $itemCard -notmatch 'Selected\s*\{[^\r\n]*RefreshVisualAndRedraw\(\)' -or $itemCard -notmatch 'Locked\s*\{[^\r\n]*SetState\(value \? KitState\.Locked : KitState\.Normal\); RefreshVisualAndRedraw\(\)' -or $itemCard -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitItemCard authored icon, accent, selection, and lock edits must use visual-only redraw."
}

$kitLabel = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitLabel.cs"
foreach ($required in @(
    "SetFontOverrideIfChanged",
    "SetFontSizeOverrideIfChanged",
    "SetColorOverrideIfChanged",
    "SetConstantOverrideIfChanged"
)) {
    if ($kitLabel -notmatch [regex]::Escape($required)) {
        Fail "KitLabel must use change-only theme override writes for $required to avoid recursive theme notifications."
    }
}
if ($kitLabel -match 'CallDeferred|_applyQueued|_exiting|_applying') {
    Fail "KitLabel must not rely on deferred/lifecycle guards for text chrome; fix recursive theme writes at the source."
}
if ($kitLabel -notmatch 'bool metricChanged = false' -or
    $kitLabel -notmatch 'bool visualChanged = false' -or
    $kitLabel -notmatch 'bool depthChanged = ApplyTextDepth' -or
    $kitLabel -notmatch 'if \(metricChanged \|\| depthChanged\)[\s\r\n]*UpdateMinimumSize\(\);' -or
    $kitLabel -notmatch 'if \(metricChanged \|\| visualChanged \|\| depthChanged\)[\s\r\n]*QueueRedraw\(\);') {
    Fail "KitLabel must only reflow/redraw after an actual theme override change."
}

$kitFiles = Get-ChildItem -Path (Join-Path $root "addons/beep_game_builder_cs/ecs/ui/kit") -Filter "*.cs" -File
foreach ($file in $kitFiles) {
    if ($file.Name -eq "KitArchetypes.cs") { continue }
    if ($file.Name -eq "KitChrome.cs") { continue }
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match '\[Export[^\r\n]*\][^\r\n]*QueueRedraw\(\)') {
        Fail "$($file.FullName) has an exported setter with inline QueueRedraw(); use a guarded RefreshMinimumAndRedraw or RefreshVisualAndRedraw path."
    }
    if ($source -match 'SkinCatalog\.HasActiveSkin|SkinCatalog\.ActiveGenre') {
        Fail "$($file.FullName) reads SkinCatalog active genre directly; kit controls must resolve genre through KitChrome.GenreOf(this)."
    }
    if ($source -match 'CustomMinimumSize' -and $source -notmatch 'override\s+Vector2\s+_GetMinimumSize') {
        Fail "$($file.FullName) sets CustomMinimumSize but does not expose _GetMinimumSize()."
    }
}
$kitControlsWithoutMinimum = @()
foreach ($file in $kitFiles) {
    if ($file.Name -eq "KitChrome.cs") { continue }
    $source = Get-Content -Path $file.FullName -Raw
    if ($source -match ':\s*KitControl' -and $source -notmatch 'override\s+Vector2\s+_GetMinimumSize\s*\(') {
        $kitControlsWithoutMinimum += $file.Name
    }
}
if ($kitControlsWithoutMinimum.Count -gt 0) {
    Fail "KitControl widgets must expose a natural _GetMinimumSize for design-time containers: $($kitControlsWithoutMinimum -join ', ')."
}
foreach ($entry in @(
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitHudText.cs"; Property = "Text" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitHudText.cs"; Property = "Role" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTableCell.cs"; Property = "CellText" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitTableCell.cs"; Property = "Role" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitWeatherForecastCard.cs"; Property = "DayText" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitWeatherForecastCard.cs"; Property = "WeatherGlyph" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitWeatherForecastCard.cs"; Property = "TemperatureText" },
    @{ Path = "addons/beep_game_builder_cs/ecs/ui/kit/KitWeatherForecastCard.cs"; Property = "WindText" }
)) {
    $source = Read $entry.Path
    $pattern = 'public\s+[^{}]*\s+' + [regex]::Escape($entry.Property) + '\s*\{.*?RefreshMinimumAndRedraw\(\).*?\}'
    if (-not [regex]::IsMatch($source, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Fail "$($entry.Path) $($entry.Property) setter must refresh minimum size for design-time text changes."
    }
}
$weatherCard = Read "addons/beep_game_builder_cs/ecs/ui/kit/KitWeatherForecastCard.cs"
if ($weatherCard -notmatch 'DrawCentered' -or $weatherCard -notmatch 'EllipsizeText\(font,\s*draw,\s*size,\s*r\.Size\.X\)') {
    Fail "KitWeatherForecastCard must ellipsize each centered text band inside its actual draw rectangle."
}
if ($weatherCard -notmatch 'DayText[\s\S]*if \(_dayText == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $weatherCard -notmatch 'WeatherGlyph[\s\S]*if \(_weatherGlyph == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $weatherCard -notmatch 'TemperatureText[\s\S]*if \(_temperatureText == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $weatherCard -notmatch 'WindText[\s\S]*if \(_windText == next\) return[\s\S]*RefreshMinimumAndRedraw\(\)' -or $weatherCard -notmatch 'WeatherRole[\s\S]*if \(_weatherRole == value\) return[\s\S]*RefreshVisualAndRedraw\(\)' -or $weatherCard -notmatch 'RefreshVisualAndRedraw[\s\S]*QueueRedraw\(\)') {
    Fail "KitWeatherForecastCard authored text must avoid duplicate layout refreshes, while weather role uses visual-only redraw."
}

$tmpIgnorePath = Join-Path $root "tmp/.gdignore"
if (-not (Test-Path $tmpIgnorePath)) { Fail "tmp/.gdignore is missing; Godot will scan generated probe renders and may report duplicate UIDs." }

Write-Host "[addon-contract] OK: tween presets, beep_ui preset registry, MCP lifecycle, editor dock API, data binders, token defaults, citybuilder Oilfield Days skin, panel headers, kit focus, wrapped text, minimum sizes, reusable size contracts, size invalidation, runtime harness, and generated-output ignores are consistent."
