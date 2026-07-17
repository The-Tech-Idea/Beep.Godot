using Godot;
using System;
using System.Collections.Generic;

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
    public partial class GameApp : Node, ISaveable
    {
        public enum Difficulty { Easy, Normal, Hard, Nightmare }
        public enum PerformanceMode { Low, Normal, High }

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
        /// <summary>True if a game is currently running (started or loaded).
        /// False when on main menu or game over. Used to enable/disable Save button.</summary>
        [Export] public bool IsGameRunning { get; set; } = false;
        /// <summary>True if game is paused (separate from IsGameRunning).</summary>
        [Export] public bool IsPaused { get; set; } = false;
        /// <summary>Current level / stage index (0-based). -1 = not in a level.</summary>
        [Export] public int CurrentLevel { get; set; } = -1;
        /// <summary>Total score accumulated this session (across levels).</summary>
        [Export] public int SessionScore { get; set; } = 0;
        /// <summary>Selected character/class id for the current run (shooter, etc.).</summary>
        [Export] public string SelectedCharacter { get; set; } = "";
        /// <summary>Selected vehicle id for the current run (racing, etc.).</summary>
        [Export] public string SelectedVehicle { get; set; } = "";
        /// <summary>Highest level reached (for unlocks/progression).</summary>
        [Export] public int MaxLevelReached { get; set; } = 0;
        /// <summary>Current difficulty level.</summary>
        [Export] public Difficulty CurrentDifficulty { get; set; } = Difficulty.Normal;
        /// <summary>Game mode (Story, Arcade, Creative, Survival, etc).</summary>
        [Export] public string GameMode { get; set; } = "Story";
        /// <summary>Difficulty multiplier for scoring (1.0 = normal, 2.0 = double points).</summary>
        [Export] public float DifficultyMultiplier { get; set; } = 1.0f;
        /// <summary>When game session started (in ticks).</summary>
        [Export] public long SessionStartTicks { get; set; } = 0;
        /// <summary>Total playtime this session (seconds).</summary>
        [Export] public double SessionPlaytimeSeconds { get; set; } = 0;

        [ExportGroup("Statistics")]
        /// <summary>Total games ever played.</summary>
        [Export] public int GamesPlayedTotal { get; set; } = 0;
        /// <summary>Total games won.</summary>
        [Export] public int GamesWonTotal { get; set; } = 0;
        /// <summary>Total games lost.</summary>
        [Export] public int GamesLostTotal { get; set; } = 0;
        /// <summary>All-time best score.</summary>
        [Export] public int BestScore { get; set; } = 0;
        /// <summary>Total playtime across all sessions (minutes).</summary>
        [Export] public int TotalPlaytimeMinutes { get; set; } = 0;
        /// <summary>Last checkpoint level for quick resume.</summary>
        [Export] public int LastCheckpointLevel { get; set; } = -1;
        /// <summary>Whether a quicksave exists.</summary>
        [Export] public bool HasQuicksave { get; set; } = false;

        [ExportGroup("Debug")]
        /// <summary>Enable dev mode (cheats, unlimited lives, etc).</summary>
        [Export] public bool DevModeEnabled { get; set; } = false;
        /// <summary>If true, player has infinite lives (dev mode).</summary>
        [Export] public bool InfiniteLives { get; set; } = false;
        /// <summary>Skip tutorial screens (dev mode).</summary>
        [Export] public bool SkipTutorial { get; set; } = false;
        /// <summary>Current FPS for performance monitoring.</summary>
        [Export] public int CurrentFPS { get; set; } = 60;
        /// <summary>Performance mode (affects graphics quality).</summary>
        [Export] public PerformanceMode CurrentPerformanceMode { get; set; } = PerformanceMode.Normal;

        // ── Achievements & Progression ──
        private HashSet<string> _unlockedAchievements = new();
        private HashSet<int> _completedLevels = new();

        // ── Signals ──
        [Signal] public delegate void SessionScoreChangedEventHandler(int total);
        [Signal] public delegate void LevelChangedEventHandler(int level);
        [Signal] public delegate void GameRunningChangedEventHandler(bool running);
        [Signal] public delegate void GamePausedEventHandler();
        [Signal] public delegate void GameResumedEventHandler();
        [Signal] public delegate void DifficultyChangedEventHandler(int difficulty);
        [Signal] public delegate void AchievementUnlockedEventHandler(string achievementId);
        [Signal] public delegate void LevelCompletedEventHandler(int level);
        [Signal] public delegate void CheckpointReachedEventHandler(int level);
        [Signal] public delegate void SessionStartedEventHandler();
        [Signal] public delegate void SessionEndedEventHandler(bool won);
        [Signal] public delegate void DevModeToggledEventHandler(bool enabled);

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

            // Persist progression — but only the autoload. Instance resolves /root/GameApp
            // specifically, and [GlobalClass] means a second GameApp can be dropped into a
            // scene; that copy must not join and overwrite the real one's Progression.
            if (!Engine.IsEditorHint() && Instance == this)
                AddToGroup(SaveableHelper.Group);
        }

        // ── Convenience accessors (so callers can do GameApp.Instance.GameName etc.) ──
        public string GameName => Info?.GameName ?? "My Game";
        public string Version => Info?.Version ?? "0.1.0";
        public string ThemePreset => Info?.DefaultThemePreset ?? "Modern";
        /// <summary>Path to the genre's main gameplay scene. Prefers the value stamped into
        /// game_info.tres, but falls back to the genre's main_scene from the skin catalog
        /// when that value is missing or points at a file that isn't there — a stale or
        /// never-generated game_info.tres otherwise left this pointing at
        /// "res://scenes/main/main.tscn", which the generator never creates (it stamps
        /// res://scenes/main/&lt;genre&gt;_main.tscn), so New Game silently did nothing.</summary>
        public string GameScenePath
        {
            get
            {
                string? path = Info?.GameScenePath;
                if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) return path;

                var genre = UI.SkinCatalog.GetGenre(Info?.GenreId ?? "");
                if (!string.IsNullOrEmpty(genre?.MainScene))
                    return $"res://scenes/main/{genre!.MainScene}";

                return path ?? "";
            }
        }
        public string MainMenuPath => Info?.MainMenuPath ?? "res://scenes/ui/main_menu.tscn";
        public string SettingsScenePath => Info?.SettingsScenePath ?? "res://scenes/ui/settings_menu.tscn";
        public string GameOverScenePath => Info?.GameOverScenePath ?? "res://scenes/ui/game_over.tscn";
        public string PauseMenuPath => Info?.PauseMenuPath ?? "res://scenes/ui/pause_menu.tscn";
        public double CurrentSessionElapsed => ((long)Time.GetTicksMsec() - SessionStartTicks) / 1000.0;
        public float WinRate => GamesPlayedTotal > 0 ? (float)GamesWonTotal / GamesPlayedTotal * 100f : 0f;

        public override void _Process(double delta)
        {
            // Inert as an autoload (those don't run in-editor), but [GlobalClass] means this is
            // droppable into a scene — where writing the CurrentFPS/SessionPlaytimeSeconds
            // exports every frame dirties it continuously.
            if (Engine.IsEditorHint()) return;

            // Track FPS
            CurrentFPS = (int)Engine.GetFramesPerSecond();

            // Track session playtime when game is running
            if (IsGameRunning && !IsPaused)
            {
                SessionPlaytimeSeconds += delta;
            }
        }

        // ── Runtime mutators (emit signals so UI can react) ──
        public void AddSessionScore(int amount)
        {
            int scaledAmount = (int)(amount * DifficultyMultiplier);
            SessionScore += scaledAmount;
            EmitSignal(SignalName.SessionScoreChanged, SessionScore);
        }

        /// <summary>Move to a level. Navigation only — this does NOT mark it completed.
        ///
        /// It used to: entering level 5 immediately added it to the completed set and emitted
        /// LevelCompleted, so IsLevelCompleted(5) was true the moment the player arrived and
        /// had beaten nothing. Call <see cref="CompleteLevel"/> when the level is actually
        /// finished.</summary>
        public void SetLevel(int level)
        {
            CurrentLevel = level;
            if (level > MaxLevelReached) MaxLevelReached = level;
            EmitSignal(SignalName.LevelChanged, level);
        }

        /// <summary>Mark a level beaten. Idempotent — re-completing emits nothing.</summary>
        public void CompleteLevel(int level)
        {
            if (!_completedLevels.Add(level)) return;
            EmitSignal(SignalName.LevelCompleted, level);
        }

        public void ResetSession()
        {
            SessionScore = 0;
            CurrentLevel = -1;
            SelectedCharacter = "";
            // Was omitted while its structural twin SelectedCharacter (declared one line
            // apart, same per-run selection role) was cleared — a racing game calling
            // ResetSession between races kept the previous run's vehicle.
            SelectedVehicle = "";
            SessionPlaytimeSeconds = 0;
            SessionStartTicks = 0;
            EmitSignal(SignalName.SessionScoreChanged, SessionScore);
            EmitSignal(SignalName.LevelChanged, CurrentLevel);
        }

        public void SetGameRunning(bool running)
        {
            if (IsGameRunning == running) return;
            IsGameRunning = running;

            if (running)
            {
                SessionStartTicks = (long)Time.GetTicksMsec();
                SessionPlaytimeSeconds = 0;
                EmitSignal(SignalName.SessionStarted);
            }

            EmitSignal(SignalName.GameRunningChanged, running);
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;

            if (paused)
                EmitSignal(SignalName.GamePaused);
            else
                EmitSignal(SignalName.GameResumed);

            GetTree().Paused = paused;
        }

        public void SetDifficulty(Difficulty difficulty)
        {
            CurrentDifficulty = difficulty;
            DifficultyMultiplier = difficulty switch
            {
                Difficulty.Easy => 0.5f,
                Difficulty.Normal => 1.0f,
                Difficulty.Hard => 1.5f,
                Difficulty.Nightmare => 2.0f,
                _ => 1.0f
            };
            EmitSignal(SignalName.DifficultyChanged, (int)difficulty);
        }

        public void RecordGameEnd(bool won)
        {
            GamesPlayedTotal++;
            if (won)
            {
                GamesWonTotal++;
                if (SessionScore > BestScore)
                    BestScore = SessionScore;
            }
            else
            {
                GamesLostTotal++;
            }

            TotalPlaytimeMinutes += (int)(SessionPlaytimeSeconds / 60.0);
            EmitSignal(SignalName.SessionEnded, won);
        }

        public void UnlockAchievement(string achievementId)
        {
            if (_unlockedAchievements.Contains(achievementId)) return;
            _unlockedAchievements.Add(achievementId);
            EmitSignal(SignalName.AchievementUnlocked, achievementId);
        }

        public bool HasAchievement(string achievementId) => _unlockedAchievements.Contains(achievementId);

        public void SetCheckpoint(int level)
        {
            LastCheckpointLevel = level;
            HasQuicksave = true;
            EmitSignal(SignalName.CheckpointReached, level);
        }

        public void ToggleDevMode()
        {
            DevModeEnabled = !DevModeEnabled;
            if (DevModeEnabled)
            {
                GD.Print("[GameApp] Dev mode ENABLED - cheats available");
            }
            else
            {
                InfiniteLives = false;
                SkipTutorial = false;
                GD.Print("[GameApp] Dev mode disabled");
            }
            EmitSignal(SignalName.DevModeToggled, DevModeEnabled);
        }

        public bool IsLevelCompleted(int level) => _completedLevels.Contains(level);
        public int TotalLevelsCompleted => _completedLevels.Count;
        public List<string> GetUnlockedAchievements() => new(_unlockedAchievements);

        // ════════════════════════════════════════════════════════════════
        //  ISaveable — progression persistence
        //
        //  GameApp tracked the level, completed levels, achievements and lifetime stats
        //  purely in memory: _completedLevels and _unlockedAchievements were plain HashSets
        //  and nothing wrote them to disk, so every achievement and every beaten level was
        //  lost on quit. CurrentLevel likewise came back as -1 after a load, which
        //  LevelLoaderComponent clamps to FirstLevelIndex — so every save reopened on
        //  level 1 regardless of where the player had got to.
        //
        //  Joins the saveables group from _Ready (see below) rather than via an opt-in
        //  export: this is the autoload, there is exactly one, so it cannot collide.
        // ════════════════════════════════════════════════════════════════

        public void Save(GameBuilder.GameStateData state)
        {
            var p = state.Progression;
            p.CurrentLevel = CurrentLevel;
            p.MaxLevelReached = MaxLevelReached;
            p.CompletedLevels = new List<int>(_completedLevels);
            p.UnlockedAchievements = new List<string>(_unlockedAchievements);
            p.GamesPlayedTotal = GamesPlayedTotal;
            p.GamesWonTotal = GamesWonTotal;
            p.GamesLostTotal = GamesLostTotal;
            p.BestScore = BestScore;
            p.TotalPlaytimeMinutes = TotalPlaytimeMinutes;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var p = state.Progression;
            MaxLevelReached = p.MaxLevelReached;
            _completedLevels = new HashSet<int>(p.CompletedLevels);
            _unlockedAchievements = new HashSet<string>(p.UnlockedAchievements);
            GamesPlayedTotal = p.GamesPlayedTotal;
            GamesWonTotal = p.GamesWonTotal;
            GamesLostTotal = p.GamesLostTotal;
            BestScore = p.BestScore;
            TotalPlaytimeMinutes = p.TotalPlaytimeMinutes;

            // Last, and via SetLevel, so LevelChanged fires for anything listening.
            SetLevel(p.CurrentLevel);
        }
    }
}
