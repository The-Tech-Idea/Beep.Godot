using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Animated menu component. Attach to any Container to animate its children.
    /// Children stagger in with configurable direction, delay, and easing.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AnimatedMenuComponent : UIComponent
    {
        public enum Direction { FromLeft, FromRight, FromTop, FromBottom, FromCenter, FadeOnly }

        [Export] public Direction EntryDirection { get; set; } = Direction.FromBottom;
        [Export] public float Duration { get; set; } = 0.3f;
        [Export] public float StaggerDelay { get; set; } = 0.05f;
        [Export] public float InitialDelay { get; set; } = 0f;
        [Export] public bool AnimateOnReady { get; set; } = true;
        [Export] public bool AnimateExit { get; set; } = false;

        [Signal] public delegate void MenuShownEventHandler();
        [Signal] public delegate void MenuHiddenEventHandler();

        private Container? _container;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (AnimateOnReady) CallDeferred(nameof(ShowAnimated));
        }

        public void ShowAnimated()
        {
            if (_container == null || !IsActive) return;
            var children = _container.GetChildren();
            float offset = GetEntryOffset();

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is not Godot.Control ctrl) continue;

                ctrl.Modulate = new Color(1, 1, 1, AnimateOnReady ? 0 : 1);

                Vector2 startPos = EntryDirection switch
                {
                    Direction.FromLeft => ctrl.Position + new Vector2(-offset, 0),
                    Direction.FromRight => ctrl.Position + new Vector2(offset, 0),
                    Direction.FromTop => ctrl.Position + new Vector2(0, -offset),
                    Direction.FromBottom => ctrl.Position + new Vector2(0, offset),
                    Direction.FromCenter => ctrl.Position,
                    _ => ctrl.Position
                };

                if (AnimateOnReady)
                {
                    if (EntryDirection != Direction.FadeOnly && EntryDirection != Direction.FromCenter)
                        ctrl.Position = startPos;
                    if (EntryDirection == Direction.FromCenter)
                        ctrl.Scale = Vector2.Zero;
                }

                var tween = ctrl.CreateTween();
                float delay = InitialDelay + i * StaggerDelay;
                tween.TweenInterval(delay);
                tween.SetParallel(true);

                if (EntryDirection != Direction.FadeOnly && EntryDirection != Direction.FromCenter)
                    tween.TweenProperty(ctrl, "position", startPos - GetDirectionOffset() * offset,
                        Duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                if (EntryDirection == Direction.FromCenter)
                    tween.TweenProperty(ctrl, "scale", Vector2.One, Duration)
                        .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);

                tween.TweenProperty(ctrl, "modulate:a", 1f, Duration * 0.6f);

                if (i == children.Count - 1)
                    tween.Finished += () => EmitSignal(SignalName.MenuShown);
            }
        }

        public void HideAnimated()
        {
            if (_container == null || !IsActive) return;
            var children = _container.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is not Godot.Control ctrl) continue;
                var tween = ctrl.CreateTween();
                tween.TweenInterval(i * StaggerDelay * 0.5f);
                tween.SetParallel(true);
                tween.TweenProperty(ctrl, "modulate:a", 0f, Duration * 0.5f);
                if (i == children.Count - 1)
                    tween.Finished += () => { _container.Visible = false; EmitSignal(SignalName.MenuHidden); };
            }
        }

        private float GetEntryOffset() => EntryDirection switch
        {
            Direction.FromLeft or Direction.FromRight => 100f,
            Direction.FromTop or Direction.FromBottom => 50f,
            _ => 0f
        };

        private Vector2 GetDirectionOffset() => EntryDirection switch
        {
            Direction.FromLeft => new Vector2(100f, 0),
            Direction.FromRight => new Vector2(-100f, 0),
            Direction.FromTop => new Vector2(0, 50f),
            Direction.FromBottom => new Vector2(0, -50f),
            _ => Vector2.Zero
        };
    }
}
