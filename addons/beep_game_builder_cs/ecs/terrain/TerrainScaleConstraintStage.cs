using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Holds every FEATURE to a minimum size, so a map carries features rather
    /// than a scatter of the beginnings of features.
    ///
    /// The biome constraint made the terrain coherent. Everything else on the
    /// map has the same problem and for the same reason: lakes, relief, rivers
    /// and woods are all placed by thresholding a field, so on a small map the
    /// thresholds are met in a dozen places by one or two tiles each. Five
    /// puddles is not a lake district; a single raised tile is not a range; a
    /// three-tile watercourse is not a river.
    ///
    /// The rule is the one the strategy games use for terrain regions, applied
    /// to features: a thing must reach a minimum size in TILES to exist. It is
    /// absolute rather than a share, which is what makes the COUNT scale with
    /// the map - a small island has room for one lake, a continent for many,
    /// and neither needs to be told how many to have.
    ///
    /// A minimum alone is half the rule, because a feature can also be too big
    /// for what it sits on. A lake sized against the map rather than against its
    /// own island can take most of that island's interior, leaving a ring of
    /// shore that is an island in name only - measured at 24% of its own
    /// bounding box, which is the shape of a crescent, not of land. So lakes are
    /// bounded ABOVE as a share of their landmass as well as below in tiles.
    ///
    /// WHY IT RUNS LAST. It works on the reduced tile grid, because "six tiles"
    /// is only meaningful there, and after the feature stage because woods are
    /// placed then. Removing something here has to leave the map consistent:
    /// a drained lake is not a hole, it becomes the land around it, and a
    /// levelled peak stops being rock.
    /// </summary>
    internal static class TerrainScaleConstraintStage
    {
        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            if (!settings.UseScaleRules)
                return;

            DrainOversizedLakes(world);
            DrainSmallLakes(world);
            LevelSmallRelief(world);
            ClearShortRivers(world);
            ThinLoneFeatures(world);
        }

        /// <summary>
        /// A lake may not swallow the landmass it sits on.
        ///
        /// The minimum-size rule below cannot catch this: the offending lake is
        /// far too BIG, not too small. On a small island one lake can take most
        /// of the interior, leaving a ring of shore that is an island in name
        /// only - and it is the small islands this hits, because the lake stage
        /// sizes lakes against the map rather than against the island it lands
        /// on. Largest first, until what remains is a lake district rather than
        /// a lagoon.
        /// </summary>
        private static void DrainOversizedLakes(TerrainWorld world)
        {
            var bodies = new List<List<int>>();
            var bodyOf = new int[world.CellsWide * world.CellsHigh];
            System.Array.Fill(bodyOf, -1);

            // A landmass is everything that is not open sea: its land, plus any
            // lakes sitting inside it.
            foreach (List<int> body in Regions(world, at => InLandmass(world, at)))
            {
                foreach (int index in body)
                    bodyOf[index] = bodies.Count;
                bodies.Add(body);
            }

            var byBody = new Dictionary<int, List<List<int>>>();
            foreach (List<int> lake in Regions(world, at => world.CellWater[at] == WaterBody.Lake))
            {
                int id = bodyOf[lake[0]];
                if (id < 0)
                    continue;

                if (!byBody.TryGetValue(id, out List<List<int>>? found))
                {
                    found = new List<List<int>>();
                    byBody[id] = found;
                }
                found.Add(lake);
            }

            foreach ((int id, List<List<int>> lakes) in byBody)
            {
                int water = 0;
                foreach (List<int> lake in lakes)
                    water += lake.Count;

                int allowed = Mathf.FloorToInt(
                    bodies[id].Count * TerrainScaleRules.MaxLakeShareOfLandmass);
                if (water <= allowed)
                    continue;

                // What a drained lake bed becomes: the landmass's own commonest
                // ground, and NOT whatever borders the lake.
                //
                // Taking the neighbouring terrain per tile is the obvious thing
                // and it is wrong here, because what borders a lake is its
                // BEACH. Every drained bed came back as one large blob of sand -
                // the shore ring dilated across the whole lake. Shore kinds are
                // excluded for the same reason: on a small island the sand rim
                // can outnumber the interior and win the vote.
                string fill = DominantLand(world, bodies[id], NotLakeBedKinds) ?? "grass";

                lakes.Sort((left, right) => right.Count.CompareTo(left.Count));
                foreach (List<int> lake in lakes)
                {
                    if (water <= allowed)
                        break;

                    foreach (int index in lake)
                    {
                        world.CellWater[index] = WaterBody.None;
                        world.CellTerrain[index] = fill;
                    }
                    water -= lake.Count;
                }
            }
        }

        /// <summary>Land, or a lake inside it - anything that is not open sea.</summary>
        private static bool InLandmass(TerrainWorld world, int at)
        {
            if (world.CellWater[at] == WaterBody.Lake)
                return true;
            if (world.CellWater[at] != WaterBody.None)
                return false;

            return world.CellTerrain[at] is not ("" or "deep_water" or "shallow_water");
        }

        /// <summary>
        /// What a drained lake bed must NOT be made of: the shore that ringed
        /// the lake, and the peak materials, because a lake bed is the lowest
        /// flat ground on its landmass rather than the highest. Without the peak
        /// kinds, a rocky islet whose commonest ground is rock came back as a
        /// solid grey island.
        /// </summary>
        private static readonly HashSet<string> NotLakeBedKinds =
            new() { "sand", "gravel", "rock", "snow" };

        /// <summary>The commonest dry-land terrain in a landmass.</summary>
        private static string? DominantLand(
            TerrainWorld world, List<int> body, HashSet<string>? exclude = null)
        {
            var counts = new Dictionary<string, int>();
            foreach (int index in body)
            {
                if (world.CellWater[index] != WaterBody.None)
                    continue;

                string kind = world.CellTerrain[index];
                if (kind is "" or "deep_water" or "shallow_water")
                    continue;
                if (exclude is not null && exclude.Contains(kind))
                    continue;

                counts[kind] = counts.GetValueOrDefault(kind) + 1;
            }

            string? best = null;
            int most = 0;
            foreach ((string kind, int count) in counts)
            {
                if (count > most)
                {
                    best = kind;
                    most = count;
                }
            }
            return best;
        }

        /// <summary>
        /// A lake below the minimum is drained and becomes the land around it.
        /// Left in, a small map reads as puddled rather than lakeside.
        /// </summary>
        private static void DrainSmallLakes(TerrainWorld world)
        {
            foreach (List<int> region in Regions(world, at => world.CellWater[at] == WaterBody.Lake))
            {
                if (region.Count >= TerrainScaleRules.MinLakeTiles)
                    continue;

                foreach (int index in region)
                {
                    world.CellWater[index] = WaterBody.None;
                    world.CellTerrain[index] = NeighbourLand(world, index) ?? "grass";
                }
            }
        }

        /// <summary>
        /// A raised cluster below the minimum is levelled. One tile of mountain
        /// is not a range, and it is the single loudest piece of scatter on a
        /// small map because relief is drawn a whole level higher.
        /// </summary>
        private static void LevelSmallRelief(TerrainWorld world)
        {
            foreach (List<int> region in Regions(world, at => world.CellRelief[at] != TerrainRelief.Flat))
            {
                if (region.Count >= TerrainScaleRules.MinReliefTiles)
                    continue;

                foreach (int index in region)
                {
                    world.CellRelief[index] = TerrainRelief.Flat;

                    // Rock and snow are what the biome table gives a PEAK. With
                    // the peak gone they would be a bare grey patch on level
                    // ground, so the tile rejoins the terrain around it.
                    // The replacement must not itself be a peak kind. Taking
                    // the commonest neighbour outright hands a snowfield back
                    // its own snow - the relief goes flat, the terrain does not,
                    // and the map grows arctic ground at sea level.
                    if (world.CellTerrain[index] is "rock" or "snow" or "gravel")
                        world.CellTerrain[index] = NeighbourLand(world, index, PeakKinds) ?? "grass";
                }
            }
        }

        /// <summary>
        /// A watercourse too short to be a river is removed. A river that peters
        /// out after two tiles reads as a rendering fault, not as water.
        /// </summary>
        private static void ClearShortRivers(TerrainWorld world)
        {
            foreach (List<int> region in Regions(world, at => world.CellWater[at] == WaterBody.River))
            {
                if (region.Count >= TerrainScaleRules.MinRiverTiles)
                    continue;

                foreach (int index in region)
                {
                    world.CellWater[index] = WaterBody.None;
                    if (world.CellTerrain[index] is "shallow_water" or "deep_water")
                        world.CellTerrain[index] = NeighbourLand(world, index) ?? "grass";
                }
            }
        }

        /// <summary>
        /// A clump of woods below the minimum is cleared. Single trees dotted
        /// across a map are the vegetation equivalent of biome confetti.
        /// </summary>
        private static void ThinLoneFeatures(TerrainWorld world)
        {
            foreach (List<int> region in Regions(world, at => world.Feature[at].Length > 0))
            {
                if (region.Count >= TerrainScaleRules.MinFeatureTiles)
                    continue;

                foreach (int index in region)
                    world.Feature[index] = string.Empty;
            }
        }

        /// <summary>
        /// The land terrain bordering a cell, whichever borders it most. What a
        /// removed feature's tile becomes: the honest answer is its surroundings.
        /// </summary>
        /// <summary>
        /// Terrain that belongs to a PEAK. Never a replacement for one that has
        /// just been levelled.
        /// </summary>
        private static readonly HashSet<string> PeakKinds = new() { "rock", "snow", "gravel" };

        private static string? NeighbourLand(TerrainWorld world, int index, HashSet<string>? exclude = null)
        {
            int wide = world.CellsWide;
            int x = index % wide;
            int y = index / wide;
            var counts = new Dictionary<string, int>();

            for (int side = 0; side < 4; side++)
            {
                int nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0);
                int ny = y + (side == 2 ? 1 : side == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= wide || ny >= world.CellsHigh)
                    continue;

                int at = world.CellIndex(nx, ny);
                if (world.CellWater[at] != WaterBody.None)
                    continue;

                string kind = world.CellTerrain[at];
                if (kind is "" or "deep_water" or "shallow_water")
                    continue;
                if (exclude is not null && exclude.Contains(kind))
                    continue;

                counts[kind] = counts.GetValueOrDefault(kind) + 1;
            }

            string? best = null;
            int most = 0;
            foreach ((string kind, int count) in counts)
            {
                if (count > most)
                {
                    best = kind;
                    most = count;
                }
            }
            return best;
        }

        /// <summary>
        /// Every four-connected run of tiles matching the test, on the tile grid.
        /// </summary>
        private static List<List<int>> Regions(TerrainWorld world, System.Func<int, bool> matches)
        {
            int wide = world.CellsWide;
            int high = world.CellsHigh;
            var seen = new bool[wide * high];
            var found = new List<List<int>>();
            var queue = new Queue<int>();

            for (int start = 0; start < seen.Length; start++)
            {
                if (seen[start] || !matches(start))
                    continue;

                var region = new List<int>();
                queue.Clear();
                queue.Enqueue(start);
                seen[start] = true;

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    region.Add(index);
                    int x = index % wide;
                    int y = index / wide;

                    for (int side = 0; side < 4; side++)
                    {
                        int nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0);
                        int ny = y + (side == 2 ? 1 : side == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= wide || ny >= high)
                            continue;

                        int at = world.CellIndex(nx, ny);
                        if (seen[at] || !matches(at))
                            continue;

                        seen[at] = true;
                        queue.Enqueue(at);
                    }
                }

                found.Add(region);
            }

            return found;
        }
    }
}
