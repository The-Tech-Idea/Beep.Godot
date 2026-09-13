using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Beep.ECS;

public partial class TerrainLibraryPack
{
    public const string ElevationProfileMetadata = "terrain_library_elevation";

    [Export] public string ElevationCustomDataLayer { get; set; } = "";
    [Export] public Godot.Collections.Dictionary<string, float> ElevationValues { get; set; } = new();

    internal string TileElevationId(TileData? tile) => tile is null || ElevationCustomDataLayer.Length == 0
        ? "" : tile.GetCustomData(ElevationCustomDataLayer).AsString();

    internal string ElevationAt(GridCellDataComponent? cells, Vector2I cell) => ElevationCustomDataLayer.Length == 0 || cells is null
        ? "" : cells.GetMetadata(cell, ElevationProfileMetadata).AsString();

    internal void AppendElevationContract(StringBuilder key)
    {
        key.Append('|').Append(ElevationCustomDataLayer);
        foreach (var pair in new SortedDictionary<string, float>(ElevationValues))
            key.Append('|').Append(pair.Key).Append('=').Append(pair.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        foreach (var pair in new SortedDictionary<string, int>(ElevationRisePixels)) key.Append('|').Append(pair.Key).Append('=').Append(pair.Value);
    }

    internal static string ConnectionKey(TileData data)
    {
        var key = new StringBuilder().Append(data.TerrainSet).Append(':').Append(data.Terrain);
        for (int b = 0; b < 16; b++)
            if (data.IsValidTerrainPeeringBit((TileSet.CellNeighbor)b)) key.Append(':').Append(data.GetTerrainPeeringBit((TileSet.CellNeighbor)b));
        return key.ToString();
    }

    internal Dictionary<(string Connection, string Profile), (int Source, Vector2I Atlas, int Alternative)> ElevationIndex()
    {
        var result = new Dictionary<(string Connection, string Profile), (int Source, Vector2I Atlas, int Alternative)>();
        if (Tiles is null || ElevationCustomDataLayer.Length == 0) return result;
        for (int s = 0; s < Tiles.GetSourceCount(); s++)
        {
            int source = Tiles.GetSourceId(s);
            if (Tiles.GetSource(source) is not TileSetAtlasSource atlas) continue;
            for (int t = 0; t < atlas.GetTilesCount(); t++)
            {
                var coord = atlas.GetTileId(t);
                for (int a = 0; a < atlas.GetAlternativeTilesCount(coord); a++)
                {
                    int alt = atlas.GetAlternativeTileId(coord, a);
                    var data = atlas.GetTileData(coord, alt);
                    if (data.TerrainSet != TerrainSet || !TerrainBindings.Values.Contains(data.Terrain)) continue;
                    string profile = TileElevationId(data);
                    if (profile.Length > 0 && data.Probability != 0)
                        throw new InvalidOperationException("Profiled tiles must have probability zero so ordinary terrain brushes cannot change elevation randomly.");
                    var key = (ConnectionKey(data), profile);
                    if (!result.TryGetValue(key, out var old)
                        || profile.Length == 0 && data.Probability > 0
                            && ((TileSetAtlasSource)Tiles.GetSource(old.Source)).GetTileData(old.Atlas, old.Alternative).Probability <= 0)
                        result[key] = (source, coord, alt);
                }
            }
        }
        return result;
    }

    internal string ValidateElevationProfiles()
    {
        if (ElevationCustomDataLayer.Length == 0)
            return ElevationValues.Count == 0 ? "" : "Elevation values require a named custom-data layer.";
        if (Tiles is null || ElevationValues.Count == 0) return "Elevation authoring requires explicit profile values.";
        int layer = Tiles.GetCustomDataLayerByName(ElevationCustomDataLayer);
        if (layer < 0 || Tiles.GetCustomDataLayerType(layer) != Variant.Type.String) return "Elevation custom data must be a String layer.";
        foreach (var pair in ElevationValues)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key != GridTerrainRules.Normalize(pair.Key)) return "Elevation profile IDs must be nonempty and normalized.";
            if (!float.IsFinite(pair.Value) || pair.Value < 0 || pair.Value > 1) return "Logical elevation values must be finite and between 0 and 1, not pixel heights.";
            if (!ElevationRisePixels.ContainsKey(pair.Key)) return $"Elevation profile '{pair.Key}' has no visual rise.";
        }
        Dictionary<(string Connection, string Profile), (int Source, Vector2I Atlas, int Alternative)> index;
        try { index = ElevationIndex(); }
        catch (InvalidOperationException error) { return error.Message; }
        if (TerrainBindings.Values.Contains(-1) && ((TileSetAtlasSource)Tiles.GetSource(BackgroundSource))
            .GetTileData(BackgroundAtlas, BackgroundAlternative).TerrainSet != TerrainSet)
            return "Elevation background must belong to the pack's terrain set.";
        var connections = new HashSet<string>();
        foreach (var key in index.Keys)
        {
            connections.Add(key.Connection);
            if (key.Profile.Length > 0 && !ElevationValues.ContainsKey(key.Profile)) return $"Unknown tile elevation profile '{key.Profile}'.";
        }
        // Base variants keep ordinary painting independent of height. Each profile
        // must cover the same authored connections, including an opaque background.
        foreach (string connection in connections)
        {
            if (!index.ContainsKey((connection, ""))) return "An elevation connection has no unprofiled base tile.";
            var baseTile = index[(connection, "")];
            if (((TileSetAtlasSource)Tiles.GetSource(baseTile.Source)).GetTileData(baseTile.Atlas, baseTile.Alternative).Probability <= 0)
                return "Each elevation connection needs an unprofiled base tile with positive probability.";
            foreach (string profile in ElevationValues.Keys)
                if (!index.ContainsKey((connection, profile))) return $"Elevation profile '{profile}' is missing an authored connection.";
        }
        return "";
    }
}
