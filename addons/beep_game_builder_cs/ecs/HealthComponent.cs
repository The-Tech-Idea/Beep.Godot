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
        [Signal] public delegate void RevivedEventHandler(float newHealth);
        [Signal] public delegate void HealthChangedEventHandler(float current, float max);

        public bool IsDead => CurrentHealth <= 0f;
        public float EffectiveMaxHealth => float.IsFinite(MaxHealth) ? Mathf.Max(1f, MaxHealth) : 1f;
        public float HealthPercent => float.IsFinite(CurrentHealth) ? Mathf.Clamp(CurrentHealth / EffectiveMaxHealth, 0f, 1f) : 0f;

        private TemperatureComponent? _temperature;
        private HungerStaminaComponent? _hunger;
        private StatsComponent? _stats;
        private ResistanceComponent? _resistance;
        private AggroComponent? _aggro;
        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            NormalizeHealth();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            _temperature = GetSiblingComponent<TemperatureComponent>();
            _hunger = GetSiblingComponent<HungerStaminaComponent>();
            _stats = GetSiblingComponent<StatsComponent>();
            _resistance = GetSiblingComponent<ResistanceComponent>();
            _aggro = GetSiblingComponent<AggroComponent>();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || IsDead) return;

            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
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
            NormalizeHealth();
            if (!float.IsFinite(damage.Amount) || damage.Amount <= 0f) return;

            // Invincibility (i-frames) blocks all incoming damage, including True — this is the
            // canonical "invincible" channel that DashComponent's i-frames and game code apply
            // via a sibling StatusEffectComponent. Nothing honored it before, so dash i-frames
            // (GrantIFrames, on by default) advertised protection and delivered none.
            if (_statusEffects != null && _statusEffects.HasEffect("invincible")) return;

            float actual;
            if (damage.Type == DamageType.True)
            {
                // True damage bypasses resistance AND armor by contract (see DamageType).
                actual = damage.Amount;
            }
            else
            {
                float amount = damage.Amount;
                if (_resistance != null) amount = _resistance.ApplyResistance(amount, damage.Type);
                if (amount <= 0f) return; // resisted to nothing (immunity)

                // Armor comes from the entity's "armor" stat when it has one (so equipment/buffs raise
                // it), otherwise this component's Armor export. The old status "damage_reduction" lookup
                // is gone — reduction is a modifier on the armor stat now, one channel not two.
                float armorValue = _stats?.GetValue("armor", Armor) ?? Armor;
                if (!float.IsFinite(armorValue)) armorValue = 0f;
                float maxArmor = float.IsFinite(MaxArmor) ? Mathf.Max(0f, MaxArmor) : 100f;
                float armorReduction = Mathf.Clamp(armorValue, 0f, maxArmor) * 0.01f;
                actual = Mathf.Max(0.1f, amount * (1f - armorReduction));
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - actual);
            EmitSignal(SignalName.Damaged, actual, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

            // Aggro whoever hit us (threat = damage dealt). This is the producer AggroComponent's
            // threat table never had, so an enemy now reacts to being attacked.
            if (damage.Source != null) _aggro?.AddThreat(damage.Source, actual);

            if (CurrentHealth <= 0)
            {
                // Award the killer XP before announcing death. The fatal hit's Source is the killer;
                // grant its LevelingComponent (if it has one) this entity's XpReward.
                if (float.IsFinite(XpReward) && XpReward > 0f && damage.Source != null)
                    EntityComponent.FindComponent<LevelingComponent>(damage.Source, false)?.AddXp(XpReward);
                EmitSignal(SignalName.Died);
            }
        }

        public void Heal(float amount)
        {
            if (!IsActive || IsDead) return;
            NormalizeHealth();
            float before = CurrentHealth;
            CurrentHealth = Mathf.Min(EffectiveMaxHealth, CurrentHealth + (float.IsFinite(amount) ? Mathf.Max(0f, amount) : 0f));
            float applied = CurrentHealth - before;
            if (applied <= 0f) return;
            EmitSignal(SignalName.Healed, applied, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }

        /// <summary>Bring the entity back to life at <paramref name="toHealth"/> (default full).
        /// <see cref="Heal"/> deliberately no-ops while IsDead so damage/heal can't resurrect by
        /// accident — so respawn/revive flows (e.g. RespawnComponent) call this instead. Idempotent
        /// on a living entity (just tops up to the target).</summary>
        public void Revive(float toHealth = -1f)
        {
            if (!IsActive) return;
            // Only a NEGATIVE value is the "full" sentinel — an explicit 0 (or any >= 0) is honored,
            // clamped up to a minimum of 1 so the entity is guaranteed alive (not instantly re-dead).
            NormalizeHealth();
            float target = float.IsFinite(toHealth) && toHealth >= 0f ? Mathf.Min(toHealth, EffectiveMaxHealth) : EffectiveMaxHealth;
            CurrentHealth = Mathf.Max(1f, target);
            EmitSignal(SignalName.Revived, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }

        // ── ISaveable Implementation (auto-called by GameStateManagerComponent) ──
        public void Save(GameBuilder.GameStateData state)
        {
            NormalizeHealth();
            state.Combat.Health = CurrentHealth;
            state.Combat.MaxHealth = EffectiveMaxHealth;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            MaxHealth = float.IsFinite(state.Combat.MaxHealth) ? Mathf.Max(1f, state.Combat.MaxHealth) : 1f;
            CurrentHealth = float.IsFinite(state.Combat.Health) ? Mathf.Clamp(state.Combat.Health, 0f, EffectiveMaxHealth) : EffectiveMaxHealth;
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }

        private void NormalizeHealth()
        {
            MaxHealth = EffectiveMaxHealth;
            CurrentHealth = float.IsFinite(CurrentHealth) ? Mathf.Clamp(CurrentHealth, 0f, MaxHealth) : MaxHealth;
        }
    }
}
