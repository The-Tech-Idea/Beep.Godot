using Godot;
using System;
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
        /// <summary>
        /// Legacy serialized export retained so older scenes still load. MainGame no longer
        /// builds HUD nodes at startup; authored scenes own their HUD layout.
        /// </summary>
        [Export] public bool BuildDefaultHud { get; set; } = false;
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
        private Godot.Control? _hudRoot;
        private Godot.Control? _pauseRoot;
        private Godot.Control? _transitionRoot;
        private Node? _currentLevel;
        private Node2D? _player;
        private readonly List<string> _resolvedLevelPaths = new();

        public Node? CurrentLevel => _currentLevel;
        public Node2D? Player => _player;
        public Godot.Control? HudRoot => _hudRoot;
        public Godot.Control? PauseRoot => _pauseRoot;
        public Godot.Control? TransitionRoot => _transitionRoot;

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
            _hudRoot = GetNodeOrNull<Godot.Control>(HudRootPath);
            _pauseRoot = GetNodeOrNull<Godot.Control>(PauseRootPath);
            _transitionRoot = GetNodeOrNull<Godot.Control>(TransitionRootPath);

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

        // MainGame intentionally does not construct HUD controls at runtime.
        // Templates and game scenes own HUD nodes.
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
