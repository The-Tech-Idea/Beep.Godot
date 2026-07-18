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
    public partial class SlideInOutComponent : EffectComponent
    {
        public enum SlideDirection { Left, Right, Up, Down }
        public enum SlideMode { SlideAndFade, SlideOnly, FadeOnly }

        [Export] public SlideDirection Direction { get; set; } = SlideDirection.Up;
        [Export] public SlideMode Mode { get; set; } = SlideMode.SlideAndFade;
        [Export] public float Duration { get; set; } = 0.4f;
        [Export] public float Distance { get; set; } = 100f;
        [Export] public bool HiddenOnStart { get; set; } = true;

        /// <summary>Auto-play SlideIn() once on load. On by default so a component dropped on a menu
        /// with the default HiddenOnStart reveals itself, instead of hiding the parent forever with
        /// nothing to un-hide it. Turn off to drive SlideIn()/SlideOut() purely from code.</summary>
        [Export] public bool PlayOnReady { get; set; } = true;

        [Signal] public delegate void SlidInEventHandler();
        [Signal] public delegate void SlidOutEventHandler();

        // Targets slid this run. Animation drives the offset_transform layer (Godot 4.7 render
        // transform containers don't overwrite — see CLAUDE.md), so the visible position is
        // always neutral (Vector2.Zero) and the hidden position is OffsetFor(Direction). Raw
        // Position tweens fought any VBox/HBox/GridContainer re-sort, so there is nothing to snapshot.
        private readonly List<Godot.Control> _slideTargets = new();
        private readonly List<Tween> _activeTweens = new();

        public override void _Ready()
        {
            base._Ready();
            Callable.From(InitTargets).CallDeferred();
        }

        private void InitTargets()
        {
            if (Engine.IsEditorHint()) return;
            _slideTargets.Clear();
            foreach (var c in Targets)
            {
                if (!GodotObject.IsInstanceValid(c)) continue;
                c.OffsetTransformEnabled = true;
                _slideTargets.Add(c);
                if (HiddenOnStart)
                {
                    c.OffsetTransformPosition = OffsetFor(Direction);
                    if (Mode != SlideMode.SlideOnly) c.Modulate = new Color(1, 1, 1, 0);
                    c.Visible = false;
                }
            }

            // Reveal on load by default. Without this, HiddenOnStart hid the parent and nothing ever
            // un-hid it unless game code called SlideIn() — a menu with the component read as broken.
            if (PlayOnReady)
                Callable.From(SlideIn).CallDeferred();
            else if (HiddenOnStart)
                GD.PushWarning($"[{Name}] SlideInOutComponent hid its target(s) (HiddenOnStart) with PlayOnReady off — they stay invisible until something calls SlideIn().");
        }

        public void SlideIn()
        {
            if (!IsActive || _slideTargets.Count == 0) return;
            StartTweens(animateIn: true);
        }

        public void SlideOut()
        {
            if (!IsActive || _slideTargets.Count == 0) return;
            StartTweens(animateIn: false);
        }

        private void StartTweens(bool animateIn)
        {
            // Kill any running tweens.
            foreach (var t in _activeTweens)
                if (GodotObject.IsInstanceValid(t)) t.Kill();
            _activeTweens.Clear();

            foreach (var c in _slideTargets)
            {
                if (!GodotObject.IsInstanceValid(c)) continue;
                if (animateIn) c.Visible = true;

                var tw = c.CreateTween().SetParallel(true);
                if (Mode != SlideMode.FadeOnly)
                {
                    Vector2 target = animateIn ? Vector2.Zero : OffsetFor(Direction);
                    Vector2 start = animateIn ? OffsetFor(Direction) : Vector2.Zero;
                    tw.TweenProperty(c, "offset_transform_position", target, Duration)
                      .From(start)   // was computed but never applied — SlideIn from a non-hidden control tweened visible->visible (no motion)
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
                        foreach (var c in _slideTargets)
                            if (GodotObject.IsInstanceValid(c)) c.Visible = false;
                    EmitSignal(animateIn ? SignalName.SlidIn : SignalName.SlidOut);
                };
            }
        }

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
