using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Star rating component. Attach to any Container to display 1-5 stars.
    /// Blind — works for reviews, player ratings, difficulty, quality.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RatingComponent : EntityComponent
    {
        [Export] public int MaxStars { get; set; } = 5;
        [Export] public float Value { get; set; } = 3.5f;
        [Export] public float StarSize { get; set; } = 24f;
        [Export] public Color FilledColor { get; set; } = new(1f, 0.84f, 0f, 1f);
        [Export] public Color EmptyColor { get; set; } = new(0.3f, 0.3f, 0.3f, 1f);
        [Export] public bool Interactive { get; set; } = false;

        [Signal] public delegate void RatingChangedEventHandler(float newValue);

        private Container? _container;
        private readonly Label[]? _stars;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent<Container>();
            if (_container == null) return;
            BuildStars();
            UpdateDisplay();
        }

        private void BuildStars()
        {
            for (int i = 0; i < MaxStars; i++)
            {
                var label = new Label { Text = "★", HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontSizeOverride("font_size", (int)StarSize);
                label.CustomMinimumSize = new Vector2(StarSize + 4, StarSize + 4);

                if (Interactive)
                {
                    int idx = i;
                    label.MouseFilter = Control.MouseFilterEnum.Stop;
                    label.GuiInput += e =>
                    {
                        if (e is InputEventMouseButton mb && mb.Pressed)
                        {
                            Value = idx + 1;
                            UpdateDisplay();
                            EmitSignal(SignalName.RatingChanged, Value);
                        }
                    };
                    label.MouseEntered += () => { Value = i + 0.8f; UpdateDisplay(); };
                    label.MouseExited += () => UpdateDisplay();
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

        public void SetValue(float value) { Value = value; UpdateDisplay(); }
    }
}
