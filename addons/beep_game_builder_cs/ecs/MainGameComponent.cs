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
            var stack = AddCornerStack(host, "TopLeft", Control.LayoutPreset.TopLeft, 12, 12);
            AddPair(stack, "ScoreLabel", "SCORE", "0");
            AddPair(stack, "LevelLabel", "LEVEL", "1");
            AddPair(stack, "LivesLabel", "LIVES", "x 3");
            AddMeter(stack, "HealthLabel", "100 / 100");
            component.Name = componentName;
            host.AddChild(component);
        }

        private void BuildRpgHud(Control host)
        {
            var stack = AddCornerStack(host, "TopLeft", Control.LayoutPreset.TopLeft, 12, 12);
            AddPair(stack, "LevelLabel", "LEVEL", "1");
            AddMeter(stack, "HealthLabel", "100 / 100");
            AddMeter(stack, "ManaLabel", "40 / 40", UiSurface.Role.Info);

            var quest = AddCornerStack(host, "QuestBox", Control.LayoutPreset.BottomLeft, 12, 12);
            AddPair(quest, "QuestLabel", "QUEST", "No active quest", 260);
            host.AddChild(new RpgHudComponent
            {
                Name = "RpgHud",
                QuestPath = new NodePath("QuestBox/Stack/QuestLabel"),
            });
        }

        private void BuildShooterHud(Control host)
        {
            BuildCommonReadouts(host);
            var bottom = AddCornerStack(host, "BottomRight", Control.LayoutPreset.BottomRight, 12, 12);
            AddMeter(bottom, "AmmoLabel", "30 / 90", UiSurface.Role.Warning);
            AddPair(bottom, "WaveLabel", "WAVE", "1", 190);
            host.AddChild(new ShooterHudComponent
            {
                Name = "ShooterHud",
                AmmoPath = new NodePath("BottomRight/Stack/AmmoLabel"),
                WavePath = new NodePath("BottomRight/Stack/WaveLabel"),
            });
        }

        private void BuildSurvivalHud(Control host)
        {
            var stack = AddCornerStack(host, "Vitals", Control.LayoutPreset.BottomLeft, 12, 12);
            AddMeter(stack, "HealthLabel", "100", UiSurface.Role.Success);
            AddMeter(stack, "HungerLabel", "100", UiSurface.Role.Warning);
            AddMeter(stack, "ThirstLabel", "100", UiSurface.Role.Info);
            AddMeter(stack, "StaminaLabel", "100", UiSurface.Role.Success);
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
            var bar = AddTopBar(host);
            AddResourceRow(bar, "PopulationRow", "PopulationLabel", "POP", "0");
            AddResourceRow(bar, "BudgetRow", "BudgetLabel", "FUNDS", "0");
            AddResourceRow(bar, "PowerRow", "PowerLabel", "POWER", "0 / 0");
            AddResourceRow(bar, "HappinessRow", "HappinessLabel", "HAPPY", "100%");
            AddResourceRow(bar, "DateRow", "DateLabel", "DATE", "Yr 1");
            host.AddChild(new CityBuilderHudComponent { Name = "CityBuilderHud" });
        }

        private void BuildStrategyHud(Control host)
        {
            var bar = AddTopBar(host);
            AddPair(bar, "GoldLabel", "GOLD", "0", 150);
            AddPair(bar, "FoodLabel", "FOOD", "0", 150);
            AddPair(bar, "WoodLabel", "WOOD", "0", 150);
            AddPair(bar, "UnitsLabel", "UNITS", "0", 150);
            AddPair(AddCornerStack(host, "TurnBox", Control.LayoutPreset.TopRight, 12, 12), "TurnLabel", "TURN", "1", 150);
            host.AddChild(new StrategyHudComponent
            {
                Name = "StrategyHud",
                TurnPath = new NodePath("TurnBox/Stack/TurnLabel"),
            });
        }

        private void BuildPuzzleHud(Control host)
        {
            var stack = AddCornerStack(host, "TopCenter", Control.LayoutPreset.CenterTop, 0, 12);
            AddPair(stack, "ScoreLabel", "SCORE", "0", 220);
            AddMeter(stack, "TargetLabel", "0 / 1000", UiSurface.Role.Info, 220);
            AddMeter(stack, "MovesLabel", "30 moves", UiSurface.Role.Warning, 220);
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
            var stats = AddCornerStack(host, "TopLeft", Control.LayoutPreset.TopLeft, 12, 12);
            AddPair(stats, "LapLabel", "LAP", "1 / 3");
            AddPair(stats, "PositionLabel", "POS", "P1");
            AddPair(stats, "LapTimeLabel", "TIME", "00:00.00");
            var speed = AddCornerStack(host, "SpeedBox", Control.LayoutPreset.BottomRight, 12, 12);
            AddMeter(speed, "SpeedLabel", "0 km/h", UiSurface.Role.Info, 220);
            host.AddChild(new RacingHudComponent
            {
                Name = "RacingHud",
                SpeedPath = new NodePath("SpeedBox/Stack/SpeedLabel"),
            });
        }

        private void BuildCardGameHud(Control host)
        {
            var topLeft = AddCornerStack(host, "TopLeft", Control.LayoutPreset.TopLeft, 12, 12);
            AddMeter(topLeft, "HealthLabel", "30 / 30", UiSurface.Role.Success);
            AddPair(AddCornerStack(host, "TopRight", Control.LayoutPreset.TopRight, 12, 12), "GoldLabel", "GOLD", "0", 170);
            AddMeter(AddCornerStack(host, "EnergyBox", Control.LayoutPreset.BottomLeft, 12, 12), "EnergyLabel", "3 / 3", UiSurface.Role.Info, 190);
            var bottom = AddCornerStack(host, "BottomRight", Control.LayoutPreset.BottomRight, 12, 12);
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
            var stack = AddCornerStack(host, "TopLeft", Control.LayoutPreset.TopLeft, 12, 12);
            AddPair(stack, "ScoreLabel", "SCORE", "0");
            AddPair(stack, "LevelLabel", "LEVEL", "1");
            AddPair(stack, "LivesLabel", "LIVES", "x 3");
            AddMeter(stack, "HealthLabel", "100 / 100");
        }

        private static VBoxContainer AddCornerStack(Control host, string name, Control.LayoutPreset preset, int x, int y)
        {
            var margin = new MarginContainer
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorsPreset = (int)preset,
            };
            margin.SetAnchorsAndOffsetsPreset(preset);
            margin.AddThemeConstantOverride("margin_left", x);
            margin.AddThemeConstantOverride("margin_right", x);
            margin.AddThemeConstantOverride("margin_top", y);
            margin.AddThemeConstantOverride("margin_bottom", y);
            host.AddChild(margin);

            var stack = new VBoxContainer { Name = name == "TopLeft" ? "StatsVBox" : "Stack", MouseFilter = Control.MouseFilterEnum.Ignore };
            stack.AddThemeConstantOverride("separation", 5);
            margin.AddChild(stack);
            return stack;
        }

        private static HBoxContainer AddTopBar(Control host)
        {
            var margin = new MarginContainer
            {
                Name = "TopBar",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 12);
            host.AddChild(margin);

            var bar = new HBoxContainer { Name = "Bar", MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AddThemeConstantOverride("separation", 6);
            margin.AddChild(bar);
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

        private static void AddMeter(Container parent, string name, string readout, UiSurface.Role fill = UiSurface.Role.Success, int width = 176)
        {
            parent.AddChild(new KitMeter
            {
                Name = name,
                CustomMinimumSize = new Vector2(width, 24),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Value = 1,
                Segments = 10,
                Fill = fill,
                EndCaps = true,
                Readout = readout,
            });
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
