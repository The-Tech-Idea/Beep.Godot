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

        [Signal] public delegate void DamagedEventHandler(float amount, float newHealth);
        [Signal] public delegate void HealedEventHandler(float amount, float newHealth);
        [Signal] public delegate void DiedEventHandler();
        [Signal] public delegate void HealthChangedEventHandler(float current, float max);

        public bool IsDead => CurrentHealth <= 0f;
        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

        public void TakeDamage(float amount)
        {
            if (!IsActive || IsDead) return;
            float actual = Mathf.Max(0, amount - Armor);
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
