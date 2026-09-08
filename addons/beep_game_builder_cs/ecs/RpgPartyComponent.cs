using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// RPG growth tuning, mana and quests. Sibling HealthComponent and LevelingComponent
    /// own combat health and progression, including their persistence.
    ///
    /// The parts that make it a character rather than three numbers:
    ///  - mana regenerates on a timer, health does NOT (healing is an action, not a wait —
    ///    passive health regen removes the reason potions and rest exist)
    ///  - levelling scales the maxima and fully restores, which is what makes a level-up feel
    ///    like a reward rather than a bigger empty bar
    ///  - the XP curve is superlinear, so each level costs more than the last
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RpgPartyComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared per-component, not inherited — implementing
        /// ISaveable is not enough on its own; the walk only finds members of the group.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public int BaseMaxHealth { get; set; } = 80;
        [Export] public int BaseMaxMana { get; set; } = 40;
        /// <summary>Added to each maximum per level gained.</summary>
        [Export] public int HealthPerLevel { get; set; } = 14;
        [Export] public int ManaPerLevel { get; set; } = 8;

        [Export] public float ManaRegenPerSecond { get; set; } = 1.6f;

        /// <summary>Below this fraction a bar is "low" — the threshold the HUD colours on.</summary>
        [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float LowThreshold { get; set; } = 0.3f;

        // ── State ─────────────────────────────────────────────────────────────────────────
        public int Level => _leveling?.EffectiveLevel ?? 1;
        public float Xp => _leveling?.CurrentXp ?? 0f;
        public float Health => _health?.CurrentHealth ?? 0f;
        public int Mana { get; private set; }

        public float MaxHealth => _health?.EffectiveMaxHealth ?? 1f;
        private int ConfiguredMaxHealth => EffectiveMax(BaseMaxHealth, HealthPerLevel, Level);
        public int MaxMana => EffectiveMax(BaseMaxMana, ManaPerLevel, Level);

        /// <summary>XP needed to reach the next level from the start of this one.</summary>
        public float XpToNextLevel => _leveling?.XpNeeded ?? 1f;

        public float HealthFraction => _health?.HealthPercent ?? 0f;
        public float ManaFraction => Mathf.Clamp((float)Mathf.Clamp(Mana, 0, MaxMana) / MaxMana, 0f, 1f);
        public float XpFraction => Mathf.Clamp((float)Mathf.Max(0, Xp) / XpToNextLevel, 0f, 1f);
        public float EffectiveManaRegenPerSecond => NonNegativeFinite(ManaRegenPerSecond);
        public float EffectiveLowThreshold => float.IsFinite(LowThreshold) ? Mathf.Clamp(LowThreshold, 0.01f, 1f) : 0.3f;

        public bool IsDead => Health <= 0;

        /// <summary>The quest currently tracked in the HUD, or null when none is active.</summary>
        public QuestState? ActiveQuest { get; private set; }

        public sealed class QuestState
        {
            public string Id = "";
            public string Title = "";
            public int Progress;
            public int Goal = 1;
            public bool IsComplete => Progress >= Goal;
            public override string ToString() =>
                Goal > 1 ? $"{Title}  {Progress}/{Goal}" : Title;
        }

        private readonly Dictionary<string, QuestState> _quests = new();

        [Signal] public delegate void StatsChangedEventHandler();
        [Signal] public delegate void LeveledUpEventHandler(int level);
        [Signal] public delegate void QuestChangedEventHandler();
        [Signal] public delegate void DiedEventHandler();

        private float _regenAccum;
        private HealthComponent? _health;
        private LevelingComponent? _leveling;
        private ulong _boundHealthId;
        private bool _initialized;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _leveling = GetSiblingComponent<LevelingComponent>();
            if (_leveling is null)
                GD.PushError($"[{Name}] RPG character stats require a sibling LevelingComponent; no independent progression state is created.");
            else
            {
                _leveling.LevelUp += OnLevelUp;
                _leveling.XpChanged += OnXpChanged;
            }
            _health = GetSiblingComponent<HealthComponent>();
            if (_health is null)
                GD.PushError($"[{Name}] RPG character stats require a sibling HealthComponent; no independent health pool is created.");
            else
            {
                if (_boundHealthId != _health.GetInstanceId())
                {
                    _health.SetMaximumHealth(ConfiguredMaxHealth, true);
                    _boundHealthId = _health.GetInstanceId();
                }
                _health.HealthChanged += OnHealthChanged;
                _health.Died += OnHealthDied;
            }
            if (!_initialized) { Mana = MaxMana; _initialized = true; }
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        public override void _ExitTree()
        {
            if (GodotObject.IsInstanceValid(_health))
            {
                _health!.HealthChanged -= OnHealthChanged;
                _health.Died -= OnHealthDied;
            }
            _health = null;
            if (GodotObject.IsInstanceValid(_leveling))
            {
                _leveling!.LevelUp -= OnLevelUp;
                _leveling.XpChanged -= OnXpChanged;
            }
            _leveling = null;
            RequestReady();
            base._ExitTree();
        }

        private void OnHealthChanged(float current, float maximum) => EmitSignal(SignalName.StatsChanged);
        private void OnHealthDied() => EmitSignal(SignalName.Died);

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || IsDead || Mana >= MaxMana) return;

            // Accumulated rather than rounded per frame: at 1.6/s a per-frame RoundToInt is 0
            // every frame, so mana would never regenerate at all.
            _regenAccum += EffectiveManaRegenPerSecond * DeltaSeconds(delta);
            if (!float.IsFinite(_regenAccum) || _regenAccum < 0f)
                _regenAccum = 0f;
            if (_regenAccum < 1f) return;
            int gained = Mathf.FloorToInt(_regenAccum);
            _regenAccum -= gained;
            Mana = Mathf.Min(MaxMana, Mana + gained);
            EmitSignal(SignalName.StatsChanged);
        }

        // ── Character API ─────────────────────────────────────────────────────────────────

        /// <summary>Apply damage. Returns true if this killed the character.</summary>
        public bool Damage(GameDamage damage)
        {
            if (!IsActive || IsDead || _health is null) return false;
            _health.TakeDamage(damage);
            return IsDead;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            _health?.Heal(amount);
        }

        /// <summary>Spend mana. Returns false and changes nothing when short — a caller must be
        /// able to reject the cast rather than discover a negative pool afterwards.</summary>
        public bool SpendMana(int amount)
        {
            if (amount <= 0) return true;
            if (Mana < amount) return false;
            Mana -= amount;
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;
            Mana = Mathf.Min(MaxMana, Mana + amount);
            EmitSignal(SignalName.StatsChanged);
        }

        /// <summary>Award XP, levelling as many times as it covers.</summary>
        public void AwardXp(float amount)
        {
            if (!IsActive || IsDead) return;
            _leveling?.AddXp(amount);
        }

        private void OnXpChanged(float current, float needed) => EmitSignal(SignalName.StatsChanged);

        private void OnLevelUp(int level, int points)
        {
            _health?.SetMaximumHealth(ConfiguredMaxHealth);
            _health?.Heal(MaxHealth);
            Mana = MaxMana;
            EmitSignal(SignalName.LeveledUp, level);
            EmitSignal(SignalName.StatsChanged);
        }

        public void Revive(float healthFraction = 1f)
        {
            float fraction = float.IsFinite(healthFraction) ? Mathf.Clamp(healthFraction, 0.01f, 1f) : 1f;
            _health?.Revive(Mathf.Max(1, Mathf.RoundToInt(MaxHealth * fraction)));
            Mana = MaxMana;
            EmitSignal(SignalName.StatsChanged);
        }

        // ── Quests ────────────────────────────────────────────────────────────────────────

        /// <summary>Start (or re-track) a quest and make it the one the HUD shows.</summary>
        public void StartQuest(string id, string title, int goal = 1)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_quests.TryGetValue(id, out var q))
                _quests[id] = q = new QuestState { Id = id, Title = title, Goal = Mathf.Max(1, goal) };
            q.Title = title;
            q.Goal = Mathf.Max(1, goal);
            q.Progress = Mathf.Clamp(q.Progress, 0, q.Goal);
            ActiveQuest = q;
            EmitSignal(SignalName.QuestChanged);
        }

        /// <summary>Advance a quest. Completing the tracked one leaves it displayed as complete
        /// rather than blanking the HUD — a quest line that vanishes the instant it completes
        /// gives the player nothing to read.</summary>
        public void AdvanceQuest(string id, int by = 1)
        {
            if (!_quests.TryGetValue(id, out var q) || q.IsComplete) return;
            q.Progress = Mathf.Min(q.Goal, q.Progress + Mathf.Max(1, by));
            if (ActiveQuest == q) EmitSignal(SignalName.QuestChanged);
        }

        public bool IsQuestComplete(string id)
            => _quests.TryGetValue(id, out var q) && q.IsComplete;

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KMana = "rpg.mana";
        private const string KQuests = "rpg.quests";
        private const string KActive = "rpg.quest_active";

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KMana] = Mana;

            var q = new Godot.Collections.Dictionary();
            foreach (var (id, s) in _quests)
                q[id] = new Godot.Collections.Array { s.Title, s.Progress, s.Goal };
            state.GameData[KQuests] = q;
            state.GameData[KActive] = ActiveQuest?.Id ?? "";
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            // HealthComponent restores both HP and its saved capacity, including runtime modifiers.
            if (d.TryGetValue(KMana, out var m)) Mana = Mathf.Clamp(VariantToInt(m, MaxMana), 0, MaxMana);

            _quests.Clear();
            ActiveQuest = null;
            if (d.TryGetValue(KQuests, out var qs) && qs.VariantType == Variant.Type.Dictionary)
                foreach (var kv in qs.AsGodotDictionary())
                {
                    if (kv.Value.VariantType != Variant.Type.Array)
                        continue;

                    var a = kv.Value.AsGodotArray();
                    if (a.Count < 3) continue;
                    string id = kv.Key.AsString();
                    _quests[id] = new QuestState
                    {
                        Id = id,
                        Title = a[0].AsString(),
                        Progress = Mathf.Max(0, VariantToInt(a[1], 0)),
                        Goal = Mathf.Max(1, VariantToInt(a[2], 1)),
                    };
                    _quests[id].Progress = Mathf.Clamp(_quests[id].Progress, 0, _quests[id].Goal);
                }
            if (d.TryGetValue(KActive, out var act) && _quests.TryGetValue(act.AsString(), out var cur))
                ActiveQuest = cur;

            EmitSignal(SignalName.StatsChanged);
            EmitSignal(SignalName.QuestChanged);
        }

        private static int EffectiveMax(int baseValue, int perLevel, int level)
        {
            long total = (long)Mathf.Max(1, baseValue) + (long)Mathf.Max(0, perLevel) * (Mathf.Max(1, level) - 1L);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        private static int VariantToInt(Variant value, int fallback)
        {
            if (value.VariantType == Variant.Type.Int)
                return value.AsInt32();
            if (value.VariantType == Variant.Type.Float)
            {
                double number = value.AsDouble();
                if (!double.IsFinite(number))
                    return fallback;
                if (number >= int.MaxValue)
                    return int.MaxValue;
                if (number <= int.MinValue)
                    return int.MinValue;
                return Mathf.RoundToInt((float)number);
            }
            if (value.VariantType == Variant.Type.String
                && int.TryParse(value.AsString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) && value > 0f ? value : 0f;
    }
}
