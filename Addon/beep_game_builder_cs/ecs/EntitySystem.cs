using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for Entity Systems.
    /// Systems track and update components by group. They process all entities
    /// that have matching components each update cycle.
    ///
    /// Usage: Add as a child of your game world node. Call ProcessAll() from _Process().
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class EntitySystem : Node
    {
        /// <summary>
        /// The component group this system tracks (e.g., "power_sources", "training_players").
        /// </summary>
        [Export]
        public string TrackedGroup { get; set; } = "";

        /// <summary>
        /// Get all entities in the tracked group.
        /// </summary>
        protected List<Node> GetEntities()
        {
            if (string.IsNullOrEmpty(TrackedGroup)) return new List<Node>();
            var nodes = new List<Node>();
            foreach (var node in GetTree().GetNodesInGroup(TrackedGroup))
                if (node is Node n) nodes.Add(n);
            return nodes;
        }

        /// <summary>
        /// Get a specific component from an entity.
        /// </summary>
        protected static T? GetComponent<T>(Node entity) where T : EntityComponent
        {
            foreach (var child in entity.GetChildren())
            {
                if (child is T comp) return comp;
            }
            return null;
        }

        /// <summary>
        /// Get all components of type T from an entity.
        /// </summary>
        protected static List<T> GetComponents<T>(Node entity) where T : EntityComponent
        {
            var result = new List<T>();
            foreach (var child in entity.GetChildren())
            {
                if (child is T comp) result.Add(comp);
            }
            return result;
        }

        /// <summary>
        /// Process all entities in this system's group. Override in subclasses.
        /// </summary>
        public abstract void ProcessAll(double delta);
    }
}
