using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// An off-hand shield. Earns its class over GameArmor with <see cref="BlockChance"/> — a
    /// real field, an active-block probability that armor has no notion of. `buckler.tres`,
    /// `tower_shield.tres` are `.tres` of this class.
    ///
    /// Resistance fields mirror <see cref="ResistanceComponent"/> (1 = no effect, 0 = immune),
    /// same as <see cref="GameArmor"/> — Phase 3b turns them into Stat contributions.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameShield : GameEquipment
    {
        [Export] public float Defense { get; set; } = 0f;
        public float EffectiveDefense => NonNegativeFinite(Defense);

        /// <summary>Probability (0–1) that an incoming hit is blocked outright.</summary>
        [Export] public float BlockChance { get; set; } = 0f;
        public float EffectiveBlockChance => float.IsFinite(BlockChance) ? Mathf.Clamp(BlockChance, 0f, 1f) : 0f;

        [ExportGroup("Resistances")]
        [Export] public float Physical { get; set; } = 1f;
        [Export] public float Fire { get; set; } = 1f;
        [Export] public float Ice { get; set; } = 1f;
        [Export] public float Poison { get; set; } = 1f;
        [Export] public float Holy { get; set; } = 1f;
        [Export] public float Dark { get; set; } = 1f;
        [Export] public float Lightning { get; set; } = 1f;
        [Export] public float True { get; set; } = 1f;

        /// <summary>Adds this shield's <see cref="Defense"/> to the wielder's "armor" stat while
        /// equipped. BlockChance and per-type resistances are read by the combat/defense components
        /// directly (a later 3b step); they are not a single stat number.</summary>
        public override System.Collections.Generic.IEnumerable<StatModifier> GetIntrinsicModifiers()
        {
            yield return new StatModifier { Stat = "armor", Op = StatOp.Add, Amount = EffectiveDefense, Duration = -1f };
        }

        /// <summary>This shield's multiplier for a damage type (1 = no effect, 0 = immune). Read by
        /// a wearer's ResistanceComponent and combined multiplicatively with armor's.</summary>
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
