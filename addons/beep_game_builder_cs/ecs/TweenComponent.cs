using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Tween preset component. Attach to any Node to add a tween animation.
    /// Blind — works on any Control or Node2D regardless of what it is.
    /// All presets in the Preset enum have concrete behavior.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TweenComponent : GameplayComponent
    {
        public enum Preset { PopIn, PopOut, FadeIn, FadeOut, SlideIn, SlideOut, BounceIn, BounceOut,
            ScaleUp, ScaleDown, RotateIn, RotateOut, Wobble, Shake, Pulse, Flip, Float,
            BtnHoverWobble, CardHoverPop, SpriteStretch, TeleportIn, FlipCard }

        [Export] public Preset Animation { get; set; } = Preset.PopIn;
        [Export] public float Duration { get; set; } = 0.3f;
        [Export] public bool PlayOnReady { get; set; } = true;

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
            if (Engine.IsEditorHint()) return;
            var parent = GetParent();
            if (!IsActive || parent == null || !GodotObject.IsInstanceValid(parent)) return;
            _tween?.Kill();
            var node = parent;

            _tween = CreateTween();
            EmitSignal(SignalName.TweenStarted);

            // A Control inside a Container has its raw scale/position overwritten every layout pass
            // (CLAUDE.md), so for a Control target the transform presets drive the render-only
            // offset_transform_* layer instead (neutral: scale One / position Zero). Node2D targets
            // keep raw scale/position. Only CardHoverPop already used offset_transform.
            bool ctrl = node is Godot.Control;
            if (ctrl) ((Godot.Control)node).OffsetTransformEnabled = true;
            string sProp = ctrl ? "offset_transform_scale" : "scale";
            string pProp = ctrl ? "offset_transform_position" : "position";
            string rProp = ctrl ? "offset_transform_rotation" : "rotation";

            switch (Animation)
            {
                case Preset.PopIn:
                    _tween.TweenProperty(node, sProp, Vector2.One, Duration)
                        .From(Vector2.Zero)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.PopOut:
                    _tween.TweenProperty(node, sProp, Vector2.Zero, Duration)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
                    break;
                case Preset.FadeIn:
                    _tween.TweenProperty(node, "modulate:a", 1f, Duration).From(0f);
                    break;
                case Preset.FadeOut:
                    _tween.TweenProperty(node, "modulate:a", 0f, Duration);
                    break;
                case Preset.SlideIn:
                    _tween.TweenProperty(node, $"{pProp}:x", 0f, Duration).From(-200f).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.SlideOut:
                    _tween.TweenProperty(node, $"{pProp}:x", 200f, Duration).From(0f).SetEase(Tween.EaseType.In);
                    break;
                case Preset.BounceIn:
                    _tween.TweenProperty(node, sProp, Vector2.One, Duration)
                        .From(Vector2.Zero)
                        .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.BounceOut:
                    _tween.TweenProperty(node, sProp, Vector2.Zero, Duration)
                        .From(Vector2.One)
                        .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.In);
                    break;
                case Preset.ScaleUp:
                    _tween.TweenProperty(node, sProp, Vector2.One * 1.15f, Duration)
                        .From(Vector2.One)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.ScaleDown:
                    _tween.TweenProperty(node, sProp, Vector2.One * 0.85f, Duration)
                        .From(Vector2.One)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.RotateIn:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, rProp, 0f, Duration)
                        .From(Mathf.DegToRad(-90f))
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    _tween.TweenProperty(node, "modulate:a", 1f, Duration).From(0f);
                    break;
                case Preset.RotateOut:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, rProp, Mathf.DegToRad(90f), Duration)
                        .From(0f)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
                    _tween.TweenProperty(node, "modulate:a", 0f, Duration);
                    break;
                case Preset.Wobble:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, $"{sProp}:x", 1.2f, Duration * 0.3f);
                    _tween.TweenProperty(node, $"{sProp}:y", 0.8f, Duration * 0.3f);
                    _tween.Chain().TweenProperty(node, sProp, Vector2.One, Duration * 0.3f);
                    break;
                case Preset.Shake:
                    // Base is the neutral offset (Zero) for a Control, else the node's current position.
                    Vector2 shakeBase = ctrl ? Vector2.Zero : node.Get("position").AsVector2();
                    for (int i = 0; i < 5; i++)
                        _tween.TweenProperty(node, pProp, shakeBase + new Vector2(GD.Randf() * 8 - 4, GD.Randf() * 8 - 4), 0.04f);
                    _tween.TweenProperty(node, pProp, shakeBase, 0.04f);   // settle back, or the node stayed offset
                    break;
                case Preset.Pulse:
                    // SetLoops(0) = loop forever (Godot 4) — Pulse is intentionally a perpetual
                    // breathing effect, so TweenFinished never fires for it. Call Stop() to end it.
                    _tween.SetLoops(0);
                    _tween.TweenProperty(node, sProp, Vector2.One * 1.1f, Duration * 0.5f);
                    _tween.TweenProperty(node, sProp, Vector2.One, Duration * 0.5f);
                    break;
                case Preset.Flip:
                    _tween.TweenProperty(node, $"{sProp}:x", -1f, Duration * 0.5f)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                    _tween.TweenProperty(node, $"{sProp}:x", 1f, Duration * 0.5f)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.Float:
                    _tween.SetLoops(0);
                    _tween.TweenProperty(node, $"{pProp}:y", -12f, Duration * 0.5f)
                        .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                    _tween.TweenProperty(node, $"{pProp}:y", 0f, Duration * 0.5f)
                        .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                    break;
                case Preset.BtnHoverWobble:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, $"{sProp}:x", 1.2f, 0.1f);
                    _tween.TweenProperty(node, $"{sProp}:y", 0.75f, 0.13f);
                    // Rotation only on Node2D — offset_transform_rotation is radians with different
                    // anchoring; a hover wobble doesn't need it on Controls.
                    if (!ctrl)
                        _tween.TweenProperty(node, "rotation_degrees", GD.Randf() > 0.5f ? 5f : -5f, 0.1f);
                    break;
                case Preset.CardHoverPop:
                    _tween.SetParallel(true);
                    // Use the type-correct properties: a Node2D has no offset_transform_* (those exist
                    // only on Control), so on a Node2D card this must animate raw scale/rotation or it
                    // errors at runtime.
                    _tween.TweenProperty(node, sProp, Vector2.One * 1.03f, 0.1f);
                    _tween.TweenProperty(node, rProp, 0f, 0.1f);
                    break;
                case Preset.SpriteStretch:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, $"{sProp}:x", 1.25f, Duration * 0.35f)
                        .From(0.8f)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    _tween.TweenProperty(node, $"{sProp}:y", 0.75f, Duration * 0.35f)
                        .From(1.2f)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    _tween.Chain().TweenProperty(node, sProp, Vector2.One, Duration * 0.35f)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    break;
                case Preset.TeleportIn:
                    _tween.SetParallel(true);
                    _tween.TweenProperty(node, sProp, Vector2.One, Duration)
                        .From(Vector2.One * 0.2f)
                        .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                    _tween.TweenProperty(node, rProp, 0f, Duration)
                        .From(Mathf.DegToRad(180f))
                        .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                    _tween.TweenProperty(node, "modulate:a", 1f, Duration * 0.75f).From(0f);
                    break;
                case Preset.FlipCard:
                    _tween.TweenProperty(node, $"{sProp}:x", 0f, Duration * 0.5f)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                    _tween.TweenProperty(node, $"{sProp}:x", 1f, Duration * 0.5f)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                    break;
            }

            _tween.Finished += OnTweenFinished;
        }

        private void OnTweenFinished() => EmitSignal(SignalName.TweenFinished);

        public void Stop() { _tween?.Kill(); }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            _tween = null;
        }
    }
}
