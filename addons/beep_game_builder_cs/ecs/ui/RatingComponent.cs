using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Star rating component. Attach to any Container to display 1-5 stars.
    /// Blind — works for reviews, player ratings, difficulty, quality.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RatingComponent : UIComponent
    {
        [Export] public int MaxStars { get; set; } = 5;
        [Export] public float Value { get; set; } = 3.5f;
        [Export] public float StarSize { get; set; } = 24f;
        [Export] public Color FilledColor { get; set; } = new(1f, 0.84f, 0f, 1f);
        [Export] public Color EmptyColor { get; set; } = new(0.3f, 0.3f, 0.3f, 1f);
        [Export] public bool Interactive { get; set; } = false;

        [Signal] public delegate void RatingChangedEventHandler(float newValue);

        private Container? _container;
        // The committed rating. Value is only the DISPLAYED value and shows a preview while hovering;
        // _committed is the truth, so moving the mouse away restores it instead of keeping the preview.
        private float _committed;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] parent is not a Container — the star row cannot be built.");
                return;
            }
            _committed = Value;
            BuildStars();
            UpdateDisplay();
        }

        private void BuildStars()
        {
            if (Engine.IsEditorHint()) return;
            for (int i = 0; i < MaxStars; i++)
            {
                var label = new Label { Text = "★", HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontSizeOverride("font_size", (int)StarSize);
                label.CustomMinimumSize = new Vector2(StarSize + 4, StarSize + 4);

                if (Interactive)
                {
                    int idx = i;
                    label.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
                    label.GuiInput += e =>
                    {
                        if (e is InputEventMouseButton mb && mb.Pressed)
                        {
                            _committed = idx + 1;        // commit the click
                            Value = _committed;
                            UpdateDisplay();
                            EmitSignal(SignalName.RatingChanged, Value);
                        }
                    };
                    label.MouseEntered += () => { Value = idx + 0.8f; UpdateDisplay(); };   // preview only
                    label.MouseExited += () => { Value = _committed; UpdateDisplay(); };     // restore the committed rating
                }

                _container?.AddChild(label);
            }
        }

        public void UpdateDisplay()
        {
            if (_container == null) return;
            var children = _container.GetChildren();
            for (int i = 0; i < children.Count && i < MaxStars; i++)
            {
                if (children[i] is Label label)
                {
                    float fill = Mathf.Clamp(Value - i, 0f, 1f);
                    Color color = fill >= 1f ? FilledColor :
                        fill > 0f ? FilledColor.Lerp(EmptyColor, 1f - fill) : EmptyColor;
                    label.AddThemeColorOverride("font_color", color);
                }
            }
        }

        public void SetValue(float value) { Value = value; _committed = value; UpdateDisplay(); }
    }
}
