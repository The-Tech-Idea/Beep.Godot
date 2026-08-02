using Godot;
using System.Collections.Generic;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// PARTIAL: Grid display + tooltip rendering for InventoryComponent.
    /// Builds the GridContainer, renders slot icons/quantities, and shows
    /// item tooltips on hover. All state is read from the main partial's
    /// Slots[] array — this partial holds NO data of its own.
    /// </summary>
    public partial class InventoryComponent
    {
        private GridContainer? _grid;
        private KitTooltip? _tooltipPanel;
        private readonly Dictionary<int, KitInventorySlot> _slotViews = new();

        // Hover state
        private int _hoveredSlot = -1;
        private float _hoverTimer;
        private bool _tooltipShowing;

        /// <summary>Build the grid UI and wire to SlotUpdated/InventoryChanged.</summary>
        private void BuildUI()
        {
            if (GetParent() is not Node parent) return;

            // Grid container.
            _grid = new GridContainer { Name = "InventoryGrid", Columns = Columns };
            _grid.AddThemeConstantOverride("h_separation", 4);
            _grid.AddThemeConstantOverride("v_separation", 4);
            parent.AddChild(_grid);
            if (parent.IsInsideTree()) _grid.Owner = parent.Owner;

            BuildSlots();
            SetupTooltip();
            WireSignals();
            RefreshAllSlots();
        }

        private void BuildSlots()
        {
            if (_grid == null) return;
            foreach (var c in _grid.GetChildren()) c.QueueFree();
            _slotViews.Clear();

            for (int i = 0; i < MaxSlots; i++)
            {
                var slot = new KitInventorySlot { Name = $"Slot_{i}", CustomMinimumSize = SlotSize };
                slot.MouseFilter = Godot.Control.MouseFilterEnum.Stop;

                // Wire the interaction handlers (Interact partial). Without this, drag-to-move,
                // right-click split, slot-click and hover tooltips were all built but never reached —
                // the slots were rendered and inert. The lambdas die with the slot on rebuild.
                int idx = i;
                slot.GuiInput += e => OnSlotGuiInput(e, idx);
                slot.MouseEntered += () => OnSlotMouseEntered(idx);
                slot.MouseExited += OnSlotMouseExited;

                _grid.AddChild(slot);
                _slotViews[i] = slot;
            }
        }

        private void WireSignals()
        {
            SlotUpdated += OnSlotUpdated;
            InventoryChanged += RefreshAllSlots;
        }

        /// <summary>Free the grid and tooltip this partial injected into the parent Control.
        /// Without this, removing the inventory node while its parent survives orphans both
        /// onscreen. Called from the component's _ExitTree.</summary>
        private void DisposeUI()
        {
            if (_grid != null && GodotObject.IsInstanceValid(_grid)) _grid.QueueFree();
            if (_tooltipPanel != null && GodotObject.IsInstanceValid(_tooltipPanel)) _tooltipPanel.QueueFree();
            _grid = null;
            _tooltipPanel = null;
            _slotViews.Clear();
        }

        /// <summary>Rebuild the grid to the current MaxSlots/Columns and repaint. Used after a Load
        /// that changed the capacity, so every slot has a cell.</summary>
        private void RebuildGrid()
        {
            if (_grid == null) return;
            _grid.Columns = Columns;
            BuildSlots();
            RefreshAllSlots();
        }

        private void OnSlotUpdated(int slot) => RefreshSlot(slot);

        /// <summary>Refresh every slot from Slots[]. Called on InventoryChanged.</summary>
        public void RefreshAllSlots()
        {
            for (int i = 0; i < MaxSlots; i++) RefreshSlot(i);
        }

        /// <summary>Refresh a single slot's visuals from the data.</summary>
        private void RefreshSlot(int index)
        {
            if (_grid == null || index >= _grid.GetChildCount()) return;
            if (_grid.GetChild(index) is not KitInventorySlot slot) return;

            slot.Icon = null;
            slot.Count = 0;
            slot.Rarity = UiSurface.Role.Neutral;
            slot.Locked = false;
            slot.Requirement = "";

            var entry = GetItemAt(index);
            if (entry != null)
            {
                slot.Icon = entry.Item.Icon;
                slot.Count = entry.Quantity;
                slot.Rarity = RoleFor(entry.Item.Rarity);
            }
        }

        // ── Tooltip ──

        private void SetupTooltip()
        {
            _tooltipPanel = new KitTooltip
            {
                Name = "InventoryTooltip",
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(240, 58),
                Visible = false
            };

            if (GetParent() is Node parent)
            {
                parent.AddChild(_tooltipPanel);
                if (parent.IsInsideTree()) _tooltipPanel.Owner = parent.Owner;
            }
        }

        /// <summary>Called every frame from Interact partial to update hover timer.</summary>
        private void ProcessHover(double delta)
        {
            if (!ShowTooltips || _hoveredSlot < 0 || _tooltipShowing) return;
            _hoverTimer -= (float)delta;
            if (_hoverTimer <= 0)
            {
                _tooltipShowing = true;
                ShowTooltip(_hoveredSlot);
            }
        }

        private void ShowTooltip(int slot)
        {
            if (_tooltipPanel == null) return;
            var entry = GetItemAt(slot);
            if (entry == null) { _tooltipPanel.Visible = false; return; }

            string rarity = entry.Item.Rarity switch
            {
                ItemRarity.Uncommon => "[Uncommon] ",
                ItemRarity.Rare => "[Rare] ",
                ItemRarity.Epic => "[Epic] ",
                ItemRarity.Legendary => "[Legendary] ",
                _ => ""
            };
            // The class is the type: "GameWeapon" -> "Weapon". No ItemType string anymore.
            string type = entry.Item.GetType().Name.Replace("Game", "");
            _tooltipPanel.Text = $"{rarity}{entry.Item.DisplayName}  {type} x{entry.Quantity}";
            _tooltipPanel.Visible = true;
            _tooltipPanel.Position = (_tooltipPanel.GetViewport()?.GetMousePosition() ?? Vector2.Zero) + new Vector2(16, 16);
        }

        private static UiSurface.Role RoleFor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => UiSurface.Role.Info,
            ItemRarity.Rare => UiSurface.Role.Accent,
            ItemRarity.Epic => UiSurface.Role.Accent2,
            ItemRarity.Legendary => UiSurface.Role.Warning,
            _ => UiSurface.Role.Neutral,
        };

        private void SetHoverSlot(int slot)
        {
            _hoveredSlot = slot;
            _hoverTimer = HoverDelay;
            _tooltipShowing = false;
            if (slot < 0 && _tooltipPanel != null) _tooltipPanel.Visible = false;
        }
    }
}
