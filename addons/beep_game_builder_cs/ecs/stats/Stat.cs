using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A single numeric attribute — "damage", "armor", "move_speed" — as a base value plus a list
    /// of <see cref="StatModifier"/>s. <see cref="Value"/> is recomputed on change and cached, so
    /// consumers read it per-frame without cost, and the recompute is idempotent (running it twice
    /// yields the same number — the bug that bit ApplyTheme).
    ///
    /// Order of combination: all <see cref="StatOp.Add"/> modifiers apply to the base first, then
    /// all <see cref="StatOp.Multiply"/> ones — so a sword's <c>+10</c> and a rage <c>×1.5</c> read
    /// as <c>(base + 10) × 1.5</c>, the conventional and least-surprising order.
    ///
    /// Only <see cref="BaseValue"/> is authored; the modifier list is runtime state (equipment and
    /// effects add to it). Withdrawal is by <see cref="StatModifier.Source"/> identity.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class Stat : Resource
    {
        [Export] public StringName Id { get; set; } = "";
        [Export] public float BaseValue { get; set; } = 0f;

        /// <summary>Emitted whenever the computed <see cref="Value"/> may have changed (a modifier
        /// added, removed, or expired). Carries the new value.</summary>
        [Signal] public delegate void ChangedEventHandler(float value);

        private readonly List<StatModifier> _modifiers = new();
        private float _cached;
        private bool _dirty = true;

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        /// <summary>The current value: base plus adds, then multiplies. Cached; recomputed only
        /// after a modifier change.</summary>
        public float Value
        {
            get { if (_dirty) Recompute(); return _cached; }
        }

        public void AddModifier(StatModifier mod)
        {
            if (mod == null) return;
            _modifiers.Add(mod);
            Invalidate();
        }

        public bool RemoveModifier(StatModifier mod)
        {
            bool removed = _modifiers.Remove(mod);
            if (removed) Invalidate();
            return removed;
        }

        /// <summary>Remove every modifier a given source added, by reference identity. Returns the
        /// count removed.</summary>
        public int RemoveBySource(GodotObject source)
        {
            int n = _modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (n > 0) Invalidate();
            return n;
        }

        /// <summary>Advance timed modifiers by <paramref name="amount"/> clock units and drop the
        /// expired ones. Permanent modifiers (Duration &lt; 0) are skipped. Driven from exactly one
        /// place — <see cref="StatsComponent"/> — off the genre's clock.</summary>
        public void TickDurations(float amount)
        {
            bool changed = false;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                var m = _modifiers[i];
                if (m.Duration < 0f) continue;      // permanent
                m.Duration -= amount;
                if (m.Duration <= 0f) { _modifiers.RemoveAt(i); changed = true; }
            }
            if (changed) Invalidate();
        }

        private void Invalidate()
        {
            _dirty = true;
            EmitSignal(SignalName.Changed, Value);  // Value forces the recompute exactly once
        }

        private void Recompute()
        {
            float add = 0f, mul = 1f;
            foreach (var m in _modifiers)
            {
                if (m.Op == StatOp.Add) add += m.Amount;
                else mul *= m.Amount;
            }
            _cached = (BaseValue + add) * mul;
            _dirty = false;
        }
    }
}
