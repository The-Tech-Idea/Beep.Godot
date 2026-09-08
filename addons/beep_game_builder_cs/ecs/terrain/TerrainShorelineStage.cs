using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>Insets ocean sand from the final fine coastline without moving water or relief.</summary>
    internal static class TerrainShorelineStage
    {
        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            world.CellTerrain.CopyTo(world.CellInlandTerrain, 0);
            if ((settings.BeachWidth <= 0f && settings.LakeShoreWidth <= 0f)
                || TerrainBiomeStage.ThemedKind(settings.Preset, 0f) is not null)
                return;
            world.BeachWidth = settings.BeachWidth;
            world.LakeShoreWidth = settings.LakeShoreWidth;
            var ocean = new bool[world.Count];
            var lake = new bool[world.Count];
            for (int i = 0; i < ocean.Length; i++) ocean[i] = world.Water[i] == WaterBody.Ocean;
            for (int i = 0; i < lake.Length; i++) lake[i] = world.Water[i] == WaterBody.Lake;
            double[] distances = TerrainEuclideanDistance.Squared(ocean, new Vector2I(world.Width, world.Height), true);
            double[] lakeDistances = TerrainEuclideanDistance.Squared(lake, new Vector2I(world.Width, world.Height), true);
            for (int i = 0; i < world.Count; i++)
                if (world.Land[i] && ((settings.BeachWidth > 0f
                    && (Math.Sqrt(distances[i]) - 0.5) / world.SamplesPerCell <= settings.BeachWidth)
                    || (settings.LakeShoreWidth > 0f && world.Relief[i] == TerrainRelief.Flat
                    && (Math.Sqrt(lakeDistances[i]) - 0.5) / world.SamplesPerCell <= settings.LakeShoreWidth)))
                    world.Terrain[i] = "sand";

            // A cell kind remains a gameplay summary, never the rendered contour.
            var counts = new Dictionary<string, int>();
            int samples = world.SamplesPerCell;
            for (int y = 0; y < world.CellsHigh; y++)
            for (int x = 0; x < world.CellsWide; x++)
            {
                int cell = world.CellIndex(x, y);
                if (world.CellWater[cell] != WaterBody.None) continue;
                counts.Clear();
                for (int sy = y * samples; sy < (y + 1) * samples; sy++)
                for (int sx = x * samples; sx < (x + 1) * samples; sx++)
                {
                    int sample = world.Index(sx, sy);
                    if (!world.Land[sample] || world.Relief[sample] != world.CellRelief[cell]) continue;
                    string kind = world.Terrain[sample];
                    counts[kind] = counts.GetValueOrDefault(kind) + 1;
                }
                int best = 0;
                foreach (var (kind, count) in counts)
                    if (count > best) { best = count; world.CellTerrain[cell] = kind; }
            }
        }

    }
}
