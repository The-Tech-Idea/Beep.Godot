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
        private bool _createdLabel;   // true only when we new'd the label (vs adopting a parent Label)
        private double _remaining;
        private bool _running;

        public override void _Ready()
        {
            base._Ready();
            _remaining = DurationSeconds;
            // Runtime only: EnsureLabel injects a Label into the parent. (The existing
            // guard below only covered AutoStart, not the label injection.)
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(EnsureLabel));
            UpdateText();
            if (AutoStart) Start();
        }

        private void EnsureLabel()
        {
            var parent = GetParent();
            if (parent is Label existing) { _label = existing; UpdateText(); return; }
            if (parent == null)
            {
                GD.PushWarning($"[{Name}] MatchTimerComponent has no parent to host its timer label.");
                return;
            }
            _createdLabel = true;
            _label = new Label { Name = "TimerLabel" };
            _label.AddThemeFontSizeOverride("font_size", FontSize);
            parent.AddChild(_label);
            if (parent.IsInsideTree()) _label.Owner = parent.Owner;
            // Render the initial time now — _Ready's UpdateText ran before this deferred build, so the
            // label showed blank until the first tick.
            UpdateText();
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

        public override void _ExitTree()
        {
            base._ExitTree();
            // Free the injected TimerLabel only if we created it (parent-hosted); if we adopted a
            // parent Label, leave it.
            if (_createdLabel && _label != null && GodotObject.IsInstanceValid(_label)) _label.QueueFree();
            _label = null;
        }
    }
}
