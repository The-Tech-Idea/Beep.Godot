using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One damage application, threaded Attack/Projectile → <see cref="HealthComponent.TakeDamage"/>.
    ///
    /// Replaces the bare-float handoff that carried a number and nothing else. The old
    /// 1-arg <c>TakeDamage(float)</c> defaulted the type to <see cref="DamageType.Physical"/>,
    /// and that one convenience default silently ate the entire type system — every hit in the
    /// framework was Physical, so a resistance/armor's Fire/Ice/etc. could never fire. There is
    /// deliberately no all-defaults constructor: <b>the type is required at every call site</b>,
    /// so the silent-Physical default cannot come back.
    ///
    /// Not a Resource — a per-hit runtime value, never authored in the inspector or saved.
    /// </summary>
    public readonly struct GameDamage
    {
        /// <summary>Raw damage, before the target's resistance and armor.</summary>
        public float Amount { get; }

        /// <summary>Damage category. Resistance and armor key off this.</summary>
        public DamageType Type { get; }

        /// <summary>Who dealt it — attacker body or projectile owner — or null for
        /// environmental damage (temperature, hunger). Lets a target credit a killer and a
        /// projectile exclude its own shooter.</summary>
        public Node2D? Source { get; }

        /// <summary>Critical hit. Carried for damage-number UI and on-hit rules.</summary>
        public bool IsCrit { get; }

        public GameDamage(float amount, DamageType type, Node2D? source = null, bool isCrit = false)
        {
            Amount = amount;
            Type = type;
            Source = source;
            IsCrit = isCrit;
        }
    }
}
