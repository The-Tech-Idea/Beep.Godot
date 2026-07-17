using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for all Entity Components.
    /// Components are BLIND — they don't know what entity they're attached to.
    /// They only expose data and emit signals. The parent entity configures them.
    ///
    /// Usage: Add as a child of any Node. The parent entity reads data via GetNode&lt;T&gt;()
    /// and connects to signals. Systems find components by group membership.
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class EntityComponent : Node
    {
        /// <summary>
        /// Name of the component group this belongs to (e.g., "power_sources", "injured_players").
        /// Systems use groups to find relevant entities.
        /// </summary>
        [Export]
        public string ComponentGroup { get; set; } = "";

        /// <summary>
        /// Whether this component is active. Systems skip inactive components.
        /// </summary>
        [Export]
        public bool IsActive { get; set; } = true;

        public override void _EnterTree()
        {
            if (!string.IsNullOrEmpty(ComponentGroup))
                AddToGroup(ComponentGroup);
        }

        public override void _ExitTree()
        {
            if (!string.IsNullOrEmpty(ComponentGroup))
                RemoveFromGroup(ComponentGroup);
        }

        /// <summary>
        /// Try to find a component of type T on the parent entity.
        /// Returns null if not found.
        /// </summary>
        protected T? GetSiblingComponent<T>() where T : EntityComponent
        {
            if (GetParent() == null) return null;
            foreach (var child in GetParent().GetChildren())
            {
                if (child is T comp && child != this)
                    return comp;
            }
            return null;
        }

        /// <summary>
        /// Find the first node of type <typeparamref name="T"/> under <paramref name="root"/>,
        /// matching by TYPE.
        ///
        /// Use this instead of <c>root.FindChild(nameof(T)) as T</c>. FindChild matches a node's
        /// NAME, so it only worked if the node happened to be named after its class — and the
        /// scenes name nodes semantically ("Health", "Seasonal", "Weather"), never
        /// "HealthComponent". Every such lookup silently returned null, which is why attacks
        /// dealt no damage and nothing reacted to the weather system.
        /// </summary>
        /// <param name="root">Where to search. Its own children are checked; root itself is not.</param>
        /// <param name="recursive">Search descendants too. Mirrors FindChild's second argument.</param>
        public static T? FindComponent<T>(Node? root, bool recursive = true) where T : class
        {
            if (root == null) return null;
            foreach (var child in root.GetChildren())
            {
                if (child is T match) return match;
                if (recursive && FindComponent<T>(child, true) is { } deeper) return deeper;
            }
            return null;
        }
    }
}
