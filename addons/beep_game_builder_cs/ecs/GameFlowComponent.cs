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
    }
}
