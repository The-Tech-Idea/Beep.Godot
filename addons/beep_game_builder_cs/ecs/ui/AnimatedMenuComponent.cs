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
        private readonly System.Collections.Generic.List<Tween> _activeTweens = new();

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            // Same silent-cast trap as ThemePresetComponent: this animates the children of
            // GetParent(), so a non-Container parent means it never runs. Both scenes that
            // ship it parent it at the root (a Control / a CanvasLayer), so it has never
            // animated anything. Reparent under the VBoxContainer holding the buttons.
            if (_container == null && !Engine.IsEditorHint())
                GD.PushWarning($"[{Name}] AnimatedMenuComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Container — no menu animation will play. Reparent it under the container holding the items to animate.");
            if (AnimateOnReady) CallDeferred(nameof(ShowAnimated));
        }

        public void ShowAnimated()
        {
            if (Engine.IsEditorHint()) return;
            if (_container == null || !IsActive) return;

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            // Only Controls animate, and this component is itself a child of the container
            // it animates — so filter first and drive the loop off the Controls. Indexing
            // raw children would stagger against gaps, and hang MenuShown off the last
            // child, which is this node (a Node, skipped) rather than the last button.
            var children = new System.Collections.Generic.List<Godot.Control>();
            foreach (var child in _container.GetChildren())
                if (child is Godot.Control c) children.Add(c);   // this component is a Node, so it self-excludes

            float offset = GetEntryOffset();

            for (int i = 0; i < children.Count; i++)
            {
                var ctrl = children[i];

                ctrl.Modulate = new Color(1, 1, 1, AnimateOnReady ? 0 : 1);

                // Animate the offset transform layer, never position/scale. This component
                // animates the children OF A CONTAINER, and a container re-sorts its
                // children's position/size every layout pass — it would fight the tween and
                // win. Godot 4.7's offset_transform_* (GH-87081) is a render-only transform
                // containers do not touch. Same reason theme_applier.gd and ui_effect.gd use
                // it. Offsets are relative to the laid-out position, so the target is always
                // zero/one and there is no end-position to capture.
                ctrl.OffsetTransformEnabled = true;

                Vector2 startOffset = EntryDirection switch
                {
                    Direction.FromLeft => new Vector2(-offset, 0),
                    Direction.FromRight => new Vector2(offset, 0),
                    Direction.FromTop => new Vector2(0, -offset),
                    Direction.FromBottom => new Vector2(0, offset),
                    _ => Vector2.Zero
                };

                if (AnimateOnReady)
                {
                    if (EntryDirection != Direction.FadeOnly && EntryDirection != Direction.FromCenter)
                        ctrl.OffsetTransformPosition = startOffset;
                    if (EntryDirection == Direction.FromCenter)
                        ctrl.OffsetTransformScale = Vector2.Zero;
                }

                var tween = ctrl.CreateTween();
                _activeTweens.Add(tween);
                float delay = InitialDelay + i * StaggerDelay;
                tween.TweenInterval(delay);
                tween.SetParallel(true);

                if (EntryDirection != Direction.FadeOnly && EntryDirection != Direction.FromCenter)
                    tween.TweenProperty(ctrl, "offset_transform_position", Vector2.Zero, Duration)
                        .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                if (EntryDirection == Direction.FromCenter)
                    tween.TweenProperty(ctrl, "offset_transform_scale", Vector2.One, Duration)
                        .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);

                tween.TweenProperty(ctrl, "modulate:a", 1f, Duration * 0.6f);

                if (i == children.Count - 1)
                    tween.Finished += OnShowFinished;
            }
        }

        private void OnShowFinished() => EmitSignal(SignalName.MenuShown);

        public void HideAnimated()
        {
            if (_container == null || !IsActive) return;

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            // Same filtering as ShowAnimated — see the note there.
            var children = new System.Collections.Generic.List<Godot.Control>();
            foreach (var child in _container.GetChildren())
                if (child is Godot.Control c) children.Add(c);   // this component is a Node, so it self-excludes

            for (int i = 0; i < children.Count; i++)
            {
                var ctrl = children[i];
                var tween = ctrl.CreateTween();
                _activeTweens.Add(tween);
                tween.TweenInterval(i * StaggerDelay * 0.5f);
                tween.SetParallel(true);
                tween.TweenProperty(ctrl, "modulate:a", 0f, Duration * 0.5f);
                if (i == children.Count - 1)
                    tween.Finished += OnHideFinished;
            }
        }

        private void OnHideFinished()
        {
            if (_container != null) _container.Visible = false;
            EmitSignal(SignalName.MenuHidden);
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

        public override void _ExitTree()
        {
            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();
        }
    }
}
