using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Body/head armor. Earns its class with <see cref="Defense"/> and per-type resistances —
    /// fields a plain GameEquipment does not carry. `helmet_iron.tres`, `plate_steel.tres` are
    /// `.tres` of this class.
    ///
    /// The resistance fields mirror <see cref="ResistanceComponent"/>'s per-type multipliers
    /// (1 = no effect, 0.5 = halves that type, 0 = immune), so an armor's values line up with the
    /// component that consumes them. Phase 3b turns these into Stat contributions so two pieces
    /// can both apply and cleanly withdraw; for now they are the authored surface.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameArmor : GameEquipment
    {
        [Export] public float Defense { get; set; } = 0f;
        public float EffectiveDefense => NonNegativeFinite(Defense);

        [ExportGroup("Resistances")]
        [Export] public float Physical { get; set; } = 1f;
        [Export] public float Fire { get; set; } = 1f;
        [Export] public float Ice { get; set; } = 1f;
        [Export] public float Poison { get; set; } = 1f;
        [Export] public float Holy { get; set; } = 1f;
        [Export] public float Dark { get; set; } = 1f;
        [Export] public float Lightning { get; set; } = 1f;
        [Export] public float True { get; set; } = 1f;

        /// <summary>Adds this armor's <see cref="Defense"/> to the wielder's "armor" stat while
        /// equipped. (Per-type resistances become "resist_*" stats in a later 3b step; for now they
        /// are authored data ResistanceComponent will read.)</summary>
        public override System.Collections.Generic.IEnumerable<StatModifier> GetIntrinsicModifiers()
        {
            yield return new StatModifier { Stat = "armor", Op = StatOp.Add, Amount = EffectiveDefense, Duration = -1f };
        }

        /// <summary>This armor's multiplier for a damage type (1 = no effect, 0.5 = halves it, 0 =
        /// immune). Read by a wearer's ResistanceComponent and combined multiplicatively.</summary>
        public float ResistFor(DamageType type) => type switch
        {
            DamageType.Physical => Multiplier(Physical),
            DamageType.Fire => Multiplier(Fire),
            DamageType.Ice => Multiplier(Ice),
            DamageType.Poison => Multiplier(Poison),
            DamageType.Holy => Multiplier(Holy),
            DamageType.Dark => Multiplier(Dark),
            DamageType.Lightning => Multiplier(Lightning),
            DamageType.True => Multiplier(True),
            _ => 1f
        };

        private static float Multiplier(float value)
            => float.IsFinite(value) ? Mathf.Clamp(value, 0f, 10f) : 1f;

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }
}
