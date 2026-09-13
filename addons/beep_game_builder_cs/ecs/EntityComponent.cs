using Godot;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

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
            => FindDirectComponent<T>(GetParent(), this);

        private static readonly ConditionalWeakTable<Node, DirectChildren> DirectComponentChildren = new();

        // Shared by every component on a body; invalidate on native structural changes.
        private sealed class DirectChildren
        {
            private Node[]? _children;
            public DirectChildren(Node parent) => parent.ChildOrderChanged += Invalidate;
            private void Invalidate() => _children = null;
            public Node[] Read(Node parent) => _children ??= parent.GetChildren().ToArray();
        }

        internal static T? FindDirectComponent<T>(Node? parent, Node? except = null) where T : Node
        {
            foreach (Node child in ReadDirectChildren(parent))
                if (child is T match && child != except && GodotObject.IsInstanceValid(child)) return match;
            return null;
        }

        internal static ReadOnlySpan<Node> ReadDirectChildren(Node? parent)
            => GodotObject.IsInstanceValid(parent)
                ? DirectComponentChildren.GetValue(parent!, static body => new(body)).Read(parent!)
                : ReadOnlySpan<Node>.Empty;

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

        /// <summary>
        /// The collaborator behind an exported NodePath: the authored path when
        /// there is one, otherwise the first matching component in the scene.
        /// Caches through <paramref name="cached"/> and re-resolves whenever
        /// that reference goes stale, so a collaborator swapped or freed at
        /// runtime is picked back up instead of leaving a dangling read.
        ///
        /// This is composition's one wiring rule, in one place. It was written
        /// out by hand 113 times across 38 files as
        /// <c>!Path.IsEmpty ? GetNodeOrNull&lt;T&gt;(Path) : IsInsideTree() ?
        /// FindComponent&lt;T&gt;(GetTree()?.CurrentScene) : null</c> - and copies
        /// drift: some cached, some re-resolved, some forgot the
        /// IsInstanceValid check that makes a freed node recoverable.
        ///
        /// Explicit path first, scene search second, is deliberate: a scene that
        /// wires a collaborator explicitly always wins over one that happens to
        /// be found, so adding a second component of a type cannot silently
        /// re-point everything that was searching for it.
        /// </summary>
        /// <param name="owner">The component doing the resolving. Static and
        /// owner-taking rather than protected, because plenty of the components
        /// that need this derive straight from <see cref="Node"/> rather than
        /// from EntityComponent - composition should not require a base class.</param>
        public static T? Resolve<T>(Node owner, NodePath path, ref T? cached) where T : class
        {
            if (cached is GodotObject existing && GodotObject.IsInstanceValid(existing))
                return cached;

            cached = !path.IsEmpty
                ? owner.GetNodeOrNull<Node>(path) as T
                : owner.IsInsideTree() ? FindComponent<T>(owner.GetTree()?.CurrentScene) : null;

            return cached;
        }

        /// <summary>The same rule, for a component that already is an EntityComponent.</summary>
        protected T? Resolve<T>(NodePath path, ref T? cached) where T : class
            => Resolve(this, path, ref cached);

        /// <summary>
        /// Resolve a non-empty path AFRESH every call, so a live re-point of the path to a
        /// different node is picked up, and fall back to the cached/tree-searched
        /// <see cref="Resolve{T}(Node, NodePath, ref T)"/> when the path is empty. Use this instead
        /// of <see cref="Resolve{T}(Node, NodePath, ref T)"/> where an inspector edit that re-points a
        /// path at another live node must take effect without a cache invalidation - Resolve returns
        /// the still-valid cached reference and would keep the stale node.
        ///
        /// This is the ONE owner of the fresh-resolve rule. Pass
        /// <paramref name="fallbackWhenEmpty"/> false for a collaborator that must stay
        /// explicit-only - the grid's <c>_grid</c> is the case that matters: an unwired path means
        /// "this component has no projection", never "adopt whichever one the scene happens to
        /// hold", so it resolves to null rather than scene-searching. Naming the policy here
        /// keeps both behaviours in one place; the alternative was every caller writing its own
        /// <c>path.IsEmpty ? null : GetNodeOrNull&lt;T&gt;(path)</c> copy of the rule.
        /// </summary>
        /// <param name="owner">The component doing the resolving; static and owner-taking because the
        /// callers derive straight from <see cref="Node"/> rather than from EntityComponent.</param>
        /// <param name="fallbackWhenEmpty">Whether an empty path falls back to the cached
        /// tree-searched <see cref="Resolve{T}(Node, NodePath, ref T)"/>. False clears the cache
        /// instead, for a caller that must not adopt a collaborator it was not wired to.</param>
        public static T? ResolveLive<T>(Node owner, NodePath path, ref T? cached, bool fallbackWhenEmpty = true) where T : class
        {
            if (!path.IsEmpty) return cached = owner.GetNodeOrNull<Node>(path) as T;
            return fallbackWhenEmpty ? Resolve(owner, path, ref cached) : cached = null;
        }
    }
}
