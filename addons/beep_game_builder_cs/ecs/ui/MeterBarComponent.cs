using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A labelled value meter — the widget that replaces `"Health: 72"` text across the
    /// genre HUDs. Shared by survival (health/hunger/thirst/stamina), rpg (health/mana),
    /// shooter (health/shield) and citybuilder (power/water).
    ///
    /// Three things a bare <see cref="ProgressBar"/> does not do, and the reason this exists:
    ///
    /// • **Thresholds.** Crossing <see cref="WarnAt"/> or <see cref="CriticalAt"/> recolours the
    ///   fill AND emits <see cref="ThresholdCrossed"/> once, latched — so a survival meter can
    ///   warn the player *before* it empties instead of reporting after. Firing every frame
    ///   would make the warning useless, so the state is held.
    /// • **Themed fill.** Survival-design guidance is explicit that a themed meter reads better
    ///   than a rectangle, so <see cref="Pulse"/> animates the fill while critical rather than
    ///   relying on colour alone.
    /// • **Inline readout.** The number rides on the bar, so the value stays legible without a
    ///   second Label to keep in sync.
    ///
    /// Colours follow the genre convention (health red, stamina green, mana blue) but default
    /// to the theme's accent so an unconfigured meter still matches the skin.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MeterBarComponent : UIComponent
    {
        [Export] public string Label { get; set; } = "";
        [Export] public Texture2D? Icon { get; set; }

        [Export] public float Value { get => _value; set { _value = value; Refresh(); } }
        private float _value = 100f;

        [Export] public float MaxValue { get => _max; set { _max = Mathf.Max(0.0001f, value); Refresh(); } }
        private float _max = 100f;

        /// <summary>Fraction (0..1) below which the meter reads as warning. 0 disables.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float WarnAt { get; set; } = 0.30f;
        /// <summary>Fraction (0..1) below which the meter reads as critical. 0 disables.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float CriticalAt { get; set; } = 0.15f;

        /// <summary>Show `72 / 100` on the bar. Off for meters where the ratio is the message.</summary>
        [Export] public bool ShowValue { get; set; } = true;
        /// <summary>Animate the fill while critical. The themed-meter cue.</summary>
        [Export] public bool Pulse { get; set; } = true;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color FillColor => UiSurface.Semantic(this, UiSurface.Role.Accent);
        public Color WarnColor => UiSurface.Semantic(this, UiSurface.Role.Warning);
        public Color CriticalColor => UiSurface.Semantic(this, UiSurface.Role.Danger);
        /// <summary>`"normal"`, `"warn"` or `"critical"`. Emitted once per crossing, not per frame.</summary>
        [Signal] public delegate void ThresholdCrossedEventHandler(string level);

        private KitMeter? _bar;
        private KitHudText? _name;
        private string _level = "normal";
        private float _pulse;

        public float Fraction => _max <= 0 ? 0f : Mathf.Clamp(_value / _max, 0f, 1f);

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            // Deferred: a node cannot AddChild to a parent that is still inside its own
            // _Ready ("Parent node is busy setting up children"), which silently produced an
            // EMPTY widget — the code ran, the error went to the log, and the UI was blank.
            // GenreHudComponent already defers its Setup for the same reason.
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            Build();
            Refresh();
        }

        /// <summary>Set both halves at once — the common case, and avoids the intermediate
        /// state where a new value is briefly measured against the old maximum.</summary>
        public void SetValue(float value, float max)
        {
            _max = Mathf.Max(0.0001f, max);
            _value = value;
            Refresh();
        }

        private void Build()
        {
            if (GetParent() is not Godot.Control parent) return;
            int fs = UiSurface.FontSize(this);
            float rowH = Mathf.Max(fs * 1.55f, 22f);

            var row = new HBoxContainer { Name = "MeterRow", MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            parent.AddChild(row);

            if (!string.IsNullOrEmpty(Label))
            {
                _name = new KitHudText
                {
                    Name = "MeterLabel", Text = Label,
                    CustomMinimumSize = new Vector2(fs * 5.6f, rowH),
                    Role = UiSurface.TextRole.Caption,
                    Align = HorizontalAlignment.Left,
                    SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter,
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                };
                row.AddChild(_name);
            }

            _bar = new KitMeter
            {
                Name = "MeterFill",
                CustomMinimumSize = new Vector2(fs * 8.5f, rowH),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter,
                CapIcon = Icon,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            row.AddChild(_bar);
        }

        private void Refresh()
        {
            if (_bar == null) return;
            float f = Fraction;
            _bar.Value = f;

            string level = CriticalAt > 0 && f <= CriticalAt ? "critical"
                         : WarnAt > 0 && f <= WarnAt ? "warn"
                         : "normal";

            var fill = level switch
            {
                "critical" => UiSurface.Role.Danger,
                "warn" => UiSurface.Role.Warning,
                _ => UiSurface.Role.Accent,
            };
            _bar.Fill = fill;
            _bar.Readout = ShowValue ? $"{Mathf.RoundToInt(_value)} / {Mathf.RoundToInt(_max)}" : "";

            // Latch: emit only on a genuine crossing. A per-frame signal would make any
            // listener (toast, vignette, audio sting) fire continuously while low.
            if (level != _level)
            {
                _level = level;
                EmitSignal(SignalName.ThresholdCrossed, level);
            }
        }

        public override void _Process(double delta)
        {
            if (!Pulse || _bar == null || _level != "critical") { if (_bar != null) _bar.Modulate = Colors.White; return; }
            _pulse += (float)delta * 4f;
            _bar.Modulate = new Color(1, 1, 1, 0.65f + 0.35f * Mathf.Sin(_pulse));
        }
    }
}
