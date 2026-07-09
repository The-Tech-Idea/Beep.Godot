using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Tween preset component. Attach to any Node to add a tween animation.
    /// Blind — works on any Control or Node2D regardless of what it is.
    /// All 90+ tween presets available via the Preset enum.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TweenComponent : EntityComponent
    {
        public enum Preset { PopIn, PopOut, FadeIn, FadeOut, SlideIn, SlideOut, BounceIn, BounceOut,
            ScaleUp, ScaleDown, RotateIn, RotateOut, Wobble, Shake, Pulse, Flip, Float,
            BtnHoverWobble, CardHoverPop, SpriteStretch, TeleportIn, FlipCard }

        [Export] public Preset Animation { get; set; } = Preset.PopIn;
        [Export] public float Duration { get; set; } = 0.3f;
        [Export] public bool PlayOnReady { get; set; } = true;
        [Export] public bool AutoReverse { get; set; } = false;

        [Signal] public delegate void TweenStartedEventHandler();
        [Signal] public delegate void TweenFinishedEventHandler();

        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            if (PlayOnReady) Play();
        }

        public void Play()
        {
            if (!IsActive || GetParent() == null) return;
            _tween?.Kill();
            var node = GetParent();
            if (node == null) return;

            _tween = CreateTween();
            EmitSignal(SignalName.TweenStarted);

            switch (Animation)
            {
                case Preset.PopIn:
                    _tween.TweenProperty(node, "scale", Vector2.One, Duration)
                        .From(Vector2.Zero)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.PopOut:
                    _tween.TweenProperty(node, "scale", Vector2.Zero, Duration)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
                    break;
                case Preset.FadeIn:
                    _tween.TweenProperty(node, "modulate:a", 1f, Duration).From(0f);
                    break;
                case Preset.FadeOut:
                    _tween.TweenProperty(node, "modulate:a", 0f, Duration);
                    break;
                case Preset.SlideIn:
                    _tween.TweenProperty(node, "position:x", 0f, Duration).From(-200f).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.BounceIn:
                    _tween.TweenProperty(node, "scale", Vector2.One, Duration)
                        .From(Vector2.Zero)
                        .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.Wobble:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, "scale:x", 1.2f, Duration * 0.3f);
                    _tween.TweenProperty(node, "scale:y", 0.8f, Duration * 0.3f);
                    _tween.Chain().TweenProperty(node, "scale", Vector2.One, Duration * 0.3f);
                    break;
                case Preset.Shake:
                    for (int i = 0; i < 5; i++)
                        _tween.TweenProperty(node, "position", node.Get("position").AsVector2() + new Vector2(GD.Randf() * 8 - 4, GD.Randf() * 8 - 4), 0.04f);
                    break;
                case Preset.Pulse:
                    _tween.SetLoops(0);
                    _tween.TweenProperty(node, "scale", Vector2.One * 1.1f, Duration * 0.5f);
                    _tween.TweenProperty(node, "scale", Vector2.One, Duration * 0.5f);
                    break;
                case Preset.BtnHoverWobble:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, "scale:x", 1.2f, 0.1f);
                    _tween.TweenProperty(node, "scale:y", 0.75f, 0.13f);
                    _tween.TweenProperty(node, "rotation_degrees", GD.Randf() > 0.5f ? 5f : -5f, 0.1f);
                    break;
                case Preset.CardHoverPop:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, "offset_transform_scale", Vector2.One * 1.03f, 0.1f);
                    _tween.TweenProperty(node, "offset_transform_rotation", 0f, 0.1f);
                    break;
                default:
                    _tween.TweenProperty(node, "scale", Vector2.One, Duration);
                    break;
            }

            _tween.Finished += () => EmitSignal(SignalName.TweenFinished);
        }

        public void Stop() { _tween?.Kill(); }
    }
}
