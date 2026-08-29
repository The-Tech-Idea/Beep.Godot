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
        [Export] public NodePath LevelLabelPath { get; set; } = "LevelLabel";
        [Export] public NodePath GameFlowPath { get; set; } = new("../GameFlow");
        [Export] public NodePath PlayerPath { get; set; } = new("../Player");
        [Export] public bool WarnMissingSources { get; set; } = false;

        /// <summary>Text shown by the level readout. {0} is the 1-based level number (GameApp is 0-based).</summary>
        [Export] public string LevelFormat { get; set; } = "Level {0}";

        private Readout? _score;
        private Readout? _lives;
        private Readout? _health;
        private Readout? _level;

        /// <summary>
        /// A readout that may be a plain <see cref="Label"/> OR a kit widget.
        ///
        /// Binding to Label alone would mean every genre's HUD had to be rewritten in lockstep,
        /// and would resolve to null the instant one scene upgraded — the same failure mode the
        /// DemandMeterPath move produced, and the reason CityBuilderHudComponent already accepts
        /// both. A scene may therefore carry "Health: 72" as a Label or a real
        /// <see cref="Kit.KitMeter"/> bar, and this component drives either.
        ///
        /// Anything else at that path is a mis-wiring and says so once, rather than leaving a
        /// readout that silently never updates.
        /// </summary>
        private sealed class Readout
        {
            private readonly Label? _label;
            private readonly Kit.KitLabelValue? _pair;
            private readonly Kit.KitMeter? _meter;

            private Readout(Label? l, Kit.KitLabelValue? p, Kit.KitMeter? m)
            { _label = l; _pair = p; _meter = m; }

            public static Readout? Resolve(Node parent, NodePath path, string owner, string what)
            {
                var n = parent.GetNodeOrNull<Node>(path);
                switch (n)
                {
                    case null: return null;
                    case Label l: return new Readout(l, null, null);
                    case Kit.KitLabelValue p: return new Readout(null, p, null);
                    case Kit.KitMeter m: return new Readout(null, null, m);
                    default:
                        GD.PushWarning($"[{owner}] HudComponent's {what} path '{path}' resolves to "
                                       + $"a {n.GetType().Name}, which is not a Label, KitLabelValue "
                                       + "or KitMeter. That readout will never update.");
                        return null;
                }
            }

            /// <summary>Textual value. A meter has no text, so it is left alone.</summary>
            public void Set(string text)
            {
                if (_label != null) _label.Text = text;
                else if (_pair != null) _pair.Value = text;
            }

            /// <summary>A proportion. A meter fills; anything else falls back to the text form,
            /// so swapping a bar back to a Label does not lose the readout.</summary>
            public void SetRatio(float current, float max, string text)
            {
                if (_meter != null) _meter.Value = max > 0f ? current / max : 0f;
                else Set(text);
            }
        }

        // Held so the subscriptions can be undone in _ExitTree. GameFlow and the player's
        // HealthComponent outlive this HUD (a scene change / overlay close frees the HUD
        // first), so a dangling += would fire OnScoreChanged/OnHealthChanged on freed
        // Labels — an ObjectDisposedException on the next score/damage event.
        private GameFlowComponent? _flow;
        private HealthComponent? _boundHealth;
        // GameApp (autoload) drives the level readout and outlives the HUD — held so the += undoes in _ExitTree.
        private GameApp? _app;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Bind));
        }

        public void Bind()
        {
            if (GetParent() is not Node parent) return;
            _score = Readout.Resolve(parent, ScoreLabelPath, Name, "score");
            _lives = Readout.Resolve(parent, LivesLabelPath, Name, "lives");
            _health = Readout.Resolve(parent, HealthLabelPath, Name, "health");
            _level = Readout.Resolve(parent, LevelLabelPath, Name, "level");

            // Level readout comes from GameApp (the autoload owns level progression), not GameFlow.
            // Optional: no LevelLabel in the scene simply means this HUD shows no level — skip silently
            // (unlike score/health, a missing level label is a common, intentional layout choice).
            if (_level != null)
            {
                if (GameApp.Instance is { } app)
                {
                    _app = app;
                    _app.LevelChanged += OnLevelChanged;
                    OnLevelChanged(_app.CurrentLevel);
                }
                else
                {
                    WarnMissing($"HudComponent has a LevelLabel but found no GameApp autoload; the level readout will not update. Enable the GameApp autoload.");
                }
            }

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
                WarnMissing($"HudComponent found no GameFlowComponent at '{GameFlowPath}' (relative to '{parent.Name}'); score/lives will not update. Point GameFlowPath at the scene's GameFlowComponent.");
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
                    WarnMissing($"HudComponent found the player node '{player.Name}' but no HealthComponent child on it; the health readout will not update.");
                }
            }
            else
            {
                WarnMissing($"HudComponent found no player node at '{PlayerPath}' (relative to '{parent.Name}'); the health readout will not update. Point PlayerPath at the player, or clear it if this HUD shows no health.");
            }
        }

        private void WarnMissing(string message)
        {
            if (WarnMissingSources)
                GD.PushWarning($"[{Name}] {message}");
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
            if (_app != null && GodotObject.IsInstanceValid(_app))
                _app.LevelChanged -= OnLevelChanged;
            _flow = null;
            _boundHealth = null;
            _app = null;
        }

        private void OnScoreChanged(int score) => _score?.Set(score.ToString());
        private void OnLivesChanged(int lives) => _lives?.Set($"× {lives}");
        private void OnHealthChanged(float current, float max)
            // A meter fills; a Label still gets "72 / 100", so a project that has not migrated
            // its HUD reads exactly as before.
            => _health?.SetRatio(current, max, $"{(int)current} / {(int)max}");
        // GameApp.CurrentLevel is 0-based (-1 before a game starts); show a friendly 1-based number.
        private void OnLevelChanged(int level)
            => _level?.Set(string.Format(LevelFormat, System.Math.Max(0, level) + 1));
    }
}
