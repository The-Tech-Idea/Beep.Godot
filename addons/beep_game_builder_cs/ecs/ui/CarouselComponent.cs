using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Horizontal card carousel. Attach to a Container with child Controls as slides.
    /// Blind — works for image galleries, featured items, tutorials, card browsers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CarouselComponent : EntityComponent
    {
        [Export] public float CardWidth { get; set; } = 280f;
        [Export] public float Spacing { get; set; } = 16f;
        [Export] public float TransitionDuration { get; set; } = 0.4f;
        [Export] public float InactiveScale { get; set; } = 0.85f;
        [Export] public float InactiveAlpha { get; set; } = 0.5f;
        [Export] public bool Loop { get; set; } = true;
        [Export] public bool AutoPlay { get; set; } = false;
        [Export] public float AutoPlayInterval { get; set; } = 3f;

        [Signal] public delegate void SlideChangedEventHandler(int index);

        private Container? _container;
        private int _currentIndex;
        private float _autoTimer;
        private int _slideCount;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent<Container>();
            if (_container == null) return;
            _slideCount = _container.GetChildCount();
            if (_slideCount == 0) return;
            GoToSlide(0, true);
        }

        public override void _Process(double delta)
        {
            if (!AutoPlay || !IsActive) return;
            _autoTimer += (float)delta;
            if (_autoTimer >= AutoPlayInterval) { _autoTimer = 0; Next(); }
        }

        public void Next() => GoToSlide((_currentIndex + 1) % _slideCount);
        public void Previous() => GoToSlide((_currentIndex - 1 + _slideCount) % _slideCount);

        public void GoToSlide(int index, bool instant = false)
        {
            if (_container == null || !IsActive || _slideCount == 0) return;
            if (!Loop && (index < 0 || index >= _slideCount)) return;
            if (!Loop) index = Mathf.Clamp(index, 0, _slideCount - 1);

            _currentIndex = ((index % _slideCount) + _slideCount) % _slideCount;

            float centerX = _container.Size.X / 2f;
            for (int i = 0; i < _slideCount; i++)
            {
                if (_container.GetChild(i) is not Control slide) continue;

                float offset = (i - _currentIndex) * (CardWidth + Spacing);
                float targetX = centerX - CardWidth / 2f + offset;
                float distance = Mathf.Abs(i - _currentIndex);
                float scale = distance < 1f ? 1f : InactiveScale;
                float alpha = distance < 1f ? 1f : InactiveAlpha;

                if (instant)
                {
                    slide.Position = new Vector2(targetX, slide.Position.Y);
                    slide.Scale = new Vector2(scale, scale);
                    slide.Modulate = new Color(1, 1, 1, alpha);
                }
                else
                {
                    var tween = slide.CreateTween().SetParallel(true);
                    tween.TweenProperty(slide, "position:x", targetX, TransitionDuration).SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(slide, "scale", new Vector2(scale, scale), TransitionDuration);
                    tween.TweenProperty(slide, "modulate:a", alpha, TransitionDuration);
                }
            }

            EmitSignal(SignalName.SlideChanged, _currentIndex);
        }
    }
}
