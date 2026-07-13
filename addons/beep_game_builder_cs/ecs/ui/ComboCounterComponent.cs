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
        [Export] public int BaseFontSize { get; set; } = 36;
        [Export] public int MaxFontSize { get; set; } = 64;
        [Export] public Color ComboColor { get; set; } = new(1f, 0.8f, 0.2f, 1f);

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
            _label = new Label { Name = "ComboLabel", Text = "", Visible = false };
            _label.AddThemeFontSizeOverride("font_size", BaseFontSize);
            _label.AddThemeColorOverride("font_color", ComboColor);
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
            _label.Scale = new Vector2(1.3f, 1.3f);
            _punchTween = CreateTween();
            _punchTween.TweenProperty(_label, "scale", Vector2.One, 0.15f).SetEase(Tween.EaseType.Out);
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
