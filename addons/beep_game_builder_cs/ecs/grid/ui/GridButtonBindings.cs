using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A panel's set of <see cref="Button.Pressed"/> subscriptions, connected and released
    /// as a unit. Replaces the per-panel <c>_connectedButtons</c> list the three button bars
    /// carried and the single-button connect/disconnect pairs the calendar and worker-spawner
    /// panels carried:
    /// <list type="bullet">
    /// <item><see cref="Bind"/> connects a handler and records it, and is idempotent per
    /// button so a panel that re-binds the same authored button on refresh does not
    /// double-subscribe.</item>
    /// <item><see cref="UnbindAll"/> disconnects every recorded handler - skipping a button
    /// already freed - and clears, so a panel disposes its wiring in one call.</item>
    /// </list>
    /// </summary>
    public sealed class GridButtonBindings
    {
        private readonly List<(Button Button, Action Handler)> _bound = new();

        /// <summary>True when this button already has a handler bound here.</summary>
        public bool IsBound(Button button)
        {
            foreach (var (b, _) in _bound)
                if (b == button) return true;
            return false;
        }

        /// <summary>Connect <paramref name="handler"/> to the button and record it. A button already bound here is left untouched.</summary>
        public void Bind(Button button, Action handler)
        {
            if (IsBound(button)) return;
            button.Pressed += handler;
            _bound.Add((button, handler));
        }

        /// <summary>Disconnect every recorded handler (skipping a freed button) and clear.</summary>
        public void UnbindAll()
        {
            foreach ((Button button, Action handler) in _bound)
                if (GodotObject.IsInstanceValid(button))
                    button.Pressed -= handler;
            _bound.Clear();
        }
    }
}
