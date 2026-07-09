using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Material-style click ripple effect. Attach to any Control.
    /// Blind — works for buttons, cards, list items, menus.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RippleComponent : EntityComponent
    {
        [Export] public Color RippleColor { get; set; } = new(1f, 1f, 1f, 0.3f);
        [Export] public float Duration { get; set; } = 0.6f;
        [Export] public float MaxRadius { get; set; } = 100f;

        private Control? _control;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent<Control>();
            if (_control != null)
                _control.GuiInput += OnGuiInput;
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (_control == null || !IsActive) return;
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                SpawnRipple(mb.Position);
            }
        }

        private void SpawnRipple(Vector2 localPos)
        {
            if (_control == null) return;

            var ripple = new ColorRect();
            ripple.Color = RippleColor;
            ripple.MouseFilter = Control.MouseFilterEnum.Ignore;
            ripple.PivotOffset = new Vector2(MaxRadius, MaxRadius);
            ripple.Size = new Vector2(MaxRadius * 2, MaxRadius * 2);
            ripple.Position = localPos - new Vector2(MaxRadius, MaxRadius);
            ripple.Scale = Vector2.Zero;

            _control.AddChild(ripple);

            var tween = ripple.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(ripple, "scale", Vector2.One, Duration)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ripple, "modulate:a", 0f, Duration * 0.5f)
                .SetDelay(Duration * 0.5f);
            tween.Finished += ripple.QueueFree;
        }
    }
}
