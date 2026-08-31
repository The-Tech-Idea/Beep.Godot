using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Where water goes, and how much of it arrives.
    ///
    /// This is the D8 drainage network: every land cell drains to its lowest
    /// neighbour of eight, and accumulation is the count of cells draining
    /// through each one. It answers two different questions for two stages, and
    /// it lives here so there is one answer rather than two implementations
    /// drifting apart:
    ///
    /// - the RIVER stage asks where the water is, and puts channels there;
    /// - the EROSION stage asks how much water passes, and cuts the land down
    ///   in proportion, because that is what carves a valley.
    ///
    /// One pass suffices because the cells are handled highest first, so a
    /// cell's own accumulation is complete before it is handed downstream.
    /// </summary>
    internal static class TerrainFlow
    {
        /// <summary>
        /// Fills <paramref name="order"/> with the land cells, highest first,
        /// <paramref name="flowsTo"/> with each cell's downhill neighbour, and
        /// <paramref name="flow"/> with accumulation. Returns the land count.
        /// </summary>
        public static int Accumulate(
            TerrainWorld world, int[] flowsTo, int[] order, float[] flow)
        {
            int count = world.Count;
            int land = 0;

            for (int index = 0; index < count; index++)
            {
                flowsTo[index] = -1;
                flow[index] = 0.0f;
                if (world.Land[index])
                    order[land++] = index;
            }

            if (land == 0)
                return 0;

            for (int i = 0; i < land; i++)
                flowsTo[order[i]] = Downhill(world, order[i]);

            // Highest first, so a cell's own accumulation is complete before it
            // is handed downstream. This is what lets one pass do the work.
            Array.Sort(order, 0, land, new HighestFirst(world.Elevation));

            for (int i = 0; i < land; i++)
                flow[order[i]] = 1.0f;

            for (int i = 0; i < land; i++)
            {
                int from = order[i];
                int to = flowsTo[from];
                if (to >= 0 && world.Land[to])
                    flow[to] += flow[from];
            }

            return land;
        }

        /// <summary>
        /// Where a cell's water goes: the steepest descent, or failing that the
        /// neighbour nearest the coast.
        ///
        /// The coast fallback is what stops flow dead-ending. A cell with no
        /// lower neighbour is a pit, and on a noisy height field there are many;
        /// sending their water toward the coast keeps the network connected
        /// instead of leaving ponds of accumulation scattered inland.
        /// </summary>
        public static int Downhill(TerrainWorld world, int current)
        {
            int x = current % world.Width;
            int y = current / world.Width;

            int lowest = -1;
            float lowestElevation = world.Elevation[current];
            int nearestToCoast = -1;
            int nearestDistance = world.CoastDistance[current];

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                        continue;

                    int atX = x + offsetX;
                    int atY = y + offsetY;
                    if (!world.InBounds(atX, atY))
                        continue;

                    int neighbour = world.Index(atX, atY);

                    // Open water is the mouth: flow leaves the land here.
                    if (!world.Land[neighbour])
                        return neighbour;

                    if (world.Elevation[neighbour] < lowestElevation)
                    {
                        lowestElevation = world.Elevation[neighbour];
                        lowest = neighbour;
                    }

                    if (world.CoastDistance[neighbour] < nearestDistance)
                    {
                        nearestDistance = world.CoastDistance[neighbour];
                        nearestToCoast = neighbour;
                    }
                }
            }

            return lowest >= 0 ? lowest : nearestToCoast;
        }

        private sealed class HighestFirst : System.Collections.Generic.IComparer<int>
        {
            private readonly float[] _elevation;

            public HighestFirst(float[] elevation) => _elevation = elevation;

            public int Compare(int left, int right)
                => _elevation[right].CompareTo(_elevation[left]);
        }
    }
}
