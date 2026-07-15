using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Health component for any entity that can take damage or be destroyed.
    /// Blind — doesn't know if parent is player, enemy, or destructible object.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HealthComponent : GameplayComponent
    {
        [Export] public float MaxHealth { get; set; } = 100f;
        [Export] public float CurrentHealth { get; set; } = 100f;
        [Export] public float Armor { get; set; } = 0f;
        [Export] public float MaxArmor { get; set; } = 100f;
        [Export] public bool TemperatureAffectsHealth { get; set; } = true;
        [Export] public bool HungerAffectsHealth { get; set; } = true;

        [Signal] public delegate void DamagedEventHandler(float amount, float newHealth);
        [Signal] public delegate void HealedEventHandler(float amount, float newHealth);
        [Signal] public delegate void DiedEventHandler();
        [Signal] public delegate void HealthChangedEventHandler(float current, float max);

        public bool IsDead => CurrentHealth <= 0f;
        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

        private TemperatureComponent? _temperature;
        private HungerStaminaComponent? _hunger;
        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            _temperature = GetSiblingComponent<TemperatureComponent>();
            _hunger = GetSiblingComponent<HungerStaminaComponent>();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || IsDead) return;

            float dt = (float)delta;
            float passiveDamage = 0f;

            if (TemperatureAffectsHealth && _temperature != null)
            {
                passiveDamage += _temperature.GetTemperatureState() switch
                {
                    TemperatureComponent.TemperatureState.Frozen => 5f * dt,
                    TemperatureComponent.TemperatureState.HeatStroke => 3f * dt,
                    _ => 0f
                };
            }

            if (HungerAffectsHealth && _hunger != null && _hunger.IsHungry)
            {
                passiveDamage += 2f * dt;
            }

            if (passiveDamage > 0)
                TakeDamage(passiveDamage);
        }

        public void TakeDamage(float amount)
        {
            if (!IsActive || IsDead) return;

            float armorReduction = Mathf.Clamp(Armor, 0f, MaxArmor) * 0.01f;
            float actual = Mathf.Max(0.1f, amount * (1f - armorReduction));

            var statusMods = _statusEffects?.GetModifiers("damage_reduction");
            if (statusMods != null && statusMods.TryGetValue("armor_multiplier", out var armorMult))
                actual *= armorMult;

            CurrentHealth = Mathf.Max(0, CurrentHealth - actual);
            EmitSignal(SignalName.Damaged, actual, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0) EmitSignal(SignalName.Died);
        }

        public void Heal(float amount)
        {
            if (!IsActive || IsDead) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            EmitSignal(SignalName.Healed, amount, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }
    }
}
