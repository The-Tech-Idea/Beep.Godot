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
        // 40 (not 5): shake is (trauma/MaxTrauma)^2 * 20px, so a trauma of 5 on the 0..100 scale is
        // ~0.05px — invisible. 40 gives a moderate, visible default shake.
        [Export] public float DefaultIntensity { get; set; } = 40f;
        [Export] public float DefaultDuration { get; set; } = 0.3f;
        [Export] public float MaxTrauma { get; set; } = 100f;

        [Signal] public delegate void ShakeStartedEventHandler(float intensity, float duration);
        [Signal] public delegate void ShakeFinishedEventHandler();

        private Camera2D? _cam;
        private float _trauma;
        private float _traumaDuration = 1f;
        private float _decayPerSec = 1f;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _cam = GetParent() as Camera2D;
            if (_cam == null)
                // Shake() writes _cam.Offset — a non-Camera2D parent makes every Shake() a silent
                // no-op. Parent this under the Camera2D it should shake.
                GD.PushWarning($"[{Name}] ScreenShakeComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Camera2D — Shake() will do nothing. Parent it under a Camera2D.");
            if (!IsInGroup("screen_shake")) AddToGroup("screen_shake");
        }

        public void Shake(float intensity = -1, float duration = -1)
        {
            if (_cam == null || !IsActive) return;
            float amount = intensity > 0 ? intensity : DefaultIntensity;
            _trauma = Mathf.Clamp(_trauma + amount, 0f, MaxTrauma);
            _traumaDuration = duration > 0 ? duration : DefaultDuration;
            // Decay so the shake lasts _traumaDuration REGARDLESS of magnitude. The old fixed
            // delta/duration made time-to-zero = trauma * duration, so Shake(100, 0.3) ran ~30s.
            _decayPerSec = _traumaDuration > 0 ? _trauma / _traumaDuration : _trauma;
            EmitSignal(SignalName.ShakeStarted, _trauma, _traumaDuration);
        }

        public override void _Process(double delta)
        {
            if (_cam == null || _trauma <= 0) return;

            _trauma = Mathf.Max(0, _trauma - _decayPerSec * (float)delta);
            float trauma01 = _trauma / MaxTrauma;
            float shake = trauma01 * trauma01;
            // GD.Randf() already returns float — no cast (per CLAUDE.md's API note).
            _cam.Offset = new Vector2(
                (GD.Randf() * 2f - 1f) * shake * 20f,
                (GD.Randf() * 2f - 1f) * shake * 20f);

            if (_trauma <= 0)
            {
                _cam.Offset = Vector2.Zero;
                EmitSignal(SignalName.ShakeFinished);
            }
        }
    }
}
