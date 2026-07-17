using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// A weapon definition. Earns its class by adding fields GameEquipment has no business
    /// carrying: base <see cref="Damage"/>, its <see cref="DamageType"/>, reach, and ranged
    /// firing. `sword_iron.tres`, `axe.tres`, `dagger.tres` are all `.tres` of THIS class —
    /// same fields, different numbers. A "SwordClass" would be a rename.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameWeapon : GameEquipment
    {
        [Export] public float Damage { get; set; } = 10f;

        /// <summary>The type this weapon deals, matched against a target's ResistanceComponent.
        /// Reuses the shared <see cref="DamageType"/> enum, so a weapon's type lines up with the
        /// resistance multipliers directly.</summary>
        [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

        /// <summary>Melee reach. Becomes real once Phase 3 replaces AttackComponent's cursor
        /// point-query with an Area2D hitbox; until then it is authored but not yet read.</summary>
        [Export] public float Range { get; set; } = 50f;

        [Export] public bool IsRanged { get; set; } = false;

        /// <summary>The projectile spawned when <see cref="IsRanged"/>. Same shape as
        /// AttackComponent.ProjectileScene — a scene whose instance carries ProjectileComponent.</summary>
        [Export] public PackedScene? ProjectileScene { get; set; }

        /// <summary>Time between uses, in CLOCK UNITS — not seconds. 3 = 3 seconds in a real-time
        /// genre, 3 turns in a turn-based one, the same `.tres`, because the genre owns the clock
        /// (Phase 7). Speed buffs are StatModifiers on this axis (Phase 2), not a second field —
        /// which is why there is no AttackSpeedMultiplier.</summary>
        [Export] public float Cooldown { get; set; } = 0.5f;

        /// <summary>What this weapon consumes per shot, or null if it needs none (a sword). A gun
        /// eats ammo regardless of how it was crafted, so this belongs on the item — Phase 7.</summary>
        [Export] public GameItem? AmmoItem { get; set; }

        [Export] public int AmmoPerUse { get; set; } = 1;
    }
}
