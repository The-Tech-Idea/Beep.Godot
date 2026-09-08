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
        private int _carrySlot = -1;

        /// <summary>The slot lifted for a keyboard or gamepad move, or -1 when nothing is held.</summary>
        public int CarrySlot => _carrySlot;

        /// <summary>Emitted when a slot is lifted or put down, with the held slot or -1. Lets a
        /// view mark what is in hand without polling.</summary>
        [Signal] public delegate void CarryChangedEventHandler(int slot);

        /// <summary>Process hover timers. Called from _Process in the main partial.</summary>
        private void ProcessInteraction(double delta)
        {
            ProcessHover(delta);
        }

        /// <summary>Handle mouse input on the grid. Call from _UnhandledInput or wire to grid GUI input.</summary>
        private void OnSlotGuiInput(InputEvent @event, int slot)
        {
            if (!IsActive) return;

            // Keyboard and gamepad reordering. Everything below this branches on
            // InputEventMouseButton, so until now an inventory could only be rearranged with a
            // pointing device: MoveItem existed and had exactly one route to it, a mouse drag.
            if (@event.IsActionPressed("ui_accept"))
            {
                CarryOrPlace(slot);
                return;
            }
            if (@event.IsActionPressed("ui_cancel") && _carrySlot >= 0)
            {
                CancelCarry();
                return;
            }

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

        /// <summary>
        /// Lift the slot, or put down what is already held onto it.
        ///
        /// This is the same <see cref="MoveItem"/> the mouse drag reaches, driven by pressing
        /// ui_accept twice — once to lift, once to place — so a player on a controller can
        /// rearrange an inventory at all. Lifting an empty slot does nothing rather than picking
        /// up a hole; placing onto the slot already held simply puts it back.
        /// </summary>
        public void CarryOrPlace(int slot)
        {
            if (!IsActive || slot < 0 || slot >= EffectiveSlotCount) return;

            if (_carrySlot < 0)
            {
                if (IsSlotEmpty(slot)) return;
                SetCarry(slot);
                return;
            }

            int from = _carrySlot;
            SetCarry(-1);
            if (from != slot) MoveItem(from, slot);
        }

        /// <summary>Put down whatever is held, leaving it where it came from.</summary>
        public void CancelCarry() => SetCarry(-1);

        private void SetCarry(int slot)
        {
            if (_carrySlot == slot) return;
            _carrySlot = slot;
            EmitSignal(SignalName.CarryChanged, slot);
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
