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

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (_control == null) return;

            _menu = new PopupMenu();
            _menu.Name = "ContextMenu";
            var items = MenuItems.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < items.Length; i++)
                _menu.AddItem(items[i].Trim(), i);
            _menu.IndexPressed += idx =>
                EmitSignal(SignalName.MenuItemSelected, (int)idx, items[(int)idx].Trim());
            _control.AddChild(_menu);

            _control.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    _menu.Position = new Vector2I((int)mb.GlobalPosition.X, (int)mb.GlobalPosition.Y);
                    _menu.Popup();
                }
            };
        }

        public void SetItems(string[] items)
        {
            MenuItems = string.Join("\n", items);
            _menu?.Clear();
            if (_menu != null)
                for (int i = 0; i < items.Length; i++)
                    _menu.AddItem(items[i], i);
        }
    }
}
