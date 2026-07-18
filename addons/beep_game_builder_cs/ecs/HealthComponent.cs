using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Health component for any entity that can take damage or be destroyed.
    /// Blind — doesn't know if parent is player, enemy, or destructible object.
    /// Implements ISaveable for state persistence (save/load).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HealthComponent : GameplayComponent, ISaveable
    {
        [Export] public float MaxHealth { get; set; } = 100f;
        [Export] public float CurrentHealth { get; set; } = 100f;
        [Export] public float Armor { get; set; } = 0f;
        [Export] public float MaxArmor { get; set; } = 100f;

        /// <summary>XP this entity is worth when killed. 0 = grants none. On death the killer —
        /// the <see cref="GameDamage.Source"/> of the fatal hit — has its LevelingComponent (if any)
        /// awarded this. This is the caller AddXp never had, so leveling can actually happen.</summary>
        [Export] public float XpReward { get; set; } = 0f;
        [Export] public bool TemperatureAffectsHealth { get; set; } = true;
        [Export] public bool HungerAffectsHealth { get; set; } = true;

        /// <summary>Include this entity's health in saves. Tick it on the player only.
        ///
        /// Off by default because this component is blind (see the class note): GameStateData
        /// keeps one Combat slot, so if every enemy's health saved too, the last one scanned
        /// would win and loading would set the player and every enemy to that value.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = false;

        [Signal] public delegate void DamagedEventHandler(float amount, float newHealth);
        [Signal] public delegate void HealedEventHandler(float amount, float newHealth);
        [Signal] public delegate void DiedEventHandler();
        [Signal] public delegate void HealthChangedEventHandler(float current, float max);

        public bool IsDead => CurrentHealth <= 0f;
        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

        private TemperatureComponent? _temperature;
        private HungerStaminaComponent? _hunger;
        private StatsComponent? _stats;
        private ResistanceComponent? _resistance;

        public override void _Ready()
        {
            base._Ready();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            _temperature = GetSiblingComponent<TemperatureComponent>();
            _hunger = GetSiblingComponent<HungerStaminaComponent>();
            _stats = GetSiblingComponent<StatsComponent>();
            _resistance = GetSiblingComponent<ResistanceComponent>();
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
                TakeDamage(new GameDamage(passiveDamage, DamageType.Physical));
        }

        /// <summary>Apply a damage packet. A sibling <see cref="ResistanceComponent"/> (if present)
        /// scales the incoming amount by its per-type multiplier first — 0 = immune, 2 = weak —
        /// so an immune target (multiplier 0) takes nothing. Armor + status reduction apply after.
        ///
        /// There is no float overload: every caller states the <see cref="DamageType"/> via a
        /// <see cref="GameDamage"/>. The old 1-arg convenience is what made every hit Physical.</summary>
        public void TakeDamage(GameDamage damage)
        {
            if (!IsActive || IsDead) return;

            float amount = damage.Amount;
            if (_resistance != null) amount = _resistance.ApplyResistance(amount, damage.Type);
            if (amount <= 0f) return; // resisted to nothing (immunity)

            // Armor comes from the entity's "armor" stat when it has one (so equipment/buffs raise
            // it), otherwise this component's Armor export. The old status "damage_reduction" lookup
            // is gone — reduction is a modifier on the armor stat now, one channel not two.
            float armorValue = _stats?.GetValue("armor", Armor) ?? Armor;
            float armorReduction = Mathf.Clamp(armorValue, 0f, MaxArmor) * 0.01f;
            float actual = Mathf.Max(0.1f, amount * (1f - armorReduction));

            CurrentHealth = Mathf.Max(0, CurrentHealth - actual);
            EmitSignal(SignalName.Damaged, actual, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0)
            {
                // Award the killer XP before announcing death. The fatal hit's Source is the killer;
                // grant its LevelingComponent (if it has one) this entity's XpReward.
                if (XpReward > 0f && damage.Source != null)
                    EntityComponent.FindComponent<LevelingComponent>(damage.Source, false)?.AddXp(XpReward);
                EmitSignal(SignalName.Died);
            }
        }

        public void Heal(float amount)
        {
            if (!IsActive || IsDead) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            EmitSignal(SignalName.Healed, amount, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }

        // ── ISaveable Implementation (auto-called by GameStateManagerComponent) ──
        public void Save(GameBuilder.GameStateData state)
        {
            state.Combat.Health = CurrentHealth;
            state.Combat.MaxHealth = MaxHealth;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            CurrentHealth = state.Combat.Health;
            MaxHealth = state.Combat.MaxHealth;
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }
    }
}
