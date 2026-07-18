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

        private EquipmentComponent? _equipment;

        public override void _Ready()
        {
            base._Ready();
            _equipment = GetSiblingComponent<EquipmentComponent>();
        }

        /// <summary>Get the multiplier for a damage type (0 = immune, 1 = normal), combining this
        /// entity's own resistance with each equipped armor/shield's — multiplicatively, so two
        /// halving pieces stack to a quarter and equipping fire-resist gear actually resists fire.</summary>
        public float GetMultiplier(DamageType type)
        {
            float m = BaseMultiplier(type);
            if (_equipment != null)
                foreach (var piece in _equipment.EquippedItems)
                    m *= piece switch
                    {
                        GameArmor a => a.ResistFor(type),
                        GameShield s => s.ResistFor(type),
                        _ => 1f
                    };
            return m;
        }

        /// <summary>This entity's intrinsic per-type multiplier, before equipment.</summary>
        private float BaseMultiplier(DamageType type) => type switch
        {
            DamageType.Physical => Physical,
            DamageType.Fire => Fire,
            DamageType.Ice => Ice,
            DamageType.Poison => Poison,
            DamageType.Holy => Holy,
            DamageType.Dark => Dark,
            DamageType.Lightning => Lightning,
            DamageType.True => True,
            _ => 1f
        };

        /// <summary>Apply resistance to incoming damage. Returns modified amount.</summary>
        public float ApplyResistance(float amount, DamageType type)
        {
            float mult = GetMultiplier(type);
            return amount * mult;
        }
    }
}
