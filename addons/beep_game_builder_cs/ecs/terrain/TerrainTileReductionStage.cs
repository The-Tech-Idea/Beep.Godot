using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Reduces the fine sample field down to ONE value per gameplay tile.
    ///
    /// This is the stage that makes the generator's output real gameplay data.
    /// Everything before it works at sub-tile resolution because that is what
    /// produces good coastlines, ranges and river courses; but a game moves
    /// units, paths, and places cities on TILES. Leaving the fine field as the
    /// output means the renderer draws a coastline curving through the middle of
    /// a tile that the game believes is entirely grass - the picture and the
    /// game state disagree, and anything trusting the picture is wrong.
    ///
    /// Two different reductions are used, deliberately:
    ///
    /// - Terrain and relief take the MAJORITY of their samples. Sampling the
    ///   tile centre instead throws away everything else in the tile, so a
    ///   headland or a small lake that does not happen to cover the exact centre
    ///   silently disappears.
    /// - Rivers take ANY sample above a small fraction. A river is narrower than
    ///   a tile, so a majority rule would delete it entirely; and a river that
    ///   survives in some tiles but not others is worse than none, because it
    ///   breaks into disconnected puddles. Continuity matters more than area.
    /// </summary>
    internal static class TerrainTileReductionStage
    {
        /// <summary>
        /// Share of a tile's samples that must be river for the tile to become a
        /// river tile. Low on purpose: rivers must stay connected.
        /// </summary>
        private const float RiverTileFraction = 0.10f;

        public static void Apply(TerrainWorld world)
        {
            int wide = world.CellsWide;
            int high = world.CellsHigh;
            int perTile = world.SamplesPerCell * world.SamplesPerCell;
            var counts = new Dictionary<string, int>();
            var waterCounts = new Dictionary<string, int>();
            var byRelief = new[]
            {
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
            };

            for (int cellY = 0; cellY < high; cellY++)
            {
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    counts.Clear();
                    waterCounts.Clear();
                    byRelief[0].Clear();
                    byRelief[1].Clear();
                    byRelief[2].Clear();
                    int land = 0;
                    int ocean = 0;
                    int lake = 0;
                    int river = 0;
                    float shade = 0.0f;
                    var relief = new int[3];

                    for (int offsetY = 0; offsetY < world.SamplesPerCell; offsetY++)
                    {
                        for (int offsetX = 0; offsetX < world.SamplesPerCell; offsetX++)
                        {
                            int x = (cellX * world.SamplesPerCell) + offsetX;
                            int y = (cellY * world.SamplesPerCell) + offsetY;
                            if (!world.InBounds(x, y))
                                continue;

                            int sample = world.Index(x, y);
                            shade += world.Shade[sample];

                            if (world.Land[sample])
                            {
                                land++;
                                int band = (int)world.Relief[sample];
                                relief[band]++;
                                string kind = world.Terrain[sample];
                                counts[kind] = counts.TryGetValue(kind, out int seen) ? seen + 1 : 1;

                                // Also tallied per relief band. Terrain and
                                // relief are two separate majorities, and two
                                // majorities of the same tile can disagree: a
                                // tile can take its terrain from the mountain
                                // samples and its relief from the flat ones, and
                                // come out as a snowfield lying on level ground.
                                Dictionary<string, int> band_counts = byRelief[band];
                                band_counts[kind] = band_counts.TryGetValue(kind, out int seenBand) ? seenBand + 1 : 1;
                            }
                            else
                            {
                                switch (world.Water[sample])
                                {
                                    case WaterBody.Ocean: ocean++; break;
                                    case WaterBody.Lake: lake++; break;
                                    case WaterBody.River: river++; break;
                                }

                                string waterKind = world.Terrain[sample];
                                waterCounts[waterKind] = waterCounts.TryGetValue(waterKind, out int seenWater) ? seenWater + 1 : 1;
                            }
                        }
                    }

                    int cell = (cellY * wide) + cellX;
                    world.CellShade[cell] = perTile > 0 ? shade / perTile : 1.0f;

                    bool mostlyWater = (ocean + lake + river) > land;
                    bool riverTile = !mostlyWater && river >= Mathf.Max(1, Mathf.RoundToInt(perTile * RiverTileFraction));

                    if (mostlyWater)
                    {
                        world.CellWater[cell] = ocean >= lake && ocean >= river
                            ? WaterBody.Ocean
                            : lake >= river ? WaterBody.Lake : WaterBody.River;
                        world.CellRelief[cell] = TerrainRelief.Flat;
                        world.CellTerrain[cell] = MostCommon(waterCounts, "deep_water");
                    }
                    else if (riverTile)
                    {
                        world.CellWater[cell] = WaterBody.River;
                        world.CellRelief[cell] = TerrainRelief.Flat;
                        world.CellTerrain[cell] = "shallow_water";
                    }
                    else
                    {
                        world.CellWater[cell] = WaterBody.None;

                        // Relief decides FIRST, then the terrain is taken from
                        // the samples that agree with it. Two independent
                        // majorities of one tile can disagree - the terrain from
                        // the mountain samples, the relief from the flat ones -
                        // and the tile comes out as a snowfield on level ground,
                        // drawn as flat white terrain beside a meadow. Falling
                        // back to the whole-tile majority keeps a tile that has
                        // no sample in its own band from being left blank.
                        int band = LargestIndex(relief);
                        world.CellRelief[cell] = (TerrainRelief)band;
                        world.CellTerrain[cell] = byRelief[band].Count > 0
                            ? MostCommon(byRelief[band], "grass")
                            : MostCommon(counts, "grass");
                    }
                }
            }
        }

        private static string MostCommon(Dictionary<string, int> counts, string fallback)
        {
            string best = fallback;
            int bestCount = 0;
            foreach ((string kind, int count) in counts)
            {
                if (count > bestCount)
                {
                    bestCount = count;
                    best = kind;
                }
            }
            return best;
        }

        private static int LargestIndex(int[] values)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[best])
                    best = i;
            }
            return best;
        }
    }
}
