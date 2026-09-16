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
            // Ocean first, then lake, through one mask and one distance field:
            // a land sample within either band becomes sand, and neither test
            // reads the terrain the other wrote.
            bool[] body = world.BoolScratch;
            float[] squared = world.FloatScratchA;
            var size = new Vector2I(world.Width, world.Height);
            if (settings.BeachWidth > 0f)
            {
                for (int i = 0; i < world.Count; i++) body[i] = world.Water[i] == WaterBody.Ocean;
                TerrainEuclideanDistance.Squared(body, size, true, squared);
                for (int i = 0; i < world.Count; i++)
                    if (world.Land[i] && TerrainEuclideanDistance.ToTiles(squared[i], world.SamplesPerCell) <= settings.BeachWidth)
                        world.Terrain[i] = "sand";
            }
            if (settings.LakeShoreWidth > 0f)
            {
                for (int i = 0; i < world.Count; i++) body[i] = world.Water[i] == WaterBody.Lake;
                TerrainEuclideanDistance.Squared(body, size, true, squared);
                // Every shore, not only the flat ones. Gating the bank on flat ground left a lake
                // that runs against rising land with no shore at all: grass met water directly and
                // the lake read as a stain on the hillside rather than as a body of water with an
                // edge. A shore is what bounds water; the ground behind it may do what it likes.
                for (int i = 0; i < world.Count; i++)
                    if (world.Land[i]
                        && TerrainEuclideanDistance.ToTiles(squared[i], world.SamplesPerCell) <= settings.LakeShoreWidth)
                        world.Terrain[i] = "sand";
            }

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
                world.CellTerrain[cell] = TerrainGeometry.MostCommon(counts, world.CellTerrain[cell]);
            }
        }

    }
}
