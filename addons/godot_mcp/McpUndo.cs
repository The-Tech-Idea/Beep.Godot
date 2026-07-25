using System;
using Godot;

namespace GodotMcp;

/// <summary>
/// Routes agent edits through Godot's own undo history.
///
/// Every write the bridge made previously mutated the edited scene directly, so Ctrl-Z
/// did not see it. The only way to recover from a wrong agent edit was to close the scene
/// without saving — which throws away the good edits too. That made leaving agent access
/// enabled genuinely risky.
///
/// With this, an agent edit is an ordinary undo entry labelled "MCP: …", so a user can
/// scan the history and see exactly what was done on their behalf and step back through
/// it. It is also what makes Phase 1's batch safe: a whole batch commits as ONE entry.
///
/// Outside the editor (runtime role, or no EditorInterface) there is no undo manager;
/// <see cref="Begin"/> returns a scope that simply applies the action directly, so
/// callers need no branching.
/// </summary>
public sealed class McpUndoScope : IDisposable
{
#if TOOLS
    private readonly EditorUndoRedoManager? _undo;
#endif
    private readonly bool _active;
    private bool _committed;

    private McpUndoScope(bool active, object? undo)
    {
        _active = active;
#if TOOLS
        _undo = undo as EditorUndoRedoManager;
#endif
    }

    /// <summary>Open an undo action. Dispose commits it (or discards it on an exception,
    /// which is what makes an atomic batch atomic).
    ///
    /// The manager comes from the EditorPlugin — it is NOT on EditorInterface — so the
    /// caller passes it in. Outside the editor there is none and edits apply directly.</summary>
    public static McpUndoScope Begin(string label, object? undoManager)
    {
#if TOOLS
        if (Engine.IsEditorHint() && undoManager is EditorUndoRedoManager undo)
        {
            undo.CreateAction($"MCP: {label}");
            return new McpUndoScope(true, undo);
        }
#endif
        return new McpUndoScope(false, null);
    }

    /// <summary>Set a property, recording the old value as the undo step.</summary>
    public void SetProperty(GodotObject target, string property, Variant value)
    {
#if TOOLS
        if (_active && _undo != null)
        {
            Variant before = target.Get(property);
            _undo.AddDoProperty(target, property, value);
            _undo.AddUndoProperty(target, property, before);
            return;
        }
#endif
        target.Set(property, value);
    }

    /// <summary>Add a child, with removal as the undo step. AddDoReference hands the node's
    /// lifetime to the undo system so an undone creation is not leaked.</summary>
    public void AddChild(Node parent, Node child, Node owner)
    {
#if TOOLS
        if (_active && _undo != null)
        {
            _undo.AddDoMethod(parent, Node.MethodName.AddChild, child);
            _undo.AddDoProperty(child, Node.PropertyName.Owner, owner);
            _undo.AddDoReference(child);
            _undo.AddUndoMethod(parent, Node.MethodName.RemoveChild, child);
            return;
        }
#endif
        parent.AddChild(child);
        child.Owner = owner;
    }

    /// <summary>Remove a node, with re-adding as the undo step.</summary>
    public void RemoveChild(Node parent, Node child, Node owner)
    {
#if TOOLS
        if (_active && _undo != null)
        {
            _undo.AddDoMethod(parent, Node.MethodName.RemoveChild, child);
            _undo.AddUndoMethod(parent, Node.MethodName.AddChild, child);
            _undo.AddUndoProperty(child, Node.PropertyName.Owner, owner);
            _undo.AddUndoReference(child);
            return;
        }
#endif
        parent.RemoveChild(child);
        child.QueueFree();
    }

    /// <summary>Reparent, with the original parent as the undo step.</summary>
    public void Reparent(Node node, Node oldParent, Node newParent)
    {
#if TOOLS
        if (_active && _undo != null)
        {
            _undo.AddDoMethod(node, Node.MethodName.Reparent, newParent, true);
            _undo.AddUndoMethod(node, Node.MethodName.Reparent, oldParent, true);
            return;
        }
#endif
        node.Reparent(newParent);
    }

    /// <summary>Abandon the action — nothing is committed. Used when a batch aborts.</summary>
    public void Discard()
    {
#if TOOLS
        if (_active && _undo != null && !_committed)
        {
            _committed = true;
            // Committing with execute:false then undoing is the supported way to drop a
            // half-built action; there is no CancelAction in EditorUndoRedoManager.
            _undo.CommitAction(false);
        }
#endif
        _committed = true;
    }

    public void Dispose()
    {
#if TOOLS
        if (_active && _undo != null && !_committed)
        {
            _committed = true;
            _undo.CommitAction();
        }
#endif
        _committed = true;
    }

    /// <summary>True when edits are actually landing in the editor's undo history.
    /// Reported back to the agent so "is this reversible?" is answerable.</summary>
    public bool IsUndoable => _active;
}
