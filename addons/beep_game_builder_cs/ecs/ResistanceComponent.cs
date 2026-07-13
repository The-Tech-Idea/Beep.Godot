using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Damage resistance modifier. Attach alongside a HealthComponent. Stores
    /// per-type multipliers (0 = immune, 0.5 = half, 1 = normal, 2 = double/weak).
    /// When HealthComponent takes damage, this component modifies the incoming
    /// amount based on the damage type.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ResistanceComponent : GameplayComponent
    {
        [Export] public float Physical { get; set; } = 1f;
        [Export] public float Fire { get; set; } = 1f;
        [Export] public float Ice { get; set; } = 1f;
        [Export] public float Poison { get; set; } = 1f;
        [Export] public float Holy { get; set; } = 1f;
        [Export] public float Dark { get; set; } = 1f;
        [Export] public float Lightning { get; set; } = 1f;
        [Export] public float True { get; set; } = 1f;

        [Signal] public delegate void ResistanceBrokenEventHandler(DamageTypeComponent.Type type);

        /// <summary>Get the multiplier for a damage type (0 = immune, 1 = normal).</summary>
        public float GetMultiplier(DamageTypeComponent.Type type) => type switch
        {
            DamageTypeComponent.Type.Physical => Physical,
            DamageTypeComponent.Type.Fire => Fire,
            DamageTypeComponent.Type.Ice => Ice,
            DamageTypeComponent.Type.Poison => Poison,
            DamageTypeComponent.Type.Holy => Holy,
            DamageTypeComponent.Type.Dark => Dark,
            DamageTypeComponent.Type.Lightning => Lightning,
            DamageTypeComponent.Type.True => True,
            _ => 1f
        };

        /// <summary>Apply resistance to incoming damage. Returns modified amount.</summary>
        public float ApplyResistance(float amount, DamageTypeComponent.Type type)
        {
            float mult = GetMultiplier(type);
            return amount * mult;
        }
    }
}
