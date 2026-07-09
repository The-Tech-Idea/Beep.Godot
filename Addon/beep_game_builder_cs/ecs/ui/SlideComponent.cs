using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Slide in/out component. Attach to any Control for animated show/hide.
    /// Blind — works for panels, menus, notifications, drawers, overlays.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SlideComponent : EntityComponent
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

        private Control? _control;
        private Vector2 _visiblePos;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent<Control>();
            if (_control == null) return;
            _visiblePos = _control.Position;

            if (HiddenOnStart)
            {
                _control.Position = GetHiddenPosition();
                if (Mode != SlideMode.SlideOnly) _control.Modulate = new Color(1, 1, 1, 0);
                _control.Visible = false;
            }
        }

        public void SlideIn()
        {
            if (_control == null || !IsActive) return;
            _control.Visible = true;
            _tween?.Kill();
            _tween = _control.CreateTween().SetParallel(true);
            _tween.TweenProperty(_control, "position", _visiblePos, Duration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            if (Mode != SlideMode.SlideOnly)
                _tween.TweenProperty(_control, "modulate:a", 1f, Duration * 0.7f);
            _tween.Finished += () => EmitSignal(SignalName.SlidIn);
        }

        public void SlideOut()
        {
            if (_control == null || !IsActive) return;
            _tween?.Kill();
            _tween = _control.CreateTween().SetParallel(true);
            _tween.TweenProperty(_control, "position", GetHiddenPosition(), Duration)
                .SetEase(Tween.EaseType.In);
            if (Mode != SlideMode.SlideOnly)
                _tween.TweenProperty(_control, "modulate:a", 0f, Duration * 0.5f).SetDelay(Duration * 0.3f);
            _tween.Finished += () => { _control.Visible = false; EmitSignal(SignalName.SlidOut); };
        }

        private Vector2 GetHiddenPosition() => Direction switch
        {
            SlideDirection.Left => _visiblePos + new Vector2(-Distance, 0),
            SlideDirection.Right => _visiblePos + new Vector2(Distance, 0),
            SlideDirection.Up => _visiblePos + new Vector2(0, -Distance),
            SlideDirection.Down => _visiblePos + new Vector2(0, Distance),
            _ => _visiblePos
        };
    }
}
