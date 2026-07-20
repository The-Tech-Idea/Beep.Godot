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

        /// <summary>Pause overlay to instance. Empty = the main menu (GameApp.MainMenuPath). A genre
        /// sets this to show its own pause screen instead (e.g. topdown's tabbed subscreen).</summary>
        [Export] public string PauseMenuPathOverride { get; set; } = "";

        [Signal] public delegate void ScoreChangedEventHandler(int score);
        [Signal] public delegate void LivesChangedEventHandler(int lives);
        [Signal] public delegate void GameOverEventHandler();
        [Signal] public delegate void LevelCompleteEventHandler();

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;   // don't run session/GameInfo logic at design time (the other [Tool] components guard too)
            // Run while the tree is paused so the pause action can CLOSE the overlay (resume), not just
            // open it — a pausable node is frozen while paused. Only _UnhandledInput is affected; this
            // component has no _Process, so gameplay stays frozen behind the overlay.
            ProcessMode = ProcessModeEnum.Always;
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
            // Emit LevelComplete once, on the crossing — not on every further score past the target
            // (which re-fired the completion each time). Reset() re-arms it for the next level.
            if (Score >= TargetScore && TargetScore > 0 && !_levelCompleteEmitted)
            {
                _levelCompleteEmitted = true;
                EmitSignal(SignalName.LevelComplete);
            }
        }

        private bool _levelCompleteEmitted;

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
            _levelCompleteEmitted = false;   // re-arm the level-complete latch for the new level
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

        // ── Pause = the main menu, shown as an overlay ───────────────────────
        // There is no dedicated pause menu: pausing just shows the main menu over the frozen
        // game, and pressing the pause action again resumes exactly where the player left off.
        // This component is the single pause toggle. It runs with ProcessMode = Always (set in
        // _Ready) so it still receives the pause action WHILE the tree is paused — a pausable
        // node is frozen while paused and could only ever open, never close. The overlay is
        // Always too, so its buttons (and its entry animation) stay live over the frozen game.

        private Node? _pauseOverlay;
        private bool _pausedByUs;   // did WE set GetTree().Paused? Only then may Close unpause it.

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Engine.IsEditorHint() || !EnablePauseMenu || !IsActive) return;
            if (string.IsNullOrEmpty(PauseAction) || !InputMap.HasAction(PauseAction)) return;
            if (!@event.IsActionPressed(PauseAction)) return;

            TogglePauseMenu();
            GetViewport()?.SetInputAsHandled();
        }

        /// <summary>Pause action pressed: open the menu overlay if closed, close (resume) if open.</summary>
        public void TogglePauseMenu()
        {
            if (GodotObject.IsInstanceValid(_pauseOverlay)) ClosePauseMenu();
            else OpenPauseMenu();
        }

        /// <summary>Instance the menu over the current scene and pause the tree. The overlay defaults
        /// to the main menu (GameApp.MainMenuPath); a genre may point PauseMenuPathOverride at its own
        /// pause screen (e.g. topdown's tabbed subscreen).</summary>
        public void OpenPauseMenu()
        {
            var tree = GetTree();
            if (tree == null) return;

            // Don't open over a pause someone ELSE owns (e.g. an open GenreScreenComponent inventory,
            // which also pauses the tree). We'd stack an invisible menu under it, and our Close would
            // then unpause the game while that screen is still up. GameFlow only manages its own pause.
            if (tree.Paused) return;

            string path = !string.IsNullOrEmpty(PauseMenuPathOverride)
                ? PauseMenuPathOverride
                : GameApp.Instance?.MainMenuPath ?? "";

            if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
            {
                GD.PushError($"[GameFlow] Pause overlay scene not found: '{path}'. Set GameInfo.MainMenuPath or PauseMenuPathOverride.");
                return;
            }

            var packed = GD.Load<PackedScene>(path);
            if (packed == null)
            {
                GD.PushError($"[GameFlow] Could not load pause overlay: {path}");
                return;
            }

            var overlay = packed.Instantiate();
            // A Control-rooted overlay (the main menu) MUST be hosted in a CanvasLayer. Parented straight
            // under the Node2D game root it would join the WORLD canvas — riding the Camera2D and drawing
            // beneath the HUD CanvasLayers (layers go up to 30, so host at 100). A CanvasLayer-rooted
            // overlay (topdown's subscreen) already provides its own screen-space canvas — add it as-is.
            Node host;
            if (overlay is CanvasLayer) host = overlay;
            else
            {
                var layer = new CanvasLayer { Name = "PauseOverlayLayer", Layer = 100 };
                layer.AddChild(overlay);
                host = layer;
            }
            // Always so the overlay's buttons — and any entry animation — run over the frozen game.
            // Set before AddChild so the animation's _Ready sees the right mode (children inherit it).
            host.ProcessMode = Node.ProcessModeEnum.Always;
            // Parent to the current scene so it dies with it, not with this node.
            (tree.CurrentScene ?? GetParent()).AddChild(host);
            _pauseOverlay = host;

            tree.Paused = true;
            _pausedByUs = true;
        }

        /// <summary>Resume: free the overlay and unpause — but only unpause the pause WE created, so a
        /// close can't resume the game underneath a screen someone else paused for.</summary>
        public void ClosePauseMenu()
        {
            var tree = GetTree();
            if (tree != null && _pausedByUs) tree.Paused = false;
            _pausedByUs = false;
            if (GodotObject.IsInstanceValid(_pauseOverlay))
                _pauseOverlay!.QueueFree();
            _pauseOverlay = null;
        }
    }
}
