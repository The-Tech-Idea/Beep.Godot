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
            if ((settings.BeachWidth <= 0f && settings.LakeShoreWidth <= 0f && !world.HasRiverBank)
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
            // THE WIDTH THE PAINTED VIEW DRAWS ITS INLAND BAND WITH, per cell, in tiles.
            //
            // Zero by default, and zero means no band. The band is drawn wherever the coast field
            // says a fragment is within this many tiles of inland water, so a cell that carries a
            // width it has no business carrying paints sand around any river that happens to pass
            // near it. Defaulting this to the map's lake width - tried first - simply reinstated
            // the constant it exists to replace, and the map came out a desert for the second time.
            float[] shoreWidth = world.CellShoreWidth;
            if (settings.LakeShoreWidth > 0f)
            {
                for (int i = 0; i < world.Count; i++) body[i] = world.Water[i] == WaterBody.Lake;
                TerrainEuclideanDistance.Squared(body, size, true, squared);
                // Every shore, not only the flat ones. Gating the bank on flat ground left a lake
                // that runs against rising land with no shore at all: grass met water directly and
                // the lake read as a stain on the hillside rather than as a body of water with an
                // edge. A shore is what bounds water; the ground behind it may do what it likes.
                //
                // A LAKE's shore is one width all round it, so the cells that edge one say so as
                // they are marked, and no other cell does.
                for (int i = 0; i < world.Count; i++)
                {
                    if (!world.Land[i]
                        || TerrainEuclideanDistance.ToTiles(squared[i], world.SamplesPerCell) > settings.LakeShoreWidth)
                        continue;
                    world.Terrain[i] = "sand";
                    shoreWidth[world.CellOf(i)] = settings.LakeShoreWidth;
                }
            }

            // A RIVER'S BANK is already shaped: the carve marked the ring outside each channel at
            // that river's own width, so there is nothing to measure here and nothing to choose.
            //
            // It sets the WIDTH and not the terrain. A bank a fraction of a tile across has no
            // business renaming the tile it sits in - and it would, because a cell holding a river
            // has most of its samples under water and excluded from the kind vote, so a thin bank
            // wins the cell and paints the whole square. That is what put hard cell-edged sand
            // around every river on the first attempt. Sub-tile detail belongs to the band, which
            // draws it at sub-tile resolution against the same distance field.
            //
            // The widest bank touching a cell wins it, so a trunk edges wider than its tributaries.
            if (world.HasRiverBank)
            {
                byte[] banks = world.RiverBank;
                float perSample = 1f / Mathf.Max(1, world.SamplesPerCell);
                for (int i = 0; i < world.Count; i++)
                {
                    if (banks[i] == 0 || !world.Land[i]) continue;
                    int cell = world.CellOf(i);
                    float tiles = banks[i] * perSample;
                    if (tiles > shoreWidth[cell]) shoreWidth[cell] = tiles;
                }
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
