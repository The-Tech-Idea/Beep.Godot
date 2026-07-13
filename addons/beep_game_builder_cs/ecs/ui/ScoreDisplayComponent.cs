using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Score display with tweened count-up. Creates its own child Label if none is
    /// present. AddScore animates the number rolling up to the new total.
    /// Connect a GameFlowComponent.ScoreChanged signal → AddScore for automatic updates.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ScoreDisplayComponent : UIComponent
    {
        [Export] public string Prefix { get; set; } = "Score: ";
        [Export] public int CurrentScore { get; set; } = 0;
        [Export] public float RollDuration { get; set; } = 0.4f;
        [Export] public int FontSize { get; set; } = 18;

        [Signal] public delegate void ScoreChangedEventHandler(int score);

        private Label? _label;
        private Tween? _roll;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(EnsureLabel));
            UpdateText(CurrentScore);
        }

        private void EnsureLabel()
        {
            if (GetParent() is Label existing) { _label = existing; return; }
            // Create a child Label if the parent isn't one.
            _label = new Label { Name = "ScoreLabel", Text = Prefix + "0" };
            _label.AddThemeFontSizeOverride("font_size", FontSize);
            GetParent().AddChild(_label);
            { var _p = GetParent(); if (_p != null && _p.IsInsideTree()) _label.Owner = _p.Owner; }
        }

        /// <summary>Add points and animate the roll. Connect GameFlow.ScoreChanged → here.</summary>
        public void AddScore(int newScore)
        {
            if (!IsActive) { UpdateText(newScore); return; }
            int from = CurrentScore;
            CurrentScore = newScore;
            _roll?.Kill();
            _roll = CreateTween();
            _roll.TweenMethod(Callable.From<int>(UpdateText), from, newScore, RollDuration);
            EmitSignal(SignalName.ScoreChanged, newScore);
        }

        public void SetScore(int score)
        {
            CurrentScore = score;
            UpdateText(score);
            EmitSignal(SignalName.ScoreChanged, score);
        }

        private void UpdateText(int value)
        {
            if (_label != null) _label.Text = Prefix + value.ToString();
        }
    }
}
