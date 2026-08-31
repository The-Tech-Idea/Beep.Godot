using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Assigns terrain FEATURES - woods, jungle, marsh, oasis - as a layer over
    /// the base terrain, the way Civilization models them: a tile is grassland
    /// *with* woods on it, not a separate "forest terrain".
    ///
    /// Keeping them separate is what lets the renderer draw a canopy as an
    /// object standing on the ground, instead of recolouring the ground darker
    /// and hoping it reads as forest.
    ///
    /// Features are decided per gameplay tile, after the tile reduction, so a
    /// feature always covers a whole tile and can never half-cover one.
    /// </summary>
    internal static class TerrainFeatureStage
    {

        /// <summary>
        /// Half-width of the band the vegetation fractal actually occupies.
        /// Measured, not guessed: fbm output sits overwhelmingly within about
        /// this much of zero, so it is what the field must be stretched by.
        /// </summary>
        private const float StandSpread = 0.32f;

        public const string None = "";
        public const string Woods = "woods";

        /// <summary>A dense stand, as opposed to the scattered woodland of Woods.</summary>
        public const string Forest = "forest";
        public const string Jungle = "jungle";
        public const string Marsh = "marsh";
        public const string Oasis = "oasis";

        public static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
        {
            if (settings.FeatureDensity <= 0.0f)
                return;

            int wide = world.CellsWide;
            int high = world.CellsHigh;
            int count = wide * high;

            // Vegetation comes from a FIELD, not a per-tile roll. A roll gives
            // every tile an independent verdict, so however the odds are
            // weighted the result is uniform speckle that can never form the
            // edge of a wood. A thresholded field makes stands connected, with
            // clearings between them - the same reason land is a thresholded
            // fractal rather than scattered dots.
            var stand = new float[count];
            var eligible = new bool[count];
            for (int cellY = 0; cellY < high; cellY++)
            {
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    int cell = world.CellIndex(cellX, cellY);
                    if (world.CellWater[cell] != WaterBody.None)
                        continue;

                    // Mountains carry no vegetation: a canopy drawn over a peak
                    // reads as a mistake.
                    if (world.CellRelief[cell] == TerrainRelief.Mountains)
                        continue;

                    // Eligible means WOODS-CAPABLE, not merely land. Jungle,
                    // swamp and desert get their features unconditionally, and
                    // ground too dry or too cold for trees must not sit in the
                    // ranking - it would be excluded by the moisture gate anyway
                    // AND drag the budget down, penalising the same tile twice.
                    string kind = world.CellTerrain[cell];
                    if (kind is not ("grass" or "dry_grass" or "tundra"))
                        continue;

                    // Dryness is not re-tested here. Whether ground is too dry
                    // for trees is already decided, by the biome - and with
                    // quotas on, the biome bands are PERCENTILES of the map's own
                    // moisture while this test was a fixed 0.26. On a dry map the
                    // whole grass band sits below that number, so every grass
                    // tile failed a gate it could never pass and whole islands of
                    // good ground grew nothing.
                    //
                    // One classification decides both the ground and what stands
                    // on it, which is how the standard biome model works: a
                    // biome and its plant cover are read off the same climate,
                    // never off two disagreeing tests.
                    int centre = world.CellCentreIndex(cellX, cellY);
                    if (world.Temperature[centre] < 0.15f)
                        continue;

                    // The ranked value is the value that gets thresholded,
                    // bias included. Ranking the bare noise and then comparing
                    // noise-plus-bias against that ranking is a quiet mistake:
                    // the bias is negative on dry ground, uniformly so across a
                    // dry island, so every cell there fell below a threshold
                    // drawn from its own unbiased field and the island grew
                    // nothing. Five islands with 49 to 171 grass tiles each came
                    // out bare that way.
                    eligible[cell] = true;
                    stand[cell] = noise.Vegetation.GetNoise2D(cellX + 0.5f, cellY + 0.5f)
                        + StandBias(world, settings, cell, cellX, cellY, kind);
                }
            }

            // The threshold is a PERCENTILE of the field, not a fixed level.
            // Measured, fbm output here spans about -0.26 to +0.27 with its
            // median below zero, so a level chosen as "0.85" covers a few percent
            // of tiles rather than the fifteen it looks like. Ranking the field
            // makes the dial mean the coverage it claims, which is how the
            // landmass and hill stages already work.
            float wanted = Mathf.Clamp(AverageWetness(world, settings), 0.0f, 0.95f);

            // The threshold is ranked PER LANDMASS, not over the whole map.
            //
            // One ranking for every island means the top slice of a smooth field
            // can land almost entirely on one of them, and it does: measured on
            // a 128x80 map, a quadrant with 886 woods-capable tiles grew ZERO
            // trees while another with 919 grew a quarter of them, and on 48x48
            // the whole right half was bare grass. Nothing was wrong with the
            // eligibility - every quadrant had ample land that could carry woods
            // - the global cut simply never reached it. Raising the noise
            // frequency only rearranges which half loses.
            //
            // Ranking within each landmass makes the coverage dial mean what it
            // says everywhere: every island gets its own share of woods, and
            // where they fall on that island is still the noise's business. It
            // is the same rule the lakes follow - a feature is bounded by the
            // landmass it sits on, not by the map.
            var thresholds = new Dictionary<int, float>();
            var densities = new Dictionary<int, float>();
            var islands = new HashSet<int>();
            for (int index = 0; index < eligible.Length; index++)
            {
                if (eligible[index])
                    islands.Add(world.CellContinent[index]);
            }

            var mask = new bool[eligible.Length];
            foreach (int island in islands)
            {
                for (int index = 0; index < mask.Length; index++)
                    mask[index] = eligible[index] && world.CellContinent[index] == island;

                thresholds[island] = TerrainGeometry.Percentile(stand, mask, 1.0f - wanted);
                densities[island] = TerrainGeometry.Percentile(stand, mask, 1.0f - (wanted * 0.45f));
            }

            for (int cellY = 0; cellY < high; cellY++)
            {
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    int cell = world.CellIndex(cellX, cellY);
                    if (world.CellWater[cell] != WaterBody.None)
                        continue;
                    if (world.CellRelief[cell] == TerrainRelief.Mountains)
                        continue;

                    int island = world.CellContinent[cell];
                    if (!thresholds.TryGetValue(island, out float threshold))
                        continue;

                    world.Feature[cell] = Choose(
                        world, settings, cell, cellX, cellY, stand[cell],
                        threshold, densities[island]);
                }
            }
        }

        /// <summary>
        /// How much of the eligible land should carry vegetation, averaged over
        /// the map. Per-cell moisture still decides WHERE within that budget,
        /// but the budget itself has to be one number for a percentile to mean
        /// anything.
        /// </summary>
        /// <summary>
        /// How much of the woods-capable land should carry vegetation, from the
        /// map's climate.
        ///
        /// Measured over ALL LAND rather than over the eligible cells, and that
        /// distinction is the whole point. These are two different questions -
        /// how wet is this world, and where on it may a tree stand - and
        /// answering the first over the answer to the second couples them:
        /// widening eligibility then pulled dry cells into the average, dropped
        /// the coverage, and left FEWER woods than before. Measured when that
        /// happened: seven bare islands became eleven.
        ///
        /// The 0.26 anchor stays absolute here, because this is the question it
        /// actually answers well - a wet world is greener than a dry one, and
        /// that is a fact about the world, not about the map's own spread.
        /// </summary>
        private static float AverageWetness(
            TerrainWorld world, TerrainGenerationSettings settings)
        {
            float total = 0.0f;
            int seen = 0;
            for (int cell = 0; cell < world.CellTerrain.Length; cell++)
            {
                if (world.CellWater[cell] != WaterBody.None)
                    continue;

                string kind = world.CellTerrain[cell];
                if (kind is "" or "deep_water" or "shallow_water")
                    continue;

                int sample = world.CellCentreIndex(cell % world.CellsWide, cell / world.CellsWide);
                float moisture = world.Moisture[sample];
                total += Mathf.Clamp((moisture - 0.26f) * 2.6f, 0.0f, 0.85f);
                seen++;
            }
            if (seen == 0)
                return 0.0f;
            return (total / seen) * Mathf.Clamp(settings.FeatureDensity, 0.0f, 4.0f);
        }

        private static string Choose(
            TerrainWorld world,
            TerrainGenerationSettings settings,
            int cell,
            int cellX,
            int cellY,
            float stand,
            float threshold,
            float dense)
        {
            string terrain = world.CellTerrain[cell];
            int sample = world.CellCentreIndex(cellX, cellY);
            float moisture = world.Moisture[sample];
            float temperature = world.Temperature[sample];
            float roll = Hash01(settings.Seed + 55001, cellX, cellY);
            float density = Mathf.Clamp(settings.FeatureDensity, 0.0f, 4.0f);

            // Terrain that already means dense vegetation always carries the
            // matching feature, so the ground under it can be ordinary soil.
            if (terrain == "jungle")
                return Jungle;
            if (terrain == "swamp")
                return Marsh;

            // An oasis is the rare exception that makes a desert readable.
            if (terrain == "desert")
                return roll < 0.012f * density ? Oasis : None;

            if (terrain is not ("grass" or "dry_grass" or "tundra"))
                return None;

            // Cold is the floor here, not dryness - and this has to agree with
            // the eligibility test, because a cell ranked there and rejected
            // here would take a share of the budget and grow nothing with it.
            if (temperature < 0.15f)
                return None;

            // The bias is already in `stand`, applied where the field was
            // ranked, so this is a straight comparison against the threshold
            // that ranking produced.
            if (stand < threshold)
                return None;

            // Well inside a stand is closed forest; the fringe is open woodland.
            // Drawing both the same is what makes a wood read as a texture rather
            // than a place with a middle and an edge.
            return stand >= dense ? Forest : Woods;
        }

        /// <summary>
        /// What shifts a cell's place in the ranking: wetter ground ranks
        /// higher, so stands thicken toward it as bigger woods rather than a
        /// finer sprinkle, with jitter to keep the edge off a clean contour and
        /// a penalty for tundra, where trees are marginal.
        /// </summary>
        private static float StandBias(
            TerrainWorld world, TerrainGenerationSettings settings,
            int cell, int cellX, int cellY, string terrain)
        {
            int sample = world.CellCentreIndex(cellX, cellY);
            float moisture = world.Moisture[sample];
            float roll = Hash01(settings.Seed + 55001, cellX, cellY);

            float bias = ((moisture - 0.45f) * 0.10f) + ((roll - 0.5f) * 0.02f);
            return terrain == "tundra" ? bias - 0.05f : bias;
        }

        private static float Hash01(int seed, int x, int y)
        {
            uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215.0f;
        }
    }
}
