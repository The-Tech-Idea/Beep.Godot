using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Inventory component. Blind — works for players, chests, shops, any entity that holds items.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InventoryComponent : EntityComponent
    {
        [Export] public int MaxSlots { get; set; } = 20;

        [Signal] public delegate void ItemAddedEventHandler(string itemId, int quantity);
        [Signal] public delegate void ItemRemovedEventHandler(string itemId, int quantity);
        [Signal] public delegate void InventoryFullEventHandler();

        public Dictionary<string, int> Items { get; private set; } = new();
        public int TotalItems => Items.Count;
        public bool IsFull => Items.Count >= MaxSlots;

        public bool AddItem(string itemId, int quantity = 1)
        {
            if (!IsActive) return false;
            if (IsFull && !Items.ContainsKey(itemId)) { EmitSignal(SignalName.InventoryFull); return false; }

            Items.TryGetValue(itemId, out int current);
            Items[itemId] = current + quantity;
            EmitSignal(SignalName.ItemAdded, itemId, quantity);
            return true;
        }

        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (!Items.TryGetValue(itemId, out int current) || current < quantity) return false;
            Items[itemId] = current - quantity;
            if (Items[itemId] <= 0) Items.Remove(itemId);
            EmitSignal(SignalName.ItemRemoved, itemId, quantity);
            return true;
        }

        public bool HasItem(string itemId, int quantity = 1)
            => Items.TryGetValue(itemId, out int c) && c >= quantity;
    }
}
