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
            yield return new StatModifier { Stat = "armor", Op = StatOp.Add, Amount = Defense, Duration = -1f };
        }
    }
}
