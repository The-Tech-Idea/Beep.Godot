using Godot;
using System;
using Beep.ECS.UI.Kit;

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
        private KitContextMenu? _menu;
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
            if (_control == null)
            {
                GD.PushWarning($"[{Name}] ContextMenuComponent needs a Control parent to catch right-clicks; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the Control it should open on.");
                return;
            }

            _menu = new KitContextMenu { Name = "ContextMenu" };
            _menu.ItemSelected += OnMenuItemPressed;
            RebuildMenu();
            _control.AddChild(_menu);

            _control.GuiInput += OnControlGuiInput;
        }

        private void RebuildMenu()
        {
            if (_menu == null) return;
            _cachedItems = MenuItems.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < _cachedItems.Length; i++) _cachedItems[i] = _cachedItems[i].Trim();
            _menu.SetItems(_cachedItems);
        }

        private void OnMenuItemPressed(int index, string label)
        {
            if (index >= 0 && index < _cachedItems.Length)
                EmitSignal(SignalName.MenuItemSelected, index, label);
        }

        private void OnControlGuiInput(InputEvent e)
        {
            if (!IsActive || _menu == null) return;
            if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                _menu.PopupAt(mb.GlobalPosition);
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
            base._ExitTree();
            if (_menu != null && GodotObject.IsInstanceValid(_menu))
                _menu.QueueFree();
            if (_control != null && GodotObject.IsInstanceValid(_control))
                _control.GuiInput -= OnControlGuiInput;
        }
    }
}
