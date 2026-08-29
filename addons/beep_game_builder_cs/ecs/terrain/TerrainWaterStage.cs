using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Carves inland lake basins, then separates ocean from lakes by what they
    /// actually connect to rather than by how they were made.
    ///
    /// Ocean is defined as water reachable from the map border; every other body
    /// of water is a lake. Deriving it this way means a lake can never silently
    /// become an ocean inlet, a bay can never be mistaken for a lake, and the
    /// lake control can never resize the landmass or replace the sea.
    ///
    /// Runs before <see cref="TerrainElevationStage"/> because carving lakes
    /// changes the coastline, and elevation is measured from the final coast.
    /// </summary>
    internal static class TerrainWaterStage
    {
        public static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
        {
            CarveLakeBasins(world, noise, settings);
            ClassifyWaterBodies(world);
        }

        /// <summary>
        /// Sinks low, flat interior land into lake basins until the requested
        /// lake coverage is met. Basins are kept well away from the coast so
        /// flooding one can never breach the shoreline and become a bay.
        /// </summary>
        private static void CarveLakeBasins(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
        {
            int requested = Mathf.RoundToInt(world.Count * Mathf.Clamp(settings.LakeCoverage, 0.0f, 0.35f));
            if (requested <= 0)
                return;

            requested = Mathf.Min(requested, Mathf.FloorToInt(CountTrue(world.Land) * 0.35f));
            if (requested <= 0)
                return;

            int[] fromWater = TerrainGeometry.DistanceTo(Negate(world.Land), world.Width, world.Height);
            // Two tiles clear of the shore. One tile is enough for a growing
            // lake to pinch a narrow neck and split one island into two.
            int minimumInland = Mathf.Max(3, world.SamplesPerCell * 2);

            var basinScore = new float[world.Count];
            var candidates = new List<int>();
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    if (!world.Land[index] || fromWater[index] < minimumInland)
                        continue;

                    Vector2 at = world.TileCentre(x, y);
                    float basin = TerrainGeometry.Normalized(noise.Lake.GetNoise2D(at.X, at.Y));
                    // Prefer places the basin noise likes, that are away from
                    // mountain crests, and that sit deep in the interior.
                    float flatness = 1.0f - TerrainGeometry.Ridged(noise.Ridge.GetNoise2D(at.X, at.Y));
                    basinScore[index] = (basin * 0.55f)
                        + (flatness * 0.30f)
                        + (Mathf.Min(fromWater[index], minimumInland * 5) / (float)(minimumInland * 5) * 0.15f);
                    candidates.Add(index);
                }
            }
            if (candidates.Count == 0)
                return;

            candidates.Sort((left, right) => basinScore[right].CompareTo(basinScore[left]));

            int carved = 0;
            var queued = new bool[world.Count];
            var frontier = new PriorityQueue<int, float>();

            // Flood outward from the best seeds so each lake is one connected
            // body of water rather than a scatter of pits.
            foreach (int seed in candidates)
            {
                if (carved >= requested)
                    break;
                if (queued[seed])
                    continue;

                frontier.Clear();
                frontier.Enqueue(seed, -basinScore[seed]);
                queued[seed] = true;

                int budget = Mathf.Max(1, requested - carved);
                int grown = 0;
                while (frontier.Count > 0 && grown < budget)
                {
                    int index = frontier.Dequeue();
                    if (!world.Land[index] || fromWater[index] < minimumInland)
                        continue;

                    world.Land[index] = false;
                    carved++;
                    grown++;

                    foreach (int neighbour in TerrainGeometry.Neighbours(
                        index % world.Width, index / world.Width, world.Width, world.Height))
                    {
                        if (queued[neighbour] || !world.Land[neighbour] || fromWater[neighbour] < minimumInland)
                            continue;
                        queued[neighbour] = true;
                        frontier.Enqueue(neighbour, -basinScore[neighbour]);
                    }
                }
            }
        }

        /// <summary>
        /// Flood fill inward from the border across water: everything reached is
        /// ocean, everything left over is an enclosed lake.
        /// </summary>
        private static void ClassifyWaterBodies(TerrainWorld world)
        {
            var queue = new Queue<int>();

            for (int x = 0; x < world.Width; x++)
            {
                EnqueueIfOpenWater(world, world.Index(x, 0), queue);
                EnqueueIfOpenWater(world, world.Index(x, world.Height - 1), queue);
            }
            for (int y = 0; y < world.Height; y++)
            {
                EnqueueIfOpenWater(world, world.Index(0, y), queue);
                EnqueueIfOpenWater(world, world.Index(world.Width - 1, y), queue);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbour in TerrainGeometry.Neighbours(
                    current % world.Width, current / world.Width, world.Width, world.Height))
                {
                    EnqueueIfOpenWater(world, neighbour, queue);
                }
            }

            for (int index = 0; index < world.Count; index++)
            {
                if (!world.Land[index] && world.Water[index] != WaterBody.Ocean)
                    world.Water[index] = WaterBody.Lake;
            }
        }

        private static void EnqueueIfOpenWater(TerrainWorld world, int index, Queue<int> queue)
        {
            if (world.Land[index] || world.Water[index] == WaterBody.Ocean)
                return;
            world.Water[index] = WaterBody.Ocean;
            queue.Enqueue(index);
        }

        private static bool[] Negate(bool[] values)
        {
            var result = new bool[values.Length];
            for (int index = 0; index < values.Length; index++)
                result[index] = !values[index];
            return result;
        }

        private static int CountTrue(bool[] values)
        {
            int count = 0;
            foreach (bool value in values)
            {
                if (value)
                    count++;
            }
            return count;
        }
    }
}
