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
        private ulong _freezeEndMsec;
        private double _priorTimeScale = 1.0;   // restore THIS, not a literal 1, so slow-mo/pause survives
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            Callable.From(WireToHealth).CallDeferred();
        }

        private void WireToHealth()
        {
            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null)
                _health.Damaged += OnDamaged;
            else
                // Entirely signal-driven — with no Health sibling it produces zero hit-stop and used
                // to do so in silence. Add it beside a HealthComponent on the same entity.
                GD.PushWarning($"[{Name}] HitStopComponent found no sibling HealthComponent — no hit-stop will ever trigger. Add it beside a HealthComponent.");
        }

        private void OnDamaged(float amount, float newHealth)
        {
            if (!IsActive || amount < MinDamageThreshold) return;
            if (_frozen) return;
            _frozen = true;
            // Wall-clock deadline (Time.GetTicksMsec is unscaled), so the freeze lasts the same real
            // duration at any framerate — the old fixed -0.016f/frame decrement made it ~2× longer at
            // 30 fps and half as long at 120 fps, and delta itself is 0 while TimeScale is 0.
            _freezeEndMsec = Time.GetTicksMsec() + (ulong)(FreezeDuration * 1000f);
            _priorTimeScale = Engine.TimeScale;   // capture what was running (slow-mo, etc.)
            Engine.TimeScale = 0f;
            EmitSignal(SignalName.HitStopTriggered);
        }

        public override void _Process(double delta)
        {
            if (!_frozen) return;
            if (Time.GetTicksMsec() >= _freezeEndMsec)
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

            if (_health != null)
                _health.Damaged -= OnDamaged;
        }
    }
}
