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

        // Held so the subscriptions can be undone in _ExitTree. GameFlow and the player's
        // HealthComponent outlive this HUD (a scene change / overlay close frees the HUD
        // first), so a dangling += would fire OnScoreChanged/OnHealthChanged on freed
        // Labels — an ObjectDisposedException on the next score/damage event.
        private GameFlowComponent? _flow;
        private HealthComponent? _boundHealth;

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

            _flow = parent.GetNodeOrNull<GameFlowComponent>(GameFlowPath);
            if (_flow != null)
            {
                _flow.ScoreChanged += OnScoreChanged;
                _flow.LivesChanged += OnLivesChanged;
                OnScoreChanged(_flow.Score);
                OnLivesChanged(_flow.Lives);
            }
            else
            {
                GD.PushWarning($"[{Name}] HudComponent found no GameFlowComponent at '{GameFlowPath}' (relative to '{parent.Name}'); score/lives will not update. Point GameFlowPath at the scene's GameFlowComponent.");
            }

            // Health lives on the player, not the HUD parent. Find it by TYPE, not by a
            // node named "HealthComponent" — no shipped scene names it that (they all use
            // "Health"), so the health readout silently never bound.
            var player = parent.GetNodeOrNull<Node>(PlayerPath);
            if (player != null)
            {
                foreach (var child in player.GetChildren())
                    if (child is HealthComponent hc) { _boundHealth = hc; break; }

                if (_boundHealth != null)
                {
                    _boundHealth.HealthChanged += OnHealthChanged;
                    OnHealthChanged(_boundHealth.CurrentHealth, _boundHealth.MaxHealth);
                }
                else
                {
                    GD.PushWarning($"[{Name}] HudComponent found the player node '{player.Name}' but no HealthComponent child on it; the health readout will not update.");
                }
            }
            else
            {
                GD.PushWarning($"[{Name}] HudComponent found no player node at '{PlayerPath}' (relative to '{parent.Name}'); the health readout will not update. Point PlayerPath at the player, or clear it if this HUD shows no health.");
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_flow != null && GodotObject.IsInstanceValid(_flow))
            {
                _flow.ScoreChanged -= OnScoreChanged;
                _flow.LivesChanged -= OnLivesChanged;
            }
            if (_boundHealth != null && GodotObject.IsInstanceValid(_boundHealth))
                _boundHealth.HealthChanged -= OnHealthChanged;
            _flow = null;
            _boundHealth = null;
        }

        private void OnScoreChanged(int score) { if (_score != null) _score.Text = score.ToString(); }
        private void OnLivesChanged(int lives) { if (_lives != null) _lives.Text = $"× {lives}"; }
        private void OnHealthChanged(float current, float max)
        {
            if (_health != null) _health.Text = $"{(int)current} / {(int)max}";
        }
    }
}
