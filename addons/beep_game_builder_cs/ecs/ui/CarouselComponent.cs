using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Horizontal card carousel. Attach to a Container (or any Control) with child
    /// Controls as slides. Blind — works for image galleries, featured items,
    /// tutorials, card browsers.
    ///
    /// The carousel fully owns slide placement (absolute x fan-out + scale + fade),
    /// so each slide is made <c>TopLevel</c> at startup to escape the parent's layout.
    /// Without that, a sorting parent (VBox/HBox/GridContainer, or even the base
    /// Container, which fits every child to its full rect) re-arranged the slides
    /// every layout pass and overwrote the carousel's own positioning and tweens.
    /// Slides are then positioned in global coordinates, anchored to the container's
    /// on-screen rect.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CarouselComponent : UIComponent
    {
        [Export] public float CardWidth { get; set; } = 280f;
        [Export] public float Spacing { get; set; } = 16f;
        [Export] public float TransitionDuration { get; set; } = 0.4f;
        [Export] public float InactiveScale { get; set; } = 0.85f;
        [Export] public float InactiveAlpha { get; set; } = 0.5f;
        [Export] public bool Loop { get; set; } = true;
        [Export] public bool AutoPlay { get => _autoPlay; set { if (_autoPlay == value) return; _autoPlay = value; UpdateProcessing(); } }
        [Export] public float AutoPlayInterval { get; set; } = 3f;

        public float EffectiveCardWidth => Mathf.Max(1f, float.IsFinite(CardWidth) ? CardWidth : 280f);
        public float EffectiveSpacing => Mathf.Max(0f, float.IsFinite(Spacing) ? Spacing : 0f);
        public float EffectiveTransitionDuration => Mathf.Max(0.001f, float.IsFinite(TransitionDuration) ? TransitionDuration : 0.001f);
        public float EffectiveInactiveScale => Mathf.Clamp(float.IsFinite(InactiveScale) ? InactiveScale : 0.85f, 0.01f, 10f);
        public float EffectiveInactiveAlpha => Mathf.Clamp(float.IsFinite(InactiveAlpha) ? InactiveAlpha : 0.5f, 0f, 1f);
        public float EffectiveAutoPlayInterval => Mathf.Max(0.05f, float.IsFinite(AutoPlayInterval) ? AutoPlayInterval : 0.05f);

        [Signal] public delegate void SlideChangedEventHandler(int index);

        private Godot.Control? _container;
        private bool _autoPlay;
        private int _currentIndex;
        private float _autoTimer;
        // The slides this carousel drives, and each slide's vertical baseline relative to the
        // container (captured before it goes TopLevel, so its Y placement is preserved).
        private readonly List<Godot.Control> _slides = new();
        private readonly Dictionary<Godot.Control, float> _baseY = new();
        private readonly List<Tween> _activeTweens = new();

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (Engine.IsEditorHint()) return;
            _container = GetParent() as Godot.Control;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] CarouselComponent needs a Control parent whose children are the slides; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the slide container.");
                return;
            }
            // Defer so the container has been laid out once — we read each slide's settled
            // position to capture its vertical baseline before making it TopLevel.
            CallDeferred(nameof(InitSlides));
        }

        private void InitSlides()
        {
            if (_container == null || !GodotObject.IsInstanceValid(_container)) return;

            _slides.Clear();
            _baseY.Clear();
            foreach (var child in _container.GetChildren())
            {
                if (child is not Godot.Control slide) continue;
                // Capture vertical baseline relative to the container, then escape its layout.
                _baseY[slide] = slide.Position.Y;
                slide.TopLevel = true;
                _slides.Add(slide);
            }

            if (_slides.Count == 0)
            {
                GD.PushWarning($"[{Name}] CarouselComponent found no Control slides under its parent — nothing to show.");
                UpdateProcessing();
                return;
            }
            GoToSlide(0, true);
            UpdateProcessing();
        }

        public override void _Process(double delta)
        {
            if (!AutoPlay || !IsActive || _slides.Count == 0)
            {
                UpdateProcessing();
                return;
            }
            _autoTimer += Mathf.Max(0f, (float)delta);
            if (_autoTimer >= EffectiveAutoPlayInterval) { _autoTimer = 0; Next(); }
        }

        private void UpdateProcessing()
            => SetProcess(!Engine.IsEditorHint() && IsActive && AutoPlay && _slides.Count > 0);

        public void Next() => GoToSlide((_currentIndex + 1) % _slides.Count);
        public void Previous() => GoToSlide((_currentIndex - 1 + _slides.Count) % _slides.Count);

        public override void _UnhandledInput(InputEvent @event)
        {
            // Keyboard / gamepad navigation. ui_left/ui_right are built-in actions (always present),
            // so no InputActionsAvailable gate is needed.
            if (!IsActive || Engine.IsEditorHint() || _slides.Count == 0) return;
            if (@event.IsActionPressed("ui_right")) { Next(); GetViewport().SetInputAsHandled(); }
            else if (@event.IsActionPressed("ui_left")) { Previous(); GetViewport().SetInputAsHandled(); }
        }

        public void GoToSlide(int index, bool instant = false)
        {
            if (_container == null || !IsActive || _slides.Count == 0) return;
            if (!Loop && (index < 0 || index >= _slides.Count)) return;
            if (!Loop) index = Mathf.Clamp(index, 0, _slides.Count - 1);

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            _currentIndex = ((index % _slides.Count) + _slides.Count) % _slides.Count;

            // Anchor to the container's on-screen rect (slides are TopLevel → global coords).
            float centerX = _container.GlobalPosition.X + _container.Size.X / 2f;
            float baseY = _container.GlobalPosition.Y;

            for (int i = 0; i < _slides.Count; i++)
            {
                var slide = _slides[i];
                if (!GodotObject.IsInstanceValid(slide)) continue;

                float cardWidth = EffectiveCardWidth;
                float offset = (i - _currentIndex) * (cardWidth + EffectiveSpacing);
                float targetX = centerX - cardWidth / 2f + offset;
                float targetY = baseY + _baseY.GetValueOrDefault(slide, 0f);
                float distance = Mathf.Abs(i - _currentIndex);
                float scale = distance < 1f ? 1f : EffectiveInactiveScale;
                float alpha = distance < 1f ? 1f : EffectiveInactiveAlpha;

                if (instant)
                {
                    slide.GlobalPosition = new Vector2(targetX, targetY);
                    slide.Scale = new Vector2(scale, scale);
                    slide.Modulate = new Color(1, 1, 1, alpha);
                }
                else
                {
                    var tween = slide.CreateTween().SetParallel(true);
                    _activeTweens.Add(tween);
                    float duration = EffectiveTransitionDuration;
                    tween.TweenProperty(slide, "global_position", new Vector2(targetX, targetY), duration).SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(slide, "scale", new Vector2(scale, scale), duration);
                    tween.TweenProperty(slide, "modulate:a", alpha, duration);
                }
            }

            EmitSignal(SignalName.SlideChanged, _currentIndex);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();
            // Undo the TopLevel we set on the (pre-existing) slide children, or a component removed
            // alone leaves them frozen TopLevel at their last global position.
            foreach (var slide in _slides)
                if (GodotObject.IsInstanceValid(slide)) slide.TopLevel = false;
            _slides.Clear();
        }
    }
}
