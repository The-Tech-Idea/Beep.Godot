using Beep.ECS;
using Godot;
using System.Collections.Generic;

/// <summary>
/// Climate and woodland measurements for terrain_climate_share_probe.gd, taken on the generated field directly.
///
/// A GDScript probe reading a 144x144 map through TerrainGeneratorComponent's per-cell accessors rebuilds
/// the forty-field settings record on every call, twice a cell; the field answers the same questions in
/// one pass.
///
/// The recipe is Oilfield Days' shape (Continents, land 0.6, climate maps and scale rules on) with the
/// climate span either custom or derived from the size - the case FIX-15 is about.
/// </summary>
public partial class TerrainClimateShareSmoke : Node
{
    /// <summary>
    /// Land cells by terrain kind for one generated map, plus "land" for their total, and the map's own
    /// "lake_coverage" and "river_coverage" diagnostics. A span of zero keeps the scale rules' own span;
    /// above zero it is the custom span.
    /// </summary>
    public Godot.Collections.Dictionary Shares(Vector2I size, int temperature, int rainfall, int seed, float span)
    {
        var generator = Configure(size, temperature, rainfall, seed, span);
        try
        {
            GeneratedTerrainField field = TerrainFieldBuilder.Build(generator.CaptureGenerationSettings());
            var result = new Godot.Collections.Dictionary
            {
                ["lake_coverage"] = field.Diagnostics.LakeCoverage,
                ["river_coverage"] = field.Diagnostics.RiverCoverage,
            };
            int land = 0;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (field.WaterSourceAtCell(cell).Length > 0) continue;
                land++;
                string kind = field.TerrainAtCell(cell);
                result[kind] = result.TryGetValue(kind, out Variant counted) ? counted.AsInt32() + 1 : 1;
            }
            result["land"] = land;
            return result;
        }
        finally { generator.Free(); }
    }

    /// <summary>
    /// On a whole-world map (span one): the share of coastal land (within 3 cells of water) that is desert
    /// or dry grass as "coast", and the same share of interior land (12 cells or more from water) as
    /// "interior", with the cell counts behind each.
    /// </summary>
    public Godot.Collections.Dictionary CoastAndInteriorDryness(Vector2I size, int seed)
    {
        var generator = Configure(size, 1, 1, seed, 1.0f);
        try
        {
            GeneratedTerrainField field = TerrainFieldBuilder.Build(generator.CaptureGenerationSettings());
            int[] distance = DistanceToWater(field, size);
            int coastDry = 0, coast = 0, interiorDry = 0, interior = 0;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                int d = distance[(y * size.X) + x];
                if (d == 0 || (d > 3 && d < 12)) continue;
                string kind = field.TerrainAtCell(new Vector2I(x, y));
                bool dry = kind is "desert" or "dry_grass";
                if (d <= 3) { coast++; if (dry) coastDry++; }
                else { interior++; if (dry) interiorDry++; }
            }
            return new Godot.Collections.Dictionary
            {
                ["coast"] = coast == 0 ? 0f : coastDry / (float)coast,
                ["coast_cells"] = coast,
                ["interior"] = interior == 0 ? 0f : interiorDry / (float)interior,
                ["interior_cells"] = interior,
            };
        }
        finally { generator.Free(); }
    }

    /// <summary>
    /// Woodland on one generated map: "woods" and "forest" cells and "land", and where the edges of the
    /// woodland fall. An edge is a pair of neighbouring land cells, one wooded and one not;
    /// "block_edge_horizontal" and "block_edge_vertical" are the shares of those edges that lie exactly on a
    /// boundary between TerrainFeatureStage's ranking blocks. Woodland shaped by its own field puts about
    /// one edge in BlockTiles there; woodland drawn by the blocks puts far more.
    ///
    /// The stands are the four-connected groups of wooded cells: "stands" counts them, "median_stand" is the
    /// median size in cells, and "in_large_stands" the share of woodland in stands of "large_stand_tiles"
    /// cells or more.
    /// </summary>
    public Godot.Collections.Dictionary Woodland(Vector2I size, int temperature, int rainfall, int seed, float span)
    {
        var generator = Configure(size, temperature, rainfall, seed, span);
        try
        {
            GeneratedTerrainField field = TerrainFieldBuilder.Build(generator.CaptureGenerationSettings());
            int block = TerrainFeatureStage.BlockTiles;
            int woods = 0, forest = 0, land = 0;
            int[] edges = new int[2], onBoundary = new int[2];
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (!IsLand(field, cell)) continue;
                land++;
                string feature = field.FeatureAtCell(cell);
                if (feature == TerrainFeatureStage.Woods) woods++;
                else if (feature == TerrainFeatureStage.Forest) forest++;

                // Toward +x (axis 0) and +y (axis 1); a boundary lies between cell block*k-1 and block*k.
                for (int axis = 0; axis < 2; axis++)
                {
                    Vector2I next = axis == 0 ? new Vector2I(x + 1, y) : new Vector2I(x, y + 1);
                    if (next.X >= size.X || next.Y >= size.Y || !IsLand(field, next)) continue;
                    if (Wooded(field, cell) == Wooded(field, next)) continue;
                    edges[axis]++;
                    if ((axis == 0 ? next.X : next.Y) % block == 0) onBoundary[axis]++;
                }
            }
            List<int> stands = StandSizes(field, size);
            stands.Sort();
            int wooded = woods + forest;
            int inLargeStands = 0;
            foreach (int stand in stands)
                if (stand >= LargeStandTiles) inLargeStands += stand;
            return new Godot.Collections.Dictionary
            {
                ["woods"] = woods,
                ["forest"] = forest,
                ["land"] = land,
                ["block_edge_horizontal"] = edges[0] == 0 ? 0f : onBoundary[0] / (float)edges[0],
                ["block_edge_vertical"] = edges[1] == 0 ? 0f : onBoundary[1] / (float)edges[1],
                ["block_edge_chance"] = 1f / block,
                ["edges"] = edges[0] + edges[1],
                ["stands"] = stands.Count,
                ["median_stand"] = stands.Count == 0 ? 0 : stands[stands.Count / 2],
                ["in_large_stands"] = wooded == 0 ? 0f : inLargeStands / (float)wooded,
                ["large_stand_tiles"] = LargeStandTiles,
            };
        }
        finally { generator.Free(); }
    }

    /// <summary>A stand this size or larger counts as a forest in "in_large_stands".</summary>
    private const int LargeStandTiles = 25;

    /// <summary>Sizes, in cells, of the four-connected stands of wooded cells.</summary>
    private static List<int> StandSizes(GeneratedTerrainField field, Vector2I size)
    {
        var seen = new bool[size.X * size.Y];
        var sizes = new List<int>();
        var frontier = new Stack<Vector2I>();
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            int start = (y * size.X) + x;
            if (seen[start] || !Wooded(field, new Vector2I(x, y))) continue;
            seen[start] = true;
            frontier.Push(new Vector2I(x, y));
            int cells = 0;
            while (frontier.Count > 0)
            {
                Vector2I cell = frontier.Pop();
                cells++;
                foreach (Vector2I step in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up })
                {
                    Vector2I next = cell + step;
                    if (next.X < 0 || next.Y < 0 || next.X >= size.X || next.Y >= size.Y) continue;
                    int at = (next.Y * size.X) + next.X;
                    if (seen[at] || !Wooded(field, next)) continue;
                    seen[at] = true;
                    frontier.Push(next);
                }
            }
            sizes.Add(cells);
        }
        return sizes;
    }

    private static bool IsLand(GeneratedTerrainField field, Vector2I cell) => field.WaterSourceAtCell(cell).Length == 0;

    private static bool Wooded(GeneratedTerrainField field, Vector2I cell)
        => field.FeatureAtCell(cell) is TerrainFeatureStage.Woods or TerrainFeatureStage.Forest;

    private static TerrainGeneratorComponent Configure(Vector2I size, int temperature, int rainfall, int seed, float span)
    {
        var generator = new TerrainGeneratorComponent { BoundsSize = size, Seed = seed };
        generator.ApplyMapSetup(0, 1, temperature, rainfall, 1, 1);
        generator.LandmassScale = 0.6f;
        generator.UseClimateBiomeMaps = true;
        generator.UseScaleRules = true;
        generator.UseCustomClimateSpan = span > 0f;
        if (span > 0f) generator.ClimateLatitudeSpan = span;
        return generator;
    }

    /// <summary>Four-neighbour distance from each cell to the nearest water cell, 0 on water.</summary>
    private static int[] DistanceToWater(GeneratedTerrainField field, Vector2I size)
    {
        var distance = new int[size.X * size.Y];
        var frontier = new Queue<Vector2I>();
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            int index = (y * size.X) + x;
            if (field.WaterSourceAtCell(new Vector2I(x, y)).Length > 0) frontier.Enqueue(new Vector2I(x, y));
            else distance[index] = int.MaxValue;
        }
        while (frontier.Count > 0)
        {
            Vector2I cell = frontier.Dequeue();
            int next = distance[(cell.Y * size.X) + cell.X] + 1;
            foreach (Vector2I step in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up })
            {
                Vector2I around = cell + step;
                if (around.X < 0 || around.Y < 0 || around.X >= size.X || around.Y >= size.Y) continue;
                int at = (around.Y * size.X) + around.X;
                if (distance[at] <= next) continue;
                distance[at] = next;
                frontier.Enqueue(around);
            }
        }
        return distance;
    }
}
