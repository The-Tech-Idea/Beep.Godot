using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for every authored item — the DEFINITION, not the instance. A `.tres` a
    /// developer makes in the inspector, drags onto an `[Export]`, stacks in an inventory, and
    /// saves. It is a <see cref="Resource"/>, so it is NOT in the scene tree and carries no
    /// components; when an item needs to exist as a node (lying in the world, wielded), it is
    /// spawned from <see cref="WorldScene"/> (or a subclass's WieldScene).
    ///
    /// Replaces the old stringly-typed InventoryComponent.InventoryItem (a nested class with a
    /// `Dictionary&lt;string, Variant&gt; Stats` bag that could not be authored, saved, or
    /// subclassed). The subclass IS the type; its typed fields ARE the stats.
    ///
    /// ⚠ A `.tres` is SHARED BY REFERENCE. Anything per-instance — quantity, current durability,
    /// socketed gems — must live on the inventory slot or the world node, never here, or every
    /// sword pointing at `sword_iron.tres` would share one count and wear out together. Godot's
    /// `resource_local_to_scene` does NOT fix this (engine bug godot#45350). See the InventorySlot.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameItem : Resource
    {
        /// <summary>Stable identity — the stacking key and the save key. Two `.tres` with the
        /// same Id stack; save/load persists this, not the resource.</summary>
        [Export] public string Id { get; set; } = "";

        [Export] public string DisplayName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public Texture2D? Icon { get; set; }
        [Export] public ItemRarity Rarity { get; set; } = ItemRarity.Common;
        [Export] public int MaxStack { get; set; } = 99;

        /// <summary>Stays put (anvil, chest, rock) rather than being carried. With
        /// <see cref="IsDestructible"/> this drives the archetype rules: a static item's world
        /// node must not carry MovementComponent/PickupComponent.</summary>
        [Export] public bool IsStatic { get; set; } = false;

        /// <summary>Can be broken. When true the world node should carry a HealthComponent as
        /// durability (MaxHealth = MaxDurability; Died = it breaks); when false it must NOT — HP
        /// on an unbreakable thing is behaviour that never happens.</summary>
        [Export] public bool IsDestructible { get; set; } = false;

        /// <summary>Durability cap — meaningful only when <see cref="IsDestructible"/>. This is
        /// the CAP on the definition; the CURRENT durability is per-instance (on the slot/node).</summary>
        [Export] public float MaxDurability { get; set; } = 100f;

        /// <summary>How this item exists as a node when it is in the world (lying on the ground,
        /// placed). Null = it has no world form. The instance carries the components; the
        /// definition only points at it — the same shape the repo uses for bullets
        /// (ProjectileScene + ProjectileComponent).</summary>
        [Export] public PackedScene? WorldScene { get; set; }
    }
}
