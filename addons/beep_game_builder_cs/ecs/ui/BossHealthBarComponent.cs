using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Segmented boss health bar at the top of the screen with multi-phase colors:
    /// phase-based color transitions driven by a sibling HealthComponent.
    /// (No slide animation — the old SlideDuration export was never wired to anything.)
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BossHealthBarComponent : UIComponent
    {
        [Export] public int PhaseCount { get; set; } = 3;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color BarColor => UiSurface.Semantic(this, UiSurface.Role.Danger);
        /// <summary>The name shown above the bar. Settable at runtime — updates the label live.</summary>
        [Export] public string BossName
        {
            get => _bossName;
            set { _bossName = value; if (_nameLabel != null) _nameLabel.Text = value; }
        }
        private string _bossName = "BOSS";

        [Signal] public delegate void PhaseChangedEventHandler(int phase);

        private KitMeter? _bar;
        private KitHudText? _nameLabel;
        private int _currentPhase;
        private VBoxContainer? _vbox;
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            if (Engine.IsEditorHint()) return;
            int fs = UiSurface.FontSize(this);
            _bar = new KitMeter
            {
                Name = "BossBar",
                CustomMinimumSize = new Vector2(fs * 28f, fs * 1.7f),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill,
                Fill = UiSurface.Role.Danger,
                EndCaps = true,
                Visible = false,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _nameLabel = new KitHudText
            {
                Name = "BossName",
                Text = _bossName,
                Role = UiSurface.TextRole.Subtitle,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };

            _vbox = new VBoxContainer { MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
            _vbox.SetAnchorsPreset(Godot.Control.LayoutPreset.TopWide);
            _vbox.AddThemeConstantOverride("separation", 4);
            _vbox.OffsetLeft = fs * 4f;
            _vbox.OffsetRight = -fs * 4f;
            _vbox.AddChild(_nameLabel);
            _vbox.AddChild(_bar);

            if (GetParent() is Node parent)
            {
                parent.AddChild(_vbox);
                if (parent.IsInsideTree())
                    _vbox.Owner = parent.Owner;
            }

            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null)
            {
                _health.HealthChanged += OnHealthChanged;
                _bar.Value = _health.MaxHealth <= 0f ? 0f : _health.CurrentHealth / _health.MaxHealth;
                _bar.Readout = $"{Mathf.RoundToInt(_health.CurrentHealth)} / {Mathf.RoundToInt(_health.MaxHealth)}";
                _bar.Visible = true;
            }
            else
            {
                GD.PushWarning($"[{Name}] BossHealthBarComponent found no sibling HealthComponent — the bar will stay hidden/empty. Add a HealthComponent alongside it (as BuffBarComponent does).");
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_bar == null || !IsActive) return;
            _bar.Value = max <= 0f ? 0f : current / max;
            _bar.Readout = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";

            if (max <= 0f || PhaseCount <= 0) return;   // guard 0/0 → NaN on a degenerate config

            // Phase transition: divide health into equal segments.
            int phase = Mathf.CeilToInt((current / max) * PhaseCount);
            if (phase != _currentPhase)
            {
                _currentPhase = phase;
                float phasePct = (float)phase / PhaseCount;
                _bar.Fill = phasePct <= 0.34f ? UiSurface.Role.Danger
                    : phasePct <= 0.67f ? UiSurface.Role.Warning
                    : UiSurface.Role.Success;
                _bar.Tier = Mathf.Max(1, phase);
                EmitSignal(SignalName.PhaseChanged, phase);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null && GodotObject.IsInstanceValid(_health))
                _health.HealthChanged -= OnHealthChanged;
            if (_vbox != null && GodotObject.IsInstanceValid(_vbox))
                _vbox.QueueFree();
        }
    }
}
