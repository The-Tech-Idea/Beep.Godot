using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The single global game node. Registered as the "GameApp" autoload so any
    /// scene references it via /root/GameApp (C# <c>GameApp.Instance</c>, GDScript
    /// <c>get_node("/root/GameApp")</c>). Drop-in referenceable from every scene.
    ///
    /// Two kinds of data, clearly separated:
    /// • <see cref="Info"/> — the static <see cref="GameBuilder.GameInfo"/> resource
    ///   (game name, version, genre, theme preset, resolution, scene paths, tuning).
    ///   Edited once in game_info.tres; rarely changes at runtime.
    /// • The fields below — RUNTIME global/session state that changes during play
    ///   (current level, session score, audio/video settings, active character…).
    ///   This is the stuff that didn't belong on the static GameInfo resource.
    ///
    /// Replaces direct GameInfo.Instance reads for code that needs both config
    /// and live state. Existing GameInfo.Instance code keeps working — GameApp
    /// exposes the same config via <see cref="Info"/>.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameApp : Node
    {
        // ── Static config (the GameInfo resource) ──
        /// <summary>The game's static configuration. Loaded from res://game_info.tres
        /// on _Ready if not set, or assigned via the inspector.</summary>
        [Export] public GameBuilder.GameInfo? Info { get; set; }

        /// <summary>OPTIONAL texture-based UI skin. When set, the theme engine builds
        /// StyleBoxTexture (9-patch) for all UI nodes that have a matching texture,
        /// instead of procedural StyleBoxFlat. Set in the inspector or via game_info.tres.</summary>
        [Export] public UI.UISkin? Skin { get; set; }

        // ── Runtime / session state (changes during play) ──
        [ExportGroup("Session")]
        /// <summary>Current level / stage index (0-based). -1 = not in a level.</summary>
        [Export] public int CurrentLevel { get; set; } = -1;
        /// <summary>Total score accumulated this session (across levels).</summary>
        [Export] public int SessionScore { get; set; } = 0;
        /// <summary>Selected character/class id for the current run (shooter, etc.).</summary>
        [Export] public string SelectedCharacter { get; set; } = "";
        /// <summary>Highest level reached (for unlocks/progression).</summary>
        [Export] public int MaxLevelReached { get; set; } = 0;

        [Signal] public delegate void SessionScoreChangedEventHandler(int total);
        [Signal] public delegate void LevelChangedEventHandler(int level);

        /// <summary>Convenience: the nearest SettingsComponent (autoload or scene).
        /// User settings (audio/display/language) are owned by SettingsComponent, not here.</summary>
        public UI.SettingsComponent? Settings => UI.SettingsComponent.Instance;

        private static GameApp? _instance;

        /// <summary>The autoloaded GameApp, or null if not registered.</summary>
        public static GameApp? Instance
        {
            get
            {
                if (_instance != null && GodotObject.IsInstanceValid(_instance)) return _instance;
                if (Engine.GetMainLoop() is SceneTree tree
                    && tree.Root.GetNodeOrNull<GameApp>("/root/GameApp") is { } ga)
                {
                    _instance = ga;
                    return ga;
                }
                return null;
            }
        }

        public override void _EnterTree()
        {
            // Cache the autoload reference so callers don't walk the tree every read.
            if (GetParent() == GetTree().Root)
                _instance = this;
        }

        public override void _Ready()
        {
            // Load the static config resource if one wasn't assigned in the inspector.
            if (Info == null && ResourceLoader.Exists(GameBuilder.GameInfo.TresPath))
                Info = ResourceLoader.Load<GameBuilder.GameInfo>(GameBuilder.GameInfo.TresPath);
            // Load the UI skin from GameInfo if not already set in the inspector.
            if (Skin == null)
                Skin = Info?.Skin;
        }

        // ── Convenience accessors (so callers can do GameApp.Instance.GameName etc.) ──
        public string GameName => Info?.GameName ?? "My Game";
        public string Version => Info?.Version ?? "0.1.0";
        public string ThemePreset => Info?.DefaultThemePreset ?? "Modern";
        public string GameScenePath => Info?.GameScenePath ?? "res://scenes/main/main.tscn";
        public string MainMenuPath => Info?.MainMenuPath ?? "res://scenes/ui/main_menu.tscn";
        public string SettingsScenePath => Info?.SettingsScenePath ?? "res://scenes/ui/settings_menu.tscn";
        public string GameOverScenePath => Info?.GameOverScenePath ?? "res://scenes/ui/game_over.tscn";

        // ── Runtime mutators (emit signals so UI can react) ──
        public void AddSessionScore(int amount)
        {
            SessionScore += amount;
            EmitSignal(SignalName.SessionScoreChanged, SessionScore);
        }

        public void SetLevel(int level)
        {
            CurrentLevel = level;
            if (level > MaxLevelReached) MaxLevelReached = level;
            EmitSignal(SignalName.LevelChanged, level);
        }

        public void ResetSession()
        {
            SessionScore = 0;
            CurrentLevel = -1;
            SelectedCharacter = "";
            EmitSignal(SignalName.SessionScoreChanged, SessionScore);
            EmitSignal(SignalName.LevelChanged, CurrentLevel);
        }
    }
}
