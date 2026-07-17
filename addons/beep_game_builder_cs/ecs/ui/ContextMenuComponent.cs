using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Right-click context menu component. Attach to any Godot.Control.
    /// Blind — works for any UI element needing a popup menu.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ContextMenuComponent : UIComponent
    {
        [Export(PropertyHint.MultilineText)]
        public string MenuItems { get; set; } = "Option 1\nOption 2\nOption 3";

        [Signal] public delegate void MenuItemSelectedEventHandler(int index, string label);

        private Godot.Control? _control;
        private PopupMenu? _menu;
        private string[] _cachedItems = System.Array.Empty<string>();

        public override void _Ready()
        {
            base._Ready();
            // Runtime only: this injects a PopupMenu into the PARENT and hooks its input.
            // Unlike a self-building widget (which builds its own internals and should be
            // visible at design time), this is [Tool] adding nodes to someone else's scene —
            // in the editor that just litters the tree.
            if (Engine.IsEditorHint()) return;

            _control = GetParent() as Godot.Control;
            if (_control == null) return;

            _menu = new PopupMenu();
            _menu.Name = "ContextMenu";
            RebuildMenu();
            _control.AddChild(_menu);

            _control.GuiInput += OnControlGuiInput;
        }

        private void RebuildMenu()
        {
            if (_menu == null) return;
            _menu.Clear();
            _cachedItems = MenuItems.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < _cachedItems.Length; i++)
                _menu.AddItem(_cachedItems[i].Trim(), i);
            _menu.IndexPressed += OnMenuItemPressed;
        }

        private void OnMenuItemPressed(long idx)
        {
            int index = (int)idx;
            if (index >= 0 && index < _cachedItems.Length)
                EmitSignal(SignalName.MenuItemSelected, index, _cachedItems[index].Trim());
        }

        private void OnControlGuiInput(InputEvent e)
        {
            if (!IsActive || _menu == null) return;
            if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                _menu.Position = (Vector2I)mb.GlobalPosition;
                _menu.Popup();
                GetViewport()?.SetInputAsHandled();
            }
        }

        public void SetItems(string[] items)
        {
            MenuItems = string.Join("\n", items);
            RebuildMenu();
        }

        public override void _ExitTree()
        {
            if (_menu != null && GodotObject.IsInstanceValid(_menu))
                _menu.QueueFree();
            if (_control != null)
                _control.GuiInput -= OnControlGuiInput;
        }
    }
}
