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
        // Per-button Pressed handlers, so the pressed button is captured directly (see
        // OnButtonPressed) rather than guessed via the focus owner — a mouse/touch press that
        // doesn't move focus used to resolve the wrong button or none.
        private readonly Dictionary<Button, System.Action> _handlers = new();
        private bool _navWired;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(WireButtons));
        }

        public void WireButtons()
        {
            if (Engine.IsEditorHint()) return;
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
            foreach (var kvp in _handlers)
                if (GodotObject.IsInstanceValid(kvp.Key))
                    kvp.Key.Pressed -= kvp.Value;
            _handlers.Clear();
            _buttons.Clear();

            if (GetParent() is not Node parent) return;
            FindButtonsRecursive(parent);

            if (_buttons.Count == 0)
                GD.PushWarning($"[{Name}] MenuComponent found no Button descendants under its parent — there is nothing to wire.");
            else if (!_navWired)
                GD.PushWarning($"[{Name}] MenuComponent found no sibling NavigationComponent — button presses emit ActionTriggered but nothing navigates. Add a NavigationComponent alongside it, or handle ActionTriggered yourself.");
        }

        private void FindButtonsRecursive(Node parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is Button btn && !_buttons.Contains(btn))
                {
                    _buttons.Add(btn);
                    System.Action handler = () => OnButtonPressed(btn);
                    _handlers[btn] = handler;
                    btn.Pressed += handler;

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

        private void OnButtonPressed(Button btn)
        {
            if (!IsActive || !GodotObject.IsInstanceValid(btn)) return;
            string action = ActionFor(btn);
            if (KnownActions.Length == 0 || System.Array.IndexOf(KnownActions, action) >= 0)
                EmitSignal(SignalName.ActionTriggered, action);
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
            base._ExitTree();
            foreach (var kvp in _handlers)
                if (GodotObject.IsInstanceValid(kvp.Key))
                    kvp.Key.Pressed -= kvp.Value;
            _handlers.Clear();

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
            // Re-arm so a remove-then-re-add + WireButtons() reconnects Navigation — otherwise the
            // nav-wire block is skipped and buttons emit ActionTriggered with nothing dispatching.
            _navWired = false;
        }
    }
}
