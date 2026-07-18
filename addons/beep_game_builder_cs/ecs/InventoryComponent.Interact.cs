using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// PARTIAL: Interaction logic for InventoryComponent. Handles mouse input
    /// for slot clicking, drag-and-drop (left-drag move, right-click split),
    /// and hover-tooltips. All mutations delegate to the core methods in the
    /// main partial (MoveItem, SplitStack, etc.).
    /// </summary>
    public partial class InventoryComponent
    {
        private int _draggedSlot = -1;
        private bool _isDragging;

        /// <summary>Process hover timers. Called from _Process in the main partial.</summary>
        private void ProcessInteraction(double delta)
        {
            ProcessHover(delta);
        }

        /// <summary>Handle mouse input on the grid. Call from _UnhandledInput or wire to grid GUI input.</summary>
        private void OnSlotGuiInput(InputEvent @event, int slot)
        {
            if (!IsActive) return;

            if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
            {
                if (mouseBtn.ButtonIndex == MouseButton.Left)
                {
                    // Start drag or click.
                    if (!IsSlotEmpty(slot))
                    {
                        _draggedSlot = slot;
                        _isDragging = true;
                    }
                    EmitSignal(SignalName.SlotClicked, slot);
                }
                else if (mouseBtn.ButtonIndex == MouseButton.Right)
                {
                    // Right-click: split stack in half.
                    if (!IsSlotEmpty(slot))
                    {
                        var entry = GetItemAt(slot);
                        if (entry != null && entry.Quantity > 1)
                            SplitStack(slot, entry.Quantity / 2);
                    }
                }
            }
            else if (@event is InputEventMouseButton mouseUp && !mouseUp.Pressed)
            {
                if (mouseUp.ButtonIndex == MouseButton.Left && _isDragging)
                {
                    // Drop onto target slot.
                    if (_draggedSlot >= 0 && _draggedSlot != slot)
                        MoveItem(_draggedSlot, slot);
                    _isDragging = false;
                    _draggedSlot = -1;
                }
            }
        }

        /// <summary>Handle mouse motion for hover detection on slots.</summary>
        private void OnSlotMouseEntered(int slot)
        {
            SetHoverSlot(slot);
        }

        private void OnSlotMouseExited()
        {
            SetHoverSlot(-1);
        }

        /// <summary>Sort the inventory using the currently-selected mode.</summary>
        public void SortInventory(SortMode mode = SortMode.ByType)
        {
            Sort(mode);
        }

        private SortMode _sortMode = SortMode.ByType;

        /// <summary>Cycle through sort modes. Tracks the current mode so each press actually advances
        /// (it used to reset to ByType every call and always sort ByRarity).</summary>
        public void CycleSortMode()
        {
            _sortMode = (SortMode)(((int)_sortMode + 1) % 4);
            Sort(_sortMode);
        }
    }
}
