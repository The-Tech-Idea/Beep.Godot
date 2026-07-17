using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The ONE inventory component. Holds the data model, handles all display
    /// (grid rendering, slot icons, quantity labels, tooltips), and all interaction
    /// (drag-and-drop, right-click split, hover tooltip, sort). Drop this single
    /// node under any Control to get a fully functional inventory.
    ///
    /// Implements ISaveable for state persistence (save/load).
    ///
    /// Split into partial files for organization:
    ///   InventoryComponent.cs          — data model + core logic (add/remove/move/stack/sort)
    ///   InventoryComponent.Display.cs  — grid rendering, slot visuals, tooltip
    ///   InventoryComponent.Interact.cs — drag-and-drop, right-click, hover, sort buttons
    ///
    /// Usage: drag this onto a Control node. Call RegisterItem() once per item type,
    /// then AddItem() to populate. The grid builds itself.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InventoryComponent : GameplayComponent, ISaveable
    {
        // ════════════════════════════════════════════════════════════════
        //  Item data model
        // ════════════════════════════════════════════════════════════════

        public class InventoryItem
        {
            public string Id = "";
            public string DisplayName = "";
            public string Description = "";
            public Texture2D? Icon;
            public int Quantity = 1;
            public int MaxStack = 99;
            public ItemRarity Rarity = ItemRarity.Common;
            public string ItemType = "misc";
            public Godot.Collections.Dictionary<string, Variant> Stats = new();
            public int SlotIndex = -1;
        }

        public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }
        public enum SortMode { ByType, ByRarity, ByName, ByQuantity }

        // ════════════════════════════════════════════════════════════════
        //  Exports — all inspector-tunable
        // ════════════════════════════════════════════════════════════════

        [ExportGroup("Capacity")]
        [Export] public int MaxSlots { get; set; } = 20;
        [Export] public bool AutoStack { get; set; } = true;

        /// <summary>Include this inventory in saves. Tick it on the player's inventory only —
        /// GameStateData keeps a single Inventory slot, so a chest or shop inventory saving
        /// too would overwrite the player's.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = false;

        [ExportGroup("Grid Display")]
        [Export] public int Columns { get; set; } = 5;
        [Export] public Vector2I SlotSize { get; set; } = new(48, 48);
        [Export] public Color SlotColor { get; set; } = new(1, 1, 1, 0.08f);
        [Export] public Color SlotColorOccupied { get; set; } = new(1, 1, 1, 0.15f);
        [Export] public bool ShowTooltips { get; set; } = true;
        [Export] public float HoverDelay { get; set; } = 0.3f;

        // ════════════════════════════════════════════════════════════════
        //  Signals
        // ════════════════════════════════════════════════════════════════

        [Signal] public delegate void InventoryChangedEventHandler();
        [Signal] public delegate void ItemAddedEventHandler(string itemId, int quantity);
        [Signal] public delegate void ItemRemovedEventHandler(string itemId, int quantity);
        [Signal] public delegate void ItemMovedEventHandler(int fromSlot, int toSlot);
        [Signal] public delegate void InventoryFullEventHandler();
        [Signal] public delegate void SlotUpdatedEventHandler(int slot);
        [Signal] public delegate void SlotClickedEventHandler(int slot);

        // ════════════════════════════════════════════════════════════════
        //  Data (the single source of truth)
        // ════════════════════════════════════════════════════════════════

        public InventoryItem?[] Slots { get; private set; } = null!;
        private readonly Dictionary<string, InventoryItem> _itemTemplates = new();

        public int UsedSlots { get { int c = 0; if (Slots != null) foreach (var i in Slots) if (i != null) c++; return c; } }
        public int FreeSlots => MaxSlots - UsedSlots;
        public bool IsFull => UsedSlots >= MaxSlots;

        public override void _Ready()
        {
            base._Ready();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            Slots = new InventoryItem[MaxSlots];
            CallDeferred(nameof(BuildUI));
        }

        public override void _Process(double delta)
        {
            ProcessInteraction(delta);
        }

        // ════════════════════════════════════════════════════════════════
        //  Item registration
        // ════════════════════════════════════════════════════════════════

        public void RegisterItem(string itemId, string displayName, string description,
            Texture2D? icon, int maxStack = 99, ItemRarity rarity = ItemRarity.Common,
            string itemType = "misc")
        {
            _itemTemplates[itemId] = new InventoryItem
            {
                Id = itemId, DisplayName = displayName, Description = description,
                Icon = icon, MaxStack = maxStack, Rarity = rarity, ItemType = itemType
            };
        }

        public InventoryItem? GetTemplate(string itemId)
            => _itemTemplates.TryGetValue(itemId, out var t) ? t : null;

        // ════════════════════════════════════════════════════════════════
        //  Core operations
        // ════════════════════════════════════════════════════════════════

        public bool AddItem(string itemId, int quantity = 1)
        {
            if (!IsActive || Slots == null) return false;

            int originalQuantity = quantity;

            if (AutoStack)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (Slots[i] != null && Slots[i]!.Id == itemId && Slots[i]!.Quantity < Slots[i]!.MaxStack)
                    {
                        int space = Slots[i]!.MaxStack - Slots[i]!.Quantity;
                        int toAdd = Mathf.Min(space, quantity);
                        Slots[i]!.Quantity += toAdd;
                        quantity -= toAdd;
                        EmitSignal(SignalName.SlotUpdated, i);
                        if (quantity <= 0) { EmitSignal(SignalName.ItemAdded, itemId, originalQuantity); EmitSignal(SignalName.InventoryChanged); return true; }
                    }
                }
            }

            while (quantity > 0)
            {
                int slot = FindEmptySlot();
                if (slot < 0) { EmitSignal(SignalName.InventoryFull); EmitSignal(SignalName.InventoryChanged); return false; }
                var template = GetTemplate(itemId);
                int maxStack = template?.MaxStack ?? 99;
                int toAdd = Mathf.Min(maxStack, quantity);
                Slots[slot] = new InventoryItem
                {
                    Id = itemId, DisplayName = template?.DisplayName ?? itemId,
                    Description = template?.Description ?? "", Icon = template?.Icon,
                    Quantity = toAdd, MaxStack = maxStack, Rarity = template?.Rarity ?? ItemRarity.Common,
                    ItemType = template?.ItemType ?? "misc", SlotIndex = slot
                };
                quantity -= toAdd;
                EmitSignal(SignalName.SlotUpdated, slot);
            }

            EmitSignal(SignalName.ItemAdded, itemId, originalQuantity);
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (!HasItem(itemId, quantity)) return false;
            int remaining = quantity;
            for (int i = 0; i < MaxSlots && remaining > 0; i++)
            {
                if (Slots?[i] != null && Slots[i]!.Id == itemId)
                {
                    int take = Mathf.Min(Slots[i]!.Quantity, remaining);
                    Slots[i]!.Quantity -= take;
                    remaining -= take;
                    if (Slots[i]!.Quantity <= 0) Slots[i] = null;
                    EmitSignal(SignalName.SlotUpdated, i);
                }
            }
            EmitSignal(SignalName.ItemRemoved, itemId, quantity);
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        public bool RemoveAt(int slot, int quantity = 1)
        {
            if (Slots == null || slot < 0 || slot >= MaxSlots || Slots[slot] == null) return false;
            Slots[slot]!.Quantity -= quantity;
            if (Slots[slot]!.Quantity <= 0) Slots[slot] = null;
            EmitSignal(SignalName.SlotUpdated, slot);
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        public void MoveItem(int fromSlot, int toSlot)
        {
            if (Slots == null || fromSlot < 0 || toSlot < 0 || fromSlot >= MaxSlots || toSlot >= MaxSlots) return;
            if (Slots[fromSlot] == null) return;

            if (Slots[toSlot] != null && Slots[toSlot]!.Id == Slots[fromSlot]!.Id
                && Slots[toSlot]!.Quantity < Slots[toSlot]!.MaxStack)
            {
                int space = Slots[toSlot]!.MaxStack - Slots[toSlot]!.Quantity;
                int move = Mathf.Min(space, Slots[fromSlot]!.Quantity);
                Slots[toSlot]!.Quantity += move;
                Slots[fromSlot]!.Quantity -= move;
                if (Slots[fromSlot]!.Quantity <= 0) Slots[fromSlot] = null;
            }
            else
            {
                (Slots[toSlot], Slots[fromSlot]) = (Slots[fromSlot], Slots[toSlot]);
            }

            if (Slots[fromSlot] != null) Slots[fromSlot]!.SlotIndex = fromSlot;
            if (Slots[toSlot] != null) Slots[toSlot]!.SlotIndex = toSlot;
            EmitSignal(SignalName.SlotUpdated, fromSlot);
            EmitSignal(SignalName.SlotUpdated, toSlot);
            EmitSignal(SignalName.ItemMoved, fromSlot, toSlot);
            EmitSignal(SignalName.InventoryChanged);
        }

        public bool SplitStack(int fromSlot, int amount)
        {
            if (Slots == null || fromSlot < 0 || fromSlot >= MaxSlots) return false;
            if (Slots[fromSlot] == null || Slots[fromSlot]!.Quantity <= amount) return false;
            int targetSlot = FindEmptySlot();
            if (targetSlot < 0) return false;

            var original = Slots[fromSlot]!;
            Slots[targetSlot] = new InventoryItem
            {
                Id = original.Id, DisplayName = original.DisplayName, Description = original.Description,
                Icon = original.Icon, Quantity = amount, MaxStack = original.MaxStack,
                Rarity = original.Rarity, ItemType = original.ItemType, SlotIndex = targetSlot
            };
            original.Quantity -= amount;
            EmitSignal(SignalName.SlotUpdated, fromSlot);
            EmitSignal(SignalName.SlotUpdated, targetSlot);
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        public void Sort(SortMode mode = SortMode.ByType)
        {
            if (Slots == null) return;
            var items = new List<InventoryItem>();
            foreach (var item in Slots) if (item != null) items.Add(item);
            items.Sort((a, b) => mode switch
            {
                SortMode.ByType => string.Compare(a.ItemType, b.ItemType, System.StringComparison.Ordinal),
                SortMode.ByRarity => ((int)a.Rarity).CompareTo((int)b.Rarity),
                SortMode.ByName => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal),
                SortMode.ByQuantity => b.Quantity.CompareTo(a.Quantity),
                _ => 0
            });
            for (int i = 0; i < MaxSlots; i++)
            {
                Slots[i] = i < items.Count ? items[i] : null;
                if (Slots[i] != null) Slots[i]!.SlotIndex = i;
                EmitSignal(SignalName.SlotUpdated, i);
            }
            EmitSignal(SignalName.InventoryChanged);
        }

        // ════════════════════════════════════════════════════════════════
        //  Queries
        // ════════════════════════════════════════════════════════════════

        public bool HasItem(string itemId, int quantity = 1)
        {
            int total = 0;
            if (Slots == null) return false;
            foreach (var item in Slots) if (item != null && item.Id == itemId) total += item.Quantity;
            return total >= quantity;
        }

        public int CountItem(string itemId)
        {
            int total = 0;
            if (Slots == null) return 0;
            foreach (var item in Slots) if (item != null && item.Id == itemId) total += item.Quantity;
            return total;
        }

        public InventoryItem? GetItemAt(int slot)
            => (Slots != null && slot >= 0 && slot < MaxSlots) ? Slots[slot] : null;

        public bool IsSlotEmpty(int slot)
            => Slots != null && slot >= 0 && slot < MaxSlots && Slots[slot] == null;

        public void Resize(int newMaxSlots)
        {
            if (Slots == null) { MaxSlots = newMaxSlots; Slots = new InventoryItem[newMaxSlots]; return; }
            var newSlots = new InventoryItem[newMaxSlots];
            for (int i = 0; i < Mathf.Min(MaxSlots, newMaxSlots); i++) newSlots[i] = Slots[i];
            Slots = newSlots;
            MaxSlots = newMaxSlots;
            EmitSignal(SignalName.InventoryChanged);
        }

        private int FindEmptySlot()
        {
            if (Slots == null) return -1;
            for (int i = 0; i < MaxSlots; i++) if (Slots[i] == null) return i;
            return -1;
        }

        // ════════════════════════════════════════════════════════════════
        //  ISaveable Implementation (auto-called by GameStateManagerComponent)
        // ════════════════════════════════════════════════════════════════

        public void Save(GameBuilder.GameStateData state)
        {
            state.Inventory.Items.Clear();
            state.Inventory.MaxSlots = MaxSlots;

            if (Slots == null) return;

            for (int i = 0; i < MaxSlots; i++)
            {
                if (Slots[i] != null)
                {
                    // Accumulate, don't assign. Items is keyed by item id, so two stacks of
                    // the same item in different slots collapsed to whichever slot came last
                    // — 99 + 99 potions restored as 99.
                    string id = Slots[i]!.Id;
                    state.Inventory.Items.TryGetValue(id, out int existing);
                    state.Inventory.Items[id] = existing + Slots[i]!.Quantity;
                }
            }
        }

        public void Load(GameBuilder.GameStateData state)
        {
            MaxSlots = state.Inventory.MaxSlots;
            Slots = new InventoryItem[MaxSlots];

            foreach (var (itemId, quantity) in state.Inventory.Items)
            {
                AddItem(itemId, quantity);
            }
        }
    }
}
