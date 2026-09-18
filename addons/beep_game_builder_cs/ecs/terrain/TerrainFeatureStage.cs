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
        /// Side of a ranking block, in tiles. A block has to hold more than one wavelength of the
        /// stand field (<see cref="StandWavelengthTiles"/>): one narrower than that sees the field as
        /// a single slope, its percentile takes the high side, and the block grid is drawn in
        /// woodland. Twenty-four holds one and a half wavelengths.
        /// </summary>
        internal const int BlockTiles = 24;

        /// <summary>
        /// Wavelength, in tiles, of the vegetation field woodland is cut from, at a
        /// FeatureFrequencyMultiplier of one. In tiles and not as a share of the map, because a
        /// forest is so many tiles whatever the map's size - the rule TerrainScaleRules keeps for
        /// every feature.
        ///
        /// Sixteen groups woodland into forests with open ground between them, the way Age of
        /// Empires II's maps do, rather than scattering copses. Measured on Oilfield Days' temperate
        /// 144-tile basin at normal rainfall, two seeds: 56-60 separate stands, a median of 22-23
        /// tiles, and 79-81% of the woodland in stands of 25 tiles or more. At six tiles the same
        /// woodland broke up: 110 stands, a median of 12-13 tiles, 37-47% in stands of 25 or more.
        ///
        /// It cannot grow much past sixteen without outgrowing the ranking blocks (BlockTiles). The
        /// field used to be scaled to the landmasses, which put its wavelength near a hundred tiles
        /// on that map; inside a block the field was then a flat slope, and the block grid came out
        /// as straight bands of forest. Measured with 24-tile blocks at normal rainfall, 16-25% of
        /// woodland edges lay on a block boundary, where chance puts one in 24; at forty tiles 13-20%
        /// did, and at sixteen 3-7% do.
        /// </summary>
        internal const float StandWavelengthTiles = 16.0f;

        /// <summary>
        /// Fewest eligible cells a block needs before it is ranked on its own.
        /// Below this it takes the map-wide threshold: ranking four tiles
        /// against each other would make woods of whichever happened to be
        /// highest.
        /// </summary>
        private const int MinBlockCells = 6;

        /// <summary>
        /// The value of the block whose centre is nearest a tile.
        /// </summary>
        private static float Blend(float[] blocks, int wide, int high, int cellX, int cellY)
        {
            float atX = ((cellX + 0.5f) / BlockTiles) - 0.5f;
            float atY = ((cellY + 0.5f) / BlockTiles) - 0.5f;

            int x0 = Mathf.Clamp(Mathf.FloorToInt(atX), 0, wide - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(atY), 0, high - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, wide - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, high - 1);

            float tx = Mathf.Clamp(atX - x0, 0.0f, 1.0f);
            float ty = Mathf.Clamp(atY - y0, 0.0f, 1.0f);

            // The NEAREST block's value, deliberately not blended between them.
            //
            // Interpolating looks like the careful choice and undoes the whole
            // point: blending a block's threshold toward its neighbours pulls a
            // low-lying region's bar up toward the high ground around it, which
            // is exactly the global behaviour that ranking locally exists to
            // replace. Measured with blending on, a quadrant holding 103
            // woods-capable tiles - WETTER than two quadrants that were more
            // than half wooded - still grew nothing. Without it the same map
            // came out 46%, 40%, 40% and 43% across its quadrants.
            //
            // The seams blending would guard against do not appear while the
            // field varies within a block, because the threshold is not what the
            // eye sees: the noise still shapes every stand, so the boundary falls
            // inside woodland that is already irregular. A field too coarse to
            // vary within a block does show them - see StandWavelengthTiles.
            int nearestX = tx < 0.5f ? x0 : x1;
            int nearestY = ty < 0.5f ? y0 : y1;
            return blocks[(nearestY * wide) + nearestX];
        }

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

        public static void Apply(TerrainGenerationBuffer world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
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
                    // The woods-capable kinds now live on the terrain-kind catalog
                    // (DUP-13), the same fact Choose reads - so the two cannot drift.
                    string kind = world.CellTerrain[cell];
                    if (TerrainKindCatalog.Standard.FeatureEligibility(kind) != "woods")
                        continue;

                    // Dryness is not re-tested here - and the generator export
                    // that once carried it is gone, because nothing read it.
                    // Whether ground is too dry
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
                    if (world.TemperatureAtCell(cell) < 0.15f)
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
            float wanted = WoodsCapableShare(world, settings, eligible);

            // The threshold is ranked LOCALLY, block by block, not once over the
            // whole map or even once per landmass.
            //
            // One ranking for a whole area lets the top slice of a smooth field
            // land in one part of it, and every part outside that lobe comes out
            // bare. Measured on one 48x48 map, woods density across its four
            // quadrants was 40%, 0%, 27%, 33% - a quarter of the land with no
            // tree on it, not because it was unsuitable but because the field
            // dipped there and the cut fell above it.
            //
            // Ranking within a block makes the coverage dial mean what it says
            // everywhere: each part of the map competes with its own
            // neighbourhood rather than with the whole. Each tile then takes the
            // threshold of its nearest block (Blend says why not an interpolated
            // one), and a block with fewer than MinBlockCells eligible cells takes
            // the map-wide threshold rather than ranking a handful of tiles
            // against each other.
            int blocksWide = Mathf.Max(1, Mathf.CeilToInt(wide / (float)BlockTiles));
            int blocksHigh = Mathf.Max(1, Mathf.CeilToInt(high / (float)BlockTiles));
            var blockThreshold = new float[blocksWide * blocksHigh];
            var blockDense = new float[blocksWide * blocksHigh];

            float[] ranked = world.FloatScratchA;
            int rankedCount = TerrainGeometry.SortedSelection(stand, eligible, ranked);
            float globalThreshold = TerrainGeometry.RankedValue(ranked, rankedCount, 1.0f - wanted);
            float globalDense = TerrainGeometry.RankedValue(ranked, rankedCount, 1.0f - (wanted * 0.45f));

            var window = new float[BlockTiles * BlockTiles];
            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    int seen = 0;

                    int fromX = blockX * BlockTiles;
                    int toX = Mathf.Min(wide - 1, ((blockX + 1) * BlockTiles) - 1);
                    int fromY = blockY * BlockTiles;
                    int toY = Mathf.Min(high - 1, ((blockY + 1) * BlockTiles) - 1);

                    for (int y = fromY; y <= toY; y++)
                    {
                        for (int x = fromX; x <= toX; x++)
                        {
                            int at = world.CellIndex(x, y);
                            if (!eligible[at])
                                continue;

                            window[seen++] = stand[at];
                        }
                    }

                    int block = (blockY * blocksWide) + blockX;
                    if (seen < MinBlockCells)
                    {
                        blockThreshold[block] = globalThreshold;
                        blockDense[block] = globalDense;
                        continue;
                    }

                    System.Array.Sort(window, 0, seen);
                    blockThreshold[block] = TerrainGeometry.RankedValue(window, seen, 1.0f - wanted);
                    blockDense[block] = TerrainGeometry.RankedValue(window, seen, 1.0f - (wanted * 0.45f));
                }
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

                    world.Feature[cell] = Choose(
                        world, settings, cell, cellX, cellY, stand[cell],
                        Blend(blockThreshold, blocksWide, blocksHigh, cellX, cellY),
                        Blend(blockDense, blocksWide, blocksHigh, cellX, cellY));
                }
            }
        }

        /// <summary>Woodland, as a share of all land, at a FeatureDensity of one.</summary>
        /// <remarks>
        /// What shipped strategy maps grow. Civilization VI caps forest at 18% of land plots at normal
        /// rainfall, 14 arid and 22 wet (FeatureGenerator.lua; ApplyMapSetup carries the rainfall part
        /// as FeatureDensity, TerrainMapSetup.VegetationScaleFor); Age of Empires II's standard land maps
        /// give forest 12-18% of the map (Arabia 13, Mongolia 12, Highland 18). The curve this replaced
        /// grew 39-45% on Oilfield Days' temperate basin, and while Rainfall still shifted moisture, 0% on
        /// an arid map and 83% on a wet one.
        /// </remarks>
        private const float ReferenceWoodlandShare = 0.18f;

        /// <summary>
        /// The share of the woods-capable cells that should carry woodland.
        ///
        /// The budget is decided as a share of ALL LAND and only then divided over the eligible cells,
        /// and that order is the whole point. How much of this world is wooded, and where on it may a
        /// tree stand, are two different questions; answering the first over the answer to the second
        /// couples them. When the budget was worked out over the eligible cells, widening eligibility
        /// pulled dry cells into it, dropped the coverage, and left FEWER woods than before. Measured
        /// when that happened: seven bare islands became eleven.
        /// </summary>
        private static float WoodsCapableShare(
            TerrainGenerationBuffer world, TerrainGenerationSettings settings, bool[] eligible)
        {
            int land = 0;
            int capable = 0;
            for (int cell = 0; cell < world.CellTerrain.Length; cell++)
            {
                if (world.CellWater[cell] != WaterBody.None)
                    continue;

                if (!TerrainTileSets.IsLandKind(world.CellTerrain[cell]))
                    continue;

                land++;
                if (eligible[cell])
                    capable++;
            }
            if (land == 0 || capable == 0)
                return 0.0f;

            float shareOfLand = ReferenceWoodlandShare * Mathf.Clamp(settings.FeatureDensity, 0.0f, 4.0f);
            return Mathf.Clamp(shareOfLand * land / capable, 0.0f, 0.95f);
        }

        private static string Choose(
            TerrainGenerationBuffer world,
            TerrainGenerationSettings settings,
            int cell,
            int cellX,
            int cellY,
            float stand,
            float threshold,
            float dense)
        {
            string terrain = world.CellTerrain[cell];
            float temperature = world.TemperatureAtCell(cell);
            float roll = TerrainGeometry.Hash01(cellX, cellY, settings.Seed + 55001);
            float density = Mathf.Clamp(settings.FeatureDensity, 0.0f, 4.0f);

            // Which feature a kind carries now lives on the terrain-kind catalog
            // (DUP-13); the oasis roll, the temperature floor and the
            // Forest-vs-Woods split stay here.
            switch (TerrainKindCatalog.Standard.FeatureEligibility(terrain))
            {
                // Terrain that already means dense vegetation always carries the
                // matching feature, so the ground under it can be ordinary soil.
                case "jungle":
                    return Jungle;
                case "marsh":
                    return Marsh;
                // An oasis is the rare exception that makes a desert readable.
                case "oasis":
                    return roll < 0.012f * density ? Oasis : None;
                // Woods-capable: fall through to the ranking comparison below.
                case "woods":
                    break;
                default:
                    return None;
            }

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
            TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            int cell, int cellX, int cellY, string terrain)
        {
            float moisture = world.MoistureAtCell(cell);
            float roll = TerrainGeometry.Hash01(cellX, cellY, settings.Seed + 55001);

            float bias = ((moisture - 0.45f) * 0.10f) + ((roll - 0.5f) * 0.02f);
            return terrain == "tundra" ? bias - 0.05f : bias;
        }

    }
}
