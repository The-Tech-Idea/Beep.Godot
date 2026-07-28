using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The RPG genre's character simulation: health, mana, experience and the active quest.
    ///
    /// <c>RpgHudComponent</c> registered Health, Mana and Quest as <c>Placeholder(...)</c>, so
    /// those three readouts showed whatever text was typed into the scene and never moved. This
    /// is the third genre to get a real one, after <see cref="CityEconomyComponent"/> and
    /// <see cref="SurvivalVitalsComponent"/>, and follows their shape deliberately.
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

        /// <summary>XP required for level 2. Later levels scale by <see cref="XpCurve"/>.</summary>
        [Export] public int BaseXpToLevel { get; set; } = 100;
        /// <summary>Superlinear so levelling slows down; 1.0 would make every level equal.</summary>
        [Export(PropertyHint.Range, "1.0,2.0,0.05")] public float XpCurve { get; set; } = 1.35f;

        [Export] public float ManaRegenPerSecond { get; set; } = 1.6f;

        /// <summary>Below this fraction a bar is "low" — the threshold the HUD colours on.</summary>
        [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float LowThreshold { get; set; } = 0.3f;

        // ── State ─────────────────────────────────────────────────────────────────────────
        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int Health { get; private set; }
        public int Mana { get; private set; }

        public int MaxHealth => BaseMaxHealth + HealthPerLevel * (Level - 1);
        public int MaxMana => BaseMaxMana + ManaPerLevel * (Level - 1);

        /// <summary>XP needed to reach the next level from the start of this one.</summary>
        public int XpToNextLevel => Mathf.Max(1, Mathf.RoundToInt(BaseXpToLevel * Mathf.Pow(Level, XpCurve)));

        public float HealthFraction => MaxHealth <= 0 ? 0f : (float)Health / MaxHealth;
        public float ManaFraction => MaxMana <= 0 ? 0f : (float)Mana / MaxMana;
        public float XpFraction => Mathf.Clamp((float)Xp / XpToNextLevel, 0f, 1f);

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

        public override void _Ready()
        {
            base._Ready();
            Health = MaxHealth;
            Mana = MaxMana;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || IsDead || Mana >= MaxMana) return;

            // Accumulated rather than rounded per frame: at 1.6/s a per-frame RoundToInt is 0
            // every frame, so mana would never regenerate at all.
            _regenAccum += ManaRegenPerSecond * (float)delta;
            if (_regenAccum < 1f) return;
            int gained = Mathf.FloorToInt(_regenAccum);
            _regenAccum -= gained;
            Mana = Mathf.Min(MaxMana, Mana + gained);
            EmitSignal(SignalName.StatsChanged);
        }

        // ── Character API ─────────────────────────────────────────────────────────────────

        /// <summary>Apply damage. Returns true if this killed the character.</summary>
        public bool Damage(int amount)
        {
            if (amount <= 0 || IsDead) return false;
            Health = Mathf.Max(0, Health - amount);
            EmitSignal(SignalName.StatsChanged);
            if (IsDead) EmitSignal(SignalName.Died);
            return IsDead;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            EmitSignal(SignalName.StatsChanged);
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
        public void AwardXp(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Xp += amount;
            // A loop, not an if: a boss award can span several levels at once, and handling one
            // level per call would silently bank the rest.
            while (Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                Level++;
                // Restoring on level-up is what makes it a reward. Scaling the maximum without
                // refilling hands the player a bigger empty bar.
                Health = MaxHealth;
                Mana = MaxMana;
                EmitSignal(SignalName.LeveledUp, Level);
            }
            EmitSignal(SignalName.StatsChanged);
        }

        public void Revive(float healthFraction = 1f)
        {
            Health = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * Mathf.Clamp(healthFraction, 0.01f, 1f)));
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
        private const string KLevel = "rpg.level";
        private const string KXp = "rpg.xp";
        private const string KHealth = "rpg.health";
        private const string KMana = "rpg.mana";
        private const string KQuests = "rpg.quests";
        private const string KActive = "rpg.quest_active";

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KLevel] = Level;
            state.GameData[KXp] = Xp;
            state.GameData[KHealth] = Health;
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
            if (d.TryGetValue(KLevel, out var l)) Level = Mathf.Max(1, l.AsInt32());
            if (d.TryGetValue(KXp, out var x)) Xp = Mathf.Max(0, x.AsInt32());
            // Clamped AFTER Level is restored, because the maxima are derived from it — loading
            // health before level would clamp against level 1's maximum and cap a level-20 save.
            if (d.TryGetValue(KHealth, out var h)) Health = Mathf.Clamp(h.AsInt32(), 0, MaxHealth);
            if (d.TryGetValue(KMana, out var m)) Mana = Mathf.Clamp(m.AsInt32(), 0, MaxMana);

            _quests.Clear();
            ActiveQuest = null;
            if (d.TryGetValue(KQuests, out var qs))
                foreach (var kv in qs.AsGodotDictionary())
                {
                    var a = kv.Value.AsGodotArray();
                    if (a.Count < 3) continue;
                    string id = kv.Key.AsString();
                    _quests[id] = new QuestState
                    {
                        Id = id,
                        Title = a[0].AsString(),
                        Progress = a[1].AsInt32(),
                        Goal = Mathf.Max(1, a[2].AsInt32()),
                    };
                }
            if (d.TryGetValue(KActive, out var act) && _quests.TryGetValue(act.AsString(), out var cur))
                ActiveQuest = cur;

            EmitSignal(SignalName.StatsChanged);
            EmitSignal(SignalName.QuestChanged);
        }
    }
}
