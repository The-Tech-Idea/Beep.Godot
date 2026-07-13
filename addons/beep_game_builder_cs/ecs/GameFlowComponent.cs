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

        [Signal] public delegate void ScoreChangedEventHandler(int score);
        [Signal] public delegate void LivesChangedEventHandler(int lives);
        [Signal] public delegate void GameOverEventHandler();
        [Signal] public delegate void LevelCompleteEventHandler();

        public override void _Ready()
        {
            base._Ready();
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
            if (!AutoNavigateOnEnd) return;
            NavigateToScene(GameBuilder.GameInfo.Instance?.GameOverScenePath ?? "res://scenes/ui/game_over.tscn");
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
    }
}
