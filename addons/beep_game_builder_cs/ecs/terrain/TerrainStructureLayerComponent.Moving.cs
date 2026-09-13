using Godot;

namespace Beep.ECS;

public partial class TerrainStructureLayerComponent
{
    private Godot.Collections.Dictionary _pendingStructureMove = new();
    [Export] public Godot.Collections.Dictionary PendingStructureMove
    {
        get => _pendingStructureMove;
        set { _pendingStructureMove = value; QueueRedraw(); }
    }

    public bool StageStructureMove(string placementId, Vector2I anchor)
    {
        Problem = "";
        if (!StructureEditActive || !CanEditInWorld())
        { if (Problem.Length == 0) Problem = "Begin structure editing first."; return false; }
        if (Working?.GetChildCount() > 0 || PendingStructureMove.Count > 0)
        { Problem = "Apply or discard the current additions or move first."; return false; }
        if (string.IsNullOrWhiteSpace(placementId) || placementId.ValidateNodeName() != placementId
            || GetNodeOrNull<Node2D>(new NodePath(placementId)) is not { } live || live.GetParent() != this)
        { Problem = "Select an existing direct structure instance."; return false; }
        if (!ValidateStaged(live)) return false;
        var footprint = live.GetMeta("terrain_structure_footprint").AsVector2I();
        if (!CellBounds.Encloses(new Rect2I(anchor, footprint)))
        { Problem = "Moved footprint would extend outside the cell bounds."; return false; }
        var mapping = GetNode<TileMapLayer>(MappingLayerPath);
        Vector2 pivot = live.GetMeta("terrain_structure_pivot").AsVector2();
        Transform2D after = live.Transform;
        after.Origin = ToLocal(mapping.ToGlobal(mapping.MapToLocal(anchor))) - after.BasisXform(pivot);
        PendingStructureMove = new()
        {
            ["name"] = placementId, ["before"] = live.Transform, ["after"] = after,
            ["old_anchor"] = live.GetMeta("terrain_structure_anchor"), ["new_anchor"] = anchor,
            ["structure"] = live.GetMeta("terrain_structure_id"),
            ["pack"] = live.GetMeta("terrain_structure_pack"), ["version"] = live.GetMeta("terrain_structure_version")
        };
        return true;
    }

    public Godot.Collections.Dictionary PrepareStructureMoveApply()
    {
        Problem = "";
        if (!StructureEditActive || PendingStructureMove.Count == 0 || !CanEditInWorld()) return new();
        var transaction = PendingStructureMove.Duplicate(true);
        if (!ValidateMove(transaction, true, out var live)) return new();
        transaction["instance"] = live;
        return transaction;
    }

    public bool CommitStructureMove(Godot.Collections.Dictionary transaction, bool forward)
    {
        Problem = "";
        if (!CanEditInWorld() || !ValidateMove(transaction, forward, out var live)) return false;
        if (PendingStructureMove.Count > 0 && (!forward
            || PendingStructureMove["name"].AsString() != transaction["name"].AsString()
            || PendingStructureMove["new_anchor"].AsVector2I() != transaction["new_anchor"].AsVector2I()))
        { Problem = "Another pending move must be resolved before applying history."; return false; }
        if (transaction.TryGetValue("instance", out var instance) && instance.AsGodotObject() != live)
        { Problem = "Structure instance was replaced; move history was retained without applying."; return false; }
        // Only placement is changed. Gameplay metadata and authored children stay on this instance.
        live.Transform = transaction[forward ? "after" : "before"].AsTransform2D();
        live.SetMeta("terrain_structure_anchor", transaction[forward ? "new_anchor" : "old_anchor"]);
        PendingStructureMove = forward ? new() : transaction.Duplicate(true);
        PendingStructureMove.Remove("instance");
        StructureEditActive = !forward;
        return true;
    }

    private bool ValidateMove(Godot.Collections.Dictionary transaction, bool forward, out Node2D live)
    {
        live = null!;
        foreach (string key in new[] { "name", "before", "after", "old_anchor", "new_anchor", "structure", "pack", "version" })
            if (!transaction.ContainsKey(key)) { Problem = "Incomplete move record; nothing changed."; return false; }
        string name = transaction["name"].AsString();
        if (name.ValidateNodeName() != name || GetNodeOrNull<Node2D>(new NodePath(name)) is not { } found || found.GetParent() != this)
        { Problem = "The original structure is missing."; return false; }
        live = found;
        if (!ValidateStaged(live)) return false;
        if (!live.Transform.IsEqualApprox(transaction[forward ? "before" : "after"].AsTransform2D())
            || live.GetMeta("terrain_structure_anchor").AsVector2I() != transaction[forward ? "old_anchor" : "new_anchor"].AsVector2I()
            || live.GetMeta("terrain_structure_id").AsString() != transaction["structure"].AsString()
            || live.GetMeta("terrain_structure_pack").AsString() != transaction["pack"].AsString()
            || live.GetMeta("terrain_structure_version").AsString() != transaction["version"].AsString())
        { Problem = "Live placement changed since staging; move was not applied."; return false; }
        var targetAnchor = transaction[forward ? "new_anchor" : "old_anchor"].AsVector2I();
        var target = transaction[forward ? "after" : "before"].AsTransform2D();
        var mapping = GetNode<TileMapLayer>(MappingLayerPath);
        if (!CellBounds.Encloses(new Rect2I(targetAnchor, live.GetMeta("terrain_structure_footprint").AsVector2I()))
            || !(GlobalTransform * target * live.GetMeta("terrain_structure_pivot").AsVector2())
                .IsEqualApprox(mapping.ToGlobal(mapping.MapToLocal(targetAnchor))))
        { Problem = "Move bounds or grid transform changed; restage the move."; return false; }
        if (Working?.GetChildCount() > 0)
        { Problem = "Apply or discard staged additions before moving a structure."; return false; }
        return true;
    }
}
