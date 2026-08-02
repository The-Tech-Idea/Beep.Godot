using Godot;
using Beep.ECS.UI.Kit;

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
        // Scale of the theme's body font, not a fixed size. The themes run 14-24, so a
        // literal renders a genre's larger type out of a control built for 14.
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float FontScale { get; set; } = 1.18f;
        private int FontSize => UiSurface.FontSize(this, FontScale);
        [Export] public bool AutoStart { get; set; } = false;

        [Signal] public delegate void TimeUpEventHandler();
        [Signal] public delegate void TickEventHandler(double remaining);

        private Godot.Control? _label;
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
            if (parent is Label existing) { _label = existing; StyleLabel(); UpdateText(); return; }
            if (parent == null)
            {
                GD.PushWarning($"[{Name}] MatchTimerComponent has no parent to host its timer label.");
                return;
            }
            _createdLabel = true;
            _label = new KitHudText
            {
                Name = "TimerLabel",
                Role = UiSurface.TextRole.Value,
                ShowPlate = true,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            StyleLabel();
            parent.AddChild(_label);
            if (parent.IsInsideTree()) _label.Owner = parent.Owner;
            // Render the initial time now — _Ready's UpdateText ran before this deferred build, so the
            // label showed blank until the first tick.
            UpdateText();
        }

        private void StyleLabel()
        {
            if (_label == null) return;
            _label.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            if (_label is Label label)
            {
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                label.ClipText = true;
                label.AddThemeFontSizeOverride("font_size", FontSize);
                label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.78f));
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 2);
            }
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
            string text = $"{Prefix}{m:D2}:{s:D2}";
            if (_label is KitHudText hud) hud.Text = text;
            else if (_label is Label label) label.Text = text;
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
