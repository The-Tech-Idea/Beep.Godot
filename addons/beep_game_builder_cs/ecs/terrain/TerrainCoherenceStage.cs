using Godot;
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
        private static readonly HashSet<string> Rainfall = new()
        {
            "desert", "dry_grass", "grass", "swamp", "jungle",
        };

        /// <summary>
        /// Kinds whose REGIONS may be dissolved when too small for the landmass.
        ///
        /// Wider than the rainfall set, because snow and tundra are the clearest
        /// case of the thing this exists to prevent: a small island cannot have
        /// an ice cap. They arrive here from altitude cooling on a peak or two,
        /// which is a couple of tiles of arctic on a temperate island - not a
        /// climate the map has, just a threshold clipped. Dissolve them and the
        /// peaks are bare rock, which is what a small island's peaks are.
        ///
        /// This stays self-regulating: on a genuinely cold map snow covers most
        /// of the land, clears the minimum easily, and is left alone.
        /// </summary>
        private static readonly HashSet<string> Absorbable = new()
        {
            "desert", "dry_grass", "grass", "swamp", "jungle", "snow", "tundra",
        };

        /// <summary>
        /// What an absorbed region may become. Rock and gravel are included so a
        /// dissolved snow cap has somewhere to go - a peak surrounded only by
        /// rock would otherwise have no candidate and survive by default.
        ///
        /// Sand is deliberately absent: it is the beach, one tile wide and
        /// placed by where the coast is, so letting an inland region become sand
        /// would put a beach in the middle of the map.
        /// </summary>
        private static readonly HashSet<string> AbsorbTargets = new()
        {
            "desert", "dry_grass", "grass", "swamp", "jungle", "snow", "tundra", "rock", "gravel",
        };

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
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
        private static void AbsorbSmallRegions(TerrainWorld world, TerrainGenerationSettings settings)
        {
            float fraction = settings.MinBiomeRegionFraction;
            if (fraction <= 0.0f)
                return;

            int land = 0;
            for (int i = 0; i < world.Land.Length; i++)
            {
                if (world.Land[i])
                    land++;
            }

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

            string fallback = "grass";
            int mostSeen = 0;
            foreach ((string kind, int count) in tally)
            {
                if (count > mostSeen)
                {
                    fallback = kind;
                    mostSeen = count;
                }
            }

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
        private static bool AbsorbOnce(TerrainWorld world, int minSamples, string fallback)
        {
            var seen = new bool[world.Terrain.Length];
            var region = new List<int>();
            var queue = new Queue<int>();
            var borders = new Dictionary<string, int>();
            bool changed = false;

            for (int start = 0; start < world.Terrain.Length; start++)
            {
                if (seen[start] || !world.Land[start] || !Absorbable.Contains(world.Terrain[start]))
                    continue;

                string kind = world.Terrain[start];
                region.Clear();
                queue.Clear();
                borders.Clear();
                queue.Enqueue(start);
                seen[start] = true;

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    region.Add(index);
                    int x = index % world.Width;
                    int y = index / world.Width;

                    for (int side = 0; side < 4; side++)
                    {
                        int nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0);
                        int ny = y + (side == 2 ? 1 : side == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= world.Width || ny >= world.Height)
                            continue;

                        int at = world.Index(nx, ny);
                        if (!world.Land[at])
                            continue;

                        string other = world.Terrain[at];
                        if (other == kind)
                        {
                            if (!seen[at])
                            {
                                seen[at] = true;
                                queue.Enqueue(at);
                            }
                        }
                        else if (AbsorbTargets.Contains(other))
                        {
                            borders[other] = borders.GetValueOrDefault(other) + 1;
                        }
                    }
                }

                if (region.Count >= minSamples)
                    continue;

                // A region below the minimum must not survive for want of a
                // neighbour to become. Beaches were doing exactly that: a snow
                // patch ringed by sand had no eligible border, was skipped, and
                // stayed - so widening the beach put arctic ground on a
                // temperate island. The fallback is the biome the map is mostly
                // made of, which is what the region would have joined anyway.
                string winner = borders.Count > 0 ? kind : fallback;
                int best = 0;
                foreach ((string other, int count) in borders)
                {
                    if (count > best)
                    {
                        winner = other;
                        best = count;
                    }
                }

                if (winner == kind)
                    continue;

                foreach (int index in region)
                    world.Terrain[index] = winner;

                changed = true;
            }

            return changed;
        }

        private static void Smooth(TerrainWorld world, TerrainGenerationSettings settings)
        {
            int passes = settings.BiomeCoherencePasses;
            if (passes <= 0)
                return;

            // Neighbours are sampled a whole TILE away, not one sample away. The
            // world is stored below tile resolution, so a one-sample
            // neighbourhood would smooth detail within a tile and leave the
            // tile-sized speckle - the only part anyone can see - untouched.
            int reach = Mathf.Max(1, world.SamplesPerCell);
            var counts = new Dictionary<string, int>();

            for (int pass = 0; pass < passes; pass++)
            {
                string[] before = (string[])world.Terrain.Clone();
                for (int y = 0; y < world.Height; y++)
                {
                    for (int x = 0; x < world.Width; x++)
                    {
                        int index = world.Index(x, y);
                        if (!world.Land[index] || !Rainfall.Contains(before[index]))
                            continue;

                        counts.Clear();
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

                                string kind = before[at];
                                total++;
                                if (kind == before[index])
                                    own++;

                                // Only a rainfall neighbour may win the vote. A
                                // beach or a peak beside a meadow is a boundary,
                                // not a majority the meadow should join.
                                if (Rainfall.Contains(kind))
                                    counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            }
                        }

                        // A cell with company keeps its kind. Only the ones
                        // standing nearly alone are reassigned.
                        if (total < 3 || own >= settings.BiomeCoherenceKeep)
                            continue;

                        string best = before[index];
                        int bestCount = own;
                        foreach ((string kind, int count) in counts)
                        {
                            if (count > bestCount)
                            {
                                best = kind;
                                bestCount = count;
                            }
                        }

                        world.Terrain[index] = best;
                    }
                }
            }
        }
    }
}
