using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Menu component. Discovers all descendant Buttons and connects each one's
    /// Pressed signal directly to a handler that resolves the action and emits
    /// ActionTriggered. The sibling NavigationComponent listens and navigates.
    ///
    /// Simple: button press → resolve action → emit signal → NavigationComponent.Dispatch.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MenuComponent : UIComponent
    {
        [Export] public string ActionMetaKey { get; set; } = "action";
        [Export] public bool InferActionFromName { get; set; } = true;
        [Export] public string[] KnownActions { get; set; } = System.Array.Empty<string>();
        [Export] public bool EnableRipple { get; set; } = false;
        [Export] public Color RippleColor { get; set; } = new(1, 1, 1, 0.3f);

        [Signal] public delegate void ActionTriggeredEventHandler(string action);

        private readonly List<Button> _buttons = new();
        private bool _navWired;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(WireButtons));
        }

        public void WireButtons()
        {
            // Connect to sibling NavigationComponent once.
            if (!_navWired && GetParent() is Node p)
            {
                foreach (var sibling in p.GetChildren())
                {
                    if (sibling is NavigationComponent nav && sibling != this)
                    {
                        ActionTriggered += nav.Dispatch;
                        _navWired = true;
                        break;
                    }
                }
            }

            // Disconnect old.
            foreach (var b in _buttons)
                if (GodotObject.IsInstanceValid(b))
                    b.Pressed -= OnButtonPressed;
            _buttons.Clear();

            if (GetParent() is not Node parent) return;
            FindButtonsRecursive(parent);
        }

        private void FindButtonsRecursive(Node parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is Button btn && !_buttons.Contains(btn))
                {
                    _buttons.Add(btn);
                    btn.Pressed += OnButtonPressed;

                    if (EnableRipple && !btn.HasNode("Ripple"))
                    {
                        var ripple = new RippleComponent { Name = "Ripple", RippleColor = RippleColor };
                        btn.AddChild(ripple);
                    }
                }
                if (child is not Button && child.GetChildCount() > 0)
                    FindButtonsRecursive(child);
            }
        }

        private void OnButtonPressed()
        {
            if (!IsActive) return;
            // Resolve which button was pressed using the focus owner.
            var focus = GetViewport().GuiGetFocusOwner();
            if (focus is Button btn)
            {
                string action = ActionFor(btn);
                if (KnownActions.Length == 0 || System.Array.IndexOf(KnownActions, action) >= 0)
                    EmitSignal(SignalName.ActionTriggered, action);
            }
        }

        private string ActionFor(Button btn)
        {
            if (btn.HasMeta(ActionMetaKey))
                return btn.GetMeta(ActionMetaKey).AsString();
            if (InferActionFromName)
            {
                string n = btn.Name.ToString();
                if (n.EndsWith("Button", System.StringComparison.OrdinalIgnoreCase))
                    n = n[..^"Button".Length];
                return n.ToSnakeCase();
            }
            return btn.Name.ToString();
        }

        public override void _ExitTree()
        {
            foreach (var btn in _buttons)
                if (GodotObject.IsInstanceValid(btn))
                    btn.Pressed -= OnButtonPressed;

            if (_navWired && GetParent() is Node p)
            {
                foreach (var sibling in p.GetChildren())
                {
                    if (sibling is NavigationComponent nav && sibling != this)
                    {
                        ActionTriggered -= nav.Dispatch;
                        break;
                    }
                }
            }
        }
    }
}
