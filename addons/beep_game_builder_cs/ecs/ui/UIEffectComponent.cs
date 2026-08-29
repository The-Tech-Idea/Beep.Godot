using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    public enum EffectType { Slide, Shake, Pulse, Bob, Flash, Glitch, Rotate, Fade, Typewriter, Bounce, Offset }
    public enum ScopeType { Self, Children, Scene, Global }
    public enum SlideDirection { Left, Right, Up, Down }
    public enum FadeDirection { In, Out, InOut }
    public enum RotateAxis { X, Y, Z }

    /// <summary>
    /// Unified UI effect component. Attach to any node to apply animated effects.
    ///
    /// Two dropdowns control everything:
    ///   Effect — what animation to play (Slide, Shake, Pulse, Bob, Flash, …)
    ///   Scope  — what to target (Self, Children, Scene, Global)
    ///
    /// Plays on _Ready by default. Call Play() / Stop() from code.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class UIEffectComponent : UIComponent
    {
        // ═══════════════════════════════════════════════════════════════
        // Core
        // ═══════════════════════════════════════════════════════════════

        [Export] public EffectType Effect { get; set; } = EffectType.Slide;
        [Export] public ScopeType Scope { get; set; } = ScopeType.Self;

        // ═══════════════════════════════════════════════════════════════
        // Timing
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0,10,0.05")]
        public float Duration { get; set; } = 0.4f;

        [Export(PropertyHint.Range, "0,5,0.05")]
        public float InitialDelay { get; set; } = 0f;

        [Export] public Tween.EaseType Easing { get; set; } = Tween.EaseType.Out;

        [Export] public Tween.TransitionType Transition { get; set; } = Tween.TransitionType.Back;

        // ═══════════════════════════════════════════════════════════════
        // Playback
        // ═══════════════════════════════════════════════════════════════

        [Export] public bool PlayOnReady { get; set; } = true;
        [Export] public bool Looping { get; set; } = false;

        [Export(PropertyHint.Range, "0,10,0.1")]
        public float LoopDelay { get; set; } = 0f;

        // ═══════════════════════════════════════════════════════════════
        // Slide
        // ═══════════════════════════════════════════════════════════════

        [Export] public SlideDirection SlideDir { get; set; } = SlideDirection.Up;

        [Export(PropertyHint.Range, "0,2000,1")]
        public float SlideDistance { get; set; } = 100f;

        // ═══════════════════════════════════════════════════════════════
        // Shake
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0,200,0.5")]
        public float ShakeIntensity { get; set; } = 10f;

        [Export(PropertyHint.Range, "1,50")]
        public int ShakeVibrato { get; set; } = 20;

        // ═══════════════════════════════════════════════════════════════
        // Pulse
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0.5,2,0.05")]
        public float PulseMinScale { get; set; } = 0.95f;

        [Export(PropertyHint.Range, "0.5,2,0.05")]
        public float PulseMaxScale { get; set; } = 1.05f;

        [Export(PropertyHint.Range, "0,20")]
        public int PulseLoops { get; set; } = 0; // 0 = infinite

        // ═══════════════════════════════════════════════════════════════
        // Bob
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0,100,0.5")]
        public float BobHeight { get; set; } = 10f;

        [Export(PropertyHint.Range, "0.1,10,0.1")]
        public float BobSpeed { get; set; } = 2f;

        // ═══════════════════════════════════════════════════════════════
        // Flash
        // ═══════════════════════════════════════════════════════════════

        [Export] public Color FlashColor { get; set; } = Colors.White;

        [Export(PropertyHint.Range, "1,10")]
        public int FlashCount { get; set; } = 2;

        // ═══════════════════════════════════════════════════════════════
        // Glitch
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0,100,0.5")]
        public float GlitchIntensity { get; set; } = 5f;

        [Export(PropertyHint.Range, "1,50")]
        public int GlitchSegments { get; set; } = 10;

        // ═══════════════════════════════════════════════════════════════
        // Rotate
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "-360,360,1")]
        public float RotateAngle { get; set; } = 360f;

        [Export] public RotateAxis RotateAxis { get; set; } = RotateAxis.Z;

        // ═══════════════════════════════════════════════════════════════
        // Fade
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0,1,0.05")]
        public float FadeTargetAlpha { get; set; } = 0f;

        [Export] public FadeDirection FadeDir { get; set; } = FadeDirection.In;

        // ═══════════════════════════════════════════════════════════════
        // Typewriter
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "1,200,1")]
        public float TypewriterSpeed { get; set; } = 30f;

        [Export] public string TypewriterCursor { get; set; } = "|";

        /// <summary>Blink the cursor while text reveals (folded in from the old standalone
        /// TypewriterComponent). Off = a steady cursor.</summary>
        [Export] public bool TypewriterBlinkCursor { get; set; } = true;

        // ═══════════════════════════════════════════════════════════════
        // Bounce
        // ═══════════════════════════════════════════════════════════════

        [Export(PropertyHint.Range, "0,200,1")]
        public float BounceHeight { get; set; } = 60f;

        [Export(PropertyHint.Range, "1,10")]
        public int BounceCount { get; set; } = 3;

        // ═══════════════════════════════════════════════════════════════
        // Offset
        // ═══════════════════════════════════════════════════════════════

        [Export] public Vector2 OffsetTarget { get; set; } = Vector2.Zero;

        public float EffectiveDuration => Mathf.Max(0.001f, FiniteOr(Duration, 0.001f));
        public float EffectiveInitialDelay => Mathf.Max(0f, FiniteOr(InitialDelay, 0f));
        public float EffectiveLoopDelay => Mathf.Max(0f, FiniteOr(LoopDelay, 0f));
        public float EffectiveSlideDistance => Mathf.Max(0f, FiniteOr(SlideDistance, 0f));
        public float EffectiveShakeIntensity => Mathf.Max(0f, FiniteOr(ShakeIntensity, 0f));
        public int EffectiveShakeVibrato => Mathf.Clamp(ShakeVibrato, 1, 200);
        public float EffectivePulseMinScale => Mathf.Clamp(Mathf.Min(FiniteOr(PulseMinScale, 0.95f), FiniteOr(PulseMaxScale, 1.05f)), 0.01f, 10f);
        public float EffectivePulseMaxScale => Mathf.Clamp(Mathf.Max(FiniteOr(PulseMinScale, 0.95f), FiniteOr(PulseMaxScale, 1.05f)), 0.01f, 10f);
        public int EffectivePulseLoops => Mathf.Clamp(PulseLoops, 0, 1000);
        public float EffectiveBobHeight => Mathf.Max(0f, FiniteOr(BobHeight, 0f));
        public float EffectiveBobSpeed => Mathf.Max(0f, FiniteOr(BobSpeed, 0f));
        public int EffectiveFlashCount => Mathf.Clamp(FlashCount, 1, 1000);
        public float EffectiveGlitchIntensity => Mathf.Max(0f, FiniteOr(GlitchIntensity, 0f));
        public int EffectiveGlitchSegments => Mathf.Clamp(GlitchSegments, 1, 1000);
        public float EffectiveRotateAngle => FiniteOr(RotateAngle, 0f);
        public float EffectiveFadeTargetAlpha => Mathf.Clamp(FiniteOr(FadeTargetAlpha, 0f), 0f, 1f);
        public float EffectiveTypewriterSpeed => Mathf.Max(1f, FiniteOr(TypewriterSpeed, 30f));
        public float EffectiveBounceHeight => Mathf.Max(0f, FiniteOr(BounceHeight, 0f));
        public int EffectiveBounceCount => Mathf.Clamp(BounceCount, 1, 1000);
        public Vector2 EffectiveOffsetTarget => new(FiniteOr(OffsetTarget.X, 0f), FiniteOr(OffsetTarget.Y, 0f));

        // ═══════════════════════════════════════════════════════════════
        // Signals
        // ═══════════════════════════════════════════════════════════════

        [Signal] public delegate void EffectStartedEventHandler();
        [Signal] public delegate void EffectCompletedEventHandler();
        [Signal] public delegate void EffectLoopedEventHandler(int loopCount);

        // ═══════════════════════════════════════════════════════════════
        // State
        // ═══════════════════════════════════════════════════════════════

        private readonly List<Godot.Control> _targets = new();
        private readonly List<Tween> _activeTweens = new();
        private bool _isPlaying;
        private int _loopCount;
        private float _processTime;

        // No position/scale snapshots: every transform effect animates the offset_transform_*
        // layer (Godot 4.7 render transform that containers never overwrite — see CLAUDE.md and
        // the GDScript twin ui_effect.gd). The offset is relative to the laid-out position, so
        // neutral is always Vector2.Zero / Vector2.One — there is nothing to capture or restore.
        // Animating raw position/scale here fought every VBox/HBox/GridContainer re-sort.

        // ═══════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (Engine.IsEditorHint()) return;
            ResolveTargets();
            if (PlayOnReady)
                CallDeferred(nameof(Play));
        }

        public override void _Process(double delta)
        {
            if (!IsActive || !_isPlaying)
            {
                UpdateProcessing();
                return;
            }

            // Bob needs per-frame sine-based animation
            if (Effect == EffectType.Bob)
            {
                _processTime += Mathf.Max(0f, (float)delta);
                float offset = (float)Math.Sin(_processTime * EffectiveBobSpeed) * EffectiveBobHeight;
                foreach (var c in _targets)
                {
                    if (c != null && GodotObject.IsInstanceValid(c))
                        c.OffsetTransformPosition = new Vector2(0, offset);
                }
            }

            // Typewriter runs per-frame for character reveal
            if (Effect == EffectType.Typewriter)
                ProcessTypewriter((float)delta);
        }

        public override void _ExitTree()
        {
            Stop();
            base._ExitTree();
        }

        // ═══════════════════════════════════════════════════════════════
        // Target Resolution
        // ═══════════════════════════════════════════════════════════════

        private void ResolveTargets()
        {
            _targets.Clear();

            switch (Scope)
            {
                case ScopeType.Self:
                    var parent = GetParent() as Godot.Control;
                    if (parent != null) AddTarget(parent);
                    else if (!Engine.IsEditorHint())
                        GD.PushWarning($"[{Name}] UIEffectComponent Scope=Self but parent is {GetParent()?.GetType().Name ?? "null"}, not a Control — nothing to animate. Reparent under the Control to affect, or use Scope=Scene/Global.");
                    break;

                case ScopeType.Children:
                    var root = GetParent() as Godot.Control;
                    if (root != null)
                    {
                        AddTarget(root);
                        CollectAllControls(root, _targets, skipRoot: true);
                    }
                    else if (!Engine.IsEditorHint())
                        GD.PushWarning($"[{Name}] UIEffectComponent Scope=Children but parent is {GetParent()?.GetType().Name ?? "null"}, not a Control — no children to animate. Reparent under the Control whose children to affect.");
                    break;

                case ScopeType.Scene:
                    var scene = GetTree()?.CurrentScene;
                    if (scene != null) CollectAllControls(scene, _targets);
                    break;

                case ScopeType.Global:
                    var treeRoot = GetTree()?.Root;
                    if (treeRoot != null) CollectAllControls(treeRoot, _targets);
                    break;
            }

            // Enable the offset transform layer on every target so the transform effects
            // render. Neutral is Vector2.Zero / Vector2.One — nothing to snapshot.
            foreach (var c in _targets)
            {
                if (c == null || !GodotObject.IsInstanceValid(c)) continue;
                c.OffsetTransformEnabled = true;
            }
        }

        private void AddTarget(Godot.Control c)
        {
            if (c != null && GodotObject.IsInstanceValid(c) && !_targets.Contains(c))
                _targets.Add(c);
        }

        private static void CollectAllControls(Node node, List<Godot.Control> list, bool skipRoot = false)
        {
            if (!skipRoot && node is Godot.Control c)
                list.Add(c);
            foreach (var child in node.GetChildren())
                if (child is Node n)
                    CollectAllControls(n, list);
        }

        // ═══════════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════════

        public void Play()
        {
            if (!IsActive) return;
            if (_targets.Count == 0)
            {
                // Nothing resolved (non-Control parent for Self/Children, or an empty Scene/Global
                // sweep) — say so instead of returning in silence, the repo's dominant defect class.
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] UIEffectComponent.Play() has no targets (Scope={Scope}) — nothing will animate.");
                return;
            }
            Stop();
            _isPlaying = true;
            _loopCount = 0;

            if (EffectiveInitialDelay > 0f)
            {
                // Use a one-shot timer for delay
                var timer = new Timer { OneShot = true, WaitTime = EffectiveInitialDelay };
                timer.Timeout += () =>
                {
                    timer.QueueFree();
                    if (_isPlaying) ExecuteEffect();   // a Stop() during the delay must cancel it
                };
                AddChild(timer);
                timer.Start();
            }
            else
            {
                ExecuteEffect();
            }
        }

        public void Stop()
        {
            _isPlaying = false;
            _processTime = 0f;
            StopTypewriter();
            UpdateProcessing();

            foreach (var t in _activeTweens)
                if (t != null && GodotObject.IsInstanceValid(t))
                    t.Kill();
            _activeTweens.Clear();
        }

        public void Reset()
        {
            Stop();
            foreach (var c in _targets)
            {
                if (c == null || !GodotObject.IsInstanceValid(c)) continue;
                // Zero the offset layer rather than restoring captured values — the layout
                // position is untouched throughout, so neutral IS zero/one.
                c.OffsetTransformPosition = Vector2.Zero;
                c.OffsetTransformScale = Vector2.One;
                c.OffsetTransformRotation = 0f;
                c.Modulate = Colors.White;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Effect Dispatcher
        // ═══════════════════════════════════════════════════════════════

        private void ExecuteEffect()
        {
            // Clear the prior cycle's tweens before starting a new one. A looping effect re-enters
            // here from OnAllCompleted, and only Stop()/Play() cleared the list — so over a long loop
            // it accumulated one (finished, auto-freed) tween reference every cycle. Killing here is
            // safe: on re-entry the previous tweens have finished.
            foreach (var t in _activeTweens)
                if (t != null && GodotObject.IsInstanceValid(t)) t.Kill();
            _activeTweens.Clear();

            EmitSignal(SignalName.EffectStarted);

            foreach (var c in _targets)
            {
                if (c == null || !GodotObject.IsInstanceValid(c)) continue;
                // Re-assert the offset layer — targets can be re-resolved after _Ready.
                c.OffsetTransformEnabled = true;

                switch (Effect)
                {
                    case EffectType.Slide: PlaySlide(c); break;
                    case EffectType.Shake: PlayShake(c); break;
                    case EffectType.Pulse: PlayPulse(c); break;
                    case EffectType.Bob: _processTime = 0f; break; // Bob runs in _Process
                    case EffectType.Flash: PlayFlash(c); break;
                    case EffectType.Glitch: PlayGlitch(c); break;
                    case EffectType.Rotate: PlayRotate(c); break;
                    case EffectType.Fade: PlayFade(c); break;
                    case EffectType.Typewriter: PlayTypewriter(c); break;
                    case EffectType.Bounce: PlayBounce(c); break;
                    case EffectType.Offset: PlayOffset(c); break;
                }
            }

            // Bob has no tween completion — it runs continuously
            if (Effect != EffectType.Bob && _activeTweens.Count > 0)
            {
                // Wait for the longest tween to finish
                var lastTween = _activeTweens[^1];
                lastTween.Finished += OnAllCompleted;
            }
            UpdateProcessing();
        }

        private void OnAllCompleted()
        {
            if (Looping && IsActive && _isPlaying)
            {
                _loopCount++;
                EmitSignal(SignalName.EffectLooped, _loopCount);

                if (EffectiveLoopDelay > 0f)
                {
                    var timer = new Timer { OneShot = true, WaitTime = EffectiveLoopDelay };
                    timer.Timeout += () => { timer.QueueFree(); if (_isPlaying) ExecuteEffect(); };
                    AddChild(timer);
                    timer.Start();
                }
                else
                {
                    ExecuteEffect();
                }
            }
            else
            {
                _isPlaying = false;
                UpdateProcessing();
                EmitSignal(SignalName.EffectCompleted);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Slide
        // ═══════════════════════════════════════════════════════════════

        private void PlaySlide(Godot.Control c)
        {
            Vector2 offset = SlideDir switch
            {
                SlideDirection.Left => new Vector2(-EffectiveSlideDistance, 0),
                SlideDirection.Right => new Vector2(EffectiveSlideDistance, 0),
                SlideDirection.Up => new Vector2(0, -EffectiveSlideDistance),
                SlideDirection.Down => new Vector2(0, EffectiveSlideDistance),
                _ => Vector2.Zero
            };

            c.OffsetTransformPosition = offset;
            c.Modulate = new Color(1, 1, 1, 0);

            var tween = c.CreateTween().SetParallel(true);
            float duration = EffectiveDuration;
            tween.TweenProperty(c, "offset_transform_position", Vector2.Zero, duration).SetEase(Easing).SetTrans(Transition);
            tween.TweenProperty(c, "modulate:a", 1f, duration * 0.6f);
            _activeTweens.Add(tween);
        }

        // ═══════════════════════════════════════════════════════════════
        // Shake
        // ═══════════════════════════════════════════════════════════════

        private void PlayShake(Godot.Control c)
        {
            int steps = EffectiveShakeVibrato;
            float duration = EffectiveDuration;
            float intensity = EffectiveShakeIntensity;

            var tween = c.CreateTween();
            for (int i = 0; i < steps; i++)
            {
                float fraction = (i + 1) / (float)steps;
                float decay = 1f - fraction;
                float xOff = (GD.Randf() * 2 - 1) * intensity * decay;
                float yOff = (GD.Randf() * 2 - 1) * intensity * decay;
                tween.TweenProperty(c, "offset_transform_position", new Vector2(xOff, yOff), duration / steps);
            }
            tween.TweenProperty(c, "offset_transform_position", Vector2.Zero, duration / steps); // settle
            _activeTweens.Add(tween);
        }

        // ═══════════════════════════════════════════════════════════════
        // Pulse
        // ═══════════════════════════════════════════════════════════════

        private void PlayPulse(Godot.Control c)
        {
            float halfDur = EffectiveDuration / 2f;
            int effectiveLoops = EffectivePulseLoops;
            int loops = effectiveLoops > 0 ? effectiveLoops : -1;
            float minScale = EffectivePulseMinScale;
            float maxScale = EffectivePulseMaxScale;

            if (loops < 0)
            {
                // Infinite: use a looping tween
                var tween = c.CreateTween().SetLoops(0);
                tween.TweenProperty(c, "offset_transform_scale", new Vector2(maxScale, maxScale), halfDur)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
                tween.TweenProperty(c, "offset_transform_scale", new Vector2(minScale, minScale), halfDur)
                    .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
                _activeTweens.Add(tween);
            }
            else
            {
                var tween = c.CreateTween();
                for (int i = 0; i < loops; i++)
                {
                    tween.TweenProperty(c, "offset_transform_scale", new Vector2(maxScale, maxScale), halfDur)
                        .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
                    tween.TweenProperty(c, "offset_transform_scale", new Vector2(minScale, minScale), halfDur)
                        .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
                }
                tween.TweenProperty(c, "offset_transform_scale", Vector2.One, halfDur);
                _activeTweens.Add(tween);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Flash
        // ═══════════════════════════════════════════════════════════════

        private void PlayFlash(Godot.Control c)
        {
            var origColor = c.Modulate;
            int count = EffectiveFlashCount;
            float flashDur = EffectiveDuration / (count * 2);
            var tween = c.CreateTween();

            for (int i = 0; i < count; i++)
            {
                tween.TweenProperty(c, "modulate", FlashColor, flashDur);
                tween.TweenProperty(c, "modulate", origColor, flashDur);
            }
            _activeTweens.Add(tween);
        }

        // ═══════════════════════════════════════════════════════════════
        // Glitch
        // ═══════════════════════════════════════════════════════════════

        private void PlayGlitch(Godot.Control c)
        {
            int segments = EffectiveGlitchSegments;
            float segDur = EffectiveDuration / segments;
            float intensity = EffectiveGlitchIntensity;
            var tween = c.CreateTween();

            for (int i = 0; i < segments; i++)
            {
                float xOff = (GD.Randf() * 2 - 1) * intensity;
                float yOff = (GD.Randf() * 2 - 1) * intensity;
                float sOff = Mathf.Max(0.01f, 1f + (GD.Randf() * 2 - 1) * intensity * 0.01f);
                float rOff = (GD.Randf() * 2 - 1) * intensity * 0.02f;

                tween.TweenProperty(c, "offset_transform_position", new Vector2(xOff, yOff), segDur * 0.5f);
                tween.TweenProperty(c, "offset_transform_scale", new Vector2(sOff, sOff), segDur * 0.5f);
                tween.TweenProperty(c, "offset_transform_rotation", rOff, segDur * 0.5f);

                if (i < segments - 1)
                {
                    tween.TweenProperty(c, "offset_transform_position", Vector2.Zero, segDur * 0.5f);
                    tween.TweenProperty(c, "offset_transform_scale", Vector2.One, segDur * 0.5f);
                    tween.TweenProperty(c, "offset_transform_rotation", 0f, segDur * 0.5f);
                }
            }

            // Final settle
            tween.TweenProperty(c, "offset_transform_position", Vector2.Zero, segDur);
            tween.TweenProperty(c, "offset_transform_scale", Vector2.One, segDur);
            tween.TweenProperty(c, "offset_transform_rotation", 0f, segDur);

            _activeTweens.Add(tween);
        }

        // ═══════════════════════════════════════════════════════════════
        // Rotate
        // ═══════════════════════════════════════════════════════════════

        private void PlayRotate(Godot.Control c)
        {
            float radians = Mathf.DegToRad(EffectiveRotateAngle);
            float duration = EffectiveDuration;

            switch (RotateAxis)
            {
                case RotateAxis.X:
                {
                    var tween = c.CreateTween();
                    tween.TweenProperty(c, "offset_transform_scale:y", 0f, duration * 0.5f);
                    tween.TweenProperty(c, "offset_transform_scale:y", 1f, duration * 0.5f);
                    _activeTweens.Add(tween);
                    break;
                }
                case RotateAxis.Y:
                {
                    var tween = c.CreateTween();
                    tween.TweenProperty(c, "offset_transform_scale:x", 0f, duration * 0.5f);
                    tween.TweenProperty(c, "offset_transform_scale:x", 1f, duration * 0.5f);
                    _activeTweens.Add(tween);
                    break;
                }
                case RotateAxis.Z:
                {
                    var tween = c.CreateTween();
                    tween.TweenProperty(c, "offset_transform_rotation", radians, duration).SetEase(Easing).SetTrans(Transition);
                    _activeTweens.Add(tween);
                    break;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Fade
        // ═══════════════════════════════════════════════════════════════

        private void PlayFade(Godot.Control c)
        {
            switch (FadeDir)
            {
                case FadeDirection.In:
                    c.Modulate = new Color(1, 1, 1, 0);
                    break;
                case FadeDirection.Out:
                    c.Modulate = new Color(1, 1, 1, 1);
                    break;
                case FadeDirection.InOut:
                    c.Modulate = new Color(1, 1, 1, 0);
                    break;
            }

            var tween = c.CreateTween();
            float target = EffectiveFadeTargetAlpha;
            float duration = EffectiveDuration;

            if (FadeDir == FadeDirection.InOut)
            {
                tween.TweenProperty(c, "modulate:a", 1f, duration * 0.5f).SetEase(Easing).SetTrans(Transition);
                tween.TweenProperty(c, "modulate:a", target, duration * 0.5f).SetEase(Easing).SetTrans(Transition);
            }
            else
            {
                tween.TweenProperty(c, "modulate:a", target, duration).SetEase(Easing).SetTrans(Transition);
            }

            _activeTweens.Add(tween);
        }

        // ═══════════════════════════════════════════════════════════════
        // Typewriter
        // ═══════════════════════════════════════════════════════════════

        // Typewriter state
        private readonly Dictionary<Godot.Control, TypewriterState> _typewriterStates = new();

        private class TypewriterState
        {
            public string FullText = "";
            public string CursorStr = "";
            public float Elapsed;
            public bool IsRichLabel;
        }

        private void PlayTypewriter(Godot.Control c)
        {
            string fullText = "";
            bool isRich = false;
            if (c is RichTextLabel rtl) { fullText = rtl.Text; isRich = true; }
            else if (c is Label lbl) { fullText = lbl.Text; isRich = false; }
            else return;

            if (string.IsNullOrEmpty(fullText)) return;
            string cursorStr = string.IsNullOrEmpty(TypewriterCursor) ? "|" : TypewriterCursor;

            _typewriterStates[c] = new TypewriterState
            {
                FullText = fullText,
                CursorStr = cursorStr,
                Elapsed = 0f,
                IsRichLabel = isRich
            };
        }

        private void ProcessTypewriter(float delta)
        {
            List<Godot.Control>? completed = null;
            foreach (var kvp in _typewriterStates)
            {
                var c = kvp.Key;
                var state = kvp.Value;
                if (!GodotObject.IsInstanceValid(c))
                {
                    (completed ??= new List<Godot.Control>()).Add(c);
                    continue;
                }
                state.Elapsed += Mathf.Max(0f, delta);

                int totalChars = state.FullText.Length;
                int visible = Mathf.Clamp((int)(state.Elapsed * EffectiveTypewriterSpeed), 0, totalChars);
                string shown = state.FullText[..visible];

                // Cursor only while still revealing; blinks unless TypewriterBlinkCursor is off.
                string cursor = "";
                if (visible < totalChars)
                    cursor = (!TypewriterBlinkCursor || Mathf.Sin(state.Elapsed * 6f) > 0f) ? state.CursorStr : " ";

                if (state.IsRichLabel && c is RichTextLabel rt)
                    rt.Text = shown + cursor;
                else if (c is Label l)
                    l.Text = shown + cursor;

                if (visible >= totalChars)
                    (completed ??= new List<Godot.Control>()).Add(c);
            }

            // Finished reveals emit once and are dropped — otherwise a completed typewriter is
            // reprocessed every frame forever (the state was never cleared).
            if (completed != null)
                foreach (var c in completed)
                {
                    _typewriterStates.Remove(c);
                    EmitSignal(SignalName.EffectCompleted);
                }

            if (_typewriterStates.Count == 0)
            {
                if (Looping && IsActive && _isPlaying)
                {
                    _loopCount++;
                    EmitSignal(SignalName.EffectLooped, _loopCount);
                    ExecuteEffect();
                }
                else
                {
                    _isPlaying = false;
                    UpdateProcessing();
                }
            }
        }

        private void StopTypewriter()
        {
            _typewriterStates.Clear();
        }

        private bool ShouldProcess()
            => IsActive && _isPlaying
            && (Effect == EffectType.Bob || (Effect == EffectType.Typewriter && _typewriterStates.Count > 0));

        private void UpdateProcessing() => SetProcess(ShouldProcess());

        // ═══════════════════════════════════════════════════════════════
        // Bounce
        // ═══════════════════════════════════════════════════════════════

        private void PlayBounce(Godot.Control c)
        {
            int count = EffectiveBounceCount;
            float perBounce = EffectiveDuration / count;
            float bounceHeight = EffectiveBounceHeight;
            var tween = c.CreateTween();

            for (int i = 0; i < count; i++)
            {
                float height = bounceHeight * (1f - (float)i / count); // decay
                tween.TweenProperty(c, "offset_transform_position:y", -height, perBounce * 0.4f)
                    .SetEase(Tween.EaseType.Out);
                tween.TweenProperty(c, "offset_transform_position:y", 0f, perBounce * 0.6f)
                    .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Bounce);
            }

            _activeTweens.Add(tween);
        }

        // ═══════════════════════════════════════════════════════════════
        // Offset
        // ═══════════════════════════════════════════════════════════════

        private void PlayOffset(Godot.Control c)
        {
            var tween = c.CreateTween();
            tween.TweenProperty(c, "offset_transform_position", EffectiveOffsetTarget, EffectiveDuration).SetEase(Easing).SetTrans(Transition);
            _activeTweens.Add(tween);
        }

        private static float FiniteOr(float value, float fallback) => float.IsFinite(value) ? value : fallback;

        // ═══════════════════════════════════════════════════════════════
        // Editor Property Visibility
        // ═══════════════════════════════════════════════════════════════

        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            string name = (string)property["name"];

            // Determine visibility groups
            bool isSlide = name.StartsWith("Slide");
            bool isShake = name.StartsWith("Shake");
            bool isPulse = name.StartsWith("Pulse");
            bool isBob = name.StartsWith("Bob");
            bool isFlash = name.StartsWith("Flash");
            bool isGlitch = name.StartsWith("Glitch");
            bool isRotate = name.StartsWith("Rotate");
            bool isFade = name.StartsWith("Fade");
            bool isTypewriter = name.StartsWith("Typewriter");
            bool isBounce = name.StartsWith("Bounce");
            bool isOffset = name.StartsWith("Offset");

            bool show = false;
            switch (Effect)
            {
                case EffectType.Slide: show = isSlide; break;
                case EffectType.Shake: show = isShake; break;
                case EffectType.Pulse: show = isPulse; break;
                case EffectType.Bob: show = isBob; break;
                case EffectType.Flash: show = isFlash; break;
                case EffectType.Glitch: show = isGlitch; break;
                case EffectType.Rotate: show = isRotate; break;
                case EffectType.Fade: show = isFade; break;
                case EffectType.Typewriter: show = isTypewriter; break;
                case EffectType.Bounce: show = isBounce; break;
                case EffectType.Offset: show = isOffset; break;
            }

            if (isSlide || isShake || isPulse || isBob || isFlash || isGlitch || isRotate || isFade || isTypewriter || isBounce || isOffset)
            {
                property["usage"] = (int)(show
                    ? Godot.PropertyUsageFlags.Default
                    : Godot.PropertyUsageFlags.NoEditor);
            }
        }
    }
}
