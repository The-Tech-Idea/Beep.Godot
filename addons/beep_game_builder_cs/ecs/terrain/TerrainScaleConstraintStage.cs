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
        /// <summary>
        /// The constraints on the LAND itself, which have to settle before
        /// anything is placed on it.
        ///
        /// Draining a lake turns its bed into ground, and ground grows things.
        /// Running that after the feature stage meant vegetation was placed on a
        /// map that did not exist yet: five islands in a twelve-island chain
        /// were mostly lake when the woods were sown, so nothing could be sown
        /// on them, and they finished as bare grassland once the lakes drained.
        /// </summary>
        public static void ApplyTerrain(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            if (!settings.UseScaleRules)
                return;

            DrainOversizedLakes(world);
            DrainSmallLakes(world);
            // Themed ground is classified by elevation, not by relief tiers.
            // Flattening a volcanic outcrop must not turn its basalt into lava/grass.
            bool themedGround = TerrainBiomeStage.ThemedKind(settings.Preset, 0f) is not null;
            LevelSmallRelief(world, themedGround);
            ClearShortRivers(world);
            ClearOrphanWaterSamples(world);
            if (!themedGround) GroundPeakMaterial(world);
        }

        /// <summary>
        /// Stone belongs on high ground. Any peak material left on the flat is
        /// given the ground around it instead.
        ///
        /// This is stated ONCE here rather than relied upon at every stage that
        /// touches terrain or relief, and the difference is not academic: the
        /// coherence stage was fixed so it could not absorb a flat region into
        /// rock, and adding erosion promptly produced five flat rock tiles by
        /// another route, because erosion changes WHICH tiles are mountains and
        /// the reduction that follows had its own way of getting there.
        ///
        /// An invariant that every upstream stage must remember to preserve is
        /// one that breaks whenever a new stage is added. Enforced in one place,
        /// last, it holds however the map got here.
        /// </summary>
        private static void GroundPeakMaterial(TerrainGenerationBuffer world)
        {
            for (int index = 0; index < world.CellTerrain.Length; index++)
            {
                if (world.CellWater[index] != WaterBody.None)
                    continue;
                if (world.CellRelief[index] != TerrainRelief.Flat)
                    continue;
                if (!PeakKinds.Contains(world.CellTerrain[index]))
                    continue;

                ReplacePeakMaterial(
                    world, index, NeighbourLand(world, index, PeakKinds) ?? "grass",
                    clearRelief: false);
            }
        }

        /// <summary>
        /// The constraints on what STANDS on the land, which can only run once
        /// it has been placed.
        /// </summary>
        public static void ApplyFeatures(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            if (!settings.UseScaleRules)
                return;

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
        private static void DrainOversizedLakes(TerrainGenerationBuffer world)
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
                        SetTile(world, index, WaterBody.None, fill, land: true);
                    water -= lake.Count;
                }
            }
        }

        /// <summary>
        /// Changes a tile, and the SAMPLES underneath it, together.
        ///
        /// The constraints run on the reduced tile grid, but the sample grid it
        /// was reduced from is still there and still read: the painted view
        /// draws from samples, while the tile and isometric views draw from
        /// cells. Writing only the cell leaves the two disagreeing about the
        /// same ground - and the bigger the change, the more obvious it gets.
        /// Draining a lake that covered most of a continent removed it from the
        /// tile view and left it, whole, in the painted one.
        ///
        /// A tile is not a separate thing from its samples; it is a summary of
        /// them. Changing the summary without the thing it summarises is what
        /// made one map look like two.
        /// </summary>
        private static void SetTile(
            TerrainGenerationBuffer world, int cell, WaterBody water, string terrain, bool land)
        {
            world.CellWater[cell] = water;
            world.CellTerrain[cell] = terrain;

            int cellX = cell % world.CellsWide;
            int cellY = cell / world.CellsWide;
            int samples = Mathf.Max(1, world.SamplesPerCell);

            for (int offsetY = 0; offsetY < samples; offsetY++)
            {
                int y = (cellY * samples) + offsetY;
                if (y >= world.Height)
                    continue;

                for (int offsetX = 0; offsetX < samples; offsetX++)
                {
                    int x = (cellX * samples) + offsetX;
                    if (x >= world.Width)
                        continue;

                    int sample = world.Index(x, y);
                    world.Water[sample] = water;
                    world.Terrain[sample] = terrain;
                    world.Land[sample] = land;
                }
            }
        }

        /// <summary>Land, or a lake inside it - anything that is not open sea.</summary>
        private static bool InLandmass(TerrainGenerationBuffer world, int at)
        {
            if (world.CellWater[at] == WaterBody.Lake)
                return true;
            if (world.CellWater[at] != WaterBody.None)
                return false;

            return TerrainTileSets.IsLandKind(world.CellTerrain[at]);
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
            TerrainGenerationBuffer world, List<int> body, HashSet<string>? exclude = null)
        {
            var counts = new Dictionary<string, int>();
            foreach (int index in body)
            {
                if (world.CellWater[index] != WaterBody.None)
                    continue;

                string kind = world.CellTerrain[index];
                if (!TerrainTileSets.IsLandKind(kind))
                    continue;
                if (exclude is not null && exclude.Contains(kind))
                    continue;

                counts[kind] = counts.GetValueOrDefault(kind) + 1;
            }

            return TerrainGeometry.MostCommon(counts, null);
        }

        /// <summary>
        /// A lake below the minimum is drained and becomes the land around it.
        /// Left in, a small map reads as puddled rather than lakeside.
        /// </summary>
        private static void DrainSmallLakes(TerrainGenerationBuffer world)
        {
            foreach (List<int> region in Regions(world, at => world.CellWater[at] == WaterBody.Lake))
            {
                if (region.Count >= TerrainScaleRules.MinLakeTiles)
                    continue;

                foreach (int index in region)
                {
                    SetTile(
                        world, index, WaterBody.None,
                        NeighbourLand(world, index) ?? "grass", land: true);
                }
            }
        }

        /// <summary>
        /// A raised cluster below the minimum is levelled. One tile of mountain
        /// is not a range, and it is the single loudest piece of scatter on a
        /// small map because relief is drawn a whole level higher.
        /// </summary>
        private static void LevelSmallRelief(TerrainGenerationBuffer world, bool themedGround)
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
                    string terrain = !themedGround && PeakKinds.Contains(world.CellTerrain[index])
                        ? NeighbourLand(world, index, PeakKinds) ?? "grass"
                        : world.CellTerrain[index];
                    ReplacePeakMaterial(world, index, terrain, clearRelief: true, replaceMaterial: !themedGround);
                }
            }
        }

        private static void ReplacePeakMaterial(
            TerrainGenerationBuffer world, int cell, string terrain, bool clearRelief, bool replaceMaterial = true)
        {
            if (replaceMaterial) world.CellTerrain[cell] = terrain;
            int samples = world.SamplesPerCell;
            int startX = (cell % world.CellsWide) * samples;
            int startY = (cell / world.CellsWide) * samples;
            for (int y = startY; y < startY + samples; y++)
            for (int x = startX; x < startX + samples; x++)
            {
                int sample = world.Index(x, y);
                // Removing a relief tier is not reclamation or excavation:
                // retain fine shores, non-peak biome detail, elevation and shade.
                if (!world.Land[sample])
                    continue;
                if (clearRelief)
                    world.Relief[sample] = TerrainRelief.Flat;
                if (replaceMaterial && PeakKinds.Contains(world.Terrain[sample]))
                    world.Terrain[sample] = terrain;
            }
        }

        /// <summary>
        /// A watercourse too short to be a river is removed. A river that peters
        /// out after two tiles reads as a rendering fault, not as water.
        /// </summary>
        private static void ClearShortRivers(TerrainGenerationBuffer world)
        {
            foreach (List<int> region in Regions(world, at => world.CellWater[at] == WaterBody.River))
            {
                if (region.Count >= TerrainScaleRules.MinRiverTiles)
                    continue;

                foreach (int index in region)
                {
                    SetTile(
                        world, index, WaterBody.None,
                        NeighbourLand(world, index) ?? "grass", land: true);
                }
            }
        }

        private static void ClearOrphanWaterSamples(TerrainGenerationBuffer world)
        {
            // Reduction can put a body's thin fringe in an otherwise dry cell.
            // Clearing only the removed water cells leaves that fringe behind.
            var seen = new bool[world.Count];
            var region = new List<int>();
            for (int start = 0; start < world.Count; start++)
            {
                WaterBody kind = world.Water[start];
                if (seen[start] || kind is not (WaterBody.Lake or WaterBody.River)) continue;
                region.Clear();
                region.Add(start);
                seen[start] = true;
                bool retained = false;
                for (int read = 0; read < region.Count; read++)
                {
                    int current = region[read];
                    int x = current % world.Width, y = current / world.Width;
                    int cell = world.CellIndex(x / world.SamplesPerCell, y / world.SamplesPerCell);
                    retained |= world.CellWater[cell] == kind;
                    for (int side = 0; side < 4; side++)
                    {
                        int nx = x + (side == 0 ? -1 : side == 1 ? 1 : 0);
                        int ny = y + (side == 2 ? -1 : side == 3 ? 1 : 0);
                        if (!world.InBounds(nx, ny)) continue;
                        int next = world.Index(nx, ny);
                        if (seen[next] || world.Water[next] != kind) continue;
                        seen[next] = true;
                        region.Add(next);
                    }
                }
                if (retained) continue;
                foreach (int sample in region)
                {
                    int cell = world.CellIndex((sample % world.Width) / world.SamplesPerCell,
                        (sample / world.Width) / world.SamplesPerCell);
                    world.Water[sample] = world.CellWater[cell];
                    world.Land[sample] = world.CellWater[cell] == WaterBody.None;
                    world.Terrain[sample] = world.CellTerrain[cell];
                    world.Relief[sample] = world.CellRelief[cell];
                    world.Elevation[sample] = world.CellElevation[cell];
                    world.Shade[sample] = world.CellShade[cell];
                }
            }
        }

        /// <summary>
        /// A clump of woods below the minimum is cleared. Single trees dotted
        /// across a map are the vegetation equivalent of biome confetti.
        /// </summary>
        private static void ThinLoneFeatures(TerrainGenerationBuffer world)
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
        /// Terrain that belongs to a PEAK. Never a replacement for one that has
        /// just been levelled.
        /// </summary>
        private static readonly HashSet<string> PeakKinds = new() { "rock", "snow", "gravel" };

        /// <summary>
        /// The land terrain bordering a cell, whichever borders it most. What a
        /// removed feature's tile becomes: the honest answer is its surroundings.
        /// </summary>
        private static string? NeighbourLand(TerrainGenerationBuffer world, int index, HashSet<string>? exclude = null)
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
                if (!TerrainTileSets.IsLandKind(kind))
                    continue;
                if (exclude is not null && exclude.Contains(kind))
                    continue;

                counts[kind] = counts.GetValueOrDefault(kind) + 1;
            }

            return TerrainGeometry.MostCommon(counts, null);
        }

        /// <summary>
        /// Every four-connected run of tiles matching the test, on the tile grid.
        /// </summary>
        private static List<List<int>> Regions(TerrainGenerationBuffer world, System.Func<int, bool> matches)
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
