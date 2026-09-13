using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Shared native terrain painting for square and diamond packs; publishes only after validation.</summary>
internal static class TerrainLibraryPainter
{
    public static int Update(TileMapLayer target, TerrainLibraryPack pack, Rect2I bounds,
        IEnumerable<Vector2I> changed, Func<Vector2I, string> kindAt, Func<Vector2I, string>? elevationAt = null)
    {
        var affected = new HashSet<Vector2I>();
        foreach (Vector2I cell in changed)
        {
            if (!bounds.HasPoint(cell)) continue;
            affected.Add(cell);
            for (int b = 0; b < 16; b++)
            {
                int remainder = b % 4;
                bool valid = pack.Projection == TerrainProjection.Tiles ? remainder is 0 or 3 : remainder is 1 or 2;
                if (!valid) continue;
                Vector2I neighbor = target.GetNeighborCell(cell, (TileSet.CellNeighbor)b);
                if (bounds.HasPoint(neighbor)) affected.Add(neighbor);
            }
        }
        if (affected.Count == 0) return 0;
        // Context stays outside the published patch so native matching can inspect its boundary.
        Rect2I area = default;
        bool first = true;
        foreach (Vector2I cell in affected)
        {
            var cellRect = new Rect2I(cell, Vector2I.One);
            area = first ? cellRect : area.Merge(cellRect);
            first = false;
        }
        area = area.Grow(2).Intersection(bounds);
        var scratch = new TileMapLayer { CollisionEnabled = false, NavigationEnabled = false };
        target.AddChild(scratch);
        scratch.Visible = false;
        try
        {
            foreach (int _ in Build(scratch, pack, area, kindAt, elevationAt)) { }
            foreach (Vector2I cell in affected)
                target.SetCell(cell, scratch.GetCellSourceId(cell), scratch.GetCellAtlasCoords(cell), scratch.GetCellAlternativeTile(cell));
        }
        finally { scratch.Free(); }
        return affected.Count;
    }

    public static IEnumerable<int> Build(TileMapLayer target, TerrainLibraryPack pack, Rect2I bounds,
        Func<Vector2I, string> kindAt, Func<Vector2I, string>? elevationAt = null)
    {
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0) throw new InvalidOperationException("Library map bounds must have positive dimensions.");
        var pending = new TileMapLayer { TileSet = pack.Tiles, CollisionEnabled = false, NavigationEnabled = false };
        target.AddChild(pending);
        pending.Visible = false;
        try
        {
            var elevationIndex = pack.ElevationIndex();
            var groups = new SortedDictionary<int, Godot.Collections.Array<Vector2I>>();
            var expected = new Dictionary<Vector2I, int>();
            for (int y = bounds.Position.Y; y < bounds.End.Y; y++)
                for (int x = bounds.Position.X; x < bounds.End.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    string kind = GridTerrainRules.Normalize(kindAt(cell));
                    if (!pack.TerrainBindings.TryGetValue(kind, out int terrain))
                        throw new InvalidOperationException($"Pack '{pack.PackId}' has no binding for '{kind}' at {cell}.");
                    expected[cell] = terrain;
                    if (terrain < 0) pending.SetCell(cell, pack.BackgroundSource, pack.BackgroundAtlas, pack.BackgroundAlternative);
                    else
                    {
                        if (!groups.TryGetValue(terrain, out var cells)) groups[terrain] = cells = new();
                        cells.Add(cell);
                    }
                    yield return 1;
                }
            foreach (var group in groups)
                for (int i = 0; i < group.Value.Count; i += 64)
                {
                    var batch = new Godot.Collections.Array<Vector2I>();
                    for (int n = i; n < Math.Min(i + 64, group.Value.Count); n++) batch.Add(group.Value[n]);
                    pending.SetCellsTerrainConnect(batch, pack.TerrainSet, group.Key, false);
                    yield return batch.Count;
                }
            foreach (var pair in expected)
            {
                // Empty-terrain background is explicit artwork, not an erased map cell.
                if (pair.Value < 0) pending.SetCell(pair.Key, pack.BackgroundSource, pack.BackgroundAtlas, pack.BackgroundAlternative);
                TileData? data = pending.GetCellTileData(pair.Key);
                if (data is null || data.Terrain != pair.Value)
                    throw new InvalidOperationException($"Terrain connection failed at {pair.Key}.");
                if (pack.ElevationCustomDataLayer.Length > 0)
                {
                    string profile = elevationAt?.Invoke(pair.Key) ?? "";
                    if (!elevationIndex.TryGetValue((TerrainLibraryPack.ConnectionKey(data), profile), out var tile))
                        throw new InvalidOperationException($"Pack '{pack.PackId}' has no matching elevation '{profile}' at {pair.Key}.");
                    pending.SetCell(pair.Key, tile.Source, tile.Atlas, tile.Alternative);
                }
                yield return 1;
            }
            foreach (Vector2I cell in pending.GetUsedCells()) if (!bounds.HasPoint(cell)) pending.EraseCell(cell);
            target.TileSet = pack.Tiles;
            target.TileMapData = pending.TileMapData;
            target.TextureFilter = pack.PixelArt ? CanvasItem.TextureFilterEnum.Nearest : CanvasItem.TextureFilterEnum.Linear;
        }
        finally { pending.Free(); }
    }
}
