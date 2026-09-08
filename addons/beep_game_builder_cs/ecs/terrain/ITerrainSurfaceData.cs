using Godot;

namespace Beep.ECS;

internal interface ITerrainSurfaceData
{
    string TerrainAtCell(Vector2I cell);
    string WaterSourceAtCell(Vector2I cell);
    TerrainRelief ReliefAtCell(Vector2I cell);
    float ElevationAtCell(Vector2I cell);
    string FeatureAtCell(Vector2I cell);
}

/// <summary>Local generation coordinates over an absolute live-cell source.</summary>
internal sealed class OffsetTerrainSurfaceData(ITerrainSurfaceData source, Vector2I origin) : ITerrainSurfaceData
{
    public string TerrainAtCell(Vector2I cell) => source.TerrainAtCell(origin + cell);
    public string WaterSourceAtCell(Vector2I cell) => source.WaterSourceAtCell(origin + cell);
    public TerrainRelief ReliefAtCell(Vector2I cell) => source.ReliefAtCell(origin + cell);
    public float ElevationAtCell(Vector2I cell) => source.ElevationAtCell(origin + cell);
    public string FeatureAtCell(Vector2I cell) => source.FeatureAtCell(origin + cell);
}

internal sealed class LiveTerrainSurfaceData(GridCellDataComponent cells) : ITerrainSurfaceData
{
    public string TerrainAtCell(Vector2I cell) => GridCellRules.TerrainKindAt(cells, cell);
    public string WaterSourceAtCell(Vector2I cell)
    {
        if (!TerrainTileSets.IsWaterKind(TerrainAtCell(cell))) return "";
        var value = cells.GetMetadata(cell, "terrain_water_source");
        return value.VariantType == Variant.Type.String ? value.AsString() : "lake";
    }
    public TerrainRelief ReliefAtCell(Vector2I cell)
        => (TerrainRelief)Mathf.Clamp(GridVariantReader.Int(cells.GetMetadata(cell, "terrain_relief"), 0), 0, 2);
    public float ElevationAtCell(Vector2I cell)
        => Mathf.Clamp(GridVariantReader.Float(cells.GetMetadata(cell, "terrain_elevation"), 0f), 0f, 1f);
    public string FeatureAtCell(Vector2I cell)
    {
        string kind = TerrainAtCell(cell);
        if (TerrainTileSets.IsWaterKind(kind) || kind == "lava" || cells.HasFlag(cell, GridCellDataComponent.CellFlags.Cleared)) return "";
        var feature = cells.GetMetadata(cell, "terrain_feature");
        return feature.VariantType == Variant.Type.String ? GridTerrainRules.Normalize(feature.AsString()) : "";
    }
}
