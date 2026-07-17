namespace Beep.ECS
{
    /// <summary>
    /// Item rarity tier. Shared vocabulary for the GameItem tree and inventory UI tinting.
    ///
    /// The same values previously lived nested in InventoryComponent; they moved here so a
    /// GameItem .tres can carry rarity without depending on the inventory component, and the
    /// nested copy was removed when InventoryComponent was refactored onto the GameItem model.
    /// </summary>
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }
}
