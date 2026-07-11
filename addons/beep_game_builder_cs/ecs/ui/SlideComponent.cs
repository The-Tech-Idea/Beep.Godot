using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Slide in/out animation. Attach as a child of a Godot.Control. SlideIn()/SlideOut()
    /// animate show/hide from a direction.
    /// Cascade: set ApplyToChildren = true to slide every descendant Control/Button.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SlideComponent : EffectComponent
    {
        public enum SlideDirection { Left, Right, Up, Down }
        public enum SlideMode { SlideAndFade, SlideOnly, FadeOnly }

        [Export] public SlideDirection Direction { get; set; } = SlideDirection.Up;
        [Export] public SlideMode Mode { get; set; } = SlideMode.SlideAndFade;
        [Export] public float Duration { get; set; } = 0.4f;
        [Export] public float Distance { get; set; } = 100f;
        [Export] public bool HiddenOnStart { get; set; } = true;

        [Signal] public delegate void SlidInEventHandler();
        [Signal] public delegate void SlidOutEventHandler();

        // Each target remembers its visible position; hidden position is offset from it.
        private readonly Dictionary<Godot.Control, Vector2> _visiblePos = new();
        private readonly List<Tween> _activeTweens = new();
        private int _finishCount;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(InitTargets));
        }

        private void InitTargets()
        {
            _visiblePos.Clear();
            foreach (var c in Targets)
            {
                if (!GodotObject.IsInstanceValid(c)) continue;
                _visiblePos[c] = c.Position;
                if (HiddenOnStart)
                {
                    c.Position = HiddenPos(c);
                    if (Mode != SlideMode.SlideOnly) c.Modulate = new Color(1, 1, 1, 0);
                    c.Visible = false;
                }
            }
        }

        public void SlideIn()
        {
            if (!IsActive || _visiblePos.Count == 0) return;
            StartTweens(animateIn: true);
        }

        public void SlideOut()
        {
            if (!IsActive || _visiblePos.Count == 0) return;
            StartTweens(animateIn: false);
        }

        private void StartTweens(bool animateIn)
        {
            // Kill any running tweens.
            foreach (var t in _activeTweens)
                if (GodotObject.IsInstanceValid(t)) t.Kill();
            _activeTweens.Clear();

            int total = 0;
            _finishCount = 0;

            foreach (var (c, visible) in _visiblePos)
            {
                if (!GodotObject.IsInstanceValid(c)) continue;
                if (animateIn) c.Visible = true;
                total++;

                var tw = c.CreateTween().SetParallel(true);
                if (Mode != SlideMode.FadeOnly)
                {
                    Vector2 target = animateIn ? visible : HiddenPos(c);
                    Vector2 start = animateIn ? HiddenPos(c) : visible;
                    tw.TweenProperty(c, "position", target, Duration)
                      .SetEase(animateIn ? Tween.EaseType.Out : Tween.EaseType.In)
                      .SetTrans(animateIn ? Tween.TransitionType.Back : Tween.TransitionType.Quad);
                }
                if (Mode != SlideMode.SlideOnly)
                    tw.TweenProperty(c, "modulate:a", animateIn ? 1f : 0f,
                        Duration * (animateIn ? 0.7f : 0.5f));
                _activeTweens.Add(tw);
            }

            // Emit the finished signal once all parallel tweens complete.
            // The last target's tween drives the count; we approximate by using the
            // first tween's Finished (parallel tweens share duration).
            if (_activeTweens.Count > 0)
            {
                _activeTweens[0].Finished += () =>
                {
                    if (!animateIn)
                        foreach (var (c, _) in _visiblePos)
                            if (GodotObject.IsInstanceValid(c)) c.Visible = false;
                    EmitSignal(animateIn ? SignalName.SlidIn : SignalName.SlidOut);
                };
            }
        }

        private Vector2 HiddenPos(Godot.Control c) =>
            _visiblePos.TryGetValue(c, out var v) ? v + OffsetFor(Direction) : c.Position + OffsetFor(Direction);

        private Vector2 OffsetFor(SlideDirection d) => d switch
        {
            SlideDirection.Left => new Vector2(-Distance, 0),
            SlideDirection.Right => new Vector2(Distance, 0),
            SlideDirection.Up => new Vector2(0, -Distance),
            SlideDirection.Down => new Vector2(0, Distance),
            _ => Vector2.Zero
        };
    }
}
