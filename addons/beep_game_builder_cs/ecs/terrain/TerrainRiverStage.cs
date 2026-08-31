using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Rivers as a DRAINAGE NETWORK, not as a set of independent traces.
    ///
    /// Every point of land drains somewhere. Following steepest descent from
    /// each one gives a flow direction; counting how much land drains THROUGH
    /// each point gives flow accumulation; and a river is simply where enough
    /// land drains through. That is the standard hydrological construction, and
    /// the reason to use it here is that it produces the thing rivers actually
    /// are: headwaters that merge.
    ///
    /// WHAT THIS REPLACES. The previous version picked scattered sources on high
    /// wet ground and walked each one downhill on its own. Two courses that met
    /// did not join - the second stopped dead, because it saw the first as water
    /// and treated it as its mouth. So the map got a handful of parallel
    /// streams, each the same width along its length, none of them a system. A
    /// tributary is not a special case here; it is what merging accumulation
    /// looks like.
    ///
    /// Width falls out of the same number. A river carrying the drainage of a
    /// hundred tiles is wider than one carrying ten, without anyone tracking how
    /// far along its course it is.
    ///
    /// DEPRESSIONS. Steepest descent alone strands flow in any local hollow, and
    /// a river that stops mid-continent looks broken. Where nothing is lower,
    /// flow goes to whichever neighbour is nearest the coast: coast distance
    /// decreases strictly toward water, so every drop reaches the sea and no
    /// cycle can form.
    /// </summary>
    internal static class TerrainRiverStage
    {
        /// <summary>
        /// Share of the land that becomes river at density 1, as a fraction.
        ///
        /// The threshold on accumulation is taken as a PERCENTILE of the
        /// accumulation the map actually has, not as an absolute number of
        /// upstream cells. Accumulation scales with map area and with how the
        /// noise came out, so a fixed number gives a continent a hundred rivers
        /// and an island none.
        ///
        /// It is far below the share of land that ends up wet, because it picks
        /// the CENTRES and carving widens each into a channel. Tune it from the
        /// finished map, not from the threshold: 2% of centres came out as 22%
        /// of the land under water.
        /// </summary>
        private const float RiverShareAtDensityOne = 0.0045f;

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            float density = Mathf.Clamp(settings.RiverDensity, 0.0f, 4.0f);
            if (density <= 0.0f)
                return;

            int count = world.Count;
            var flowsTo = new int[count];
            var order = new int[count];
            var flow = new float[count];

            // The drainage network is shared with the erosion stage rather than
            // computed twice - see TerrainFlow.
            int land = TerrainFlow.Accumulate(world, flowsTo, order, flow);
            if (land == 0)
                return;

            float share = Mathf.Clamp(RiverShareAtDensityOne * density, 0.0f, 0.5f);
            float threshold = Threshold(flow, order, land, share);
            if (threshold <= 1.0f)
                return;

            for (int i = 0; i < land; i++)
            {
                int index = order[i];
                if (flow[index] < threshold)
                    continue;

                // Width from flow, so a trunk is wider than the streams feeding
                // it. Never below one sample of margin: a river a single sample
                // across is thinner than a pixel at map zoom and reads as a
                // seam rather than as water.
                int radius = Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Log(flow[index] / threshold + 1.0f) * 1.6f), 1, 3);
                Carve(world, index, radius);
            }
        }

        /// <summary>
        /// The accumulation above which a cell counts as river, chosen so the
        /// requested share of the land ends up wet.
        /// </summary>
        private static float Threshold(float[] flow, int[] order, int land, float share)
        {
            if (share <= 0.0f)
                return float.MaxValue;

            var values = new float[land];
            for (int i = 0; i < land; i++)
                values[i] = flow[order[i]];

            Array.Sort(values);
            int at = Mathf.Clamp(Mathf.RoundToInt((1.0f - share) * (land - 1)), 0, land - 1);
            return values[at];
        }

        private static void Carve(TerrainWorld world, int index, int radius)
        {
            int cx = index % world.Width;
            int cy = index / world.Width;

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if ((offsetX * offsetX) + (offsetY * offsetY) > radius * radius)
                        continue;

                    int atX = cx + offsetX;
                    int atY = cy + offsetY;
                    if (!world.InBounds(atX, atY))
                        continue;

                    int at = world.Index(atX, atY);
                    if (!world.Land[at])
                        continue;

                    world.Land[at] = false;
                    world.Water[at] = WaterBody.River;
                }
            }
        }

    }
}
