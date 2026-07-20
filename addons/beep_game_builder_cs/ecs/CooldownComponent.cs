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
        public bool IsReady => _timer <= 0;
        public float Remaining => Mathf.Max(0, _timer);
        public float Progress => CooldownDuration > 0 ? 1f - (_timer / CooldownDuration) : 1f;

        public override void _Ready()
        {
            base._Ready();
            if (StartOnReady) Trigger();
        }

        public override void _Process(double delta)
        {
            if (_timer <= 0) return;
            _timer -= (float)delta;
            EmitSignal(SignalName.CooldownProgress, Progress);
            if (_timer <= 0)
            {
                _timer = 0;
                EmitSignal(SignalName.CooldownReady);
            }
        }

        /// <summary>Start the cooldown timer.</summary>
        public void Trigger()
        {
            if (!IsActive) return;
            _timer = CooldownDuration;
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
    }
}
