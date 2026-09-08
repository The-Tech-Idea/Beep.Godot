using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Numbers each separate landmass, so gameplay can tell "the same continent"
    /// from "across the sea" without re-deriving it. Start placement uses it to
    /// spread players between landmasses rather than crowding one.
    ///
    /// Labelling runs on the reduced TILE grid, not the sample field, because
    /// "can I walk there" is a question about tiles: two shores one sample apart
    /// but a whole tile of water apart are not the same continent to a unit.
    /// </summary>
    internal static class TerrainContinentStage
    {
        public static void Apply(TerrainGenerationBuffer world)
        {
            int wide = world.CellsWide;
            int high = world.CellsHigh;
            // A tile is numbered as it is queued, so it enters once and the
            // sample-sized scratch always holds the whole tile grid.
            int[] queue = world.IntScratchA;
            Span<int> around = stackalloc int[4];
            int nextId = 0;

            for (int start = 0; start < wide * high; start++)
            {
                if (world.CellWater[start] != WaterBody.None || world.CellContinent[start] != 0)
                    continue;

                nextId++;
                world.CellContinent[start] = nextId;
                int head = 0;
                int tail = 0;
                queue[tail++] = start;

                while (head < tail)
                {
                    int current = queue[head++];
                    int sides = TerrainGeometry.Neighbours4(current, wide, high, around);
                    for (int side = 0; side < sides; side++)
                    {
                        int neighbour = around[side];
                        if (world.CellWater[neighbour] != WaterBody.None || world.CellContinent[neighbour] != 0)
                            continue;
                        world.CellContinent[neighbour] = nextId;
                        queue[tail++] = neighbour;
                    }
                }
            }
        }
    }
}
