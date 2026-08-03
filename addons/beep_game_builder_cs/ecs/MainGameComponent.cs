using Godot;
using System.Collections.Generic;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Shared gameplay shell based on the MainGame pattern:
    /// stable roots for systems, level content, entities, effects, HUD, pause, transitions and debug.
    ///
    /// Genre scenes become content/configuration. The shell stays alive while levels are swapped
    /// underneath it, so player, HUD, game flow, weather and save/session systems have stable homes.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MainGameComponent : Node
    {
        [Export] public bool AutoStart { get; set; } = true;
        [Export] public bool AutoConfigureFromGameInfo { get; set; } = true;
        [Export] public int FirstLevelIndex { get; set; } = 1;

        [ExportGroup("Scene Paths")]
        [Export] public string GenreIdOverride { get; set; } = "";
        [Export] public string PlayerScenePath { get; set; } = "res://scenes/player/player_template.tscn";
        [Export] public string AddonPlayerScenePath { get; set; } = "res://addons/beep_game_builder_cs/templates/scenes/player_template.tscn";
        [Export] public string AtmosphereScenePath { get; set; } = "res://addons/beep_game_builder_cs/templates/scenes/atmosphere.tscn";
        [Export] public bool BuildDefaultHud { get; set; } = true;
        [Export] public bool BuildGenreScreenOpeners { get; set; } = true;

        [ExportGroup("Roots")]
        [Export] public NodePath SystemsRootPath { get; set; } = new("Systems");
        [Export] public NodePath LevelRootPath { get; set; } = new("World/LevelRoot");
        [Export] public NodePath EntityRootPath { get; set; } = new("World/EntityRoot");
        [Export] public NodePath EffectRootPath { get; set; } = new("World/EffectRoot");
        [Export] public NodePath HudRootPath { get; set; } = new("HudLayer/HudRoot");
        [Export] public NodePath PauseRootPath { get; set; } = new("PauseLayer/PauseRoot");
        [Export] public NodePath TransitionRootPath { get; set; } = new("TransitionLayer/TransitionRoot");

        [Signal] public delegate void PlayerReadyEventHandler(Node2D player);
        [Signal] public delegate void LevelLoadedEventHandler(int level, Node levelRoot);
        [Signal] public delegate void LevelLoadFailedEventHandler(int level, string reason);

        private Node? _systemsRoot;
        private Node2D? _levelRoot;
        private Node2D? _entityRoot;
        private Node2D? _effectRoot;
        private Control? _hudRoot;
        private Control? _pauseRoot;
        private Control? _transitionRoot;
        private Node? _currentLevel;
        private Node2D? _player;
        private readonly List<string> _resolvedLevelPaths = new();

        public Node? CurrentLevel => _currentLevel;
        public Node2D? Player => _player;
        public Control? HudRoot => _hudRoot;
        public Control? PauseRoot => _pauseRoot;
        public Control? TransitionRoot => _transitionRoot;

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;
            ResolveRoots();
            ConfigureFromGameInfo();
            if (AutoStart) CallDeferred(nameof(StartGame));
        }

        private void ResolveRoots()
        {
            _systemsRoot = GetNodeOrNull(SystemsRootPath);
            _levelRoot = GetNodeOrNull<Node2D>(LevelRootPath);
            _entityRoot = GetNodeOrNull<Node2D>(EntityRootPath);
            _effectRoot = GetNodeOrNull<Node2D>(EffectRootPath);
            _hudRoot = GetNodeOrNull<Control>(HudRootPath);
            _pauseRoot = GetNodeOrNull<Control>(PauseRootPath);
            _transitionRoot = GetNodeOrNull<Control>(TransitionRootPath);

            _systemsRoot ??= EnsureChild<Node>("Systems", this);
            _levelRoot ??= EnsurePath<Node2D>("World/LevelRoot");
            _entityRoot ??= EnsurePath<Node2D>("World/EntityRoot");
            _effectRoot ??= EnsurePath<Node2D>("World/EffectRoot");
        }

        private void ConfigureFromGameInfo()
        {
            _resolvedLevelPaths.Clear();
            string genre = GenreId();
            for (int i = FirstLevelIndex; i < FirstLevelIndex + 64; i++)
            {
                string stamped = $"res://scenes/levels/{genre}/level_{i}.tscn";
                string addon = $"res://addons/beep_game_builder_cs/templates/scenes/levels/{genre}/level_{i}.tscn";
                if (ResourceLoader.Exists(stamped)) _resolvedLevelPaths.Add(stamped);
                else if (ResourceLoader.Exists(addon)) _resolvedLevelPaths.Add(addon);
                else if (i == FirstLevelIndex) continue;
                else break;
            }

            if (!AutoConfigureFromGameInfo) return;
            var info = GameBuilder.GameInfo.Instance;
            if (info == null) return;
            if (!string.IsNullOrEmpty(info.PlayerScenePath))
                PlayerScenePath = info.PlayerScenePath;
        }

        public void StartGame()
        {
            EnsureAtmosphere();
            EnsurePlayer();
            EnsureDefaultHud();
            EnsureGenreScreenOpeners();
            int level = GameApp.Instance?.CurrentLevel ?? FirstLevelIndex;
            if (level < FirstLevelIndex) level = FirstLevelIndex;
            LoadLevel(level);
        }

        public void LoadLevel(int level)
        {
            if (_levelRoot == null)
            {
                EmitSignal(SignalName.LevelLoadFailed, level, "LevelRoot missing");
                GD.PushError("[MainGame] LevelRoot missing. Add World/LevelRoot or set LevelRootPath.");
                return;
            }

            int index = level - FirstLevelIndex;
            if (index < 0 || index >= _resolvedLevelPaths.Count)
            {
                string reason = $"no level path for level {level} in genre '{GenreId()}'";
                EmitSignal(SignalName.LevelLoadFailed, level, reason);
                GD.PushError($"[MainGame] {reason}.");
                return;
            }

            string path = _resolvedLevelPaths[index];
            var packed = GD.Load<PackedScene>(path);
            if (packed == null)
            {
                EmitSignal(SignalName.LevelLoadFailed, level, "PackedScene load failed");
                GD.PushError($"[MainGame] Could not load level scene: {path}");
                return;
            }

            if (_currentLevel != null && GodotObject.IsInstanceValid(_currentLevel))
                _currentLevel.QueueFree();

            _currentLevel = packed.Instantiate();
            _levelRoot.AddChild(_currentLevel);
            PlacePlayerAtSpawn();
            SetupLevelCamera();
            EmitSignal(SignalName.LevelLoaded, level, _currentLevel);
        }

        private void EnsurePlayer()
        {
            if (_player != null && GodotObject.IsInstanceValid(_player)) return;
            if (_entityRoot == null)
            {
                GD.PushError("[MainGame] EntityRoot missing. Add World/EntityRoot or set EntityRootPath.");
                return;
            }

            string path = ResourceLoader.Exists(PlayerScenePath) ? PlayerScenePath : AddonPlayerScenePath;
            var packed = GD.Load<PackedScene>(path);
            if (packed == null)
            {
                GD.PushError($"[MainGame] Could not load player scene: {path}");
                return;
            }

            var instance = packed.Instantiate();
            if (instance is not Node2D body)
            {
                instance.Free();
                GD.PushError($"[MainGame] Player scene root must be Node2D: {path}");
                return;
            }

            _player = body;
            _player.Name = "Player";
            _entityRoot.AddChild(_player);
            EmitSignal(SignalName.PlayerReady, _player);
        }

        private void EnsureDefaultHud()
        {
            if (!BuildDefaultHud || _hudRoot == null) return;
            if (_hudRoot.FindChild("RuntimeHud", false, false) != null) return;

            var host = new Control
            {
                Name = "RuntimeHud",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            };
            host.AnchorRight = 1;
            host.AnchorBottom = 1;
            _hudRoot.AddChild(host);

            string genre = GenreId().ToLowerInvariant();
            switch (genre)
            {
                case "cardgame": BuildCardGameHud(host); break;
                case "citybuilder": BuildCityBuilderHud(host); break;
                case "puzzle": BuildPuzzleHud(host); break;
                case "racing": BuildRacingHud(host); break;
                case "rpg": BuildRpgHud(host); break;
                case "shooter": BuildShooterHud(host); break;
                case "strategy": BuildStrategyHud(host); break;
                case "survival": BuildSurvivalHud(host); break;
                case "topdown": BuildCommonHud(host, "TopDownHud", new TopDownHudComponent()); break;
                case "platformer":
                default: BuildCommonHud(host, "PlatformerHud", new PlatformerHudComponent()); break;
            }
        }

        private void BuildCommonHud(Control host, string componentName, GenreHudComponent component)
        {
            EnsureHudCollapse(host);
            var stack = AddFramedStack(host, "TopLeft", "StatsFrame", componentName.Contains("TopDown") ? "Field" : "Run",
                                       Control.LayoutPreset.TopLeft, 24, 24, new Vector2(200, 142));
            AddPair(stack, "ScoreLabel", "SCORE", "0", 180);
            AddPair(stack, "LevelLabel", componentName.Contains("TopDown") ? "AREA" : "LEVEL", "1", 180);
            AddPair(stack, "LivesLabel", "LIVES", "x 3", 180);
            AddMeter(stack, "HealthLabel", "100", UiSurface.Role.Success, 180, 18);
            AddForecast(host);
            if (componentName.Contains("TopDown"))
                AddMinimap(host, "Minimap", Control.LayoutPreset.TopRight, new Vector2(156, 156), new Vector2(-18, 120));
            component.Name = componentName;
            host.AddChild(component);
        }

        private void BuildRpgHud(Control host)
        {
            EnsureHudCollapse(host);
            var dock = AddEdgePanel(host, "BottomDock", Control.LayoutPreset.CenterBottom,
                                    new Vector2(724, 118), new Vector2(0, -20), "BottomFrame", "", new Vector2(26, 18));
            dock.AddThemeConstantOverride("margin_left", 18);
            dock.AddThemeConstantOverride("margin_top", 12);
            dock.AddThemeConstantOverride("margin_right", 18);
            dock.AddThemeConstantOverride("margin_bottom", 12);

            var bar = new HBoxContainer { Name = "Bar", MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AddThemeConstantOverride("separation", 14);
            bar.Alignment = BoxContainer.AlignmentMode.Center;
            dock.AddChild(bar);

            AddOrb(bar, "HealthLabel", "HP", UiSurface.Role.Danger, 92);
            var command = new VBoxContainer
            {
                Name = "CommandStack",
                CustomMinimumSize = new Vector2(456, 94),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            command.AddThemeConstantOverride("separation", 8);
            command.Alignment = BoxContainer.AlignmentMode.Center;
            bar.AddChild(command);
            AddPair(command, "LevelLabel", "LV", "1", 72);
            var slots = new HBoxContainer { Name = "ActionSlots", MouseFilter = Control.MouseFilterEnum.Ignore };
            slots.AddThemeConstantOverride("separation", 8);
            slots.Alignment = BoxContainer.AlignmentMode.Center;
            command.AddChild(slots);
            for (int i = 1; i <= 4; i++) AddSlot(slots, $"Skill{i}", i == 1, 0, i == 2 ? UiSurface.Role.Info : UiSurface.Role.Neutral);
            AddSlot(slots, "Potion1", false, 3, UiSurface.Role.Warning);
            AddSlot(slots, "Potion2", false, 2, UiSurface.Role.Info);
            AddOrb(bar, "ManaLabel", "MP", UiSurface.Role.Info, 92);

            var quest = AddEdgePanel(host, "QuestBox", Control.LayoutPreset.TopRight,
                                     new Vector2(300, 42), new Vector2(-34, 196), "QuestFrame", "Quest", new Vector2(20, 16));
            var q = new KitLabel
            {
                Name = "QuestLabel",
                Text = "Quest: Reach the village",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                ClipText = true,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            quest.AddChild(q);
            AddMinimap(host, "Minimap", Control.LayoutPreset.TopRight, new Vector2(140, 140), new Vector2(-18, 18));
            host.AddChild(new RpgHudComponent
            {
                Name = "RpgHud",
                LevelPath = new NodePath("BottomDock/Bar/CommandStack/LevelLabel"),
                HealthPath = new NodePath("BottomDock/Bar/HealthLabel"),
                ManaPath = new NodePath("BottomDock/Bar/ManaLabel"),
                QuestPath = new NodePath("QuestBox/QuestLabel"),
            });
        }

        private void BuildShooterHud(Control host)
        {
            EnsureHudCollapse(host);
            BuildCommonReadouts(host);
            AddForecast(host);
            var bottom = AddCornerStack(host, "BottomRight", Control.LayoutPreset.BottomRight, 24, 16, new Vector2(214, 76));
            AddPair(bottom, "AmmoLabel", "AMMO", "30 / 90", 210);
            AddPair(bottom, "WaveLabel", "WAVE", "1", 210);
            host.AddChild(new ShooterHudComponent
            {
                Name = "ShooterHud",
                AmmoPath = new NodePath("BottomRight/Stack/AmmoLabel"),
                WavePath = new NodePath("BottomRight/Stack/WaveLabel"),
            });
        }

        private void BuildSurvivalHud(Control host)
        {
            EnsureHudCollapse(host);
            AddForecast(host);
            var stack = AddFramedStack(host, "Vitals", "StatsFrame", "Vitals",
                                       Control.LayoutPreset.BottomLeft, 24, 18, new Vector2(226, 106));
            AddMeter(stack, "HealthLabel", "100", UiSurface.Role.Success, 214, 20);
            AddMeter(stack, "HungerLabel", "100", UiSurface.Role.Warning, 214, 20);
            AddMeter(stack, "ThirstLabel", "100", UiSurface.Role.Info, 214, 20);
            AddMeter(stack, "StaminaLabel", "100", UiSurface.Role.Success, 214, 20);
            AddMinimap(host, "Minimap", Control.LayoutPreset.TopRight, new Vector2(156, 156), new Vector2(-16, 120));
            host.AddChild(new SurvivalHudComponent
            {
                Name = "SurvivalHud",
                HealthPath = new NodePath("Vitals/Stack/HealthLabel"),
                HungerPath = new NodePath("Vitals/Stack/HungerLabel"),
                ThirstPath = new NodePath("Vitals/Stack/ThirstLabel"),
                StaminaPath = new NodePath("Vitals/Stack/StaminaLabel"),
            });
        }

        private void BuildCityBuilderHud(Control host)
        {
            EnsureHudCollapse(host);
            var strip = new HBoxContainer
            {
                Name = "ResourceStrip",
                CustomMinimumSize = new Vector2(780, 52),
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            strip.AddThemeConstantOverride("separation", 8);
            PlaceEdge(strip, Control.LayoutPreset.TopLeft, strip.CustomMinimumSize, new Vector2(16, 12));
            host.AddChild(strip);
            AddResourceBadge(strip, "Population", "0", UiSurface.Role.Success, "icon_population.png", 148);
            AddResourceBadge(strip, "Budget", "50,000", UiSurface.Role.Warning, "icon_treasury.png", 176);
            AddResourceBadge(strip, "Power", "0 / 0", UiSurface.Role.Info, "icon_power.png", 148);
            AddResourceBadge(strip, "Happiness", "100%", UiSurface.Role.Success, "icon_happiness.png", 148);
            AddResourceBadge(strip, "Date", "Yr 1", UiSurface.Role.Neutral, "icon_calendar.png", 172);

            var speed = new HBoxContainer { Name = "SpeedBar", MouseFilter = Control.MouseFilterEnum.Pass };
            speed.CustomMinimumSize = new Vector2(174, 38);
            PlaceEdge(speed, Control.LayoutPreset.TopRight, speed.CustomMinimumSize, new Vector2(16, 12));
            speed.AddChild(new GameSpeedComponent { Name = "Speed", TogglePauseAction = "" });
            host.AddChild(speed);

            var right = new KitPanelContainer
            {
                Name = "RightGadget",
                Title = "",
                TitleFontScale = 0.72f,
                TitleStyle = KitPanelContainer.HeaderStyle.None,
                Intent = KitPanelIntent.Hud,
                ShowWell = false,
                ExtraPadding = new Vector2(10, 8),
                CustomMinimumSize = new Vector2(242, 308),
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            PlaceEdge(right, Control.LayoutPreset.TopRight, right.CustomMinimumSize, new Vector2(16, 78));
            host.AddChild(right);
            var body = new VBoxContainer { Name = "Body", MouseFilter = Control.MouseFilterEnum.Ignore };
            body.AddThemeConstantOverride("separation", 8);
            right.AddChild(body);
            body.AddChild(new DemandMeterComponent
            {
                Name = "DemandMeter",
                CustomMinimumSize = new Vector2(218, 96),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                DrawBackdrop = false,
                LetterFontScale = 0.72f,
            });
            body.AddChild(new MinimapComponent
            {
                Name = "Minimap",
                CustomMinimumSize = new Vector2(218, 168),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var bottom = new KitPanelContainer
            {
                Name = "BottomDock",
                Title = "",
                TitleFontScale = 0.72f,
                TitleStyle = KitPanelContainer.HeaderStyle.None,
                Intent = KitPanelIntent.Hud,
                ShowWell = false,
                ExtraPadding = new Vector2(10, 8),
                CustomMinimumSize = new Vector2(0, 134),
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            PlaceEdge(bottom, Control.LayoutPreset.BottomWide, bottom.CustomMinimumSize, new Vector2(14, 14));
            host.AddChild(bottom);
            var margin = new MarginContainer { Name = "BuildMargin", MouseFilter = Control.MouseFilterEnum.Pass };
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            bottom.AddChild(margin);
            margin.AddChild(new BuildToolbarComponent
            {
                Name = "Toolbar",
                ItemSize = new Vector2(104, 82),
                TabSize = new Vector2(104, 34),
                PaletteHeight = 88f,
            });

            host.AddChild(new CityBuilderHudComponent
            {
                Name = "CityBuilderHud",
                PopulationPath = new NodePath("ResourceStrip/Population"),
                BudgetPath = new NodePath("ResourceStrip/Budget"),
                PowerPath = new NodePath("ResourceStrip/Power"),
                HappinessPath = new NodePath("ResourceStrip/Happiness"),
                DatePath = new NodePath("ResourceStrip/Date"),
                DemandMeterPath = new NodePath("RightGadget/Body/DemandMeter"),
            });
        }

        private void BuildStrategyHud(Control host)
        {
            EnsureHudCollapse(host);
            var bar = AddTopPanelBar(host, "TopBar", "Command", 64);
            AddPair(bar, "GoldLabel", "GOLD", "0", 184);
            AddPair(bar, "FoodLabel", "FOOD", "0", 184);
            AddPair(bar, "WoodLabel", "WOOD", "0", 184);
            AddMeter(bar, "UnitsLabel", "0", UiSurface.Role.Info, 168, 26);
            AddPair(AddCornerStack(host, "TurnBox", Control.LayoutPreset.CenterTop, 0, 76, new Vector2(240, 36)),
                    "TurnLabel", "TURN", "1", 240);
            AddMinimap(host, "Minimap", Control.LayoutPreset.BottomRight, new Vector2(180, 180), new Vector2(-16, -16));
            host.AddChild(new StrategyHudComponent
            {
                Name = "StrategyHud",
                TurnPath = new NodePath("TurnBox/Stack/TurnLabel"),
            });
        }

        private void BuildPuzzleHud(Control host)
        {
            EnsureHudCollapse(host);
            var stack = AddFramedStack(host, "TopCenter", "StatsFrame", "Puzzle",
                                       Control.LayoutPreset.CenterTop, 0, 22, new Vector2(300, 92));
            AddPair(stack, "ScoreLabel", "SCORE", "0", 260);
            AddMeter(stack, "TargetLabel", "0 / 1000", UiSurface.Role.Info, 300, 24);
            AddMeter(stack, "MovesLabel", "30 moves", UiSurface.Role.Warning, 260, 22);
            host.AddChild(new PuzzleHudComponent
            {
                Name = "PuzzleHud",
                ScorePath = new NodePath("TopCenter/Stack/ScoreLabel"),
                TargetPath = new NodePath("TopCenter/Stack/TargetLabel"),
                MovesPath = new NodePath("TopCenter/Stack/MovesLabel"),
            });
        }

        private void BuildRacingHud(Control host)
        {
            EnsureHudCollapse(host);
            AddForecast(host);
            var stats = AddFramedStack(host, "TopLeft", "StatsFrame", "Race",
                                       Control.LayoutPreset.TopLeft, 24, 24, new Vector2(222, 106));
            AddPair(stats, "LapLabel", "LAP", "1 / 3", 198);
            AddPair(stats, "PositionLabel", "POS", "P1", 198);
            AddPair(stats, "LapTimeLabel", "TIME", "00:00.00", 198);
            var speed = AddCornerStack(host, "SpeedBox", Control.LayoutPreset.BottomRight, 24, 24, new Vector2(190, 146));
            AddRadial(speed, "SpeedLabel", "0", UiSurface.Role.Info, 154);
            speed.AddChild(new KitLabel
            {
                Name = "SpeedUnit",
                Text = "km/h",
                CustomMinimumSize = new Vector2(154, 20),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            host.AddChild(new RacingHudComponent
            {
                Name = "RacingHud",
                SpeedPath = new NodePath("SpeedBox/Stack/SpeedLabel"),
            });
        }

        private void BuildCardGameHud(Control host)
        {
            EnsureHudCollapse(host);
            var topLeft = AddFramedStack(host, "TopLeft", "StatsFrame", "Hero",
                                         Control.LayoutPreset.TopLeft, 24, 28, new Vector2(206, 48));
            AddMeter(topLeft, "HealthLabel", "30", UiSurface.Role.Success, 196, 24);
            AddPair(AddCornerStack(host, "TopRight", Control.LayoutPreset.TopRight, 16, 18, new Vector2(194, 34)),
                    "GoldLabel", "GOLD", "0", 194);
            AddMeter(AddCornerStack(host, "EnergyBox", Control.LayoutPreset.BottomLeft, 24, 18, new Vector2(200, 60)),
                     "EnergyLabel", "3 / 3", UiSurface.Role.Info, 196, 28);
            var hand = new KitPanelContainer
            {
                Name = "HandZone",
                Title = "",
                TitleStyle = KitPanelContainer.HeaderStyle.None,
                Intent = KitPanelIntent.Hud,
                CustomMinimumSize = new Vector2(460, 76),
                ExtraPadding = new Vector2(14, 8),
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            PlaceEdge(hand, Control.LayoutPreset.CenterBottom, hand.CustomMinimumSize, new Vector2(0, 18));
            hand.AddChild(new KitLabel
            {
                Name = "HandLabel",
                Text = "Cards in hand",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            host.AddChild(hand);
            var bottom = AddCornerStack(host, "BottomRight", Control.LayoutPreset.BottomRight, 18, 18, new Vector2(190, 70));
            AddPair(bottom, "DeckLabel", "DECK", "0", 170);
            AddPair(bottom, "DiscardLabel", "DISCARD", "0", 170);
            host.AddChild(new CardGameHudComponent
            {
                Name = "CardGameHud",
                HealthPath = new NodePath("TopLeft/StatsVBox/HealthLabel"),
                GoldPath = new NodePath("TopRight/Stack/GoldLabel"),
                EnergyPath = new NodePath("EnergyBox/Stack/EnergyLabel"),
                DeckPath = new NodePath("BottomRight/Stack/DeckLabel"),
                DiscardPath = new NodePath("BottomRight/Stack/DiscardLabel"),
            });
        }

        private void BuildCommonReadouts(Control host)
        {
            var stack = AddFramedStack(host, "TopLeft", "StatsFrame", "Combat",
                                       Control.LayoutPreset.TopLeft, 24, 24, new Vector2(200, 142));
            AddPair(stack, "ScoreLabel", "SCORE", "0", 180);
            AddPair(stack, "LevelLabel", "LEVEL", "1", 180);
            AddPair(stack, "LivesLabel", "LIVES", "x 3", 180);
            AddMeter(stack, "HealthLabel", "100", UiSurface.Role.Success, 180, 18);
        }

        private static VBoxContainer AddCornerStack(Control host, string name, Control.LayoutPreset preset, int x, int y, Vector2? minSize = null)
        {
            var margin = new MarginContainer
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            Vector2 size = minSize ?? new Vector2(176, 30);
            margin.CustomMinimumSize = size;
            PlaceEdge(margin, preset, size, new Vector2(x, y));
            margin.AddThemeConstantOverride("margin_left", 0);
            margin.AddThemeConstantOverride("margin_right", 0);
            margin.AddThemeConstantOverride("margin_top", 0);
            margin.AddThemeConstantOverride("margin_bottom", 0);
            host.AddChild(margin);

            var stack = new VBoxContainer { Name = name == "TopLeft" ? "StatsVBox" : "Stack", MouseFilter = Control.MouseFilterEnum.Ignore };
            stack.AddThemeConstantOverride("separation", 5);
            margin.AddChild(stack);
            return stack;
        }

        private static VBoxContainer AddFramedStack(Control host, string panelName, string frameName, string title,
                                                    Control.LayoutPreset preset, int x, int y, Vector2 minSize)
        {
            var stack = AddCornerStack(host, panelName, preset, x, y, minSize);
            AddLinkedFrame(host, frameName, title, $"../{panelName}", new Vector2(42, 26),
                           preset, new Vector2(x, y), minSize, stack.GetParent<Control>());
            return stack;
        }

        private static MarginContainer AddEdgePanel(Control host, string name, Control.LayoutPreset preset,
                                                    Vector2 minSize, Vector2 offset, string frameName,
                                                    string title, Vector2 padding)
        {
            var panel = new MarginContainer
            {
                Name = name,
                CustomMinimumSize = minSize,
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            Vector2 inset = InsetFromLegacyOffset(preset, offset);
            PlaceEdge(panel, preset, minSize, inset);
            host.AddChild(panel);
            AddLinkedFrame(host, frameName, title, $"../{name}", padding, preset, inset, minSize, panel);
            return panel;
        }

        private static void AddLinkedFrame(Control host, string frameName, string title, string targetPath, Vector2 padding,
                                           Control.LayoutPreset preset, Vector2 targetInset, Vector2 targetSize, Control target)
        {
            Vector2 frameSize = targetSize + padding;
            var frame = new KitPanel
            {
                Name = frameName,
                Title = title,
                Intent = KitPanelIntent.Hud,
                TargetPath = new NodePath(targetPath),
                TargetPadding = padding,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = frameSize,
            };
            PlaceEdge(frame, preset, frameSize, FrameInset(preset, targetInset, padding));
            host.AddChild(frame);
            host.MoveChild(frame, Mathf.Max(0, target.GetIndex()));
        }

        private static Vector2 FrameInset(Control.LayoutPreset preset, Vector2 targetInset, Vector2 padding)
        {
            float x = Mathf.Max(0f, targetInset.X - padding.X * 0.5f);
            float y = Mathf.Max(0f, targetInset.Y - padding.Y * 0.5f);
            return preset switch
            {
                Control.LayoutPreset.TopWide or Control.LayoutPreset.BottomWide => new Vector2(0f, y),
                Control.LayoutPreset.CenterTop or Control.LayoutPreset.CenterBottom => new Vector2(targetInset.X, y),
                _ => new Vector2(x, y),
            };
        }

        private static HBoxContainer AddTopPanelBar(Control host, string name, string title, float height)
        {
            var panel = new KitPanelContainer
            {
                Name = name,
                Title = "",
                TitleStyle = KitPanelContainer.HeaderStyle.None,
                Intent = KitPanelIntent.Hud,
                ExtraPadding = new Vector2(10, 8),
                CustomMinimumSize = new Vector2(0, height),
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            PlaceEdge(panel, Control.LayoutPreset.TopWide, panel.CustomMinimumSize, Vector2.Zero);
            host.AddChild(panel);

            var bar = new HBoxContainer { Name = "Bar", MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AddThemeConstantOverride("separation", 10);
            bar.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(bar);
            return bar;
        }

        private static void AddResourceRow(Container bar, string rowName, string labelName, string label, string value)
        {
            var row = new HBoxContainer { Name = rowName, MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(new KitLabelValue
            {
                Name = labelName,
                CustomMinimumSize = new Vector2(150, 30),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Label = label,
                Value = value,
            });
            bar.AddChild(row);
        }

        private static void AddResourceBadge(Container parent, string name, string value, UiSurface.Role accent, string iconFile, int width)
        {
            Texture2D? icon = null;
            string path = $"res://addons/beep_game_builder_cs/textures/citybuilder/icons/{iconFile}";
            if (ResourceLoader.Exists(path)) icon = GD.Load<Texture2D>(path);
            parent.AddChild(new ResourceBadgeComponent
            {
                Name = name,
                Value = value,
                Accent = accent,
                Icon = icon,
                CustomMinimumSize = new Vector2(width, 48),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        private static void AddMeter(Container parent, string name, string readout, UiSurface.Role fill = UiSurface.Role.Success, int width = 176, int height = 24)
        {
            parent.AddChild(new KitMeter
            {
                Name = name,
                CustomMinimumSize = new Vector2(width, height),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Value = 1,
                Segments = 10,
                Fill = fill,
                EndCaps = true,
                Readout = readout,
            });
        }

        private static void AddOrb(Container parent, string name, string text, UiSurface.Role fill, int side)
        {
            parent.AddChild(new KitOrbMeter
            {
                Name = name,
                CustomMinimumSize = new Vector2(side, side),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Value = 1,
                Fill = fill,
                CentreText = text,
                Symbol = text,
            });
        }

        private static void AddRadial(Container parent, string name, string text, UiSurface.Role fill, int side)
        {
            parent.AddChild(new KitRadialMeter
            {
                Name = name,
                CustomMinimumSize = new Vector2(side, side),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Value = 0,
                Segments = 20,
                Fill = fill,
                GapDegrees = 70,
                CentreText = text,
            });
        }

        private static void AddSlot(Container parent, string name, bool selected, int count, UiSurface.Role rarity)
        {
            parent.AddChild(new KitInventorySlot
            {
                Name = name,
                CustomMinimumSize = new Vector2(46, 46),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Selected = selected,
                Count = count,
                Rarity = rarity,
            });
        }

        private static WeatherForecastUI AddForecast(Control host)
        {
            var forecast = new WeatherForecastUI
            {
                Name = "WeatherForecast",
                CustomMinimumSize = new Vector2(270, 82),
                MouseFilter = Control.MouseFilterEnum.Pass,
                StartCollapsed = true,
                ItemSize = new Vector2(58, 70),
                ItemSpacing = 6,
            };
            PlaceEdge(forecast, Control.LayoutPreset.TopRight, forecast.CustomMinimumSize, new Vector2(22, 22));
            host.AddChild(forecast);
            return forecast;
        }

        private static MinimapComponent AddMinimap(Control host, string name, Control.LayoutPreset preset, Vector2 size, Vector2 offset)
        {
            var map = new MinimapComponent
            {
                Name = name,
                CustomMinimumSize = size,
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            PlaceEdge(map, preset, size, InsetFromLegacyOffset(preset, offset));
            host.AddChild(map);
            return map;
        }

        private static Vector2 InsetFromLegacyOffset(Control.LayoutPreset preset, Vector2 offset) => preset switch
        {
            Control.LayoutPreset.TopRight or Control.LayoutPreset.BottomRight
                => new Vector2(Mathf.Abs(offset.X), Mathf.Abs(offset.Y)),
            Control.LayoutPreset.CenterBottom
                => new Vector2(Mathf.Abs(offset.X), Mathf.Abs(offset.Y)),
            Control.LayoutPreset.BottomLeft or Control.LayoutPreset.BottomWide
                => new Vector2(Mathf.Abs(offset.X), Mathf.Abs(offset.Y)),
            _ => new Vector2(Mathf.Abs(offset.X), Mathf.Abs(offset.Y)),
        };

        private static void PlaceEdge(Control c, Control.LayoutPreset preset, Vector2 size, Vector2 inset)
        {
            switch (preset)
            {
                case Control.LayoutPreset.TopLeft:
                    c.AnchorLeft = c.AnchorTop = c.AnchorRight = c.AnchorBottom = 0f;
                    c.OffsetLeft = inset.X; c.OffsetTop = inset.Y;
                    c.OffsetRight = inset.X + size.X; c.OffsetBottom = inset.Y + size.Y;
                    break;
                case Control.LayoutPreset.TopRight:
                    c.AnchorLeft = c.AnchorRight = 1f; c.AnchorTop = c.AnchorBottom = 0f;
                    c.OffsetLeft = -inset.X - size.X; c.OffsetRight = -inset.X;
                    c.OffsetTop = inset.Y; c.OffsetBottom = inset.Y + size.Y;
                    break;
                case Control.LayoutPreset.BottomLeft:
                    c.AnchorLeft = c.AnchorRight = 0f; c.AnchorTop = c.AnchorBottom = 1f;
                    c.OffsetLeft = inset.X; c.OffsetRight = inset.X + size.X;
                    c.OffsetTop = -inset.Y - size.Y; c.OffsetBottom = -inset.Y;
                    break;
                case Control.LayoutPreset.BottomRight:
                    c.AnchorLeft = c.AnchorRight = c.AnchorTop = c.AnchorBottom = 1f;
                    c.OffsetLeft = -inset.X - size.X; c.OffsetRight = -inset.X;
                    c.OffsetTop = -inset.Y - size.Y; c.OffsetBottom = -inset.Y;
                    break;
                case Control.LayoutPreset.CenterTop:
                    c.AnchorLeft = c.AnchorRight = 0.5f; c.AnchorTop = c.AnchorBottom = 0f;
                    c.OffsetLeft = -size.X * 0.5f + inset.X; c.OffsetRight = size.X * 0.5f + inset.X;
                    c.OffsetTop = inset.Y; c.OffsetBottom = inset.Y + size.Y;
                    break;
                case Control.LayoutPreset.CenterBottom:
                    c.AnchorLeft = c.AnchorRight = 0.5f; c.AnchorTop = c.AnchorBottom = 1f;
                    c.OffsetLeft = -size.X * 0.5f + inset.X; c.OffsetRight = size.X * 0.5f + inset.X;
                    c.OffsetTop = -inset.Y - size.Y; c.OffsetBottom = -inset.Y;
                    break;
                case Control.LayoutPreset.TopWide:
                    c.AnchorLeft = 0f; c.AnchorRight = 1f; c.AnchorTop = c.AnchorBottom = 0f;
                    c.OffsetLeft = inset.X; c.OffsetRight = -inset.X;
                    c.OffsetTop = inset.Y; c.OffsetBottom = inset.Y + size.Y;
                    break;
                case Control.LayoutPreset.BottomWide:
                    c.AnchorLeft = 0f; c.AnchorRight = 1f; c.AnchorTop = c.AnchorBottom = 1f;
                    c.OffsetLeft = inset.X; c.OffsetRight = -inset.X;
                    c.OffsetTop = -inset.Y - size.Y; c.OffsetBottom = -inset.Y;
                    break;
                default:
                    c.SetAnchorsAndOffsetsPreset(preset);
                    c.CustomMinimumSize = size;
                    break;
            }
        }

        private static void EnsureHudCollapse(Control host)
        {
            if (host.GetNodeOrNull<HudCollapseComponent>("HudCollapse") != null) return;
            host.AddChild(new HudCollapseComponent { Name = "HudCollapse" });
        }

        private static void AddPair(Container parent, string name, string label, string value, int width = 176)
        {
            parent.AddChild(new KitLabelValue
            {
                Name = name,
                CustomMinimumSize = new Vector2(width, 30),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Label = label,
                Value = value,
            });
        }

        private void EnsureGenreScreenOpeners()
        {
            if (!BuildGenreScreenOpeners || _systemsRoot == null) return;
            if (_systemsRoot.FindChild("GenreScreens", false, false) != null) return;
            var info = GameBuilder.GameInfo.Instance;
            if (info == null || info.GenreScenePaths.Count == 0) return;

            var host = new Node { Name = "GenreScreens" };
            _systemsRoot.AddChild(host);
            foreach (var entry in info.GenreScenePaths)
            {
                string key = entry.Key;
                if (string.IsNullOrEmpty(key) || IsEndStateRoute(key)) continue;
                var opener = new GenreScreenComponent
                {
                    Name = $"{ToPascal(key)}Screen",
                    ScreenKey = key,
                    OpenAction = key,
                    ScreenLayer = 30,
                    PauseWhileOpen = true,
                };
                host.AddChild(opener);
            }
        }

        private static bool IsEndStateRoute(string key)
            => key.EndsWith("Path", System.StringComparison.OrdinalIgnoreCase);

        private static string ToPascal(string key)
        {
            string[] parts = key.Split(new[] { '_', '-', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "Genre";
            var text = "";
            foreach (string part in parts)
                text += char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part[1..] : "");
            return text;
        }

        private void EnsureAtmosphere()
        {
            if (_effectRoot == null || string.IsNullOrEmpty(AtmosphereScenePath)) return;
            if (_effectRoot.FindChild("Atmosphere", false, false) != null) return;
            if (!ResourceLoader.Exists(AtmosphereScenePath)) return;
            var packed = GD.Load<PackedScene>(AtmosphereScenePath);
            var atmosphere = packed?.Instantiate();
            if (atmosphere != null) _effectRoot.AddChild(atmosphere);
        }

        private void PlacePlayerAtSpawn()
        {
            if (_player == null || _currentLevel == null) return;
            if (FindSpawn(_currentLevel) is { } spawn)
                _player.GlobalPosition = spawn.GlobalPosition;
        }

        private void SetupLevelCamera()
        {
            if (_currentLevel == null) return;
            Camera2D? camera = EntityComponent.FindComponent<Camera2D>(_currentLevel, true)
                ?? EntityComponent.FindComponent<Camera2D>(_player, true);
            if (camera == null) return;
            camera.Enabled = true;
            camera.MakeCurrent();

            if (_player != null)
            {
                foreach (var prop in camera.GetPropertyList())
                {
                    if (prop.TryGetValue("name", out var name) && name.AsString() == "target")
                    {
                        camera.Set("target", _player);
                        break;
                    }
                }
            }
        }

        private static Marker2D? FindSpawn(Node root)
            => root.FindChild("PlayerSpawn", true, false) as Marker2D
               ?? root.FindChild("DefaultPlayerSpawn", true, false) as Marker2D
               ?? root.FindChild("Spawn", true, false) as Marker2D;

        private string GenreId()
        {
            if (!string.IsNullOrEmpty(GenreIdOverride)) return GenreIdOverride;
            return GameBuilder.GameInfo.Instance?.GenreId ?? "platformer";
        }

        private T EnsurePath<T>(string path) where T : Node, new()
        {
            string[] parts = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            Node parent = this;
            for (int i = 0; i < parts.Length; i++)
            {
                Node? next = parent.GetNodeOrNull(parts[i]);
                if (next == null)
                {
                    next = i == parts.Length - 1 ? new T { Name = parts[i] } : new Node2D { Name = parts[i] };
                    parent.AddChild(next);
                }
                parent = next;
            }
            return (T)parent;
        }

        private static T EnsureChild<T>(string name, Node parent) where T : Node, new()
        {
            if (parent.GetNodeOrNull<T>(name) is { } existing) return existing;
            var node = new T { Name = name };
            parent.AddChild(node);
            return node;
        }
    }
}
