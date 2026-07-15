using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Screen shake component. Attach to a Camera2D.
    /// Blind — any Camera2D, works for impacts, explosions, footsteps.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ScreenShakeComponent : ControllerComponent
    {
        [Export] public float DefaultIntensity { get; set; } = 5f;
        [Export] public float DefaultDuration { get; set; } = 0.3f;

        [Signal] public delegate void ShakeStartedEventHandler(float intensity, float duration);
        [Signal] public delegate void ShakeFinishedEventHandler();

        private Camera2D? _cam;
        private float _trauma;
        private float _traumaDecay = 1f;

        public override void _Ready()
        {
            base._Ready();
            _cam = GetParent() as Camera2D;
            if (!IsInGroup("screen_shake")) AddToGroup("screen_shake");
        }

        public void Shake(float intensity = -1, float duration = -1)
        {
            if (_cam == null || !IsActive) return;
            _trauma = Mathf.Clamp(_trauma + (intensity > 0 ? intensity : DefaultIntensity), 0f, 20f);
            _traumaDecay = duration > 0 ? duration : DefaultDuration;
            EmitSignal(SignalName.ShakeStarted, _trauma, _traumaDecay);
        }

        public override void _Process(double delta)
        {
            if (_cam == null || _trauma <= 0) return;

            _trauma = Mathf.Max(0, _trauma - (float)delta / _traumaDecay * 5f);
            float shake = _trauma * _trauma;
            _cam.Offset = new Vector2(
                (float)(GD.Randf() * 2 - 1) * shake,
                (float)(GD.Randf() * 2 - 1) * shake);

            if (_trauma <= 0)
            {
                _cam.Offset = Vector2.Zero;
                EmitSignal(SignalName.ShakeFinished);
            }
        }
    }
}
