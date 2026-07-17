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
    /// Usage: author <see cref="GameItem"/> `.tres` files, then AddItem(gameItem) to populate.
    /// The grid builds itself. Items are shared definitions; per-instance state lives on the slot.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InventoryComponent : GameplayComponent, ISaveable
    {
        // ════════════════════════════════════════════════════════════════
        //  Item data model
        // ════════════════════════════════════════════════════════════════

        /// <summary>One occupied slot. The <see cref="Item"/> is a SHARED definition (a `.tres`);
        /// everything else is PER-INSTANCE and must live here, never on the resource, or every
        /// sword pointing at the same `.tres` would share one count, wear out together, and hold
        /// the same gems. This is the load-bearing distinction of the item model.</summary>
        public class InventorySlot
        {
            public GameItem Item = null!;
            public int Quantity = 1;
            /// <summary>Current durability — the CAP is <see cref="GameItem.MaxDurability"/> on the
            /// definition. Mutated/persisted from Phase 7; initialised to the cap.</summary>
            public float Durability;
            /// <summary>Gems socketed into THIS instance — Phase 7 (composition). Empty until then.</summary>
            public GameItem[] Socketed = System.Array.Empty<GameItem>();
        }

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

        public InventorySlot?[] Slots { get; private set; } = null!;

        public int UsedSlots { get { int c = 0; if (Slots != null) foreach (var s in Slots) if (s != null) c++; return c; } }
        public int FreeSlots => MaxSlots - UsedSlots;
        public bool IsFull => UsedSlots >= MaxSlots;

        public override void _Ready()
        {
            base._Ready();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            Slots = new InventorySlot[MaxSlots];
            CallDeferred(nameof(BuildUI));
        }

        public override void _Process(double delta)
        {
            ProcessInteraction(delta);
        }

        private static InventorySlot NewSlot(GameItem item, int quantity) => new()
        {
            Item = item,
            Quantity = quantity,
            Durability = item.MaxDurability,
            Socketed = System.Array.Empty<GameItem>()
        };

        // ════════════════════════════════════════════════════════════════
        //  Core operations
        // ════════════════════════════════════════════════════════════════

        /// <summary>Add a stack of an authored item. Stacks into existing slots first (respecting
        /// <see cref="GameItem.MaxStack"/>), then fills empty slots. Returns false if it ran out
        /// of room (emitting InventoryFull) — partial adds still keep what fit.</summary>
        public bool AddItem(GameItem item, int quantity = 1)
        {
            if (!IsActive || Slots == null) return false;
            if (item == null)
            {
                GD.PushWarning($"[{Name}] AddItem called with a null GameItem — nothing added.");
                return false;
            }
            if (string.IsNullOrEmpty(item.Id))
                GD.PushWarning($"[{Name}] AddItem: '{item.DisplayName}' has an empty Id — it will not stack or survive a save/load round-trip. Set GameItem.Id.");

            int originalQuantity = quantity;

            if (AutoStack)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (Slots[i] != null && Slots[i]!.Item.Id == item.Id && Slots[i]!.Quantity < Slots[i]!.Item.MaxStack)
                    {
                        int space = Slots[i]!.Item.MaxStack - Slots[i]!.Quantity;
                        int toAdd = Mathf.Min(space, quantity);
                        Slots[i]!.Quantity += toAdd;
                        quantity -= toAdd;
                        EmitSignal(SignalName.SlotUpdated, i);
                        if (quantity <= 0) { EmitSignal(SignalName.ItemAdded, item.Id, originalQuantity); EmitSignal(SignalName.InventoryChanged); return true; }
                    }
                }
            }

            while (quantity > 0)
            {
                int slot = FindEmptySlot();
                if (slot < 0) { EmitSignal(SignalName.InventoryFull); EmitSignal(SignalName.InventoryChanged); return false; }
                int toAdd = Mathf.Min(item.MaxStack, quantity);
                Slots[slot] = NewSlot(item, toAdd);
                quantity -= toAdd;
                EmitSignal(SignalName.SlotUpdated, slot);
            }

            EmitSignal(SignalName.ItemAdded, item.Id, originalQuantity);
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        /// <summary>Remove a quantity of an item by its id, across stacks. Returns false if the
        /// inventory does not hold that many.</summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (!HasItem(itemId, quantity)) return false;
            int remaining = quantity;
            for (int i = 0; i < MaxSlots && remaining > 0; i++)
            {
                if (Slots?[i] != null && Slots[i]!.Item.Id == itemId)
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

            if (Slots[toSlot] != null && Slots[toSlot]!.Item.Id == Slots[fromSlot]!.Item.Id
                && Slots[toSlot]!.Quantity < Slots[toSlot]!.Item.MaxStack)
            {
                int space = Slots[toSlot]!.Item.MaxStack - Slots[toSlot]!.Quantity;
                int move = Mathf.Min(space, Slots[fromSlot]!.Quantity);
                Slots[toSlot]!.Quantity += move;
                Slots[fromSlot]!.Quantity -= move;
                if (Slots[fromSlot]!.Quantity <= 0) Slots[fromSlot] = null;
            }
            else
            {
                (Slots[toSlot], Slots[fromSlot]) = (Slots[fromSlot], Slots[toSlot]);
            }

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
            Slots[targetSlot] = new InventorySlot
            {
                Item = original.Item,
                Quantity = amount,
                Durability = original.Durability,
                Socketed = System.Array.Empty<GameItem>()
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
            var items = new List<InventorySlot>();
            foreach (var slot in Slots) if (slot != null) items.Add(slot);
            items.Sort((a, b) => mode switch
            {
                // The class IS the type now (there is no ItemType string): sort by the resource's type.
                SortMode.ByType => string.Compare(a.Item.GetType().Name, b.Item.GetType().Name, System.StringComparison.Ordinal),
                SortMode.ByRarity => ((int)a.Item.Rarity).CompareTo((int)b.Item.Rarity),
                SortMode.ByName => string.Compare(a.Item.DisplayName, b.Item.DisplayName, System.StringComparison.Ordinal),
                SortMode.ByQuantity => b.Quantity.CompareTo(a.Quantity),
                _ => 0
            });
            for (int i = 0; i < MaxSlots; i++)
            {
                Slots[i] = i < items.Count ? items[i] : null;
                EmitSignal(SignalName.SlotUpdated, i);
            }
            EmitSignal(SignalName.InventoryChanged);
        }

        // ════════════════════════════════════════════════════════════════
        //  Queries
        // ════════════════════════════════════════════════════════════════

        public bool HasItem(string itemId, int quantity = 1) => CountItem(itemId) >= quantity;

        public int CountItem(string itemId)
        {
            int total = 0;
            if (Slots == null) return 0;
            foreach (var slot in Slots) if (slot != null && slot.Item.Id == itemId) total += slot.Quantity;
            return total;
        }

        public InventorySlot? GetItemAt(int slot)
            => (Slots != null && slot >= 0 && slot < MaxSlots) ? Slots[slot] : null;

        public bool IsSlotEmpty(int slot)
            => Slots != null && slot >= 0 && slot < MaxSlots && Slots[slot] == null;

        public void Resize(int newMaxSlots)
        {
            if (Slots == null) { MaxSlots = newMaxSlots; Slots = new InventorySlot[newMaxSlots]; return; }
            var newSlots = new InventorySlot[newMaxSlots];
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
                    string id = Slots[i]!.Item.Id;
                    state.Inventory.Items.TryGetValue(id, out int existing);
                    state.Inventory.Items[id] = existing + Slots[i]!.Quantity;
                }
            }
            // NOTE: per-instance durability/sockets are NOT persisted yet — nothing mutates them
            // before Phase 7. When Phase 7 lands, this becomes a per-slot record
            // ({id, quantity, durability, socketed[ids]}) so a saved sword keeps its wear and gems.
        }

        public void Load(GameBuilder.GameStateData state)
        {
            MaxSlots = state.Inventory.MaxSlots;
            Slots = new InventorySlot[MaxSlots];

            foreach (var (itemId, quantity) in state.Inventory.Items)
            {
                var item = GameItemCatalog.Resolve(itemId);
                if (item == null)
                {
                    GD.PushWarning($"[{Name}] Load: no GameItem catalogued for id '{itemId}' — that stack was dropped. " +
                                   "Ensure the item's .tres is under GameItemCatalog.ItemsRoot, or Register() it before loading.");
                    continue;
                }
                AddItem(item, quantity);
            }
        }
    }
}
