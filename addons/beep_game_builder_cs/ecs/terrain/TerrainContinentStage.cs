using Godot;
using System.Collections.Generic;

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
        public static void Apply(TerrainWorld world)
        {
            int wide = world.CellsWide;
            int high = world.CellsHigh;
            var queue = new Queue<int>();
            int nextId = 0;

            for (int start = 0; start < wide * high; start++)
            {
                if (world.CellWater[start] != WaterBody.None || world.CellContinent[start] != 0)
                    continue;

                nextId++;
                world.CellContinent[start] = nextId;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    foreach (int neighbour in TerrainGeometry.Neighbours(
                        current % wide, current / wide, wide, high))
                    {
                        if (world.CellWater[neighbour] != WaterBody.None || world.CellContinent[neighbour] != 0)
                            continue;
                        world.CellContinent[neighbour] = nextId;
                        queue.Enqueue(neighbour);
                    }
                }
            }
        }
    }
}
