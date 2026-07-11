using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Grid-based inventory. Creates a child GridContainer of empty slots.
    /// AddItem places a TextureRect item into the first free slot; RemoveItem
    /// frees it. Emits SlotClicked(index) so the game can show item details.
    /// The parent should be a Control; the grid is added as a child.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InventoryGridComponent : UIComponent
    {
        public struct Item
        {
            public string Id;
            public string Name;
            public Texture2D? Icon;
        }

        [Export] public int Columns { get; set; } = 5;
        [Export] public int SlotCount { get; set; } = 20;
        [Export] public Vector2I SlotSize { get; set; } = new(48, 48);
        [Export] public Color SlotColor { get; set; } = new(1, 1, 1, 0.08f);

        [Signal] public delegate void SlotClickedEventHandler(int index);

        private GridContainer? _grid;
        private readonly List<Item> _items = new();

        public override void _Ready()
        {
            base._Ready();
            EnsureGrid();
        }

        private void EnsureGrid()
        {
            if (GetParent() is not Node parent) return;
            _grid = new GridContainer
            {
                Name = "InventoryGrid",
                Columns = Columns
            };
            _grid.AddThemeConstantOverride("h_separation", 4);
            _grid.AddThemeConstantOverride("v_separation", 4);
            parent.AddChild(_grid);
            _grid.Owner = parent;
            RebuildSlots();
        }

        private void RebuildSlots()
        {
            if (_grid == null) return;
            foreach (var c in _grid.GetChildren()) c.QueueFree();
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = new PanelContainer { CustomMinimumSize = SlotSize };
                var sb = new StyleBoxFlat { BgColor = SlotColor };
                sb.SetCornerRadiusAll(4);
                slot.AddThemeStyleboxOverride("panel", sb);
                _grid.AddChild(slot);
            }
        }

        public void AddItem(Item item)
        {
            if (!IsActive) return;
            int idx = _items.Count;
            if (idx >= SlotCount) return;
            _items.Add(item);
            RefreshSlot(idx, item);
        }

        public void RemoveItem(int index)
        {
            if (!IsActive || index < 0 || index >= _items.Count) return;
            _items.RemoveAt(index);
            RefreshSlot(index, default);
        }

        private void RefreshSlot(int index, Item item)
        {
            if (_grid == null || index >= _grid.GetChildCount()) return;
            if (_grid.GetChild(index) is not PanelContainer slot) return;
            // Clear existing icon.
            foreach (var c in slot.GetChildren()) c.QueueFree();
            if (item.Icon != null)
            {
                var tex = new TextureRect
                {
                    Texture = item.Icon,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    CustomMinimumSize = SlotSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                };
                slot.AddChild(tex);
            }
        }
    }
}
