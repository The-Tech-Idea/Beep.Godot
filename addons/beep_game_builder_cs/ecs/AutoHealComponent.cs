using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Auto-recovery component. Blind — auto-heals HP, regens mana, recovers stamina.
    /// Finds a sibling HealthComponent and heals over time after a delay since last damage.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AutoHealComponent : EntityComponent
    {
        [Export] public float HealPerSecond { get; set; } = 2f;
        [Export] public float HealDelay { get; set; } = 5f;
        [Export] public bool HealOnlyOutOfCombat { get; set; } = true;
        [Export] public string StatName { get; set; } = "health"; // "health", "mana", "stamina"

        [Signal] public delegate void HealTickEventHandler(float amount, float newValue);

        private HealthComponent? _health;
        private float _timeSinceLastDamage;

        public override void _Ready()
        {
            base._Ready();
            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null)
            {
                _health.Damaged += (_, _) => _timeSinceLastDamage = 0;
                _health.Died += () => IsActive = false;
            }
        }

        public override void _Process(double delta)
        {
            if (_health == null || !IsActive || _health.IsDead) return;
            _timeSinceLastDamage += (float)delta;

            if (_timeSinceLastDamage >= HealDelay)
            {
                float amount = HealPerSecond * (float)delta;
                _health.Heal(amount);
                EmitSignal(SignalName.HealTick, amount, _health.CurrentHealth);
            }
        }
    }
}
