using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Observes an explicitly scoped resource subtree without owning resource balances.</summary>
internal sealed class TerrainResourceViewBinding(Action changed) : IDisposable
{
    private Node? _root;
    private readonly HashSet<Node> _observed = new();
    private readonly HashSet<GridResourceNodeComponent> _nodes = new();

    public void Bind(Node? root)
    {
        if (_root == root) return;
        Dispose();
        _root = root;
        if (_root is null || !_root.IsInsideTree()) return;
        Observe(_root);
    }

    public IEnumerable<(Vector2I Cell, string Resource)> Entries()
    {
        foreach (var node in _nodes)
        {
            if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion() || node.IsDepleted) continue;
            Vector2I cell = node.CurrentCell();
            if (cell.X == int.MinValue || cell.Y == int.MinValue) continue;
            string id = GridTerrainRules.Normalize(node.ResourceId);
            if (id.Length > 0) yield return (cell, id);
        }
    }

    /// <summary>
    /// Observe the subtree from the nodes themselves, never from the engine's SceneTree.
    ///
    /// This used to subscribe SceneTree.NodeAdded/NodeRemoved and filter the callbacks with
    /// IsAncestorOf. The SceneTree is engine-owned: it outlives an assembly reload, and _ExitTree
    /// does NOT run on one, so the delegate stayed attached to the live tree with a dead method
    /// handle. That pins the assembly it came from - which is what makes ".NET: Failed to unload
    /// assemblies" happen at all - and then spams node_added/node_removed until the scene is
    /// reopened. ChildEnteredTree/ChildExitingTree are emitted by the subtree's own nodes, so an
    /// addition anywhere under the root is still seen. Owners must also Dispose before assembly
    /// serialization: this helper is not a GodotObject, and its delegates cannot be restored as
    /// node-method targets on hot reload. Scene-scoped signals alone do not cover that lifetime.
    /// </summary>
    private void Observe(Node node)
    {
        if (!GodotObject.IsInstanceValid(node) || !_observed.Add(node)) return;
        node.ChildEnteredTree += OnChildEntered;
        node.ChildExitingTree += OnChildExiting;
        Track(node);
        foreach (Node child in node.GetChildren()) Observe(child);
    }

    private void Unobserve(Node node)
    {
        // Not observed means nothing below it was either: Observe only ever recurses from an
        // observed node, so the descendants cannot be holding subscriptions we would miss here.
        if (!_observed.Remove(node)) return;
        foreach (Node child in node.GetChildren()) Unobserve(child);
        if (GodotObject.IsInstanceValid(node))
        {
            node.ChildEnteredTree -= OnChildEntered;
            node.ChildExitingTree -= OnChildExiting;
        }
        Untrack(node);
    }

    private void OnChildEntered(Node node)
    {
        int tracked = _nodes.Count;
        Observe(node);
        // Rebuild only when a resource actually appeared, so an authored container or a stray
        // helper node does not cost a redraw.
        if (_nodes.Count != tracked) changed();
    }

    private void OnChildExiting(Node node)
    {
        bool wasTracked = node is GridResourceNodeComponent resource && _nodes.Contains(resource);
        Unobserve(node);
        if (wasTracked) changed();
    }

    private void Track(Node node)
    {
        if (node is GridResourceNodeComponent resource && _nodes.Add(resource))
            resource.ResourceChanged += OnChanged;
    }

    private void Untrack(Node node)
    {
        if (node is not GridResourceNodeComponent resource || !_nodes.Remove(resource)) return;
        if (GodotObject.IsInstanceValid(resource)) resource.ResourceChanged -= OnChanged;
    }

    public void Dispose()
    {
        foreach (Node node in _observed)
            if (GodotObject.IsInstanceValid(node))
            {
                node.ChildEnteredTree -= OnChildEntered;
                node.ChildExitingTree -= OnChildExiting;
            }
        _observed.Clear();
        foreach (var node in _nodes)
            if (GodotObject.IsInstanceValid(node)) node.ResourceChanged -= OnChanged;
        _nodes.Clear();
        _root = null;
    }

    private void OnChanged() => changed();
}
