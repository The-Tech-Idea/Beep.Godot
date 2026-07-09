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
    }
}
