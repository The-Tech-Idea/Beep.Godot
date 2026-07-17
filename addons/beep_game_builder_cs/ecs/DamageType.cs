namespace Beep.ECS
{
    /// <summary>
    /// Damage category. Consumed by <see cref="ResistanceComponent"/> (per-type multiplier),
    /// carried on a <see cref="GameDamage"/> packet, and set by a GameWeapon's DamageType.
    ///
    /// Extracted from the former DamageTypeComponent (now deleted) so the enum outlives the
    /// node — every resolver of that component returned null, but this vocabulary is load-bearing.
    /// <c>True</c> is unresisted (armor and resistance still ignore it by convention at the
    /// call site — see ResistanceComponent's multipliers).
    /// </summary>
    public enum DamageType { Physical, Fire, Ice, Poison, Holy, Dark, Lightning, True }
}
