using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class TerrainWorldComponent
{
    internal const string StructureWorldGroup = "terrain_library_structure_worlds";

    private IEnumerable<TerrainStructureLayerComponent> RegisteredStructureLayers()
    {
        if (!IsInsideTree()) yield break;
        var seen = new HashSet<TerrainStructureLayerComponent>();
        foreach (var path in StructureLayerPaths)
            if (!path.IsEmpty && GetNodeOrNull<TerrainStructureLayerComponent>(path) is { } layer && seen.Add(layer))
                yield return layer;
    }

    public bool ManagesStructureLayer(TerrainStructureLayerComponent target)
    {
        foreach (var layer in RegisteredStructureLayers()) if (layer == target) return true;
        return false;
    }

    public void RefreshStructureVisibility()
    {
        foreach (var layer in RegisteredStructureLayers())
            layer.Visible = layer.LibraryPack is { } pack
                && pack.Projection is TerrainProjection.Tiles or TerrainProjection.IsometricAutotile
                && pack.Projection == Projection;
    }
}
