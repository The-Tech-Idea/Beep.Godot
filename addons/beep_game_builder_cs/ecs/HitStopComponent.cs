using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Freeze-frame hit stop for impact feel. Listens to a sibling
    /// HealthComponent's Damaged signal and briefly sets Engine.TimeScale to 0
    /// so the world pauses for a few frames, making heavy hits feel weighty.
    ///
    /// Attach as a child of the same node that has a HealthComponent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HitStopComponent : WorldComponent
    {
        [Export] public float FreezeDuration { get; set; } = 0.05f;
        [Export] public float MinDamageThreshold { get; set; } = 10f;

        [Signal] public delegate void HitStopTriggeredEventHandler();

        private bool _frozen;
        private float _freezeTimer;
        private double _priorTimeScale = 1.0;   // restore THIS, not a literal 1, so slow-mo/pause survives

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(WireToHealth));
        }

        private void WireToHealth()
        {
            var health = GetSiblingComponent<HealthComponent>();
            if (health != null)
                health.Damaged += OnDamaged;
        }

        private void OnDamaged(float amount, float newHealth)
        {
            if (!IsActive || amount < MinDamageThreshold) return;
            if (_frozen) return;
            _frozen = true;
            _freezeTimer = FreezeDuration;
            _priorTimeScale = Engine.TimeScale;   // capture what was running (slow-mo, etc.)
            Engine.TimeScale = 0f;
            EmitSignal(SignalName.HitStopTriggered);
        }

        public override void _Process(double delta)
        {
            if (!_frozen) return;
            // delta is 0 when time scale is 0, so use a fixed real-time decrement.
            _freezeTimer -= 0.016f;
            if (_freezeTimer <= 0)
            {
                _frozen = false;
                Engine.TimeScale = _priorTimeScale;   // restore what was running, not a literal 1
            }
        }

        public override void _ExitTree()
        {
            // If this entity is freed mid-freeze (e.g. it took the hit and died), _Process stops and
            // TimeScale would be stuck at 0, freezing the whole game. Restore it here.
            if (_frozen) { Engine.TimeScale = _priorTimeScale; _frozen = false; }

            var health = GetSiblingComponent<HealthComponent>();
            if (health != null)
                health.Damaged -= OnDamaged;
        }
    }
}
