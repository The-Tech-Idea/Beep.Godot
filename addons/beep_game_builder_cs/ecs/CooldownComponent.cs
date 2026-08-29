using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Generic ability cooldown timer. Stack as many as you need on an entity —
    /// one per ability. Trigger() starts the cooldown; IsReady tells you when
    /// the ability is available again. Emits CooldownReady when it expires.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CooldownComponent : GameplayComponent
    {
        [Export] public float CooldownDuration { get; set; } = 1f;
        [Export] public bool StartOnReady { get; set; } = false;

        [Signal] public delegate void CooldownReadyEventHandler();
        [Signal] public delegate void CooldownProgressEventHandler(float pct);

        private float _timer;
        public bool IsReady => !float.IsFinite(_timer) || _timer <= 0f;
        public float Remaining => Mathf.Max(0f, FiniteOr(_timer, 0f));
        public float Progress => EffectiveDuration > 0f ? Mathf.Clamp(1f - (Remaining / EffectiveDuration), 0f, 1f) : 1f;
        public float EffectiveDuration => NonNegative(CooldownDuration);

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() && StartOnReady) Trigger();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;
            if (!float.IsFinite(_timer))
            {
                _timer = 0f;
                EmitSignal(SignalName.CooldownReady);
                return;
            }
            if (_timer <= 0f) return;
            _timer = Mathf.Max(0f, _timer - DeltaSeconds(delta));
            EmitSignal(SignalName.CooldownProgress, Progress);
            if (_timer <= 0f)
            {
                _timer = 0f;
                EmitSignal(SignalName.CooldownReady);
            }
        }

        /// <summary>Start the cooldown timer.</summary>
        public void Trigger()
        {
            if (!IsActive) return;
            _timer = EffectiveDuration;
            if (_timer <= 0f)
                EmitSignal(SignalName.CooldownReady);
        }

        /// <summary>Force the cooldown to end immediately.</summary>
        public void Reset()
        {
            bool wasCoolingDown = _timer > 0;
            _timer = 0;
            // Announce readiness so listeners gated on CooldownReady learn the ability is available
            // after a forced reset (they otherwise only heard it via the natural _Process expiry).
            if (wasCoolingDown) EmitSignal(SignalName.CooldownReady);
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static float FiniteOr(float value, float fallback) =>
            float.IsFinite(value) ? value : fallback;
    }
}
