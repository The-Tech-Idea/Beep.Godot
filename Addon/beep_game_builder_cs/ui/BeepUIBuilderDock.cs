using Godot;
using System;

[Tool]
public partial class BeepUIBuilderDock : VBoxContainer
{
    private TextEdit _output;
    private CheckBox _overwriteCb;

    public override void _Ready()
    {
        Name = "Beep UI Builder";
        BuildUI();
    }

    private void BuildUI()
    {
        var title = new Label { Text = "Beep UI Builder (C#)", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        AddChild(title);

        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(tabs);

        AddDataTab(tabs);
        AddHudTab(tabs);
        AddCanvasTab(tabs);
        AddScriptTab(tabs);

        _overwriteCb = new CheckBox { Text = "Overwrite Existing Files" };
        AddChild(_overwriteCb);

        _output = new TextEdit { CustomMinimumSize = new Vector2(0, 140), Editable = false, PlaceholderText = "Output..." };
        AddChild(_output);
    }

    private void Log(string msg) => _output.Text += msg + "\n";

    private ScrollContainer MakeTab(TabContainer tabs, string title)
    {
        var s = new ScrollContainer { Name = title, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; s.AddChild(b); tabs.AddChild(s); return s;
    }
    private static VBoxContainer Box(ScrollContainer s) => s.GetChild(0) as VBoxContainer;
    private static Button Btn(Node p, string t, System.Action a) { var b = new Button { Text = t }; b.Pressed += a; p.AddChild(b); return b; }
    private static Label Lbl(Node p, string t) { var l = new Label { Text = t }; p.AddChild(l); return l; }

    private void AddDataTab(TabContainer tabs)
    {
        var b = Box(MakeTab(tabs, "Core"));
        Lbl(b, "Data Binding, Architecture & Infra");
        Btn(b, "Generate BeepDataBinder", () => Gen("res://scripts/ui/BeepDataBinder.cs", "// BeepDataBinder"));
        Btn(b, "Generate BeepDataGrid", () => Gen("res://scripts/ui/BeepDataGrid.cs", "// BeepDataGrid"));
        Btn(b, "Generate BeepFormBuilder", () => Gen("res://scripts/ui/BeepFormBuilder.cs", "// BeepFormBuilder"));
        Btn(b, "Generate BeepTreeView", () => Gen("res://scripts/ui/BeepTreeView.cs", "// BeepTreeView"));
        Btn(b, "Generate BeepDropdown", () => Gen("res://scripts/ui/BeepDropdown.cs", "// BeepDropdown"));
        Btn(b, "Generate BeepKeybindManager", () => Gen("res://scripts/ui/BeepKeybindManager.cs", "// BeepKeybindManager"));
        Btn(b, "Generate BeepStateMachine + EventBus", () => Gen("res://scripts/ui/BeepStateMachine.cs", "// BeepStateMachine + BeepEventBus"));
        Btn(b, "Generate BeepPool + SaveManager", () => Gen("res://scripts/ui/BeepPoolSaveManager.cs", "// BeepPoolManager + BeepSaveManager"));
        Btn(b, "Generate BeepAudioManager", () => Gen("res://scripts/ui/BeepAudioManager.cs", "// BeepAudioManager"));
        Btn(b, "Generate BeepLocalization", () => Gen("res://scripts/ui/BeepLocalization.cs", "// BeepLocalization + LocalizedLabel"));
        Btn(b, "Generate BeepCoroutine", () => Gen("res://scripts/ui/BeepCoroutine.cs", "// BeepCoroutine"));
        Btn(b, "Generate BeepConfigManager + InputBuffer", () => Gen("res://scripts/ui/BeepConfigManager.cs", "// BeepConfigManager + InputBuffer"));
        Btn(b, "Generate BeepWeightedTable", () => Gen("res://scripts/ui/BeepWeightedTable.cs", "// BeepWeightedTable - loot tables"));
        Btn(b, "Generate BeepCommandHistory", () => Gen("res://scripts/ui/BeepCommandHistory.cs", "// BeepCommandHistory - undo/redo"));
        Btn(b, "Generate BeepServiceLocator + GridNav + TweenChain + Math", () => Gen("res://scripts/ui/BeepServiceLocator.cs", "// BeepServiceLocator + GridNavigator + TweenChain + MathHelper"));
        Btn(b, "Generate ALL Core Scripts", () => {
            foreach (var n in ALL_CORE) GenAny(n); Log("All core scripts generated.");
        });
    }

    private void AddHudTab(TabContainer tabs)
    {
        var b = Box(MakeTab(tabs, "HUD"));
        Lbl(b, "HUD Components");
        Btn(b, "Generate BeepHealthBar", () => GenHud("BeepHealthBar"));
        Btn(b, "Generate BeepMinimap", () => GenHud("BeepMinimap"));
        Btn(b, "Generate BeepScoreDisplay", () => GenHud("BeepScoreDisplay"));
        Btn(b, "Generate BeepAmmoDisplay", () => GenHud("BeepAmmoDisplay"));
        Btn(b, "Generate BeepCompass", () => GenHud("BeepCompass"));
        Btn(b, "Generate BeepCrosshair", () => GenHud("BeepCrosshair"));
        Btn(b, "Generate BeepTimer", () => GenHud("BeepTimer"));
        Btn(b, "Generate BeepInteractionPrompt", () => GenHud("BeepInteractionPrompt"));
        Btn(b, "Generate BeepQuestLog", () => GenHud("BeepQuestLog"));
        Btn(b, "Generate BeepNotifications", () => Gen("res://scripts/hud/BeepNotifications.cs", "// Notifications"));
        Btn(b, "Generate BeepFloatingDamage", () => GenHud("BeepFloatingDamage"));
        Btn(b, "Generate BeepConsoleLog", () => GenHud("BeepConsoleLog"));
        Btn(b, "Generate BeepVirtualJoystick", () => GenHud("BeepVirtualJoystick"));
        Btn(b, "Generate BeepInputHints", () => GenHud("BeepInputHints"));
        Btn(b, "Generate BeepKillFeed", () => GenHud("BeepKillFeed"));
        Btn(b, "Generate BeepRespawnOverlay", () => GenHud("BeepRespawnOverlay"));
        Btn(b, "Generate BeepObjectiveMarkers", () => GenHud("BeepObjectiveMarkers"));
        Btn(b, "Generate BeepDebugOverlay", () => GenHud("BeepDebugOverlay"));
        Btn(b, "Generate BeepHitIndicator", () => GenHud("BeepHitIndicator"));
        Btn(b, "Generate BeepSpectatorLabel", () => GenHud("BeepSpectatorLabel"));
        Btn(b, "Generate BeepSegmentedProgress", () => GenHud("BeepSegmentedProgress"));
        Btn(b, "Generate BeepEdgeIndicator", () => GenHud("BeepEdgeIndicator"));
        Btn(b, "Generate BeepWeaponWheel", () => GenHud("BeepWeaponWheel"));
        Btn(b, "Generate BeepSubtitles", () => GenHud("BeepSubtitles"));
        Btn(b, "Generate BeepVignette", () => GenHud("BeepVignette"));
        Btn(b, "Generate BeepSpeedometer", () => GenHud("BeepSpeedometer"));
        Btn(b, "Generate BeepAltitudeMeter", () => GenHud("BeepAltitudeMeter"));
        Btn(b, "Generate BeepLeaderboard", () => GenHud("BeepLeaderboard"));
        Btn(b, "Generate BeepChatBox", () => GenHud("BeepChatBox"));
        Btn(b, "Generate BeepPickupLog", () => GenHud("BeepPickupLog"));
        Btn(b, "Generate BeepBossHealthBar", () => GenHud("BeepBossHealthBar"));
        Btn(b, "Generate BeepCooldownIndicator", () => GenHud("BeepCooldownIndicator"));
        Btn(b, "Generate BeepStatusEffectIcons", () => GenHud("BeepStatusEffectIcons"));
        Btn(b, "Generate BeepWaveCounter", () => GenHud("BeepWaveCounter"));
        Btn(b, "Generate BeepComboCounter", () => GenHud("BeepComboCounter"));
        Btn(b, "Generate BeepAccuracyDisplay", () => GenHud("BeepAccuracyDisplay"));
        Btn(b, "Generate BeepMatchTimer", () => GenHud("BeepMatchTimer"));
        Btn(b, "Generate BeepTeammatePanel", () => GenHud("BeepTeammatePanel"));
        Btn(b, "Generate BeepSkillTree", () => GenHud("BeepSkillTree"));
        Btn(b, "Generate BeepCraftingMenu", () => GenHud("BeepCraftingMenu"));
        Btn(b, "Generate BeepLootPopup", () => GenHud("BeepLootPopup"));
        Btn(b, "Generate BeepDamagePreview", () => GenHud("BeepDamagePreview"));
        Btn(b, "Generate BeepZoneWarning", () => GenHud("BeepZoneWarning"));
        Btn(b, "Generate BeepReticle + PingSystem + MiniScoreboard", () => GenHud("BeepReticlePing"));
        Btn(b, "Generate ALL HUD Scripts", () => {
            foreach (var n in ALL_HUD) GenHud(n); Log("All HUD scripts generated.");
        });
    }

    private void AddCanvasTab(TabContainer tabs)
    {
        var b = Box(MakeTab(tabs, "Canvas & FX"));
        Lbl(b, "Canvas Utilities & Screen FX");
        Btn(b, "Generate BeepCanvasAnchor", () => GenCanvas("BeepCanvasAnchor"));
        Btn(b, "Generate BeepSafeArea", () => GenCanvas("BeepSafeArea"));
        Btn(b, "Generate BeepScreenFX", () => GenCanvas("BeepScreenFX"));
        Btn(b, "Generate BeepScreenShake", () => GenCanvas("BeepScreenShake"));
        Btn(b, "Generate BeepSceneTransition", () => GenCanvas("BeepSceneTransition"));
        Btn(b, "Generate BeepTooltip", () => GenCanvas("BeepTooltip"));
        Btn(b, "Generate BeepDragDrop", () => GenCanvas("BeepDragDrop"));
        Btn(b, "Generate BeepTabPanel", () => GenCanvas("BeepTabPanel"));
        Btn(b, "Generate BeepContextMenu", () => GenCanvas("BeepContextMenu"));
        Btn(b, "Generate BeepAccordion", () => GenCanvas("BeepAccordion"));
        Btn(b, "Generate BeepRadialMenu", () => GenCanvas("BeepRadialMenu"));
        Btn(b, "Generate BeepCarousel", () => GenCanvas("BeepCarousel"));
        Btn(b, "Generate BeepWizard", () => GenCanvas("BeepWizard"));
        Btn(b, "Generate BeepThemeManager", () => GenCanvas("BeepThemeManager"));
        Btn(b, "Generate BeepInventoryGrid", () => GenCanvas("BeepInventoryGrid"));
        Btn(b, "Generate BeepSpriteAnim", () => GenCanvas("BeepSpriteAnim"));
        Btn(b, "Generate BeepButtonGroup", () => GenCanvas("BeepButtonGroup"));
        Btn(b, "Generate BeepParallaxBackground", () => GenCanvas("BeepParallaxBackground"));
        Btn(b, "Generate BeepMarquee", () => GenCanvas("BeepMarquee"));
        Btn(b, "Generate BeepShimmer", () => GenCanvas("BeepShimmer"));
        Btn(b, "Generate BeepGradientBackground", () => GenCanvas("BeepGradientBackground"));
        Btn(b, "Generate BeepAspectRatioContainer", () => GenCanvas("BeepAspectRatioContainer"));
        Btn(b, "Generate BeepGridView", () => GenCanvas("BeepGridView"));
        Btn(b, "Generate BeepTypewriterLabel", () => GenCanvas("BeepTypewriterLabel"));
        Btn(b, "Generate BeepParticleUI", () => GenCanvas("BeepParticleUI"));
        Btn(b, "Generate BeepScanlines", () => GenCanvas("BeepScanlines"));
        Btn(b, "Generate BeepBlurPanel", () => GenCanvas("BeepBlurPanel"));
        Btn(b, "Generate BeepRippleEffect", () => GenCanvas("BeepRippleEffect"));
        Btn(b, "Generate BeepPulseRing", () => GenCanvas("BeepPulseRing"));
        Btn(b, "Generate BeepGlitchEffect", () => GenCanvas("BeepGlitchEffect"));
        Btn(b, "Generate BeepFlipCard", () => GenCanvas("BeepFlipCard"));
        Btn(b, "Generate BeepElasticScroll", () => GenCanvas("BeepElasticScroll"));
        Btn(b, "Generate BeepChromaticAberration", () => GenCanvas("BeepChromaticAberration"));
        Btn(b, "Generate BeepFilmGrain", () => GenCanvas("BeepFilmGrain"));
        Btn(b, "Generate BeepColorGrade", () => GenCanvas("BeepColorGrade"));
        Btn(b, "Generate BeepDissolveEffect", () => GenCanvas("BeepDissolveEffect"));
        Btn(b, "Generate BeepOutline + ShadowText", () => GenCanvas("BeepOutlineShadow"));
        Btn(b, "Generate BeepAnimatedNumber + Breathe", () => GenCanvas("BeepAnimatedNumber"));
        Btn(b, "Generate BeepMotionBlur + LensFlare + Zoom + Mirror", () => GenCanvas("BeepMoreFX"));
        Btn(b, "Generate BeepPixelate + WaterFX + FreezeFrame + Wipes", () => GenCanvas("BeepMoreFX2"));
        Btn(b, "Generate Pause Menu", () => Gen("res://scripts/ui/PauseMenu.cs", "public partial class PauseMenu : Control { public override void _Ready() { Visible = false; } public void Toggle() { Visible = !Visible; GetTree().Paused = Visible; } }"));
        Btn(b, "Generate Main Menu", () => Gen("res://scripts/ui/MainMenuUI.cs", "public partial class MainMenuUI : Control { public override void _Ready() { } private void OnStart() => GetTree().ChangeSceneToFile(\"res://scenes/main/main.tscn\"); private void OnQuit() => GetTree().Quit(); }"));
        Btn(b, "Generate Settings Menu", () => Gen("res://scripts/ui/SettingsMenu.cs", "public partial class SettingsMenu : Control { public override void _Ready() { } }"));
        Btn(b, "Generate Dialog Box", () => Gen("res://scripts/ui/DialogBox.cs", "public partial class DialogBox : Control { public void Show(string title, string msg, Action onOk=null){} }"));
        Btn(b, "Generate Loading Screen", () => Gen("res://scripts/ui/LoadingScreen.cs", "public partial class LoadingScreen : Control { public async void Load(string path){} }"));
        Btn(b, "Generate ALL Canvas Scripts", () => {
            foreach (var n in ALL_CANVAS) GenCanvas(n); Log("All canvas scripts generated.");
        });
    }

    private void AddScriptTab(TabContainer tabs)
    {
        var b = Box(MakeTab(tabs, "Quick UI"));
        Lbl(b, "Quick Prefabs");
        Btn(b, "Generate ALL (84 scripts)", () => {
            foreach (var n in ALL_CORE) GenAny(n);
            foreach (var n in ALL_HUD) GenAny(n);
            foreach (var n in ALL_CANVAS) GenAny(n);
            Log($"All {ALL_CORE.Length + ALL_HUD.Length + ALL_CANVAS.Length} scripts generated.");
        });
    }

    private static readonly string[] ALL_CORE = {"BeepDataBinder","BeepDataGrid","BeepFormBuilder","BeepTreeView","BeepDropdown","BeepKeybindManager","BeepStateMachine","BeepPoolSaveManager","BeepAudioManager","BeepLocalization","BeepCoroutine","BeepConfigManager","BeepWeightedTable","BeepCommandHistory","BeepServiceLocator","BeepEncryptionPathfinding","BeepAchievementDebug","BeepProceduralAnim"};
    private static readonly string[] ALL_HUD = {"BeepHealthBar","BeepMinimap","BeepScoreDisplay","BeepAmmoDisplay","BeepCompass","BeepCrosshair","BeepTimer","BeepInteractionPrompt","BeepQuestLog","BeepNotifications","BeepFloatingDamage","BeepConsoleLog","BeepVirtualJoystick","BeepInputHints","BeepKillFeed","BeepRespawnOverlay","BeepObjectiveMarkers","BeepDebugOverlay","BeepHitIndicator","BeepSpectatorLabel","BeepSegmentedProgress","BeepEdgeIndicator","BeepWeaponWheel","BeepSubtitles","BeepVignette","BeepSpeedometer","BeepAltitudeMeter","BeepLeaderboard","BeepChatBox","BeepPickupLog","BeepBossHealthBar","BeepCooldownIndicator","BeepStatusEffectIcons","BeepWaveCounter","BeepComboCounter","BeepAccuracyDisplay","BeepMatchTimer","BeepTeammatePanel","BeepSkillTree","BeepCraftingMenu","BeepLootPopup","BeepDamagePreview","BeepZoneWarning","BeepReticlePing","BeepEquipmentShop","BeepQuestMap","BeepTutorialEndScreen","BeepDialogMinigame"};
    private static readonly string[] ALL_CANVAS = {"BeepCanvasAnchor","BeepSafeArea","BeepScreenFX","BeepScreenShake","BeepSceneTransition","BeepTooltip","BeepDragDrop","BeepTabPanel","BeepContextMenu","BeepAccordion","BeepRadialMenu","BeepCarousel","BeepWizard","BeepThemeManager","BeepInventoryGrid","BeepSpriteAnim","BeepButtonGroup","BeepParallaxBackground","BeepMarquee","BeepShimmer","BeepGradientBackground","BeepAspectRatioContainer","BeepGridView","BeepTypewriterLabel","BeepParticleUI","BeepScanlines","BeepBlurPanel","BeepRippleEffect","BeepPulseRing","BeepGlitchEffect","BeepFlipCard","BeepElasticScroll","BeepChromaticAberration","BeepFilmGrain","BeepColorGrade","BeepDissolveEffect","BeepOutlineShadow","BeepAnimatedNumber","BeepMoreFX","BeepMoreFX2","BeepTrailNeonLiquidSplit","BeepMagnifierMore","BeepRetroFX","BeepTechTree","BeepTextFX"};

    private static void Gen(string path, string content) { BeepUIGenerator.WriteScript(path, content); }
    private static void GenHud(string name) => Gen($"res://scripts/hud/{name}.cs", $"// {name} - see addons/beep_ui_builder_cs/hud/{name}.cs");
    private static void GenCanvas(string name) => Gen($"res://scripts/ui/{name}.cs", $"// {name} - see addons/beep_ui_builder_cs/canvas/{name}.cs");
    private static void GenAll(string[] names) { foreach (var n in names) Gen($"res://scripts/ui/{n}.cs", $"// {n}"); }
    private static void GenAny(string name)
    {
        if (name.StartsWith("BeepHealth")||name.StartsWith("BeepMini")||name.StartsWith("BeepScore")||name.StartsWith("BeepAmmo")||name.StartsWith("BeepCompass")||name.StartsWith("BeepCross")||name.StartsWith("BeepTimer")||name.StartsWith("BeepInteraction")||name.StartsWith("BeepQuest")||name.StartsWith("BeepNotif")||name.StartsWith("BeepFloat")||name.StartsWith("BeepConsole")||name.StartsWith("BeepVirtual")||name.StartsWith("BeepInputHints")||name.StartsWith("BeepKill")||name.StartsWith("BeepRespawn")||name.StartsWith("BeepObjective")||name.StartsWith("BeepDebug")||name.StartsWith("BeepHitIndicator")||name.StartsWith("BeepSpectator")||name.StartsWith("BeepSegmented")||name.StartsWith("BeepEdge")||name.StartsWith("BeepWeapon")||name.StartsWith("BeepSubtitles")||name.StartsWith("BeepVignette")||name.StartsWith("BeepSpeed")||name.StartsWith("BeepAltitude")||name.StartsWith("BeepLeader")||name.StartsWith("BeepChat")||name.StartsWith("BeepPickup")||name.StartsWith("BeepBoss")||name.StartsWith("BeepCooldown")||name.StartsWith("BeepStatus")||name.StartsWith("BeepWave")||name.StartsWith("BeepCombo")||name.StartsWith("BeepAccur")||name.StartsWith("BeepMatch")||name.StartsWith("BeepTeammate")||name.StartsWith("BeepSkill")||name.StartsWith("BeepCraft")||name.StartsWith("BeepLoot")||name.StartsWith("BeepDamagePreview")||name.StartsWith("BeepZone")||name.StartsWith("BeepReticle")||name.StartsWith("BeepEquipment")||name.StartsWith("BeepQuestMap"))
            GenHud(name);
        else if (name.StartsWith("BeepCanvas")||name.StartsWith("BeepSafe")||name.StartsWith("BeepScreen")||name.StartsWith("BeepScene")||name.StartsWith("BeepTool")||name.StartsWith("BeepDrag")||name.StartsWith("BeepTab")||name.StartsWith("BeepContext")||name.StartsWith("BeepAccord")||name.StartsWith("BeepRadial")||name.StartsWith("BeepCarousel")||name.StartsWith("BeepWizard")||name.StartsWith("BeepTheme")||name.StartsWith("BeepInventory")||name.StartsWith("BeepSprite")||name.StartsWith("BeepButtonGroup")||name.StartsWith("BeepParallax")||name.StartsWith("BeepMarquee")||name.StartsWith("BeepShimmer")||name.StartsWith("BeepGradient")||name.StartsWith("BeepAspect")||name.StartsWith("BeepGridView")||name.StartsWith("BeepTypewriter")||name.StartsWith("BeepParticle")||name.StartsWith("BeepScan")||name.StartsWith("BeepBlur")||name.StartsWith("BeepRipple")||name.StartsWith("BeepPulse")||name.StartsWith("BeepGlitch")||name.StartsWith("BeepFlip")||name.StartsWith("BeepElastic")||name.StartsWith("BeepChrom")||name.StartsWith("BeepFilm")||name.StartsWith("BeepColorGrade")||name.StartsWith("BeepDissolve")||name.StartsWith("BeepOutline")||name.StartsWith("BeepAnimated")||name.StartsWith("BeepBreathe")||name.StartsWith("BeepMore")||name.StartsWith("BeepTrail")||name.StartsWith("BeepNeon")||name.StartsWith("BeepLiquid")||name.StartsWith("BeepSplit"))
            GenCanvas(name);
        else Gen($"res://scripts/ui/{name}.cs", $"// {name}");
    }
}

// BeepUIGenerator is defined in BeepGameBuilderDock.cs
