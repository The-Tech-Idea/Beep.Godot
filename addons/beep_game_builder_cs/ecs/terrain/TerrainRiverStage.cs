using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Traces rivers from high wet ground downhill until they reach water.
    ///
    /// Sources are picked where rain actually falls on high ground, then each
    /// river follows steepest descent, which is what makes it hug valleys and
    /// wrap around ridges instead of running in a straight line to the coast.
    ///
    /// Steepest descent alone can strand a river in a local depression, and a
    /// river that stops in the middle of a continent looks broken. Where no
    /// lower neighbour exists, the trace falls back to the neighbour nearest the
    /// coast. Coast distance strictly decreases toward water, so that fallback
    /// guarantees every river terminates in a lake or the sea rather than
    /// looping or dead-ending inland.
    ///
    /// Rivers widen downstream, standing in for the flow they have accumulated.
    /// </summary>
    internal static class TerrainRiverStage
    {
        /// <summary>One river per this many tiles of land, before density scaling.</summary>
        private const float TilesPerRiver = 40.0f;

        private const int MaximumRivers = 64;

        /// <summary>Only ground above this elevation percentile can be a source.</summary>
        private const float SourcePercentile = 0.65f;

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            float density = Mathf.Clamp(settings.RiverDensity, 0.0f, 4.0f);
            if (density <= 0.0f)
                return;

            int landSamples = 0;
            for (int index = 0; index < world.Count; index++)
            {
                if (world.Land[index])
                    landSamples++;
            }
            if (landSamples == 0)
                return;

            float samplesPerTile = world.SamplesPerCell * world.SamplesPerCell;
            float landTiles = landSamples / Mathf.Max(1.0f, samplesPerTile);
            int wanted = Mathf.Clamp(
                Mathf.RoundToInt(landTiles / TilesPerRiver * density), 0, MaximumRivers);
            if (wanted == 0)
                return;

            foreach (int source in ChooseSources(world, wanted))
                Trace(world, source);
        }

        /// <summary>
        /// High, wet ground, spread out so rivers do not all spring from the
        /// same massif.
        /// </summary>
        private static List<int> ChooseSources(TerrainWorld world, int wanted)
        {
            float threshold = TerrainGeometry.Percentile(world.Elevation, world.Land, SourcePercentile);

            var candidates = new List<int>();
            var score = new float[world.Count];
            for (int index = 0; index < world.Count; index++)
            {
                if (!world.Land[index] || world.Elevation[index] < threshold)
                    continue;
                score[index] = (world.Elevation[index] * 0.6f) + (world.Moisture[index] * 0.4f);
                candidates.Add(index);
            }
            candidates.Sort((left, right) => score[right].CompareTo(score[left]));

            float minimumSpacing = world.SamplesPerCell * 4.0f;
            float minimumSpacingSquared = minimumSpacing * minimumSpacing;
            var sources = new List<int>();
            foreach (int candidate in candidates)
            {
                if (sources.Count >= wanted)
                    break;

                int cx = candidate % world.Width;
                int cy = candidate / world.Width;
                bool farEnough = true;
                foreach (int chosen in sources)
                {
                    float dx = cx - (chosen % world.Width);
                    float dy = cy - (chosen / world.Width);
                    if ((dx * dx) + (dy * dy) < minimumSpacingSquared)
                    {
                        farEnough = false;
                        break;
                    }
                }
                if (farEnough)
                    sources.Add(candidate);
            }
            return sources;
        }

        /// <summary>
        /// Walks the whole course first, then carves it.
        ///
        /// These two steps must stay separate. Carving as it goes turns the
        /// cell the river is standing on into water, and the very next search
        /// for somewhere downhill sees that fresh water as the sea and stops -
        /// so every river ends one step from its own source.
        /// </summary>
        private static void Trace(TerrainWorld world, int source)
        {
            var visited = new HashSet<int>();
            var path = new List<int>();
            int current = source;
            int maximumSteps = (world.Width + world.Height) * 2;

            for (int step = 0; step < maximumSteps; step++)
            {
                if (!visited.Add(current))
                    break;

                // Reaching water - the sea, a lake, or an earlier river - is the
                // river's mouth, and it is not carved over.
                if (!world.Land[current])
                    break;

                path.Add(current);

                int next = NextDownhill(world, current, visited);
                if (next < 0)
                    break;
                current = next;
            }

            for (int step = 0; step < path.Count; step++)
                Carve(world, path[step], RadiusFor(world, step));
        }

        /// <summary>
        /// Steepest descent, falling back to whichever neighbour is closest to
        /// the coast when the river is sitting in a depression.
        /// </summary>
        private static int NextDownhill(TerrainWorld world, int current, HashSet<int> visited)
        {
            int x = current % world.Width;
            int y = current / world.Width;
            float here = world.Elevation[current];

            int lowest = -1;
            float lowestElevation = here;
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

                    // Open water ends the trace immediately, whichever it is.
                    if (!world.Land[neighbour])
                        return neighbour;

                    if (visited.Contains(neighbour))
                        continue;

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

        /// <summary>
        /// A river gathers flow as it runs, so it widens downstream. The minimum
        /// is one sample of margin either side, because a river only one sample
        /// across is thinner than a pixel at normal zoom and reads as a seam
        /// rather than as water.
        /// </summary>
        private static int RadiusFor(TerrainWorld world, int step)
            => Mathf.Clamp(1 + (step / Mathf.Max(1, world.SamplesPerCell * 6)), 1, 3);

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
                    // Never overwrite the sea or a lake: a river joins them, it
                    // does not replace them.
                    if (!world.Land[at])
                        continue;

                    world.Land[at] = false;
                    world.Water[at] = WaterBody.River;
                }
            }
        }
    }
}
