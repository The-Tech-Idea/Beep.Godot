using Godot;
using System.Collections.Generic;

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
        private PanelContainer? _tooltipPanel;
        private Label? _tooltipLabel;
        private readonly Dictionary<int, Label> _slotQtyLabels = new();

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
            _slotQtyLabels.Clear();

            for (int i = 0; i < MaxSlots; i++)
            {
                var slot = new PanelContainer { Name = $"Slot_{i}", CustomMinimumSize = SlotSize };
                slot.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
                slot.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = SlotColor, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });

                // Wire the interaction handlers (Interact partial). Without this, drag-to-move,
                // right-click split, slot-click and hover tooltips were all built but never reached —
                // the slots were rendered and inert. The lambdas die with the slot on rebuild.
                int idx = i;
                slot.GuiInput += e => OnSlotGuiInput(e, idx);
                slot.MouseEntered += () => OnSlotMouseEntered(idx);
                slot.MouseExited += OnSlotMouseExited;

                _grid.AddChild(slot);
            }
        }

        private void WireSignals()
        {
            SlotUpdated += OnSlotUpdated;
            InventoryChanged += RefreshAllSlots;
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
            if (_grid.GetChild(index) is not PanelContainer slot) return;

            foreach (var c in slot.GetChildren()) c.QueueFree();
            _slotQtyLabels.Remove(index);

            var entry = GetItemAt(index);
            if (entry != null)
            {
                slot.AddThemeStyleboxOverride("panel",
                    new StyleBoxFlat { BgColor = SlotColorOccupied, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });

                if (entry.Item.Icon != null)
                {
                    var tex = new TextureRect
                    {
                        Texture = entry.Item.Icon,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        CustomMinimumSize = SlotSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                    };
                    slot.AddChild(tex);
                }

                if (entry.Quantity > 1)
                {
                    var qty = new Label
                    {
                        Text = entry.Quantity.ToString(),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Position = new Vector2(SlotSize.X - 22, SlotSize.Y - 16)
                    };
                    qty.AddThemeFontSizeOverride("font_size", 10);
                    slot.AddChild(qty);
                    _slotQtyLabels[index] = qty;
                }
            }
            else
            {
                slot.AddThemeStyleboxOverride("panel",
                    new StyleBoxFlat { BgColor = SlotColor, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            }
        }

        // ── Tooltip ──

        private void SetupTooltip()
        {
            _tooltipLabel = new Label
            {
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(220, 0)
            };
            _tooltipLabel.AddThemeFontSizeOverride("font_size", 12);
            _tooltipPanel = new PanelContainer { Visible = false };
            _tooltipPanel.AddChild(_tooltipLabel);

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
            if (_tooltipPanel == null || _tooltipLabel == null) return;
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
            _tooltipLabel.Text = $"{rarity}{entry.Item.DisplayName}\nType: {type}  x{entry.Quantity}\n{entry.Item.Description}";
            _tooltipPanel.Visible = true;
            _tooltipPanel.Position = (_tooltipPanel.GetViewport()?.GetMousePosition() ?? Vector2.Zero) + new Vector2(16, 16);
        }

        private void SetHoverSlot(int slot)
        {
            _hoveredSlot = slot;
            _hoverTimer = HoverDelay;
            _tooltipShowing = false;
            if (slot < 0 && _tooltipPanel != null) _tooltipPanel.Visible = false;
        }
    }
}
