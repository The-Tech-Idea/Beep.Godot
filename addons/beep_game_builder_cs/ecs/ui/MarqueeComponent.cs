using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Scrolling text marquee/ticker. Attach to any Label.
    /// Blind — works for news tickers, song titles, status bars, long names.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MarqueeComponent : UIComponent
    {
        [Export] public float Speed { get; set; } = 80f;
        [Export] public float PauseAtStart { get; set; } = 2f;
        [Export] public float PauseAtEnd { get; set; } = 2f;
        [Export] public bool Bounce { get; set; } = false;
        [Export] public bool AutoStart { get; set; } = true;

        private Label? _label;
        private float _scrollPos;
        private float _pauseTimer;
        private bool _pausing = true;
        private bool _forward = true;
        private bool _running;

        /// <summary>Whether the ticker is currently scrolling.</summary>
        public bool IsRunning => _running;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _label = GetParent() as Label;
            if (_label != null)
            {
                _label.ClipContents = true;
                _pauseTimer = PauseAtStart;
                // Scroll on the offset_transform layer so a container parent can't overwrite the
                // Label's position each layout pass (which stopped the marquee dead).
                _label.OffsetTransformEnabled = true;
            }
            else GD.PushWarning($"[{Name}] MarqueeComponent needs a Label parent to scroll; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the Label.");
            _running = AutoStart;
        }

        /// <summary>Begin scrolling. Use this when AutoStart is false (drive the ticker from code).</summary>
        public void Start() => _running = true;

        /// <summary>Stop scrolling. The ticker holds its current offset until Start() is called.</summary>
        public void Stop() => _running = false;

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (_label == null || !IsActive || !_running) return;

            float textWidth = _label.GetThemeDefaultFont()?.GetStringSize(_label.Text, fontSize: _label.GetThemeFontSize("font_size")).X ?? _label.Size.X;

            if (_pausing)
            {
                _pauseTimer -= (float)delta;
                if (_pauseTimer <= 0) _pausing = false;
                return;
            }

            _scrollPos += Speed * (float)delta * (_forward ? 1 : -1);
            _label.OffsetTransformPosition = new Vector2(-_scrollPos, 0);

            float maxScroll = Mathf.Max(0, textWidth - _label.Size.X);
            if (_scrollPos >= maxScroll && _forward)
            {
                if (Bounce) { _forward = false; _pauseTimer = 0.5f; _pausing = true; }
                else { _scrollPos = 0; _pauseTimer = PauseAtEnd; _pausing = true; }
            }
            else if (_scrollPos <= 0 && !_forward)
            {
                _forward = true; _pauseTimer = PauseAtStart; _pausing = true;
            }
        }
    }
}
