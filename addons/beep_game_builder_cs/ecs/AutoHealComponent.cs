using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Auto-recovery component. Blind — auto-heals HP, regens mana, recovers stamina.
    /// Finds a sibling HealthComponent and heals over time after a delay since last damage.
    /// Integrates with TemperatureComponent (modulates healing rate) and StatusEffectComponent
    /// (blocks healing if negative effects active like poison/curse).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AutoHealComponent : GameplayComponent
    {
        [Export] public float HealPerSecond { get; set; } = 2f;
        [Export] public float HealDelay { get; set; } = 5f;
        [Export] public float MaxHealPerSecond { get; set; } = 100f;
        [Export] public bool HealOnlyOutOfCombat { get; set; } = true;
        [Export] public string StatName { get; set; } = "health"; // "health", "mana", "stamina"
        [Export] public bool BlockHealingWhenPoisoned { get; set; } = true;
        [Export] public bool BlockHealingWhenCursed { get; set; } = true;
        [Export] public bool UseTemperatureModifier { get; set; } = true;

        [Signal] public delegate void HealTickEventHandler(float amount, float newValue);

        private HealthComponent? _health;
        private TemperatureComponent? _temperature;
        private StatusEffectComponent? _statusEffects;
        private float _timeSinceLastDamage;

        public override void _Ready()
        {
            base._Ready();
            _health = GetSiblingComponent<HealthComponent>();
            _temperature = GetSiblingComponent<TemperatureComponent>();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();

            if (_health != null)
            {
                _health.Damaged += (_, _) => _timeSinceLastDamage = 0;
                _health.Died += () => IsActive = false;
            }
        }

        public override void _Process(double delta)
        {
            if (_health == null || !IsActive || _health.IsDead) return;

            // Check if healing is blocked by negative status effects
            if (BlockHealingWhenPoisoned && (_statusEffects?.HasEffect("poisoned") ?? false)) return;
            if (BlockHealingWhenCursed && (_statusEffects?.HasEffect("cursed") ?? false)) return;

            _timeSinceLastDamage += (float)delta;

            if (_timeSinceLastDamage >= HealDelay)
            {
                // Apply temperature modifier if available
                float tempModifier = GetTemperatureModifier();
                if (tempModifier <= 0) return;  // Don't heal in extreme conditions

                float healRate = Mathf.Min(HealPerSecond * tempModifier, MaxHealPerSecond);
                float amount = healRate * (float)delta;
                _health.Heal(amount);
                EmitSignal(SignalName.HealTick, amount, _health.CurrentHealth);
            }
        }

        /// <summary>Get healing rate multiplier based on temperature state.</summary>
        private float GetTemperatureModifier()
        {
            if (!UseTemperatureModifier || _temperature == null) return 1.0f;

            return _temperature.GetTemperatureState() switch
            {
                TemperatureComponent.TemperatureState.Frozen => 0.0f,      // No healing when freezing
                TemperatureComponent.TemperatureState.Cold => 0.5f,         // 50% slower
                TemperatureComponent.TemperatureState.Overheating => 0.7f,  // 30% slower
                TemperatureComponent.TemperatureState.HeatStroke => 0.0f,   // No healing in heat stroke
                _ => 1.0f
            };
        }
    }
}
