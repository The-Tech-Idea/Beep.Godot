using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Pulls the rainfall biomes into coherent regions, so a small map reads as
    /// a place rather than as a scatter of every climate at once.
    ///
    /// The problem this solves is specific to DISCRETE terrain. The biome table
    /// classifies every sample independently, so wherever rainfall wanders
    /// across a threshold it produces a lone tile of something else. A painter
    /// hides that by blending; a tilemap cannot, and draws it as confetti.
    ///
    /// The fix is the standard one: a Moore-neighbourhood majority filter. A
    /// cell that has few neighbours of its own kind takes the kind its
    /// neighbours actually are. Run once or twice it removes the isolated tiles
    /// and leaves the regions; run many times it erodes everything toward one
    /// kind, so the pass count is a dial and not a switch.
    ///
    /// WHAT IT WILL NOT TOUCH. Only the rainfall biomes - desert, dry grass,
    /// grass, swamp, jungle - are smoothed. Beaches, peaks, snow, tundra and
    /// gravel are STRUCTURAL: they are placed by where the coast and the relief
    /// are, not by a threshold on a noise field, and a majority filter would
    /// happily erase a one-tile beach or shave the cap off a mountain. Smoothing
    /// those would not be tidying the map, it would be deleting it.
    /// </summary>
    internal static class TerrainCoherenceStage
    {
        /// <summary>
        /// The kinds the rainfall table decides. Only these are SMOOTHED: they
        /// come from a threshold on a noise field, so a lone tile of one is
        /// noise rather than a feature.
        /// </summary>
        private static readonly string[] RainfallKinds = { "desert", "dry_grass", "grass", "swamp", "jungle" };
        private static readonly HashSet<string> Rainfall = new(RainfallKinds);

        // Absorbable, AbsorbTarget and the peak materials moved to the terrain-kind catalog (DUP-13);
        // TerrainKind documents each, and TerrainKindCatalog.Standard.PeakMaterialKinds is the set.

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            Smooth(world, settings);
            AbsorbSmallRegions(world, settings);
        }

        /// <summary>
        /// Absorbs any biome region too small to be a region.
        ///
        /// This is what keeps a small map from holding every climate at once,
        /// and it is how Civilization does it - not by capping how many biomes a
        /// map may have, but by requiring each to muster a region of a minimum
        /// size stated relative to the landmass. A continent has room for
        /// several; an island does not, so it ends up with one or two without
        /// anyone declaring a number. The count falls out of the area.
        ///
        /// A region below the threshold is handed to whichever biome borders it
        /// most. It cannot simply be deleted: the tiles have to become
        /// something, and the honest answer is whatever surrounds them.
        /// </summary>
        private static void AbsorbSmallRegions(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            float fraction = settings.MinBiomeRegionFraction;
            if (fraction <= 0.0f)
                return;

            int land = TerrainGeometry.CountTrue(world.Land);
            if (land == 0)
                return;

            // Counted in SAMPLES, because that is the resolution the world is
            // stored at; the fraction is of the land either way.
            int minSamples = Mathf.Max(1, Mathf.RoundToInt(land * fraction));

            // What an orphaned region becomes when nothing eligible borders it:
            // whichever rainfall biome covers most of the land.
            var tally = new Dictionary<string, int>();
            for (int i = 0; i < world.Terrain.Length; i++)
            {
                if (world.Land[i] && Rainfall.Contains(world.Terrain[i]))
                    tally[world.Terrain[i]] = tally.GetValueOrDefault(world.Terrain[i]) + 1;
            }

            string fallback = TerrainGeometry.MostCommon(tally, "grass");

            // Repeated, because absorbing one region can leave its neighbour
            // still short - and the point is that a kind which never reaches the
            // size disappears rather than surviving as fragments.
            for (int pass = 0; pass < 8; pass++)
            {
                if (!AbsorbOnce(world, minSamples, fallback))
                    break;
            }
        }

        /// <summary>Absorbs every undersized region once; true if anything changed.</summary>
        private static bool AbsorbOnce(TerrainGenerationBuffer world, int minSamples, string fallback)
        {
            // The search queue doubles as the region: every sample enters it
            // once, in the order it is reached, so when a search ends the run it
            // filled IS the region in the order the search visited it.
            bool[] seen = world.BoolScratch;
            Array.Clear(seen);
            int[] queue = world.IntScratchA;
            Span<int> around = stackalloc int[4];
            var borders = new Dictionary<string, int>();
            bool changed = false;

            for (int start = 0; start < world.Terrain.Length; start++)
            {
                if (seen[start] || !world.Land[start] || !TerrainKindCatalog.Standard.Absorbable(world.Terrain[start]))
                    continue;

                string kind = world.Terrain[start];
                borders.Clear();
                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                seen[start] = true;

                while (head < tail)
                {
                    int index = queue[head++];

                    int sides = TerrainGeometry.Neighbours4(index, world.Width, world.Height, around);
                    for (int side = 0; side < sides; side++)
                    {
                        int at = around[side];
                        if (!world.Land[at])
                            continue;

                        string other = world.Terrain[at];
                        if (other == kind)
                        {
                            if (!seen[at])
                            {
                                seen[at] = true;
                                queue[tail++] = at;
                            }
                        }
                        else if (TerrainKindCatalog.Standard.AbsorbTarget(other))
                        {
                            borders[other] = borders.GetValueOrDefault(other) + 1;
                        }
                    }
                }

                ReadOnlySpan<int> region = queue.AsSpan(0, tail);
                if (region.Length >= minSamples)
                    continue;

                // Raised snow/tundra regions may merge into exposed peak material.
                // Rainfall ground cover must not become rock just because it is
                // elevated or touches a rocky neighbour.
                int raised = 0;
                foreach (int at in region)
                {
                    if (world.Relief[at] != TerrainRelief.Flat)
                        raised++;
                }

                if (Rainfall.Contains(kind) || raised * 2 < region.Length)
                {
                    foreach (string peak in TerrainKindCatalog.Standard.PeakMaterialKinds)
                        borders.Remove(peak);
                }

                // A region below the minimum must not survive for want of a
                // neighbour to become. Beaches were doing exactly that: a snow
                // patch ringed by sand had no eligible border, was skipped, and
                // stayed - so widening the beach put arctic ground on a
                // temperate island. The fallback is the biome the map is mostly
                // made of, which is what the region would have joined anyway.
                string winner = borders.Count > 0 ? TerrainGeometry.MostCommon(borders, kind) : fallback;

                if (winner == kind)
                    continue;

                foreach (int index in region)
                    world.Terrain[index] = winner;

                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// A kind's position in RainfallKinds plus one, or zero for anything the
        /// rainfall table did not decide - water, beach, peak, tundra.
        /// </summary>
        private static byte RainfallIndex(string kind)
        {
            for (int i = 0; i < RainfallKinds.Length; i++)
            {
                if (RainfallKinds[i] == kind)
                    return (byte)(i + 1);
            }
            return 0;
        }

        private static void Smooth(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            int passes = settings.BiomeCoherencePasses;
            if (passes <= 0)
                return;

            // Neighbours are sampled a whole TILE away, not one sample away. The
            // world is stored below tile resolution, so a one-sample
            // neighbourhood would smooth detail within a tile and leave the
            // tile-sized speckle - the only part anyone can see - untouched.
            int reach = Mathf.Max(1, world.SamplesPerCell);

            // What every sample was when the pass began, as a rainfall index:
            // the vote only asks whether a neighbour is land and, if so, which
            // rainfall kind it is, so a byte per sample answers it. Each pass
            // used to clone the whole string field to remember this.
            byte[] before = world.ByteScratch;
            // Votes per rainfall kind, and the kinds in the order they were
            // first met - the order the winner is chosen in, so a tie between
            // two neighbouring kinds still goes to the one met first.
            Span<int> counts = stackalloc int[RainfallKinds.Length + 1];
            Span<byte> met = stackalloc byte[RainfallKinds.Length];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int index = 0; index < world.Count; index++)
                    before[index] = RainfallIndex(world.Terrain[index]);
                for (int y = 0; y < world.Height; y++)
                {
                    for (int x = 0; x < world.Width; x++)
                    {
                        int index = world.Index(x, y);
                        if (!world.Land[index] || before[index] == 0)
                            continue;

                        counts.Clear();
                        int metCount = 0;
                        int own = 0;
                        int total = 0;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;

                                int nx = x + (dx * reach);
                                int ny = y + (dy * reach);
                                if (nx < 0 || ny < 0 || nx >= world.Width || ny >= world.Height)
                                    continue;

                                int at = world.Index(nx, ny);
                                if (!world.Land[at])
                                    continue;

                                byte kind = before[at];
                                total++;
                                if (kind == before[index])
                                    own++;

                                // Only a rainfall neighbour may win the vote. A
                                // beach or a peak beside a meadow is a boundary,
                                // not a majority the meadow should join.
                                if (kind == 0)
                                    continue;
                                if (counts[kind]++ == 0)
                                    met[metCount++] = kind;
                            }
                        }

                        // A cell with company keeps its kind. Only the ones
                        // standing nearly alone are reassigned.
                        if (total < 3 || own >= settings.BiomeCoherenceKeep)
                            continue;

                        byte best = before[index];
                        int bestCount = own;
                        for (int i = 0; i < metCount; i++)
                        {
                            byte kind = met[i];
                            if (counts[kind] > bestCount)
                            {
                                best = kind;
                                bestCount = counts[kind];
                            }
                        }

                        world.Terrain[index] = RainfallKinds[best - 1];
                    }
                }
            }
        }
    }
}
