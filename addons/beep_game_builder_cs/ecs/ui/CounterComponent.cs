using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Animated number counter. Attach to any Label.
    /// Blind — counts currency, scores, stats, timers, health with smooth animation.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CounterComponent : UIComponent
    {
        [Export] public float StartValue { get; set; } = 0f;
        [Export] public float Duration { get; set; } = 1f;
        [Export] public string Prefix { get; set; } = "";
        [Export] public string Suffix { get; set; } = "";
        [Export] public string Format { get; set; } = "N0";
        [Export] public bool PlayOnReady { get; set; } = false;

        [Signal] public delegate void CountReachedEventHandler(float value);
        [Signal] public delegate void CountStartedEventHandler();

        private Label? _label;
        private float _fromValue;
        private float _toValue;
        private float _elapsed;
        private bool _counting;

        public override void _Ready()
        {
            base._Ready();
            _label = GetParent() as Label;
            if (PlayOnReady) CountTo(StartValue);
        }

        public void CountTo(float target)
        {
            if (_label == null || !IsActive) return;
            _fromValue = float.TryParse(_label.Text.Replace(Prefix, "").Replace(Suffix, ""), out float v) ? v : 0;
            _toValue = target;
            _elapsed = 0;
            _counting = true;
            EmitSignal(SignalName.CountStarted);
        }

        public void SetImmediate(float value)
        {
            _counting = false;
            _toValue = value;
            if (_label != null) _label.Text = $"{Prefix}{value.ToString(Format)}{Suffix}";
        }

        public override void _Process(double delta)
        {
            if (!_counting || _label == null || !IsActive) return;
            _elapsed += (float)delta;
            float t = Mathf.Clamp(_elapsed / Duration, 0f, 1f);
            t = t * t * (3f - 2f * t); // Smoothstep
            float current = Mathf.Lerp(_fromValue, _toValue, t);
            _label.Text = $"{Prefix}{current.ToString(Format)}{Suffix}";

            if (_elapsed >= Duration)
            {
                _counting = false;
                _label.Text = $"{Prefix}{_toValue.ToString(Format)}{Suffix}";
                EmitSignal(SignalName.CountReached, _toValue);
            }
        }
    }
}
