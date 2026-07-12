using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Menu action dispatcher. Attach as a child of a Control that contains Buttons.
    /// On each Button's <c>pressed</c> signal, emits <see cref="ActionTriggered"/>
    /// with the button's configured action name — so menu navigation/flow logic
    /// lives in ONE place (a NavigationComponent sibling) instead of per-button scripts.
    ///
    /// Buttons declare their action via their <c>Name</c> (by default) or a custom
    /// metadata key (<c>"action"</c>) set in the editor. Place a button named
    /// <c>PlayButton</c> and it fires action <c>"play"</c>; or set metadata
    /// <c>action = "start_game"</c> for explicit names.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MenuComponent : UIComponent
    {
        /// <summary>Metadata key read from each Button to find its action.</summary>
        [Export] public string ActionMetaKey { get; set; } = "action";

        /// <summary>If true, derive the action from the Button Name minus "Button" (PlayButton→play).</summary>
        [Export] public bool InferActionFromName { get; set; } = true;

        /// <summary>If non-empty, only buttons whose action is in this list are wired.</summary>
        [Export] public string[] KnownActions { get; set; } = System.Array.Empty<string>();

        /// <summary>OPTIONAL button effect: if enabled, each menu button gets a click-ripple
        /// (a RippleComponent child). Off by default — set true in the inspector for polish.</summary>
        [Export] public bool EnableRipple { get; set; } = false;
        [Export] public Color RippleColor { get; set; } = new(1f, 1f, 1f, 0.3f);

        /// <summary>Fires when a menu button is pressed. <paramref name="action"/> is the button's action name.</summary>
        [Signal] public delegate void ActionTriggeredEventHandler(string action);

        private readonly List<Button> _buttons = new();

        public override void _Ready()
        {
            base._Ready();
            // Defer so children are guaranteed to be in the tree.
            CallDeferred(nameof(WireButtons));
        }

        private bool _navWired;

        /// <summary>(Re)discover sibling Buttons and connect their pressed signals.</summary>
        public void WireButtons()
        {
            // Auto-wire to a sibling NavigationComponent once: ActionTriggered → Dispatch.
            // This makes the menu flow work out-of-the-box with no [connection] blocks
            // or .gd glue — place MenuComponent + NavigationComponent as siblings.
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

            // Disconnect any previously-wired buttons before re-binding.
            foreach (var b in _buttons)
            {
                if (GodotObject.IsInstanceValid(b))
                    b.Pressed -= OnButtonPressed;
            }
            _buttons.Clear();

            // Scan ALL descendant buttons (not just direct children) — buttons are
            // typically nested inside Center/MenuVBox, Panel/VBox, etc.
            if (GetParent() is not Node parent) return;
            FindButtonsRecursive(parent);
        }

        /// <summary>Recursively find all Button nodes under parent and wire them.</summary>
        private void FindButtonsRecursive(Node parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is Button btn)
                {
                    _buttons.Add(btn);
                    btn.Pressed += OnButtonPressed;

                    // Optional ripple effect: attach a RippleComponent child to the
                    // button if one isn't already present.
                    if (EnableRipple && !btn.HasNode("Ripple"))
                    {
                        var ripple = new RippleComponent
                        {
                            Name = "Ripple",
                            RippleColor = RippleColor
                        };
                        btn.AddChild(ripple);
                    }
                }
                // Recurse into containers (VBox, HBox, Panel, Margin, etc.)
                if (child is not Button && child.GetChildCount() > 0)
                    FindButtonsRecursive(child);
            }
        }

        private void OnButtonPressed()
        {
            if (!IsActive) return;
            // The sender is whichever button fired; iterate to find the one whose
            // Pressed signal invoked this handler (Godot doesn't pass it directly).
            // We resolve by checking the tree's current focus owner as a fallback.
            string action = ResolveAction();
            if (action.Length == 0) return;
            if (KnownActions.Length > 0 && !KnownActions.Contains(action)) return;
            EmitSignal(SignalName.ActionTriggered, action);
        }

        private string ResolveAction()
        {
            // Prefer the focused button (the one just clicked keeps focus).
            var focusOwner = GetViewport().GuiGetFocusOwner();
            if (focusOwner is Button btn)
                return ActionFor(btn);
            // Fallback: last button in our list (press ordering is reliable in practice).
            if (_buttons.Count > 0 && GodotObject.IsInstanceValid(_buttons[^1]))
                return ActionFor(_buttons[^1]);
            return "";
        }

        private string ActionFor(Button btn)
        {
            // Explicit metadata wins.
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var b in _buttons)
                    if (GodotObject.IsInstanceValid(b))
                        b.Pressed -= OnButtonPressed;
            }
            base.Dispose(disposing);
        }
    }
}
