using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Segmented boss health bar with multi-phase colors. Slides in at the top
    /// of the screen when the boss takes damage, shows phase-based color
    /// transitions, and slides out on death.
    /// Listens to sibling HealthComponent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BossHealthBarComponent : UIComponent
    {
        [Export] public int PhaseCount { get; set; } = 3;
        [Export] public Color BarColor { get; set; } = new(0.8f, 0.1f, 0.1f, 1f);
        [Export] public float SlideDuration { get; set; } = 0.5f;

        [Signal] public delegate void PhaseChangedEventHandler(int phase);

        private ProgressBar? _bar;
        private Label? _nameLabel;
        private int _currentPhase;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _bar = new ProgressBar
            {
                Name = "BossBar",
                CustomMinimumSize = new Vector2(600, 24),
                ShowPercentage = false,
                Visible = false
            };
            _nameLabel = new Label { Name = "BossName", Text = "BOSS", HorizontalAlignment = HorizontalAlignment.Center };
            _nameLabel.AddThemeFontSizeOverride("font_size", 18);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset(Godot.Control.LayoutPreset.TopWide);
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_nameLabel);
            vbox.AddChild(_bar);

            if (GetParent() is Node parent)
            {
                parent.AddChild(vbox);
                if (parent.IsInsideTree())
                    vbox.Owner = parent.Owner;
            }

            var health = GetSiblingComponent<HealthComponent>();
            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
                _bar.MaxValue = health.MaxHealth;
                _bar.Value = health.CurrentHealth;
                _bar.Visible = true;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_bar == null || !IsActive) return;
            _bar.MaxValue = max;
            _bar.Value = current;

            // Phase transition: divide health into equal segments.
            int phase = Mathf.CeilToInt((current / max) * PhaseCount);
            if (phase != _currentPhase)
            {
                _currentPhase = phase;
                float phasePct = (float)phase / PhaseCount;
                _bar.AddThemeStyleboxOverride("fill",
                    new StyleBoxFlat { BgColor = BarColor.Darkened(1f - phasePct) });
                EmitSignal(SignalName.PhaseChanged, phase);
            }
        }
    }
}
