using Godot;

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

        private ProgressBar? _bar;
        private Label? _text;
        private Label? _name;
        private TextureRect? _icon;
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

            var row = new HBoxContainer { Name = "MeterRow", MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            parent.AddChild(row);

            if (Icon != null)
            {
                _icon = new TextureRect
                {
                    Name = "MeterIcon", Texture = Icon,
                    CustomMinimumSize = new Vector2(20, 20),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter,
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                };
                row.AddChild(_icon);
            }

            if (!string.IsNullOrEmpty(Label))
            {
                _name = new Label
                {
                    Name = "MeterLabel", Text = Label,
                    CustomMinimumSize = new Vector2(78, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter,
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                };
                _name.AddThemeFontSizeOverride("font_size", UiSurface.FontSize(this, 0.93f));
                row.AddChild(_name);
            }

            // The bar and its readout share a cell, so the number cannot drift away from the fill.
            var stack = new Godot.Control
            {
                Name = "MeterStack",
                CustomMinimumSize = new Vector2(120, 20),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
            };
            row.AddChild(stack);

            _bar = new ProgressBar { Name = "MeterFill", ShowPercentage = false, MinValue = 0, MaxValue = 1 };
            _bar.SetAnchorsPreset(Godot.Control.LayoutPreset.FullRect);
            _bar.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            stack.AddChild(_bar);

            _text = new Label
            {
                Name = "MeterValue",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = ShowValue,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
            };
            _text.SetAnchorsPreset(Godot.Control.LayoutPreset.FullRect);
            _text.AddThemeFontSizeOverride("font_size", UiSurface.FontSize(this, 0.86f));
            stack.AddChild(_text);
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
                "critical" => CriticalColor,
                "warn" => WarnColor,
                _ => FillColor,
            };
            var box = new StyleBoxFlat { BgColor = fill };
            box.SetCornerRadiusAll(3);
            _bar.AddThemeStyleboxOverride("fill", box);

            if (_text != null && ShowValue)
                _text.Text = $"{Mathf.RoundToInt(_value)} / {Mathf.RoundToInt(_max)}";

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
