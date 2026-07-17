using Godot;

namespace Beep.ECS
{
    /// <summary>How a <see cref="StatModifier"/> combines with the base value.</summary>
    public enum StatOp { Add, Multiply }

    /// <summary>
    /// One contribution to a <see cref="Stat"/> — a sword's +10 damage, a rage buff's ×1.5, a
    /// permanent level-up bonus. Authorable as a `.tres` (a GameWeapon can carry the modifiers it
    /// contributes on equip), or built at runtime by a status effect.
    ///
    /// <see cref="Duration"/> is what unifies equipment, timed buffs, and permanent upgrades into
    /// one list with one recalculation: it is in the genre's CLOCK UNITS (Phase 7) — seconds in a
    /// real-time genre, turns in a turn-based one, the same `.tres`, because the genre owns the
    /// clock. <b>&lt; 0 = permanent</b> (never ticks); 0 is a legitimate instantaneous duration.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StatModifier : Resource
    {
        /// <summary>Which stat this modifies — "damage", "armor", "move_speed", …</summary>
        [Export] public StringName Stat { get; set; } = "";

        [Export] public StatOp Op { get; set; } = StatOp.Add;
        [Export] public float Amount { get; set; } = 0f;

        /// <summary>Lifetime in clock units. &lt; 0 = permanent (equipment, upgrades). A timed buff
        /// is e.g. 5; a turn-based buff of 3 lasts three EndTurns. Ticked in exactly one place —
        /// <see cref="StatsComponent"/> — off the genre's clock.</summary>
        [Export] public float Duration { get; set; } = -1f;

        /// <summary>WHO added this modifier — the GameEquipment, the status-effect marker — so it
        /// can be withdrawn by IDENTITY on unequip/expiry, never by value-matching (removing "any
        /// +10" would strip an unrelated buff). Set at runtime by whoever adds it; not authored.</summary>
        public GodotObject? Source { get; set; }
    }
}
