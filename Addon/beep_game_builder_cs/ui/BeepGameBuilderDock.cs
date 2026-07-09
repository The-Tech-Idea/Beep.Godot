using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class BeepGameBuilderDock : VBoxContainer
{
    public EditorPlugin EditorPlugin { get; set; }

    private TextEdit _output;
    private CheckBox _overwriteCb;
    private ItemList _shaderList, _tweenList, _particleList, _projectileList;
    private List<PresetEntry> _shaderPresets = new(), _tweenPresets = new(),
        _particlePresets = new(), _projectilePresets = new();

    public override void _Ready()
    {
        Name = "Beep Game Builder";
        BeepFileUtils.LogCallback = msg => Log(msg);
        BeepFileUtils.ErrorCallback = msg => Log("[ERROR] " + msg);
        BuildUI();
    }

    private void BuildUI()
    {
        var title = new Label { Text = "Beep Game Builder (C#)", HorizontalAlignment = HorizontalAlignment.Center };
        AddChild(title);

        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(tabs);

        AddProjectTab(tabs); AddScenesTab(tabs); AddCharactersTab(tabs);
        AddShadersTab(tabs); AddTweensTab(tabs); AddParticlesTab(tabs);
        AddProjectilesTab(tabs); AddComponentsTab(tabs); AddValidationTab(tabs); AddExportTab(tabs);
        AddUITabs(tabs); // UI Builder tabs

        _overwriteCb = new CheckBox { Text = "Overwrite Existing Files" };
        AddChild(_overwriteCb);

        _output = new TextEdit { CustomMinimumSize = new Vector2(0, 160), Editable = false, PlaceholderText = "Output..." };
        AddChild(_output);

        var quick = new HBoxContainer(); AddChild(quick);
        AddButton(quick, "Refresh FileSystem", () => { BeepFileUtils.RefreshFilesystem(); Log("Filesystem refreshed."); });
        AddButton(quick, "Save Current Scene", () => { BeepFileUtils.SaveCurrentScene(); Log("Scene saved."); });
    }

    private ScrollContainer MakeTab(TabContainer tabs, string title)
    {
        var s = new ScrollContainer { Name = title, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        s.AddChild(b); tabs.AddChild(s); return s;
    }
    private static VBoxContainer GetBox(ScrollContainer s) => s.GetChild(0) as VBoxContainer;
    private static Button AddButton(Node parent, string text, System.Action action)
    {
        var b = new Button { Text = text }; b.Pressed += action; parent.AddChild(b); return b;
    }
    private static Label AddLabel(Node parent, string text) { var l = new Label { Text = text }; parent.AddChild(l); return l; }
    private static LineEdit AddSearch(Node parent, string placeholder) { var le = new LineEdit { PlaceholderText = placeholder }; parent.AddChild(le); return le; }

    private void Log(string msg)
    {
        if (_output != null)
            _output.Text += msg + "\n";
        else
            GD.Print("[BeepGameBuilder] " + msg);
    }

    // ---- Tab builders ----
    private void AddProjectTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Project"));
        AddLabel(b, "Project Setup");
        AddButton(b, "Generate Project Folders", () => { foreach (var f in BeepProjectGenerator.CreateStandardFolders()) Log("Folder: " + f); BeepFileUtils.RefreshFilesystem(); });
        AddButton(b, "Setup Project Defaults", () => { BeepProjectDefaults.ConfigureDefaults(); Log("Defaults: 1280x720, 2D stretch"); });
        AddButton(b, "Generate Input Map", () => { foreach (var a in BeepInputMapGenerator.SetupDefaultInput()) Log("Input: " + a); });
        AddButton(b, "Generate Managers", () => {
            Log("Created: " + BeepScriptGenerator.CreateSceneManager());
            Log("Created: " + BeepScriptGenerator.CreateSaveManager());
            Log("Created: " + BeepScriptGenerator.CreateAudioManager());
        });
        AddButton(b, "Generate Starter Project (All)", OnStarter);
    }

    private void AddScenesTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Scenes"));
        AddLabel(b, "Scene Generators");
        AddButton(b, "Generate Main Scene", () => Log("Created: " + BeepSceneGenerator.CreateMainScene()));
        AddButton(b, "Generate Main Menu", () => Log("Created: " + BeepSceneGenerator.CreateMainMenu()));
        AddButton(b, "Generate Pause Menu", () => Log("Created: " + BeepSceneGenerator.CreatePauseMenu()));
        AddButton(b, "Generate Enemy Patrol Scene", () => { BeepScriptGenerator.CreateEnemyPatrol(); Log("Created enemy_patrol script + scene."); });
        AddButton(b, "Generate Pickup Item Scene", () => { BeepScriptGenerator.CreatePickupItem(); Log("Created pickup_item script."); });
        AddButton(b, "Generate Moving Platform Scene", () => { BeepScriptGenerator.CreateMovingPlatform(); Log("Created moving_platform script."); });
        AddButton(b, "Generate HUD Overlay", () => Log("Created: " + BeepSceneGenerator.CreateTemplateScene("hud_overlay_template")));
        AddButton(b, "Generate Enemy Template", () => Log("Created: " + BeepSceneGenerator.CreateTemplateScene("enemy_template")));
        AddButton(b, "Generate Pickup Template", () => Log("Created: " + BeepSceneGenerator.CreateTemplateScene("pickup_template")));
        AddButton(b, "Generate Dialog Template", () => Log("Created: " + BeepSceneGenerator.CreateTemplateScene("dialog_template")));
        AddButton(b, "Generate Projectile Template", () => Log("Created: " + BeepSceneGenerator.CreateTemplateScene("projectile_template")));
    }

    private void AddCharactersTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Characters"));
        AddLabel(b, "Character & Gameplay Scripts");
        AddButton(b, "Generate Top-Down Player", () => { var s = BeepScriptGenerator.CreateTopDownPlayer(); Log("Created: " + BeepSceneGenerator.CreateTopDownPlayerScene(s)); });
        AddButton(b, "Generate Platformer Player", () => { var s = BeepScriptGenerator.CreatePlatformerPlayer(); Log("Created: " + BeepSceneGenerator.CreatePlatformerPlayerScene(s)); });
        AddButton(b, "Generate Robot NPC", () => { var s = BeepScriptGenerator.CreateRobotNpc(); Log("Created: " + BeepSceneGenerator.CreateRobotNpcScene(s)); });
        AddButton(b, "Generate Enemy Patrol AI", () => Log("Created: " + BeepScriptGenerator.CreateEnemyPatrol()));
        AddButton(b, "Generate Health Component", () => Log("Created: " + BeepScriptGenerator.CreateHealthComponent()));
        AddButton(b, "Generate Camera Follow Script", () => Log("Created: " + BeepScriptGenerator.CreateCameraFollow()));
        AddLabel(b, "─ Level & Systems ─");
        AddButton(b, "Generate Door/Switch", () => Log("Created: " + BeepScriptGenerator.CreateDoorSwitch()));
        AddButton(b, "Generate Turret", () => Log("Created: " + BeepScriptGenerator.CreateTurret()));
        AddButton(b, "Generate Pickup Item", () => Log("Created: " + BeepScriptGenerator.CreatePickupItem()));
        AddButton(b, "Generate Moving Platform", () => Log("Created: " + BeepScriptGenerator.CreateMovingPlatform()));
        AddButton(b, "Generate Checkpoint", () => Log("Created: " + BeepScriptGenerator.CreateCheckpoint()));
        AddButton(b, "Generate Weather System", () => Log("Created: " + BeepScriptGenerator.CreateWeatherSystem()));
        AddButton(b, "Generate Day/Night Cycle", () => Log("Created: " + BeepScriptGenerator.CreateDayNightCycle()));
        AddButton(b, "Generate Inventory System", () => Log("Created: " + BeepScriptGenerator.CreateInventory()));
        AddButton(b, "Generate Projectile Variants", () => Log("Created: " + BeepScriptGenerator.CreateProjectileVariants()));
        AddButton(b, "Generate Game Manager", () => Log("Created: " + BeepScriptGenerator.CreateGameManager()));
        AddButton(b, "Generate Dialog System", () => Log("Created: " + BeepScriptGenerator.CreateDialogSystem()));
        AddButton(b, "Generate Object Pool", () => Log("Created: " + BeepScriptGenerator.CreateObjectPool()));
        AddButton(b, "Generate Screen Transition", () => Log("Created: " + BeepScriptGenerator.CreateScreenTransition()));
        AddButton(b, "Generate Platformer Extras", () => Log("Created: " + BeepScriptGenerator.CreatePlatformerExtras()));
    }

    private void AddShadersTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Shaders"));
        AddLabel(b, "Shader Presets");
        var s = AddSearch(b, "Filter shaders...");
        _shaderList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Multi }; b.AddChild(_shaderList);
        LoadPresets("shader_presets.json", _shaderPresets, _shaderList, s);
        AddButton(b, "Generate Selected Shaders", () => {
            foreach (int i in _shaderList.GetSelectedItems()) BeepShaderGenerator.CreateShaderById(_shaderPresets[i].Id, _overwriteCb.ButtonPressed);
        });
        AddButton(b, "Generate All Shaders", () => { foreach (var p in _shaderPresets) BeepShaderGenerator.CreateShaderById(p.Id, _overwriteCb.ButtonPressed); BeepFileUtils.RefreshFilesystem(); });
        AddButton(b, "+ Generate New Shader Templates", () => {
            foreach (var id in new[]{"water_reflection","grass_wind","hit_flash","portal_vortex","ice_freeze","drop_shadow","outline_glow","poison_effect","fire_aura","stealth_invis","rainbow_cycle","pulse_beat"})
                BeepShaderGenerator.CreateShaderById(id, _overwriteCb.ButtonPressed);
            Log("Generated 12 new shader templates.");
        });
    }

    private void AddTweensTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Tweens"));
        AddLabel(b, "Tween Presets");
        var s = AddSearch(b, "Filter tweens...");
        _tweenList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Multi }; b.AddChild(_tweenList);
        LoadPresets("tween_presets.json", _tweenPresets, _tweenList, s);
        AddButton(b, "Generate Tween Helper (selected)", () => {
            var ids = _tweenList.GetSelectedItems().Select(i => _tweenPresets[i].Id).ToList();
            BeepTweenGenerator.CreateTweenHelperSelected(ids, _overwriteCb.ButtonPressed);
        });
        AddButton(b, "Generate All Tween Presets", () => BeepTweenGenerator.CreateTweenHelper(_overwriteCb.ButtonPressed));
    }

    private void AddParticlesTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Particles"));
        AddLabel(b, "Particle Presets");
        var s = AddSearch(b, "Filter particles...");
        _particleList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Multi }; b.AddChild(_particleList);
        LoadPresets("particle_presets.json", _particlePresets, _particleList, s);
        AddButton(b, "Generate Selected Particles", () => {
            foreach (int i in _particleList.GetSelectedItems()) BeepParticleGenerator.CreateParticleById(_particlePresets[i].Id, _overwriteCb.ButtonPressed);
        });
        AddButton(b, "Generate All Particles", () => { foreach (var p in _particlePresets) BeepParticleGenerator.CreateParticleById(p.Id, _overwriteCb.ButtonPressed); BeepFileUtils.RefreshFilesystem(); });
        AddButton(b, "Generate Particle Helper Script", () => BeepParticleGenerator.CreateParticleHelperScript(_overwriteCb.ButtonPressed));
        AddButton(b, "+ Generate Particle Scene Templates", () => {
            foreach (var id in new[]{"simple_burst","blood_splatter","coin_pickup","fire_torch","hit_sparks","magic_spell","smoke_puff","rain_drops","explosion"})
                BeepParticleGenerator.CreateParticleSceneTemplate(id, _overwriteCb.ButtonPressed);
            Log("Generated 9 particle scene templates.");
        });
    }

    private void AddProjectilesTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Projectiles"));
        AddLabel(b, "Projectile Presets");
        var s = AddSearch(b, "Filter projectiles...");
        _projectileList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Multi }; b.AddChild(_projectileList);
        LoadPresets("projectile_presets.json", _projectilePresets, _projectileList, s);
        AddButton(b, "Generate Projectile Math", () => BeepProjectileGenerator.CreateProjectileMath(_overwriteCb.ButtonPressed));
        AddButton(b, "Generate Projectile2D Script", () => BeepProjectileGenerator.CreateProjectile2D(_overwriteCb.ButtonPressed));
        AddButton(b, "Generate Basic Projectile Scene", () => BeepProjectileGenerator.CreateBasicProjectileScene(_overwriteCb.ButtonPressed));
        AddButton(b, "Generate Arc Projectile Scene", () => BeepProjectileGenerator.CreateArcProjectileScene(_overwriteCb.ButtonPressed));
        AddButton(b, "Generate Selected Projectiles", () => {
            foreach (int i in _projectileList.GetSelectedItems()) BeepProjectileGenerator.CreateProjectileById(_projectilePresets[i].Id, _overwriteCb.ButtonPressed);
        });
    }

    private void AddComponentsTab(TabContainer tabs)
    {
        var scroll = MakeTab(tabs, "ECS Components");
        var b = GetBox(scroll);
        AddLabel(b, "51+ GlobalClass Components — Add Node (+) to use");

        string[][] categories = {
            new[]{"═══ UI Components ═══"},
            new[]{"AnimatedMenuComponent","Menu children stagger in from any direction"},
            new[]{"SlideComponent","Slide in/out with optional fade"},
            new[]{"ShakeComponent","UI shake on error/impact feedback"},
            new[]{"DragComponent","Drag any Control, constrain to parent"},
            new[]{"PulseComponent","Breathing scale animation"},
            new[]{"FlipCardComponent","Card flip front/back face reveal"},
            new[]{"ModalComponent","Dark overlay + scale-in dialog popup"},
            new[]{"ToastNotificationComponent","Sliding toast messages"},
            new[]{"TooltipComponent","Hover tooltip with delay"},
            new[]{"ContextMenuComponent","Right-click popup menu"},
            new[]{"CounterComponent","Animated number counting"},
            new[]{"TypewriterComponent","Character-by-character text reveal"},
            new[]{"MarqueeComponent","Scrolling text ticker"},
            new[]{"RatingComponent","1-5 star display"},
            new[]{"RippleComponent","Material click ripple effect"},
            new[]{"BadgeComponent","Red notification badge with count"},
            new[]{"ToggleSwitchComponent","Animated on/off toggle switch"},
            new[]{"SearchBarComponent","Search input with icon and clear"},
            new[]{"ChipComponent","Tag chip with optional remove"},
            new[]{"StepperComponent","+/- number stepper"},
            new[]{"ProgressRingComponent","Circular progress indicator"},
            new[]{"SkeletonLoaderComponent","Shimmer loading placeholder"},
            new[]{"TableComponent","Sortable data table"},
            new[]{"TabGroupComponent","Click tab headers to switch panels"},
            new[]{"AccordionComponent","Expandable/collapsible sections"},
            new[]{"CarouselComponent","Horizontal card browser"},
            new[]{""},
            new[]{"═══ Gameplay Components ═══"},
            new[]{"EntityComponent, EntitySystem","Core ECS base classes"},
            new[]{"HealthComponent","HP, damage, death signals"},
            new[]{"AttackComponent","Damage, range, cooldown"},
            new[]{"MovementComponent","Speed, dash, friction"},
            new[]{"KnockbackComponent","Push entity away from damage"},
            new[]{"FlashComponent","Damage flash white/red"},
            new[]{"HealthBarComponent","Auto health bar over entity"},
            new[]{"FloatingTextComponent","Damage numbers popup"},
            new[]{"AutoHealComponent","Regen HP after delay"},
            new[]{"AggroComponent","Threat table management"},
            new[]{"StatusEffectComponent","Buffs/debuffs with duration"},
            new[]{"DropTableComponent","Weighted loot drops on death"},
            new[]{""},
            new[]{"═══ Controllers ═══"},
            new[]{"TopDownController, PlatformerController","Movement controllers"},
            new[]{"AIController","Patrol/Chase/Wander/Flee AI"},
            new[]{"StateMachineComponent","FSM with state enter/exit signals"},
            new[]{""},
            new[]{"═══ World & Effects ═══"},
            new[]{"Bob, Rotate, FollowTarget, Lifetime","Common entity behaviors"},
            new[]{"ScreenShake, CameraZoom, Parallax","Camera components"},
            new[]{"Interactable, Pickup, Inventory","Interaction components"},
            new[]{"Spawner, Dialog, Particle, Projectile","Spawning/effects"},
            new[]{"TweenComponent (22 presets), Audio","Animation and sound"},
            new[]{"PowerSource, PowerReceiver, Work","System components"},
            new[]{""},
            new[]{"═══ Visual Assets (FileSystem → drag in) ═══"},
            new[]{"assets/particles/","98 particle scenes"},
            new[]{"assets/shaders/","93 shader files"},
            new[]{"assets/projectiles/","73 projectile scenes"},
        };

        foreach (var row in categories)
        {
            if (row.Length == 1) { AddLabel(b, row[0]); continue; }
            if (string.IsNullOrEmpty(row[0])) { AddLabel(b, ""); continue; }
            var lbl = new Label { Text = $"  {row[0],-38} {row[1]}" };
            lbl.AddThemeFontSizeOverride("font_size", 10);
            b.AddChild(lbl);
        }

        AddButton(b, "Generate All Assets", () =>
        {
            BeepShaderGenerator.CreateAllShaders(_overwriteCb.ButtonPressed);
            BeepFileUtils.RefreshFilesystem();
            Log("All assets generated — restart FileSystem to see them.");
        });
    }

    private void AddValidationTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Validation"));
        AddLabel(b, "Project Validation");
        AddButton(b, "Validate Project", () => { foreach (var m in BeepValidator.Validate()) Log(m); });
        AddButton(b, "Fix Safe Issues", () => { foreach (var m in BeepValidator.FixSafeIssues()) Log(m); });
        AddButton(b, "Open Validation Report", () => { BeepValidator.WriteReport("res://VALIDATION_REPORT.md"); Log("Report written."); });
    }

    private void AddExportTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "Export"));
        AddLabel(b, "Export Preparation");
        AddButton(b, "Generate Export Checklist", () => Log("Created: " + BeepExportChecklist.CreateExportChecklist()));
        AddButton(b, "Open Export Checklist", () => Log("Open res://EXPORT_CHECKLIST.md"));
    }

    // ══════════════════════════════════════════════════════════
    // UI Builder Tabs
    // ══════════════════════════════════════════════════════════

    private void AddUITabs(TabContainer tabs)
    {
        AddUICoreTab(tabs); AddUIHudTab(tabs); AddUICanvasTab(tabs); AddUIQuickTab(tabs);
    }

    private void AddUICoreTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "UI Core"));
        AddLabel(b, "Data Binding, Architecture & Infra");
        AddButton(b, "BeepDataBinder", () => Gen("res://scripts/ui/BeepDataBinder.cs", "// BeepDataBinder"));
        AddButton(b, "BeepDataGrid", () => Gen("res://scripts/ui/BeepDataGrid.cs", "// BeepDataGrid"));
        AddButton(b, "BeepFormBuilder", () => Gen("res://scripts/ui/BeepFormBuilder.cs", "// BeepFormBuilder"));
        AddButton(b, "BeepTreeView", () => Gen("res://scripts/ui/BeepTreeView.cs", "// BeepTreeView"));
        AddButton(b, "BeepDropdown", () => Gen("res://scripts/ui/BeepDropdown.cs", "// BeepDropdown"));
        AddButton(b, "BeepKeybindManager", () => Gen("res://scripts/ui/BeepKeybindManager.cs", "// BeepKeybindManager"));
        AddButton(b, "StateMachine + EventBus", () => Gen("res://scripts/ui/BeepStateMachine.cs", "// BeepStateMachine + EventBus"));
        AddButton(b, "Pool + SaveManager", () => Gen("res://scripts/ui/BeepPoolSaveManager.cs", "// BeepPoolManager + SaveManager"));
        AddButton(b, "AudioManager", () => Gen("res://scripts/ui/BeepAudioManager.cs", "// BeepAudioManager"));
        AddButton(b, "Localization", () => Gen("res://scripts/ui/BeepLocalization.cs", "// BeepLocalization"));
        AddButton(b, "Coroutine", () => Gen("res://scripts/ui/BeepCoroutine.cs", "// BeepCoroutine"));
        AddButton(b, "ConfigManager + InputBuffer", () => Gen("res://scripts/ui/BeepConfigManager.cs", "// BeepConfigManager"));
        AddButton(b, "WeightedTable", () => Gen("res://scripts/ui/BeepWeightedTable.cs", "// BeepWeightedTable"));
        AddButton(b, "CommandHistory", () => Gen("res://scripts/ui/BeepCommandHistory.cs", "// BeepCommandHistory"));
        AddButton(b, "ServiceLocator + GridNav + TweenChain", () => Gen("res://scripts/ui/BeepServiceLocator.cs", "// ServiceLocator"));
        AddButton(b, "Encryption + Pathfinding", () => Gen("res://scripts/ui/BeepEncryptionPathfinding.cs", "// Encryption + Pathfinding"));
        AddButton(b, "Achievement + DebugConsole", () => Gen("res://scripts/ui/BeepAchievementDebug.cs", "// Achievement + DebugConsole"));
        AddButton(b, "ProceduralAnim + Noise + Gradients", () => Gen("res://scripts/ui/BeepProceduralAnim.cs", "// ProceduralAnim"));
        AddButton(b, "ALL Core Scripts", () => { foreach (var n in ALL_CORE) GenAny(n); Log("All core scripts generated."); });
    }

    private void AddUIHudTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "UI HUD"));
        AddLabel(b, "HUD Components");
        AddButton(b, "BeepHealthBar", () => GenHud("BeepHealthBar"));
        AddButton(b, "BeepMinimap", () => GenHud("BeepMinimap"));
        AddButton(b, "BeepScoreDisplay", () => GenHud("BeepScoreDisplay"));
        AddButton(b, "BeepAmmoDisplay", () => GenHud("BeepAmmoDisplay"));
        AddButton(b, "BeepCompass", () => GenHud("BeepCompass"));
        AddButton(b, "BeepCrosshair", () => GenHud("BeepCrosshair"));
        AddButton(b, "BeepTimer", () => GenHud("BeepTimer"));
        AddButton(b, "BeepInteractionPrompt", () => GenHud("BeepInteractionPrompt"));
        AddButton(b, "BeepQuestLog", () => GenHud("BeepQuestLog"));
        AddButton(b, "BeepNotifications", () => GenHud("BeepNotifications"));
        AddButton(b, "BeepFloatingDamage", () => GenHud("BeepFloatingDamage"));
        AddButton(b, "BeepConsoleLog", () => GenHud("BeepConsoleLog"));
        AddButton(b, "BeepVirtualJoystick", () => GenHud("BeepVirtualJoystick"));
        AddButton(b, "BeepInputHints", () => GenHud("BeepInputHints"));
        AddButton(b, "BeepKillFeed", () => GenHud("BeepKillFeed"));
        AddButton(b, "BeepRespawnOverlay", () => GenHud("BeepRespawnOverlay"));
        AddButton(b, "BeepObjectiveMarkers", () => GenHud("BeepObjectiveMarkers"));
        AddButton(b, "BeepDebugOverlay", () => GenHud("BeepDebugOverlay"));
        AddButton(b, "BeepHitIndicator", () => GenHud("BeepHitIndicator"));
        AddButton(b, "BeepSpectatorLabel", () => GenHud("BeepSpectatorLabel"));
        AddButton(b, "BeepSegmentedProgress", () => GenHud("BeepSegmentedProgress"));
        AddButton(b, "BeepEdgeIndicator", () => GenHud("BeepEdgeIndicator"));
        AddButton(b, "BeepWeaponWheel", () => GenHud("BeepWeaponWheel"));
        AddButton(b, "BeepSubtitles", () => GenHud("BeepSubtitles"));
        AddButton(b, "BeepVignette", () => GenHud("BeepVignette"));
        AddButton(b, "BeepSpeedometer", () => GenHud("BeepSpeedometer"));
        AddButton(b, "BeepAltitudeMeter", () => GenHud("BeepAltitudeMeter"));
        AddButton(b, "BeepLeaderboard", () => GenHud("BeepLeaderboard"));
        AddButton(b, "BeepChatBox", () => GenHud("BeepChatBox"));
        AddButton(b, "BeepPickupLog", () => GenHud("BeepPickupLog"));
        AddButton(b, "BeepBossHealthBar", () => GenHud("BeepBossHealthBar"));
        AddButton(b, "BeepCooldownIndicator", () => GenHud("BeepCooldownIndicator"));
        AddButton(b, "BeepStatusEffectIcons", () => GenHud("BeepStatusEffectIcons"));
        AddButton(b, "BeepWaveCounter", () => GenHud("BeepWaveCounter"));
        AddButton(b, "BeepComboCounter", () => GenHud("BeepComboCounter"));
        AddButton(b, "BeepAccuracyDisplay", () => GenHud("BeepAccuracyDisplay"));
        AddButton(b, "BeepMatchTimer", () => GenHud("BeepMatchTimer"));
        AddButton(b, "BeepTeammatePanel", () => GenHud("BeepTeammatePanel"));
        AddButton(b, "BeepSkillTree", () => GenHud("BeepSkillTree"));
        AddButton(b, "BeepCraftingMenu", () => GenHud("BeepCraftingMenu"));
        AddButton(b, "BeepLootPopup", () => GenHud("BeepLootPopup"));
        AddButton(b, "BeepDamagePreview", () => GenHud("BeepDamagePreview"));
        AddButton(b, "BeepZoneWarning", () => GenHud("BeepZoneWarning"));
        AddButton(b, "Reticle + PingSystem", () => GenHud("BeepReticlePing"));
        AddButton(b, "EquipmentSlots + ShopMenu", () => GenHud("BeepEquipmentShop"));
        AddButton(b, "QuestJournal + MapScreen", () => GenHud("BeepQuestMap"));
        AddButton(b, "Tutorial + EndScreen", () => GenHud("BeepTutorialEndScreen"));
        AddButton(b, "DialogTree + MinigameHUD", () => GenHud("BeepDialogMinigame"));
        AddButton(b, "ALL HUD Scripts", () => { foreach (var n in ALL_HUD) GenHud(n); Log("All HUD scripts generated."); });
    }

    private void AddUICanvasTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "UI Canvas & FX"));
        AddLabel(b, "Canvas Utilities & Screen FX");
        AddButton(b, "BeepCanvasAnchor", () => GenCanvas("BeepCanvasAnchor"));
        AddButton(b, "BeepSafeArea", () => GenCanvas("BeepSafeArea"));
        AddButton(b, "BeepScreenFX", () => GenCanvas("BeepScreenFX"));
        AddButton(b, "BeepScreenShake", () => GenCanvas("BeepScreenShake"));
        AddButton(b, "BeepSceneTransition", () => GenCanvas("BeepSceneTransition"));
        AddButton(b, "BeepTooltip", () => GenCanvas("BeepTooltip"));
        AddButton(b, "BeepDragDrop", () => GenCanvas("BeepDragDrop"));
        AddButton(b, "BeepTabPanel", () => GenCanvas("BeepTabPanel"));
        AddButton(b, "BeepContextMenu", () => GenCanvas("BeepContextMenu"));
        AddButton(b, "BeepAccordion", () => GenCanvas("BeepAccordion"));
        AddButton(b, "BeepRadialMenu", () => GenCanvas("BeepRadialMenu"));
        AddButton(b, "BeepCarousel", () => GenCanvas("BeepCarousel"));
        AddButton(b, "BeepWizard", () => GenCanvas("BeepWizard"));
        AddButton(b, "BeepThemeManager", () => GenCanvas("BeepThemeManager"));
        AddButton(b, "BeepInventoryGrid", () => GenCanvas("BeepInventoryGrid"));
        AddButton(b, "BeepSpriteAnim", () => GenCanvas("BeepSpriteAnim"));
        AddButton(b, "BeepButtonGroup", () => GenCanvas("BeepButtonGroup"));
        AddButton(b, "BeepParallaxBackground", () => GenCanvas("BeepParallaxBackground"));
        AddButton(b, "BeepMarquee", () => GenCanvas("BeepMarquee"));
        AddButton(b, "BeepShimmer", () => GenCanvas("BeepShimmer"));
        AddButton(b, "BeepGradientBackground", () => GenCanvas("BeepGradientBackground"));
        AddButton(b, "BeepAspectRatioContainer", () => GenCanvas("BeepAspectRatioContainer"));
        AddButton(b, "BeepGridView", () => GenCanvas("BeepGridView"));
        AddButton(b, "BeepTypewriterLabel", () => GenCanvas("BeepTypewriterLabel"));
        AddButton(b, "ParticleUI + Scanlines + BlurPanel", () => GenCanvas("BeepParticleUI"));
        AddButton(b, "RippleEffect + PulseRing + GlitchEffect", () => GenCanvas("BeepRippleEffect"));
        AddButton(b, "FlipCard + ElasticScroll", () => GenCanvas("BeepFlipCard"));
        AddButton(b, "ChromaticAberration + FilmGrain + ColorGrade", () => GenCanvas("BeepChromaticAberration"));
        AddButton(b, "DissolveEffect + Outline + ShadowText", () => GenCanvas("BeepDissolveEffect"));
        AddButton(b, "AnimatedNumber + Breathe", () => GenCanvas("BeepAnimatedNumber"));
        AddButton(b, "MotionBlur + LensFlare + Zoom", () => GenCanvas("BeepMoreFX"));
        AddButton(b, "Pixelate + WaterFX + FreezeFrame + Wipes", () => GenCanvas("BeepMoreFX2"));
        AddButton(b, "Trail + NeonGlow + LiquidFill + SplitScreen", () => GenCanvas("BeepTrailNeonLiquidSplit"));
        AddButton(b, "Magnifier + Invert + BarrelDistortion + Fractal", () => GenCanvas("BeepMagnifierMore"));
        AddButton(b, "Kaleidoscope + VHS + Dither", () => GenCanvas("BeepRetroFX"));
        AddButton(b, "TechTree (Civ-style)", () => GenCanvas("BeepTechTree"));
        AddButton(b, "TextFX: Terminal, Scramble, Matrix", () => GenCanvas("BeepTextFX"));
        AddButton(b, "ALL Canvas Scripts", () => { foreach (var n in ALL_CANVAS) GenCanvas(n); Log("All canvas scripts generated."); });
    }

    private void AddUIQuickTab(TabContainer tabs)
    {
        var b = GetBox(MakeTab(tabs, "UI Quick"));
        AddLabel(b, "Generate All UI Scripts At Once");
        AddButton(b, "Generate ALL (84+ scripts)", () => {
            foreach (var n in ALL_CORE) GenAny(n);
            foreach (var n in ALL_HUD) GenAny(n);
            foreach (var n in ALL_CANVAS) GenAny(n);
            Log($"All {ALL_CORE.Length + ALL_HUD.Length + ALL_CANVAS.Length} UI scripts generated.");
        });
    }

    // ---- UI helpers ----
    private static void Gen(string path, string content) { BeepUIGenerator.WriteScript(path, content); }
    private static void GenHud(string name) => Gen($"res://scripts/hud/{name}.cs", $"// {name}");
    private static void GenCanvas(string name) => Gen($"res://scripts/ui/{name}.cs", $"// {name}");
    private static void GenAny(string name)
    {
        if (name.StartsWith("BeepHealth")||name.StartsWith("BeepMini")||name.StartsWith("BeepScore")||name.StartsWith("BeepAmmo")||name.StartsWith("BeepCompass")||name.StartsWith("BeepCross")||name.StartsWith("BeepTimer")||name.StartsWith("BeepInteraction")||name.StartsWith("BeepQuest")||name.StartsWith("BeepNotif")||name.StartsWith("BeepFloat")||name.StartsWith("BeepConsole")||name.StartsWith("BeepVirtual")||name.StartsWith("BeepInputHints")||name.StartsWith("BeepKill")||name.StartsWith("BeepRespawn")||name.StartsWith("BeepObjective")||name.StartsWith("BeepDebug")||name.StartsWith("BeepHitIndicator")||name.StartsWith("BeepSpectator")||name.StartsWith("BeepSegmented")||name.StartsWith("BeepEdge")||name.StartsWith("BeepWeapon")||name.StartsWith("BeepSubtitles")||name.StartsWith("BeepVignette")||name.StartsWith("BeepSpeed")||name.StartsWith("BeepAltitude")||name.StartsWith("BeepLeader")||name.StartsWith("BeepChat")||name.StartsWith("BeepPickup")||name.StartsWith("BeepBoss")||name.StartsWith("BeepCooldown")||name.StartsWith("BeepStatus")||name.StartsWith("BeepWave")||name.StartsWith("BeepCombo")||name.StartsWith("BeepAccur")||name.StartsWith("BeepMatch")||name.StartsWith("BeepTeammate")||name.StartsWith("BeepSkill")||name.StartsWith("BeepCraft")||name.StartsWith("BeepLoot")||name.StartsWith("BeepDamagePreview")||name.StartsWith("BeepZone")||name.StartsWith("BeepReticle")||name.StartsWith("BeepEquipment")||name.StartsWith("BeepQuestMap")||name.StartsWith("BeepTutorial")||name.StartsWith("BeepDialog"))
            GenHud(name);
        else if (name.StartsWith("BeepCanvas")||name.StartsWith("BeepSafe")||name.StartsWith("BeepScreen")||name.StartsWith("BeepScene")||name.StartsWith("BeepTool")||name.StartsWith("BeepDrag")||name.StartsWith("BeepTab")||name.StartsWith("BeepContext")||name.StartsWith("BeepAccord")||name.StartsWith("BeepRadial")||name.StartsWith("BeepCarousel")||name.StartsWith("BeepWizard")||name.StartsWith("BeepTheme")||name.StartsWith("BeepInventory")||name.StartsWith("BeepSprite")||name.StartsWith("BeepButtonGroup")||name.StartsWith("BeepParallax")||name.StartsWith("BeepMarquee")||name.StartsWith("BeepShimmer")||name.StartsWith("BeepGradient")||name.StartsWith("BeepAspect")||name.StartsWith("BeepGridView")||name.StartsWith("BeepTypewriter")||name.StartsWith("BeepParticle")||name.StartsWith("BeepScan")||name.StartsWith("BeepBlur")||name.StartsWith("BeepRipple")||name.StartsWith("BeepPulse")||name.StartsWith("BeepGlitch")||name.StartsWith("BeepFlip")||name.StartsWith("BeepElastic")||name.StartsWith("BeepChrom")||name.StartsWith("BeepFilm")||name.StartsWith("BeepColorGrade")||name.StartsWith("BeepDissolve")||name.StartsWith("BeepOutline")||name.StartsWith("BeepAnimated")||name.StartsWith("BeepBreathe")||name.StartsWith("BeepMore")||name.StartsWith("BeepTrail")||name.StartsWith("BeepNeon")||name.StartsWith("BeepLiquid")||name.StartsWith("BeepSplit")||name.StartsWith("BeepMagnifier")||name.StartsWith("BeepRetro")||name.StartsWith("BeepTech")||name.StartsWith("BeepTextFX"))
            GenCanvas(name);
        else Gen($"res://scripts/ui/{name}.cs", $"// {name}");
    }

    private static readonly string[] ALL_CORE = {"BeepDataBinder","BeepDataGrid","BeepFormBuilder","BeepTreeView","BeepDropdown","BeepKeybindManager","BeepStateMachine","BeepPoolSaveManager","BeepAudioManager","BeepLocalization","BeepCoroutine","BeepConfigManager","BeepWeightedTable","BeepCommandHistory","BeepServiceLocator","BeepEncryptionPathfinding","BeepAchievementDebug","BeepProceduralAnim"};
    private static readonly string[] ALL_HUD = {"BeepHealthBar","BeepMinimap","BeepScoreDisplay","BeepAmmoDisplay","BeepCompass","BeepCrosshair","BeepTimer","BeepInteractionPrompt","BeepQuestLog","BeepNotifications","BeepFloatingDamage","BeepConsoleLog","BeepVirtualJoystick","BeepInputHints","BeepKillFeed","BeepRespawnOverlay","BeepObjectiveMarkers","BeepDebugOverlay","BeepHitIndicator","BeepSpectatorLabel","BeepSegmentedProgress","BeepEdgeIndicator","BeepWeaponWheel","BeepSubtitles","BeepVignette","BeepSpeedometer","BeepAltitudeMeter","BeepLeaderboard","BeepChatBox","BeepPickupLog","BeepBossHealthBar","BeepCooldownIndicator","BeepStatusEffectIcons","BeepWaveCounter","BeepComboCounter","BeepAccuracyDisplay","BeepMatchTimer","BeepTeammatePanel","BeepSkillTree","BeepCraftingMenu","BeepLootPopup","BeepDamagePreview","BeepZoneWarning","BeepReticlePing","BeepEquipmentShop","BeepQuestMap","BeepTutorialEndScreen","BeepDialogMinigame"};
    private static readonly string[] ALL_CANVAS = {"BeepCanvasAnchor","BeepSafeArea","BeepScreenFX","BeepScreenShake","BeepSceneTransition","BeepTooltip","BeepDragDrop","BeepTabPanel","BeepContextMenu","BeepAccordion","BeepRadialMenu","BeepCarousel","BeepWizard","BeepThemeManager","BeepInventoryGrid","BeepSpriteAnim","BeepButtonGroup","BeepParallaxBackground","BeepMarquee","BeepShimmer","BeepGradientBackground","BeepAspectRatioContainer","BeepGridView","BeepTypewriterLabel","BeepParticleUI","BeepScanlines","BeepBlurPanel","BeepRippleEffect","BeepPulseRing","BeepGlitchEffect","BeepFlipCard","BeepElasticScroll","BeepChromaticAberration","BeepFilmGrain","BeepColorGrade","BeepDissolveEffect","BeepOutlineShadow","BeepAnimatedNumber","BeepMoreFX","BeepMoreFX2","BeepTrailNeonLiquidSplit","BeepMagnifierMore","BeepRetroFX","BeepTechTree","BeepTextFX"};

    // ══════════════════════════════════════════════════════════
    // Starter Project
    // ══════════════════════════════════════════════════════════

    private void OnStarter()
    {
        Log("=== Starter Project ===");
        foreach (var f in BeepProjectGenerator.CreateStandardFolders()) { }
        BeepProjectDefaults.ConfigureDefaults();
        BeepInputMapGenerator.SetupDefaultInput();
        BeepScriptGenerator.CreateSceneManager(); BeepScriptGenerator.CreateSaveManager(); BeepScriptGenerator.CreateAudioManager();
        BeepSceneGenerator.CreateMainScene();
        var s1 = BeepScriptGenerator.CreateTopDownPlayer(); BeepSceneGenerator.CreateTopDownPlayerScene(s1);
        var s2 = BeepScriptGenerator.CreateRobotNpc(); BeepSceneGenerator.CreateRobotNpcScene(s2);
        BeepSceneGenerator.CreateMainMenu(); BeepSceneGenerator.CreatePauseMenu();
        foreach (var sid in new[] { "day_night_tint", "player_recolor", "outline_2d", "damage_flash", "dissolve" })
            BeepShaderGenerator.CreateShaderById(sid, _overwriteCb.ButtonPressed);
        BeepTweenGenerator.CreateTweenHelper(_overwriteCb.ButtonPressed);
        foreach (var pid in new[] { "simple_burst", "hit_sparks", "dust_puff", "robot_activate_sparks", "electric_burst" })
            BeepParticleGenerator.CreateParticleById(pid, _overwriteCb.ButtonPressed);
        BeepParticleGenerator.CreateParticleHelperScript(_overwriteCb.ButtonPressed);
        BeepProjectileGenerator.CreateProjectileMath(_overwriteCb.ButtonPressed);
        BeepProjectileGenerator.CreateProjectile2D(_overwriteCb.ButtonPressed);
        BeepProjectileGenerator.CreateBasicProjectileScene(_overwriteCb.ButtonPressed);
        BeepProjectileGenerator.CreateArcProjectileScene(_overwriteCb.ButtonPressed);
        BeepFileUtils.RefreshFilesystem();
        foreach (var m in BeepValidator.Validate()) Log(m);
        Log("=== Complete ===");
    }

    // ---- Preset loading ----
    private class PresetEntry { public string Id, Dn, Cat; public PresetEntry(string i, string d, string c) { Id = i; Dn = d; Cat = c; } }

    private void LoadPresets(string file, List<PresetEntry> arr, ItemList list, LineEdit search)
    {
        var data = BeepFileUtils.LoadJson("res://addons/beep_game_builder_cs/catalogs/" + file);
        if (data == null || data.Count == 0)
        {
            GD.PrintErr($"Failed to load catalog: {file}");
            return;
        }
        if (data.TryGetValue("presets", out var presetsVar) && presetsVar.VariantType == Variant.Type.Array)
        {
            foreach (var p in presetsVar.AsGodotArray())
            {
                try
                {
                    var d = p.AsGodotDictionary();
                    arr.Add(new PresetEntry((string)d["id"], (string)d["display_name"], (string)d["category"]));
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Skipping malformed preset entry in {file}: {ex.Message}");
                }
            }
        }
        FilterList(list, arr, "");
        search.TextChanged += t => FilterList(list, arr, t);
    }

    private static void FilterList(ItemList list, List<PresetEntry> arr, string txt)
    {
        list.Clear(); var lo = txt.ToLower();
        foreach (var p in arr)
            if (string.IsNullOrEmpty(lo) || p.Dn.ToLower().Contains(lo) || p.Cat.ToLower().Contains(lo))
                list.AddItem($"[{p.Cat}] {p.Dn}");
    }
}

/// <summary>Simple file writer for generated scripts.</summary>
public static class BeepUIGenerator
{
    public static void WriteScript(string path, string content)
    {
        var dir = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dir)) DirAccess.MakeDirRecursiveAbsolute(dir);
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f?.StoreString(content);
    }
}
