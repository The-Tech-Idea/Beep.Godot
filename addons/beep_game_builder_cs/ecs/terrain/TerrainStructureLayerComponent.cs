using Godot;

namespace Beep.ECS;

/// <summary>Opt-in authored scene placement; never writes logical terrain or navigation data.</summary>
[Tool, GlobalClass]
public partial class TerrainStructureLayerComponent : Node2D
{
    [Export] public TerrainLibraryPack? LibraryPack { get; set; }
    [Export] public NodePath MappingLayerPath { get; set; } = new("");
    [Export] public Rect2I CellBounds { get; set; } = new(Vector2I.Zero, new Vector2I(64, 64));
    public string Problem { get; private set; } = "";

    public string StructureSetupProblem()
    {
        if (LibraryPack is null) return "Assign a TerrainLibraryPack.";
        if (MappingLayerPath.IsEmpty || GetNodeOrNull<TileMapLayer>(MappingLayerPath) is not { } mapping)
            return "Assign MappingLayerPath to the pack's TileMapLayer.";
        if (mapping.TileSet is null || mapping.TileSet != LibraryPack.Tiles)
            return "Mapping layer must use the selected pack's TileSet.";
        if (CellBounds.Size.X <= 0 || CellBounds.Size.Y <= 0) return "Cell bounds must have positive dimensions.";
        if (LibraryPack.Structures.Count == 0) return "The selected pack has no authored structure scenes.";
        return "";
    }

    public string[] ExistingStructureIds()
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (Node child in GetChildren())
            if (child is Node2D && child.HasMeta("terrain_structure_id") && child.HasMeta("terrain_structure_anchor"))
                names.Add(child.Name.ToString());
        names.Sort(System.StringComparer.Ordinal);
        return names.ToArray();
    }

    public Node2D? Place(string placementId, string structureId, Vector2I anchor)
        => PlaceInto(this, placementId, structureId, anchor);

    private Node2D? PlaceInto(Node target, string placementId, string structureId, Vector2I anchor)
    {
        Problem = "";
        if (!IsInsideTree()) return Reject("Structure layer must be inside the scene tree.");
        if (string.IsNullOrWhiteSpace(placementId) || placementId.ValidateNodeName() != placementId)
            return Reject("Placement ID must be a valid, non-empty node name.");
        if (HasNode(new NodePath(placementId))) return Reject("Placement ID already exists; existing structures were retained.");
        if (target.HasNode(new NodePath(placementId))) return Reject("Placement ID already exists in the working copy.");
        var pack = LibraryPack;
        var mapping = GetNodeOrNull<TileMapLayer>(MappingLayerPath);
        if (pack is null || mapping is null || mapping.TileSet != pack.Tiles)
            return Reject("Assign a library pack and a mapping layer using its TileSet.");
        string validation = pack.Validate(pack.Projection);
        if (validation.Length > 0) return Reject(validation);
        if (!pack.Structures.TryGetValue(structureId, out var scene) || scene is null
            || !pack.StructureLayouts.TryGetValue(structureId, out var layout) || layout is null)
            return Reject("Structure requires both an authored scene and a layout in the selected pack.");
        validation = layout.Validate();
        if (validation.Length > 0) return Reject(validation);
        if (!CellBounds.Encloses(new Rect2I(anchor, layout.Footprint)))
            return Reject("Structure footprint extends outside the configured cell bounds.");
        if (Mathf.IsZeroApprox(GlobalTransform.Determinant()) || Mathf.IsZeroApprox(mapping.GlobalTransform.Determinant()))
            return Reject("Placement layers require invertible transforms.");
        if (!scene.CanInstantiate()) return Reject("Structure scene cannot be instantiated.");

        Node instance = scene.Instantiate();
        if (instance is not Node2D visual)
        {
            instance.Free();
            return Reject("Structure scenes must have a Node2D root.");
        }
        // Preserve authored rotation/scale and convert the native grid basis, including
        // transforms on either layer. Rise describes the art; it is not an extra lift.
        Transform2D basis = GlobalTransform.AffineInverse() * mapping.GlobalTransform;
        Transform2D authored = visual.Transform;
        authored.Origin = mapping.MapToLocal(anchor) - authored.BasisXform(layout.Pivot);
        visual.Transform = basis * authored;
        visual.Name = placementId;
        visual.ZIndex = layout.SortOffset;
        visual.SetMeta("terrain_structure_id", structureId);
        visual.SetMeta("terrain_structure_pack", pack.PackId);
        visual.SetMeta("terrain_structure_version", pack.Version);
        visual.SetMeta("terrain_structure_anchor", anchor);
        visual.SetMeta("terrain_structure_footprint", layout.Footprint);
        visual.SetMeta("terrain_structure_rise_pixels", layout.RisePixels);
        visual.SetMeta("terrain_structure_projection", (int)pack.Projection);
        visual.SetMeta("terrain_structure_pivot", layout.Pivot);
        visual.SetMeta("terrain_structure_scene", scene);
        target.AddChild(visual);
        TerrainAuthoring.Adopt(visual, this);
        return visual;
    }

    private Node2D? Reject(string reason)
    {
        Problem = reason;
        return null;
    }
}
