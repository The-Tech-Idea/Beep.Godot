using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Heads-up display binder. Attach as a child of a CanvasLayer (the HUD root).
    /// Discovers sibling <c>GameFlowComponent</c> and a <c>HealthComponent</c> on
    /// the player node, then updates named child Labels whenever score/lives/health
    /// change. Pure binding — no layout; the scene's Labels provide the layout.
    ///
    /// Expected child Labels (by Node name; create only the ones you need):
    ///   "ScoreLabel", "LivesLabel", "HealthLabel", "LevelLabel", "LevelName".
    /// Missing labels are silently skipped.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HudComponent : UIComponent
    {
        [Export] public NodePath ScoreLabelPath { get; set; } = "ScoreLabel";
        [Export] public NodePath LivesLabelPath { get; set; } = "LivesLabel";
        [Export] public NodePath HealthLabelPath { get; set; } = "HealthLabel";
        [Export] public NodePath GameFlowPath { get; set; } = new("../GameFlow");
        [Export] public NodePath PlayerPath { get; set; } = new("../Player");

        private Label? _score;
        private Label? _lives;
        private Label? _health;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Bind));
        }

        public void Bind()
        {
            if (GetParent() is not Node parent) return;
            _score = parent.GetNodeOrNull<Label>(ScoreLabelPath);
            _lives = parent.GetNodeOrNull<Label>(LivesLabelPath);
            _health = parent.GetNodeOrNull<Label>(HealthLabelPath);

            var flow = parent.GetNodeOrNull<GameFlowComponent>(GameFlowPath);
            if (flow != null)
            {
                flow.ScoreChanged += OnScoreChanged;
                flow.LivesChanged += OnLivesChanged;
                OnScoreChanged(flow.Score);
                OnLivesChanged(flow.Lives);
            }

            // Health lives on the player, not the HUD parent. Find it by TYPE, not by a
            // node named "HealthComponent" — no shipped scene names it that (they all use
            // "Health"), so the health readout silently never bound.
            var player = parent.GetNodeOrNull<Node>(PlayerPath);
            if (player != null)
            {
                HealthComponent? health = null;
                foreach (var child in player.GetChildren())
                    if (child is HealthComponent hc) { health = hc; break; }

                if (health != null)
                {
                    health.HealthChanged += OnHealthChanged;
                    OnHealthChanged(health.CurrentHealth, health.MaxHealth);
                }
            }
        }

        private void OnScoreChanged(int score) { if (_score != null) _score.Text = score.ToString(); }
        private void OnLivesChanged(int lives) { if (_lives != null) _lives.Text = $"× {lives}"; }
        private void OnHealthChanged(float current, float max)
        {
            if (_health != null) _health.Text = $"{(int)current} / {(int)max}";
        }
    }
}
