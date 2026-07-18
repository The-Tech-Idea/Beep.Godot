using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Gameplay state component: score, lives, and win/lose flow. Attach as a
    /// child of the game-scene root (or an autoload). UI components (HudComponent)
    /// connect to its signals to update their display.
    ///
    /// This is the runtime/playing counterpart to <c>GameInfo</c> (which holds
    /// static configuration). GameFlow holds the live state that changes during play.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameFlowComponent : GameplayComponent
    {
        [Export] public int Score { get; set; }
        [Export] public int Lives { get; set; } = 3;
        [Export] public int TargetScore { get; set; } = 1000;
        [Export] public bool AutoLoseOnZeroLives { get; set; } = true;

        /// <summary>When true, GameOver/LevelComplete signals automatically change
        /// the scene to the configured GameOver/LevelComplete paths from GameInfo.
        /// Set false if you want to handle the signal yourself (e.g. custom animation first).</summary>
        [Export] public bool AutoNavigateOnEnd { get; set; } = true;

        /// <summary>Delay (seconds) before navigating after GameOver/LevelComplete fires.
        /// Gives time for death animations, particle bursts, etc.</summary>
        [Export] public float NavigateDelay { get; set; } = 0f;

        [ExportGroup("Pause")]
        /// <summary>Open the pause overlay on the pause action (Escape by default), giving
        /// every gameplay scene a way back to the main menu. The overlay is instanced on
        /// top of the game rather than navigated to, so the scene stays loaded.</summary>
        [Export] public bool EnablePauseMenu { get; set; } = true;

        /// <summary>Input action that opens the pause overlay. The generator maps
        /// "pause" to Escape.</summary>
        [Export] public string PauseAction { get; set; } = "pause";

        /// <summary>Overlay to instance. Empty = GameInfo.PauseMenuPath.</summary>
        [Export] public string PauseMenuPathOverride { get; set; } = "";

        [Signal] public delegate void ScoreChangedEventHandler(int score);
        [Signal] public delegate void LivesChangedEventHandler(int lives);
        [Signal] public delegate void GameOverEventHandler();
        [Signal] public delegate void LevelCompleteEventHandler();

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;   // don't run session/GameInfo logic at design time (the other [Tool] components guard too)
            // Mark game as running
            if (GameApp.Instance != null)
                GameApp.Instance.SetGameRunning(true);

            // Every gameplay entry point comes through here, so this is where a run's state
            // is established: apply a save queued by Continue/Load, or seed fresh state for
            // a new run. Without it nothing ever called NewGame() and every save no-opped.
            GameStateManagerComponent.Instance?.BeginSession();

            // Seed from GameInfo tuning if available (target score, etc.).
            var info = GameBuilder.GameInfo.Instance;
            if (info != null) TargetScore = info.TargetScore;
            // Auto-wire GameOver/LevelComplete to scene transitions.
            if (AutoNavigateOnEnd)
            {
                GameOver += OnGameOver;
                LevelComplete += OnLevelComplete;
            }
        }

        public void AddScore(int amount)
        {
            if (!IsActive) return;
            Score += amount;
            // Keep the global session total in step with the live run score. These used to
            // diverge — the HUD read GameFlow.Score while GameApp.SessionScore (used for the
            // best-score record) was updated only by the now-removed ScoreComponent.
            GameApp.Instance?.AddSessionScore(amount);
            EmitSignal(SignalName.ScoreChanged, Score);
            if (Score >= TargetScore && TargetScore > 0)
                EmitSignal(SignalName.LevelComplete);
        }

        public void LoseLife(int amount = 1)
        {
            if (!IsActive) return;
            Lives = Mathf.Max(0, Lives - amount);
            EmitSignal(SignalName.LivesChanged, Lives);
            if (AutoLoseOnZeroLives && Lives <= 0)
                EmitSignal(SignalName.GameOver);
        }

        public void GainLife(int amount = 1)
        {
            if (!IsActive) return;
            Lives += amount;
            EmitSignal(SignalName.LivesChanged, Lives);
        }

        public void Reset(int startLives = 3)
        {
            Score = 0;
            Lives = startLives;
            EmitSignal(SignalName.ScoreChanged, Score);
            EmitSignal(SignalName.LivesChanged, Lives);
        }

        public void TriggerGameOver() => EmitSignal(SignalName.GameOver);
        public void TriggerLevelComplete() => EmitSignal(SignalName.LevelComplete);

        /// <summary>Called when the GameOver signal fires. If AutoNavigateOnEnd is true,
        /// changes scene to the GameOver path from GameInfo after an optional delay.</summary>
        public void OnGameOver()
        {
            if (GameApp.Instance != null)
                GameApp.Instance.SetGameRunning(false);

            if (!AutoNavigateOnEnd) return;
            // Prefer a genre's LevelFailedPath (puzzle ships level_failed.tscn) when set, so a
            // loss lands on the genre's fail screen instead of always the shared game_over.
            // Mirrors OnLevelComplete's fallback chain. Only NavigationComponent read
            // LevelFailedPath before, and nothing instances that, so level_failed was unreachable.
            var info = GameBuilder.GameInfo.Instance;
            string path = info?.LevelFailedPath;
            if (string.IsNullOrEmpty(path)) path = info?.GameOverScenePath ?? "res://scenes/ui/game_over.tscn";
            NavigateToScene(path);
        }

        /// <summary>Called when the LevelComplete signal fires. If AutoNavigateOnEnd is true,
        /// changes scene to the LevelComplete/LevelResults path from GameInfo after an optional delay.</summary>
        public void OnLevelComplete()
        {
            if (!AutoNavigateOnEnd) return;
            // Use LevelCompletePath if set (puzzle), otherwise LevelResultsPath (platformer),
            // otherwise fall back to game over.
            var info = GameBuilder.GameInfo.Instance;
            string path = info?.LevelCompletePath;
            if (string.IsNullOrEmpty(path)) path = info?.LevelResultsPath;
            if (string.IsNullOrEmpty(path)) path = info?.GameOverScenePath ?? "res://scenes/ui/game_over.tscn";
            NavigateToScene(path);
        }

        private void NavigateToScene(string path)
        {
            if (string.IsNullOrEmpty(path) || !IsActive) return;
            if (NavigateDelay > 0f)
            {
                // Defer the scene change so animations can play first.
                var tree = GetTree();
                if (tree != null)
                {
                    var timer = tree.CreateTimer(NavigateDelay);
                    timer.Timeout += () =>
                    {
                        if (GodotObject.IsInstanceValid(this) && IsActive)
                        {
                            var t = GetTree();
                            t?.ChangeSceneToFile(path);
                        }
                    };
                }
            }
            else
            {
                var tree2 = GetTree();
                tree2?.ChangeSceneToFile(path);
            }
        }

        // ── Pause overlay ────────────────────────────────────────────────────
        // Every gameplay scene needs a way back to the main menu. The pause overlay
        // provides it, but it is a separate scene that nothing instanced — so the
        // pause action did nothing during play. This component is already present in
        // every genre's main scene, so opening the overlay from here wires it up
        // everywhere without touching the scenes.
        //
        // Opening and closing are split by ProcessMode, so they never both fire:
        //   unpaused → this component gets input and opens the overlay
        //   paused   → the overlay's own PauseComponent is WhenPaused, so it gets the
        //              input and closes itself (as does its Resume button).

        private Node? _pauseOverlay;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Engine.IsEditorHint() || !EnablePauseMenu || !IsActive) return;
            if (string.IsNullOrEmpty(PauseAction) || !InputMap.HasAction(PauseAction)) return;
            if (!@event.IsActionPressed(PauseAction)) return;

            OpenPauseMenu();
            GetViewport()?.SetInputAsHandled();
        }

        /// <summary>Instance the pause overlay over the current scene and pause the tree.
        /// Reuses the existing instance if it is still alive (the overlay's Resume button
        /// frees itself, in which case a fresh one is created).</summary>
        public void OpenPauseMenu()
        {
            string path = !string.IsNullOrEmpty(PauseMenuPathOverride)
                ? PauseMenuPathOverride
                : GameApp.Instance?.PauseMenuPath ?? "";

            if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
            {
                GD.PushError($"[GameFlow] Pause menu scene not found: '{path}'. Set GameInfo.PauseMenuPath.");
                return;
            }

            if (!GodotObject.IsInstanceValid(_pauseOverlay))
            {
                var packed = GD.Load<PackedScene>(path);
                if (packed == null)
                {
                    GD.PushError($"[GameFlow] Could not load pause menu: {path}");
                    return;
                }

                _pauseOverlay = packed.Instantiate();
                // Parent to the current scene so it dies with it, not with this node.
                (GetTree()?.CurrentScene ?? GetParent()).AddChild(_pauseOverlay);
            }

            // Let the overlay's own PauseComponent show itself and pause the tree — it
            // owns the visibility/ProcessMode rules. Fall back to pausing directly if the
            // overlay doesn't ship one.
            if (FindPauseComponent(_pauseOverlay!) is { } pause)
                pause.Pause();
            else
            {
                _pauseOverlay!.ProcessMode = Node.ProcessModeEnum.WhenPaused;
                var tree = GetTree();
                if (tree != null) tree.Paused = true;
            }
        }

        private static UI.PauseComponent? FindPauseComponent(Node node)
        {
            if (node is UI.PauseComponent p) return p;
            foreach (var child in node.GetChildren())
                if (FindPauseComponent(child) is { } found) return found;
            return null;
        }
    }
}
