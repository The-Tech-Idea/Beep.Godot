using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// XP and leveling system for RPGs, roguelikes, and progression-based games.
    /// Attach to the player entity alongside HealthComponent / GameFlowComponent.
    /// Call AddXp() to grant experience; when XP exceeds the threshold, the entity
    /// levels up and awards stat points to spend.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LevelingComponent : GameplayComponent
    {
        [Export] public int Level { get; set; } = 1;
        [Export] public int MaxLevel { get; set; } = 99;
        [Export] public float BaseXp { get; set; } = 100f;
        [Export] public float XpGrowthMultiplier { get; set; } = 1.5f;
        [Export] public int StatPointsPerLevel { get; set; } = 3;
        public int EffectiveMaxLevel => Mathf.Max(1, MaxLevel);
        public int EffectiveLevel => Mathf.Clamp(Level, 1, EffectiveMaxLevel);
        public float EffectiveBaseXp => PositiveFinite(BaseXp, 100f);
        public float EffectiveXpGrowthMultiplier => PositiveFinite(XpGrowthMultiplier, 1f);
        public int EffectiveStatPointsPerLevel => Mathf.Max(0, StatPointsPerLevel);

        [Signal] public delegate void XpChangedEventHandler(float current, float needed);
        [Signal] public delegate void LevelUpEventHandler(int newLevel, int statPoints);
        [Signal] public delegate void MaxLevelReachedEventHandler();

        public float CurrentXp { get; private set; }
        public float XpNeeded
        {
            get
            {
                float needed = EffectiveBaseXp * Mathf.Pow(EffectiveXpGrowthMultiplier, EffectiveLevel - 1);
                return float.IsFinite(needed) && needed > 0f ? needed : EffectiveBaseXp;
            }
        }
        public int StatPoints { get; set; }
        public bool IsMaxLevel => EffectiveLevel >= EffectiveMaxLevel;

        private StatsComponent? _stats;

        public override void _Ready()
        {
            base._Ready();
            NormalizeProgressionState();
            _stats = GetSiblingComponent<StatsComponent>();
        }

        /// <summary>Grant XP. Automatically levels up if threshold exceeded.</summary>
        public void AddXp(float amount)
        {
            NormalizeProgressionState();
            if (!IsActive || IsMaxLevel || !float.IsFinite(amount) || amount <= 0f) return;
            CurrentXp += amount;
            EmitSignal(SignalName.XpChanged, CurrentXp, XpNeeded);

            while (CurrentXp >= XpNeeded && !IsMaxLevel)
            {
                CurrentXp -= XpNeeded;
                Level++;
                StatPoints += EffectiveStatPointsPerLevel;
                EmitSignal(SignalName.LevelUp, Level, StatPoints);
                EmitSignal(SignalName.XpChanged, CurrentXp, XpNeeded);
            }

            if (IsMaxLevel)
                EmitSignal(SignalName.MaxLevelReached);
        }

        /// <summary>Spend stat points to PERMANENTLY raise a stat — the destination StatPoints never
        /// had. Adds a permanent <see cref="StatModifier"/> (<c>{stat, Add, points × amountPerPoint}</c>,
        /// Duration &lt; 0) to the entity's <see cref="StatsComponent"/>. Returns false if there aren't
        /// enough points, or if there is no StatsComponent to raise (warns rather than eating the
        /// points silently).</summary>
        public bool SpendPoints(StringName stat, int points, float amountPerPoint = 1f)
        {
            if (points <= 0 || StatPoints < points || !float.IsFinite(amountPerPoint)) return false;
            if (_stats == null)
            {
                GD.PushWarning(
                    $"[{Name}] SpendPoints: no sibling StatsComponent — the points have nowhere to go. " +
                    $"Add a StatsComponent to raise '{stat}'.");
                return false;
            }
            StatPoints -= points;
            _stats.AddModifier(new StatModifier
            {
                Stat = stat, Op = StatOp.Add, Amount = points * amountPerPoint, Duration = -1f, Source = this
            });
            return true;
        }

        private void NormalizeProgressionState()
        {
            Level = EffectiveLevel;
            MaxLevel = EffectiveMaxLevel;
            if (!float.IsFinite(CurrentXp) || CurrentXp < 0f) CurrentXp = 0f;
            StatPoints = Mathf.Max(0, StatPoints);
        }

        private static float PositiveFinite(float value, float fallback)
            => float.IsFinite(value) && value > 0f ? value : fallback;
    }
}
