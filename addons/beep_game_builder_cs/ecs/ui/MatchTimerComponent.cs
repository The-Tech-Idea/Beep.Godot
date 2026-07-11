using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Countdown match timer. Creates/uses a child Label showing mm:ss.
    /// Start() begins the countdown; emits TimeUp when it reaches zero.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MatchTimerComponent : UIComponent
    {
        [Export] public double DurationSeconds { get; set; } = 120.0;
        [Export] public string Prefix { get; set; } = "";
        [Export] public int FontSize { get; set; } = 18;
        [Export] public bool AutoStart { get; set; } = false;

        [Signal] public delegate void TimeUpEventHandler();
        [Signal] public delegate void TickEventHandler(double remaining);

        private Label? _label;
        private double _remaining;
        private bool _running;

        public override void _Ready()
        {
            base._Ready();
            EnsureLabel();
            _remaining = DurationSeconds;
            UpdateText();
            if (AutoStart && !Engine.IsEditorHint()) Start();
        }

        private void EnsureLabel()
        {
            if (GetParent() is Label existing) { _label = existing; return; }
            _label = new Label { Name = "TimerLabel" };
            _label.AddThemeFontSizeOverride("font_size", FontSize);
            GetParent().AddChild(_label);
            _label.Owner = GetParent();
        }

        public void Start()
        {
            _remaining = DurationSeconds;
            _running = true;
        }

        public void Stop() => _running = false;
        public void Reset() { _remaining = DurationSeconds; _running = false; UpdateText(); }

        public override void _Process(double delta)
        {
            if (!_running || !IsActive) return;
            _remaining -= delta;
            if (_remaining <= 0)
            {
                _remaining = 0;
                _running = false;
                UpdateText();
                EmitSignal(SignalName.TimeUp);
                return;
            }
            EmitSignal(SignalName.Tick, _remaining);
            UpdateText();
        }

        private void UpdateText()
        {
            if (_label == null) return;
            int total = (int)Mathf.Ceil(_remaining);
            int m = total / 60;
            int s = total % 60;
            _label.Text = $"{Prefix}{m:D2}:{s:D2}";
        }
    }
}
