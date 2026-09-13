using Godot;
using System;

namespace Beep.ECS;

public partial class TerrainLibraryEditSession
{
    /// <summary>Prepare a native tile-data edit; callers register the before/after bytes with editor undo.</summary>
    public Godot.Collections.Dictionary PrepareElevationPaint(Rect2I region, string profile)
    {
        Problem = "";
        var empty = new Godot.Collections.Dictionary();
        if (!Active || Pack is null || WorkingLayer is not { } working || working.TileSet != Pack.Tiles)
        { Problem = "Begin a terrain edit session first."; return empty; }
        Problem = Pack.Validate(Pack.Projection);
        if (Problem.Length > 0) return empty;
        if (StablePackKey() != BaselinePackKey) { Problem = "Pack changed during editing."; return empty; }
        if (!Pack.ElevationValues.ContainsKey(profile)) { Problem = "Choose an authored elevation profile."; return empty; }
        if (region.Size.X <= 0 || region.Size.Y <= 0 || !new Rect2I(BoundsOrigin, BoundsSize).Encloses(region))
        { Problem = "Elevation region must be inside the map with positive dimensions."; return empty; }
        var scratch = new TileMapLayer { TileSet = Pack.Tiles, TileMapData = (byte[])working.TileMapData.Clone() };
        try
        {
            var index = Pack.ElevationIndex();
            for (int y = region.Position.Y; y < region.End.Y; y++)
                for (int x = region.Position.X; x < region.End.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    var tile = scratch.GetCellTileData(cell);
                    if (tile is null || !index.TryGetValue((TerrainLibraryPack.ConnectionKey(tile), profile), out var choice))
                    { Problem = $"No authored elevation connection at {cell}; paint its ground first."; return empty; }
                    scratch.SetCell(cell, choice.Source, choice.Atlas, choice.Alternative);
                }
            return new() { ["before"] = (byte[])working.TileMapData.Clone(), ["after"] = scratch.TileMapData };
        }
        catch (Exception error) { Problem = error.Message; return empty; }
        finally { scratch.Free(); }
    }
}
