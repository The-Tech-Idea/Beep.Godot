using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Observes an explicitly scoped resource subtree without owning resource balances.</summary>
internal sealed class TerrainResourceViewBinding(Action changed) : IDisposable
{
    private Node? _root;
    private SceneTree? _tree;
    private readonly HashSet<GridResourceNodeComponent> _nodes = new();

    public void Bind(Node? root)
    {
        if (_root == root) return;
        Dispose();
        _root = root;
        if (_root is null || !_root.IsInsideTree()) return;
        Collect(_root);
        _tree = _root.GetTree();
        _tree.NodeAdded += OnAdded;
        _tree.NodeRemoved += OnRemoved;
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

    private void Collect(Node node)
    {
        Track(node);
        foreach (Node child in node.GetChildren()) Collect(child);
    }

    private void Track(Node node)
    {
        if (node is GridResourceNodeComponent resource && _nodes.Add(resource))
            resource.ResourceChanged += OnChanged;
    }

    private void OnAdded(Node node)
    {
        if (node is not GridResourceNodeComponent || !GodotObject.IsInstanceValid(_root)
            || (node != _root && !_root!.IsAncestorOf(node))) return;
        Track(node);
        changed();
    }

    private void OnRemoved(Node node)
    {
        if (node is not GridResourceNodeComponent resource || !_nodes.Remove(resource)) return;
        resource.ResourceChanged -= OnChanged;
        changed();
    }

    public void Dispose()
    {
        if (GodotObject.IsInstanceValid(_tree))
        {
            _tree!.NodeAdded -= OnAdded;
            _tree.NodeRemoved -= OnRemoved;
        }
        foreach (var node in _nodes)
            if (GodotObject.IsInstanceValid(node)) node.ResourceChanged -= OnChanged;
        _nodes.Clear();
        _root = null;
        _tree = null;
    }

    private void OnChanged() => changed();
}
