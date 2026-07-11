using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// UI shake component. Attach to any Control to shake it on demand.
    /// Blind — works for error feedback, attention grab, impact response.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ShakeComponent : EntityComponent
    {
        [Export] public float Intensity { get; set; } = 10f;
        [Export] public float Duration { get; set; } = 0.3f;
        [Export] public int Vibrato { get; set; } = 20;

        [Signal] public delegate void ShakeStartedEventHandler();
        [Signal] public delegate void ShakeFinishedEventHandler();

        private Control? _control;
        private Vector2 _originalPos;
        private float _elapsed;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent<Control>();
        }

        public void Shake(float intensity = -1, float duration = -1)
        {
            if (_control == null || !IsActive) return;
            _originalPos = _control.Position;
            _elapsed = 0;
            Intensity = intensity > 0 ? intensity : Intensity;
            Duration = duration > 0 ? duration : Duration;
            EmitSignal(SignalName.ShakeStarted);
        }

        public override void _Process(double delta)
        {
            if (_control == null || _elapsed >= Duration) return;
            _elapsed += (float)delta;
            float decay = 1f - (_elapsed / Duration);
            float x = (float)(GD.Randf() * 2 - 1) * Intensity * decay;
            float y = (float)(GD.Randf() * 2 - 1) * Intensity * decay;
            _control.Position = _originalPos + new Vector2(x, y);

            if (_elapsed >= Duration)
            {
                _control.Position = _originalPos;
                EmitSignal(SignalName.ShakeFinished);
            }
        }
    }
}
