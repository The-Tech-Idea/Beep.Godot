using Godot;

namespace Beep.ECS
{
    /// <summary>Where a piece of equipment goes on the body. Declared here because
    /// GameEquipment is the first thing that needs it; Phase 2's EquipmentComponent reads it.</summary>
    public enum EquipSlot { MainHand, OffHand, Head, Body, Accessory }

    /// <summary>
    /// Base for anything worn or wielded — weapons, shields, armor. Adds the equip slot and the
    /// <see cref="WieldScene"/> (the node instanced into the wielder's hand when equipped, by
    /// Phase 2's EquipmentComponent). A wielded instance MAY carry AttackComponent, a hitbox, and
    /// HealthComponent-as-durability — that is the "two representations" model: the definition
    /// stacks and saves, the WieldScene instance does the swinging.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameEquipment : GameItem
    {
        [Export] public EquipSlot Slot { get; set; } = EquipSlot.MainHand;

        /// <summary>The node held in the hand when equipped. Distinct from
        /// <see cref="GameItem.WorldScene"/> (how it looks lying on the ground): a wielded sword
        /// and a dropped sword can be different scenes.</summary>
        [Export] public PackedScene? WieldScene { get; set; }

        /// <summary>How many sockets this piece has. The COUNT is on the definition; the gems
        /// actually socketed are per-instance (on the inventory slot) — Phase 7 (composition).</summary>
        [Export] public int SocketCount { get; set; } = 0;

        /// <summary>The stat modifiers this piece contributes while equipped — a sword's
        /// <c>{damage, Add, 10}</c>, a ring's <c>{move_speed, Multiply, 1.1}</c>. Authored on the
        /// `.tres`; EquipmentComponent DUPLICATES them per wearer (so two entities holding the same
        /// `.tres` don't share modifier state) and withdraws them by source on unequip. Equipment
        /// modifiers are normally permanent (Duration &lt; 0).</summary>
        [Export] public StatModifier[] Modifiers { get; set; } = System.Array.Empty<StatModifier>();
    }
}
