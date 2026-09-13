using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Opt-in art bindings. Tile resources remain usable without this engine resource.</summary>
[Tool, GlobalClass]
public partial class TerrainLibraryPack : Resource
{
    [Export] public string PackId { get; set; } = "";
    [Export] public string Version { get; set; } = "1.0.0";
    [Export] public TerrainProjection Projection { get; set; } = TerrainProjection.Tiles;
    [Export] public bool PixelArt { get; set; }
    [Export] public TileSet? Tiles { get; set; }
    [Export] public int TerrainSet { get; set; }
    [Export] public Godot.Collections.Dictionary<string, int> TerrainBindings { get; set; } = new();
    [Export] public bool RequireCompleteBinaryConnections { get; set; } = true;
    // A binary overlay may use Godot's empty terrain (-1) as an opaque background.
    [Export] public int BackgroundSource { get; set; } = -1;
    [Export] public Vector2I BackgroundAtlas { get; set; }
    [Export] public int BackgroundAlternative { get; set; }
    [Export] public Godot.Collections.Dictionary<string, int> ElevationRisePixels { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> Structures { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, TerrainStructureLayout> StructureLayouts { get; set; } = new();

    internal string RenderKey()
    {
        var key = new System.Text.StringBuilder();
        key.Append(GetInstanceId()).Append('|').Append(Tiles?.GetInstanceId()).Append('|').Append(TerrainSet)
            .Append('|').Append(Projection).Append('|').Append(PixelArt).Append('|').Append(Version)
            .Append('|').Append(BackgroundSource).Append('|').Append(BackgroundAtlas).Append('|').Append(BackgroundAlternative);
        foreach (var pair in new SortedDictionary<string, int>(TerrainBindings)) key.Append('|').Append(pair.Key).Append('=').Append(pair.Value);
        AppendElevationContract(key);
        return key.ToString();
    }

    public string Validate(TerrainProjection view)
    {
        if (string.IsNullOrWhiteSpace(PackId) || string.IsNullOrWhiteSpace(Version)) return "Pack ID and version are required.";
        if (Projection != view || view is not (TerrainProjection.Tiles or TerrainProjection.IsometricAutotile))
            return $"Pack '{PackId}' is for {Projection}, not {view}.";
        if (Tiles is null) return "Assign a prepared TileSet.";
        var shape = view == TerrainProjection.Tiles ? TileSet.TileShapeEnum.Square : TileSet.TileShapeEnum.Isometric;
        var size = view == TerrainProjection.Tiles ? new Vector2I(64, 64) : new Vector2I(64, 32);
        if (Tiles.TileShape != shape || Tiles.TileSize != size) return $"New {view} packs require {shape} cells of {size}.";
        if (TerrainSet < 0 || TerrainSet >= Tiles.GetTerrainSetsCount()) return "Terrain set does not exist.";
        if (TerrainBindings.Count == 0) return "At least one logical terrain binding is required.";
        var ids = new HashSet<int>();
        foreach (var pair in TerrainBindings)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key != GridTerrainRules.Normalize(pair.Key))
                return $"Terrain key '{pair.Key}' must be normalized.";
            if (pair.Value < -1 || pair.Value >= Tiles.GetTerrainsCount(TerrainSet)) return $"Invalid terrain ID for '{pair.Key}'.";
            ids.Add(pair.Value);
        }
        if (ids.Contains(-1))
        {
            if (!Tiles.HasSource(BackgroundSource) || Tiles.GetSource(BackgroundSource) is not TileSetAtlasSource bg
                || !bg.HasTile(BackgroundAtlas) || !bg.HasAlternativeTile(BackgroundAtlas, BackgroundAlternative)
                || bg.GetTileData(BackgroundAtlas, BackgroundAlternative).Terrain != -1)
                return "An explicit empty-terrain background tile is required for binding -1.";
        }
        foreach (int rise in ElevationRisePixels.Values)
            if (rise < 0 || rise % 16 != 0) return "Elevation rises must be non-negative multiples of 16 game pixels.";
        var signatures = new Dictionary<int, HashSet<int>>();
        if (RequireCompleteBinaryConnections && ids.Count != 2)
            return "A complete binary pack must bind exactly two terrain IDs (one may be -1).";
        foreach (int id in ids) if (id >= 0) signatures[id] = new();
        bool cornersAndSides = Tiles.GetTerrainSetMode(TerrainSet) == TileSet.TerrainMode.CornersAndSides;
        if (RequireCompleteBinaryConnections && !cornersAndSides) return "47-configuration validation requires Match Corners and Sides.";
        int[]? bitOrder = null;
        for (int s = 0; s < Tiles.GetSourceCount(); s++)
        {
            if (Tiles.GetSource(Tiles.GetSourceId(s)) is not TileSetAtlasSource atlas) continue;
            if (atlas.Texture is null) return "An atlas texture is missing.";
            for (int t = 0; t < atlas.GetTilesCount(); t++)
            {
                Vector2I coord = atlas.GetTileId(t);
                int frames = atlas.GetTileAnimationFramesCount(coord);
                if (frames > 1 && atlas.GetTileAnimationSpeed(coord) <= 0) return "Animated tile has no playback speed.";
                for (int f = 0; f < frames; f++)
                {
                    Rect2I region = atlas.GetTileTextureRegion(coord, f);
                    if (!new Rect2I(Vector2I.Zero, (Vector2I)atlas.Texture.GetSize()).Encloses(region))
                        return $"Tile {coord}, frame {f} lies outside its texture.";
                }
                for (int a = 0; a < atlas.GetAlternativeTilesCount(coord); a++)
                {
                    TileData data = atlas.GetTileData(coord, atlas.GetAlternativeTileId(coord, a));
                    if (data.TerrainSet != TerrainSet || !signatures.TryGetValue(data.Terrain, out var masks)) continue;
                    var valid = new List<int>();
                    for (int b = 0; b < 16; b++) if (data.IsValidTerrainPeeringBit((TileSet.CellNeighbor)b)) valid.Add(b);
                    bitOrder ??= valid.ToArray();
                    int mask = 0;
                    for (int b = 0; b < valid.Count; b++)
                    {
                        int peer = data.GetTerrainPeeringBit((TileSet.CellNeighbor)valid[b]);
                        if (peer < -1 || peer >= Tiles.GetTerrainsCount(TerrainSet)) return "Invalid terrain peering ID.";
                        if (RequireCompleteBinaryConnections && peer >= 0 && !ids.Contains(peer)) return "Binary pack references an unbound third terrain.";
                        if (peer == data.Terrain) mask |= 1 << b;
                    }
                    masks.Add(mask);
                }
            }
        }
        foreach (var pair in signatures)
        {
            if (pair.Value.Count == 0) return $"Terrain {pair.Key} has no assigned tiles.";
            if (!RequireCompleteBinaryConnections) continue;
            if (bitOrder?.Length != 8) return "Expected eight valid peering positions for this projection.";
            // Neighbor enums run clockwise. Square sides are 0 mod 4; diamond sides are 2 mod 4.
            int sideRemainder = shape == TileSet.TileShapeEnum.Square ? 0 : 2;
            for (int mask = 0; mask < 256; mask++)
            {
                bool legal = true;
                for (int b = 0; b < 8; b++)
                    if (bitOrder[b] % 4 != sideRemainder && (mask & (1 << b)) != 0
                        && ((mask & (1 << ((b + 7) % 8))) == 0 || (mask & (1 << ((b + 1) % 8))) == 0)) legal = false;
                if (legal && !pair.Value.Contains(mask)) return $"Terrain {pair.Key} is missing connection mask {mask}.";
            }
        }
        return ValidateElevationProfiles();
    }
}
