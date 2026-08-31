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

                    int centre = world.CellCentreIndex(cellX, cellY);
                    if (world.Moisture[centre] < 0.26f || world.Temperature[centre] < 0.15f)
                        continue;

                    eligible[cell] = true;
                    stand[cell] = noise.Vegetation.GetNoise2D(cellX + 0.5f, cellY + 0.5f);
                }
            }

            // The threshold is a PERCENTILE of the field, not a fixed level.
            // Measured, fbm output here spans about -0.26 to +0.27 with its
            // median below zero, so a level chosen as "0.85" covers a few percent
            // of tiles rather than the fifteen it looks like. Ranking the field
            // makes the dial mean the coverage it claims, which is how the
            // landmass and hill stages already work.
            float wanted = Mathf.Clamp(AverageWetness(world, settings, eligible), 0.0f, 0.95f);

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
        private static float AverageWetness(
            TerrainWorld world, TerrainGenerationSettings settings, bool[] eligible)
        {
            float total = 0.0f;
            int seen = 0;
            for (int cell = 0; cell < eligible.Length; cell++)
            {
                if (!eligible[cell])
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

            // Woods want rain and not too much heat.
            if (moisture < 0.26f || temperature < 0.15f)
                return None;

            // Local moisture shifts this cell's place in the ranking, so stands
            // still thicken toward wet ground - but as bigger stands rather than
            // a finer sprinkle. Jitter keeps the boundary off a clean contour.
            float bias = ((moisture - 0.45f) * 0.10f) + ((roll - 0.5f) * 0.02f);
            float value = stand + bias;
            if (terrain == "tundra")
                value -= 0.05f;

            if (value < threshold)
                return None;

            // Well inside a stand is closed forest; the fringe is open woodland.
            // Drawing both the same is what makes a wood read as a texture rather
            // than a place with a middle and an edge.
            return value >= dense ? Forest : Woods;
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
