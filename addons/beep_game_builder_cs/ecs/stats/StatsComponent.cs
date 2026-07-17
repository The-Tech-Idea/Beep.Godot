using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The entity's stat block — the ONE place stats live, so whoever computes damage or defense
    /// reads them and nobody owns a private copy. This is deliberate: the shooter has no
    /// AttackComponent, so a bonus bolted onto AttackComponent would leave that genre inert; a Stat
    /// owned by the entity is read by both damage paths with nothing to fork.
    ///
    /// It is also the single ticker for modifier durations — <see cref="StatModifier.Duration"/> is
    /// decremented here off the genre's clock (per frame in a real-time genre, once per turn in a
    /// turn-based one, detected by whether a <see cref="TurnManager"/> autoload is in the tree).
    /// Producers (EquipmentComponent, StatusEffectComponent) only <see cref="AddModifier"/> /
    /// <see cref="RemoveBySource"/>; they never tick.
    ///
    /// Blind — no parent-type requirement. Equipment/stats are data on any node.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StatsComponent : GameplayComponent
    {
        /// <summary>Authored starting stats (base values). A template ships an entity's damage,
        /// armor, speed, etc. from the inspector; modifiers accrue at runtime.</summary>
        [Export] public Stat[] Stats { get; set; } = System.Array.Empty<Stat>();

        private readonly Dictionary<StringName, Stat> _byId = new();
        private bool _turnBased;

        public override void _Ready()
        {
            base._Ready();
            _byId.Clear();
            foreach (var s in Stats)
                if (s != null && !s.Id.IsEmpty) _byId[s.Id] = s;

            if (Engine.IsEditorHint()) return;
            _turnBased = TurnManager.Instance != null;
            if (_turnBased) TurnManager.Instance!.TurnEnded += OnTurnEnded;
        }

        /// <summary>The stat resource for an id, or null if the entity has none.</summary>
        public Stat? GetStat(StringName id) => _byId.TryGetValue(id, out var s) ? s : null;

        /// <summary>The computed value of a stat, or <paramref name="fallback"/> if the entity has
        /// no such stat (an entity with no modifiers behaves exactly as its base values).</summary>
        public float GetValue(StringName id, float fallback = 0f)
            => _byId.TryGetValue(id, out var s) ? s.Value : fallback;

        /// <summary>Add a modifier, routing it to the stat it targets (creating that stat at base 0
        /// if the entity did not declare one, so a buff on an undeclared stat still works).</summary>
        public void AddModifier(StatModifier mod)
        {
            if (mod == null) return;
            if (mod.Stat.IsEmpty)
            {
                GD.PushWarning($"[{Name}] AddModifier: modifier has an empty Stat id — it targets nothing and was ignored.");
                return;
            }
            GetOrCreate(mod.Stat).AddModifier(mod);
        }

        /// <summary>Withdraw every modifier a source added, across all stats, by identity. Used on
        /// unequip and on effect expiry — the removal that must never match by value.</summary>
        public void RemoveBySource(GodotObject source)
        {
            foreach (var s in _byId.Values) s.RemoveBySource(source);
        }

        private Stat GetOrCreate(StringName id)
        {
            if (_byId.TryGetValue(id, out var s)) return s;
            var created = new Stat { Id = id, BaseValue = 0f };
            _byId[id] = created;
            return created;
        }

        public override void _Process(double delta)
        {
            // Real-time only; a turn-based genre ticks from TurnEnded, once per turn.
            if (Engine.IsEditorHint() || _turnBased) return;
            TickDurations((float)delta);
        }

        private void OnTurnEnded(int turn) => TickDurations(1f);

        private void TickDurations(float amount)
        {
            foreach (var s in _byId.Values) s.TickDurations(amount);
        }

        public override void _ExitTree()
        {
            if (_turnBased && TurnManager.Instance != null)
                TurnManager.Instance.TurnEnded -= OnTurnEnded;
            base._ExitTree();
        }
    }
}
