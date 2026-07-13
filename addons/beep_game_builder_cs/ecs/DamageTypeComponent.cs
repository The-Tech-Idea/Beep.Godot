using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Typed damage modifier. Attach to the same node as an AttackComponent or
    /// ProjectileComponent to give its damage a type (physical, fire, ice, etc.)
    /// and a multiplier. The receiving HealthComponent + ResistanceComponent
    /// will apply the resistance.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DamageTypeComponent : GameplayComponent
    {
        public enum Type { Physical, Fire, Ice, Poison, Holy, Dark, Lightning, True }

        [Export] public Type DamageType { get; set; } = Type.Physical;
        [Export] public float Multiplier { get; set; } = 1.0f;

        /// <summary>Compute final damage for a given base amount.</summary>
        public float GetDamage(float baseAmount) => baseAmount * Multiplier;

        [Signal] public delegate void DamageDealtEventHandler(Node target, Type type, float amount);
    }
}
