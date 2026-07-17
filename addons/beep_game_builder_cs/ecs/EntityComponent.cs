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
        /// Group joined by THIS COMPONENT NODE (e.g., "power_sources", "fog_layer").
        /// Use when something looks for the component itself — the shape
        /// <c>GetNodesInGroup(g).OfType&lt;SomeComponent&gt;()</c>.
        ///
        /// This is NOT how you tag an entity as a player or an enemy — a component is a
        /// <see cref="Node"/>, not a <see cref="Node2D"/>, and every targeting lookup in this
        /// addon filters <c>is Node2D</c> (AIController, TurretComponent,
        /// ProjectileModifierComponent), so a component in "players" is silently skipped.
        /// Use <see cref="EntityGroup"/> for that.
        /// </summary>
        [Export]
        public string ComponentGroup { get; set; } = "";

        /// <summary>
        /// Group joined by the PARENT ENTITY (the body this component hangs off) — e.g.
        /// "players", "enemies". This is the tag targeting reads:
        /// <see cref="AIController"/>, <see cref="TurretComponent"/> and
        /// <see cref="ProjectileModifierComponent"/> all scan a group and keep only Node2D bodies.
        ///
        /// Spawned entities are grouped by <see cref="SpawnerComponent.SpawnGroup"/> instead;
        /// this covers entities authored directly into a scene, such as the player.
        /// </summary>
        [Export]
        public string EntityGroup { get; set; } = "";

        /// <summary>
        /// Whether this component is active. Systems skip inactive components.
        /// </summary>
        [Export]
        public bool IsActive { get; set; } = true;

        public override void _EnterTree()
        {
            if (!string.IsNullOrEmpty(ComponentGroup))
                AddToGroup(ComponentGroup);

            if (string.IsNullOrEmpty(EntityGroup)) return;

            var entity = GetParent();
            if (entity == null)
            {
                GD.PushWarning(
                    $"{GetType().Name} ('{Name}'): EntityGroup is '{EntityGroup}' but this " +
                    "component has no parent, so nothing was grouped. Make it a child of the " +
                    "entity body.");
                return;
            }

            entity.AddToGroup(EntityGroup);

            // Targeting keeps only Node2D. Grouping a non-Node2D parent succeeds and then finds
            // nothing, which is indistinguishable from "no enemies exist".
            if (entity is not Node2D)
                GD.PushWarning(
                    $"{GetType().Name} ('{Name}'): EntityGroup '{EntityGroup}' was applied to " +
                    $"parent '{entity.Name}' ({entity.GetType().Name}), which is not a Node2D. " +
                    "AIController/TurretComponent/ProjectileModifierComponent skip non-Node2D " +
                    "members, so this entity will never be targeted. Parent this component to " +
                    "the body (CharacterBody2D/Area2D/Node2D).");
        }

        public override void _ExitTree()
        {
            if (!string.IsNullOrEmpty(ComponentGroup))
                RemoveFromGroup(ComponentGroup);

            if (!string.IsNullOrEmpty(EntityGroup))
                GetParent()?.RemoveFromGroup(EntityGroup);
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
