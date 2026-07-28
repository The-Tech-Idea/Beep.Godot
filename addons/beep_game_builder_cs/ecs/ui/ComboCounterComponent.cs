using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Combo counter display. Call Increment() each time the player lands a hit
    /// or chains an action. The combo number grows with font size + shake, and
    /// auto-resets to 0 after ResetTime seconds of inactivity.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ComboCounterComponent : UIComponent
    {
        [Export] public float ResetTime { get; set; } = 2f;
        // Scale of the theme's body font, not a fixed size. The themes run 14-24, so a
        // literal renders a genre's larger type out of a control built for 14.
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float BaseFontScale { get; set; } = 2.6f;
        private int BaseFontSize => UiSurface.FontSize(this, BaseFontScale);
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float MaxFontScale { get; set; } = 4.6f;
        private int MaxFontSize => UiSurface.FontSize(this, MaxFontScale);
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color ComboColor => UiSurface.Semantic(this, UiSurface.Role.Warning);
        [Signal] public delegate void ComboChangedEventHandler(int count);
        [Signal] public delegate void ComboResetEventHandler();

        private Label? _label;
        private int _count;
        private float _resetTimer;
        private Tween? _punchTween;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(SetupLabel));
        }

        private void SetupLabel()
        {
            if (Engine.IsEditorHint()) return;
            _label = new Label { Name = "ComboLabel", Text = "", Visible = false };
            _label.AddThemeFontSizeOverride("font_size", BaseFontSize);
            _label.AddThemeColorOverride("font_color", ComboColor);
            // Punch on the offset_transform layer so a container parent can't overwrite the
            // scale (matches the other migrated effects).
            _label.OffsetTransformEnabled = true;
            if (GetParent() is Node parent)
            {
                parent.AddChild(_label);
                if (parent.IsInsideTree())
                    _label.Owner = parent.Owner;
            }
        }

        public override void _Process(double delta)
        {
            if (_count == 0 || !IsActive) return;
            _resetTimer -= (float)delta;
            if (_resetTimer <= 0) ResetCombo();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _punchTween?.Kill();   // consistency with the repo's tween-owning components
            _punchTween = null;
            // _label was AddChild'd to the parent — free it or a stray "ComboLabel" is orphaned.
            if (_label != null && GodotObject.IsInstanceValid(_label)) _label.QueueFree();
            _label = null;
        }

        /// <summary>Add one to the combo counter and reset the timer.</summary>
        public void Increment()
        {
            if (!IsActive) return;
            _count++;
            _resetTimer = ResetTime;
            if (_label == null) return;
            _label.Text = $"{_count}x";
            _label.Visible = true;

            // Punch: scale up briefly then settle.
            int fontSize = Mathf.Clamp(BaseFontSize + _count * 2, BaseFontSize, MaxFontSize);
            _label.AddThemeFontSizeOverride("font_size", fontSize);
            _punchTween?.Kill();
            _label.PivotOffset = _label.Size / 2f;   // punch from the center, not the corner
            _label.OffsetTransformScale = new Vector2(1.3f, 1.3f);
            _punchTween = CreateTween();
            _punchTween.TweenProperty(_label, "offset_transform_scale", Vector2.One, 0.15f).SetEase(Tween.EaseType.Out);
            EmitSignal(SignalName.ComboChanged, _count);
        }

        /// <summary>Reset combo to 0 and hide the label.</summary>
        public void ResetCombo()
        {
            _count = 0;
            if (_label != null) _label.Visible = false;
            EmitSignal(SignalName.ComboReset);
        }
    }
}
