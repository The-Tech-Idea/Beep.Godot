using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Material-style click ripple effect. Attach as a child of a Godot.Control.
    ///
    /// TWO modes (inherited from <see cref="EffectComponent"/>):
    /// • Single (ApplyToChildren = false, default): ripples the parent Control only.
    /// • Cascade (ApplyToChildren = true): ripples ALL descendant Controls — or
    ///   Buttons only when ButtonsOnly = true (default). So one RippleComponent
    ///   under a VBoxContainer of buttons makes every button ripple.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RippleComponent : EffectComponent
    {
        [Export] public Color RippleColor { get; set; } = new(1f, 1f, 1f, 0.3f);
        [Export] public float Duration { get; set; } = 0.6f;
        [Export] public float MaxRadius { get; set; } = 100f;

        public override void _Ready()
        {
            base._Ready();
            // After ResolveTargets runs (deferred), hook GuiInput on each target.
            CallDeferred(nameof(HookInputs));
        }

        private void HookInputs()
        {
            foreach (var t in Targets)
                if (GodotObject.IsInstanceValid(t))
                    t.GuiInput += OnTargetGuiInput;
        }

        private void OnTargetGuiInput(InputEvent @event)
        {
            if (!IsActive) return;
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                var owner = GetViewport()?.GuiGetHoveredControl();
                if (owner != null && Targets.Contains(owner))
                    SpawnRipple(mb.Position, owner);
            }
        }

        private void SpawnRipple(Vector2 localPos, Godot.Control owner)
        {
            var ripple = new ColorRect();
            ripple.Color = RippleColor;
            ripple.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            ripple.PivotOffset = new Vector2(MaxRadius, MaxRadius);
            ripple.Size = new Vector2(MaxRadius * 2, MaxRadius * 2);
            ripple.Position = localPos - new Vector2(MaxRadius, MaxRadius);
            ripple.Scale = Vector2.Zero;

            owner.AddChild(ripple);

            var tween = ripple.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(ripple, "scale", Vector2.One, Duration)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ripple, "modulate:a", 0f, Duration * 0.5f)
                .SetDelay(Duration * 0.5f);
            tween.Finished += ripple.QueueFree;
        }

        public override void _ExitTree()
        {
            foreach (var t in Targets)
                if (GodotObject.IsInstanceValid(t))
                    t.GuiInput -= OnTargetGuiInput;
            base._ExitTree();
        }
    }
}
