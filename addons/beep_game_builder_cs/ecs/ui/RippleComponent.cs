using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Kit click ripple effect. Attach as a child of a Godot.Control.
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

        // Targets we actually connected GuiInput on. _ExitTree must disconnect ONLY these:
        // HookInputs is deferred, so a ThemePresetComponent re-theme (ApplyTheme runs several
        // times per load) can free this node before the deferred hook ever ran. Disconnecting
        // the whole Targets list then hit connections that were never made — Godot logs
        // "Attempt to disconnect a nonexistent connection ... gui_input" for every button.
        private readonly System.Collections.Generic.List<Godot.Control> _hooked = new();

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;   // don't wire GuiInput / spawn runtime overlays at edit time
            // After ResolveTargets runs (deferred), hook GuiInput on each target.
            Callable.From(HookInputs).CallDeferred();
        }

        private void HookInputs()
        {
            foreach (var t in Targets)
                if (GodotObject.IsInstanceValid(t) && !_hooked.Contains(t))
                {
                    t.GuiInput += OnTargetGuiInput;
                    _hooked.Add(t);
                }
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
            var ripple = new KitColorOverlay
            {
                Color = RippleColor,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                PivotOffset = new Vector2(MaxRadius, MaxRadius),
                Size = new Vector2(MaxRadius * 2, MaxRadius * 2),
                Position = localPos - new Vector2(MaxRadius, MaxRadius),
                Scale = Vector2.Zero
            };

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
            foreach (var t in _hooked)
                if (GodotObject.IsInstanceValid(t))
                    t.GuiInput -= OnTargetGuiInput;
            _hooked.Clear();
            base._ExitTree();
        }
    }
}
