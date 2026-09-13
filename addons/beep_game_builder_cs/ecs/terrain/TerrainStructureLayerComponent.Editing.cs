using Godot;

namespace Beep.ECS;

public partial class TerrainStructureLayerComponent
{
    private const string WorkingName = "StructureWorkingCopy";
    [Export] public bool StructureEditActive { get; set; }

    private Node2D? Working => GetNodeOrNull<Node2D>(WorkingName);

    public bool HasPendingStructureEdits() => StructureEditActive || Working?.GetChildCount() > 0 || PendingStructureMove.Count > 0;

    public bool BeginStructureEdit()
    {
        Problem = "";
        if (!IsInsideTree()) { Problem = "Structure layer must be in a scene."; return false; }
        if (!CanEditInWorld()) return false;
        if (HasNode(WorkingName) && Working is null)
        { Problem = "The working-copy name is occupied by another node."; return false; }
        if (Working is null)
        {
            var working = new Node2D { Name = WorkingName };
            AddChild(working);
            TerrainAuthoring.Adopt(working, this);
        }
        StructureEditActive = true;
        return true;
    }

    public Node2D? StageStructure(string placementId, string structureId, Vector2I anchor)
    {
        if (PendingStructureMove.Count > 0) return Reject("Apply or discard the pending move first.");
        if (!StructureEditActive || Working is null) return Reject("Begin structure editing first.");
        if (Working.Transform != Transform2D.Identity) return Reject("Working-copy transform must remain identity.");
        return PlaceInto(Working, placementId, structureId, anchor);
    }

    public Godot.Collections.Array<Node> PrepareStructureApply()
    {
        Problem = "";
        var nodes = new Godot.Collections.Array<Node>();
        if (PendingStructureMove.Count > 0) { Problem = "Prepare the pending move instead of additions."; return nodes; }
        if (!StructureEditActive || Working is null) { Problem = "No active structure edit."; return nodes; }
        foreach (Node child in Working.GetChildren())
        {
            if (!ValidateStaged(child)) return new();
            if (HasNode(new NodePath(child.Name))) { Problem = "A placement ID conflicts with the live scene."; return new(); }
            nodes.Add(child);
        }
        if (nodes.Count == 0) Problem = "No staged structures to apply.";
        return nodes;
    }

    // Reparent the same instances so authored child state is not reconstructed on undo.
    public bool CommitStructures(Godot.Collections.Array<Node> nodes, bool forward)
    {
        Problem = "";
        if (!CanEditInWorld()) return false;
        if (PendingStructureMove.Count > 0) { Problem = "Resolve the pending move before applying addition history."; return false; }
        var working = Working;
        if (working is null || working.Transform != Transform2D.Identity)
        { Problem = "Structure working copy is missing or transformed."; return false; }
        Node source = forward ? working : this;
        Node destination = forward ? this : working;
        var seen = new System.Collections.Generic.HashSet<Node>();
        foreach (var node in nodes)
        {
            if (!GodotObject.IsInstanceValid(node) || !seen.Add(node) || node.GetParent() != source || destination.HasNode(new NodePath(node.Name)))
            { Problem = "Structure history conflicts with scene changes; nothing was moved."; return false; }
            if (!ValidateStaged(node)) return false;
        }
        foreach (var node in nodes)
        {
            node.Reparent(destination, false);
            TerrainAuthoring.Adopt(node, this);
        }
        StructureEditActive = !forward;
        return true;
    }

    private bool CanEditInWorld()
    {
        if (!IsInsideTree()) { Problem = "Structure layer must be in a scene."; return false; }
        foreach (var node in GetTree().GetNodesInGroup(TerrainWorldComponent.StructureWorldGroup))
        {
            if (node is not TerrainWorldComponent world || !world.ManagesStructureLayer(this)) continue;
            if (world.IsGenerating)
            { Problem = "Wait for world generation to finish before editing structures."; return false; }
            if (LibraryPack is null || world.Projection != LibraryPack.Projection)
            { Problem = "Switch to this structure pack's projection before editing or applying history."; return false; }
        }
        return true;
    }

    private bool ValidateStaged(Node node)
    {
        var pack = LibraryPack;
        var mapping = GetNodeOrNull<TileMapLayer>(MappingLayerPath);
        string id = node.GetMeta("terrain_structure_id", "").AsString();
        if (pack is null || mapping is null || mapping.TileSet != pack.Tiles || pack.Validate(pack.Projection).Length > 0
            || node is not Node2D visual || !pack.StructureLayouts.TryGetValue(id, out var layout) || layout is null
            || !pack.Structures.TryGetValue(id, out var scene) || layout.Validate().Length > 0
            || node.GetMeta("terrain_structure_pack", "").AsString() != pack.PackId
            || node.GetMeta("terrain_structure_version", "").AsString() != pack.Version
            || node.GetMeta("terrain_structure_projection", -1).AsInt32() != (int)pack.Projection
            || node.GetMeta("terrain_structure_scene", default).AsGodotObject() != scene
            || node.GetMeta("terrain_structure_pivot", Vector2.Inf).AsVector2() != layout.Pivot
            || node.GetMeta("terrain_structure_footprint", Vector2I.Zero).AsVector2I() != layout.Footprint
            || node.GetMeta("terrain_structure_rise_pixels", -1).AsInt32() != layout.RisePixels
            || visual.ZIndex != layout.SortOffset)
        { Problem = "Structure contract changed or is missing; retain this copy and restage before Apply."; return false; }
        var anchor = node.GetMeta("terrain_structure_anchor").AsVector2I();
        if (!CellBounds.Encloses(new Rect2I(anchor, layout.Footprint))
            || !visual.ToGlobal(layout.Pivot).IsEqualApprox(mapping.ToGlobal(mapping.MapToLocal(anchor))))
        { Problem = "Structure anchor, bounds or layer transform changed; restage before Apply."; return false; }
        return true;
    }

    public void DiscardStructures()
    {
        PendingStructureMove = new();
        // Only the explicitly staged additions belong to this disposable container.
        if (Working is { } working)
            foreach (Node node in working.GetChildren()) { working.RemoveChild(node); node.QueueFree(); }
        StructureEditActive = false;
        Problem = "";
    }
}
