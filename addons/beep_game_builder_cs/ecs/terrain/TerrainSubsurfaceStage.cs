using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Lays the UNDERGROUND stratum: ore veins, oil and gas fields, water ice -
    /// deposits that lie beneath land or seabed, invisible on the surface, and
    /// extracted by buildings rather than walked up to.
    ///
    /// Underground deposits are AREAS, not markers. Each Underground resource
    /// gets its own low-frequency value noise (a unique seed offset per
    /// definition), thresholded by its DepositScale, and every cell above the
    /// threshold joins the field with a RICHNESS taken from how far above it
    /// the noise sits. That is what makes an oil basin a basin - contiguous,
    /// richest at its centre, thinning at its rim - instead of a sprinkle of
    /// independent cells.
    ///
    /// Occurrence rules apply to the surface ABOVE the deposit: a definition
    /// listing desert and shallow_water is a field under both an erg and its
    /// continental shelf. Depth is a fact of the RESOURCE (its authored band),
    /// carried per cell so a published map answers without a catalogue lookup.
    /// </summary>
    internal static class TerrainSubsurfaceStage
    {
        /// <summary>Tiles per broad noise feature; the scale of a basin.</summary>
        private const float FieldTiles = 9.0f;

        /// <summary>
        /// The thinnest a deposit is: a cell that holds one holds at least this much. Anything that
        /// later rescales richness (the start-distance pass, FEAT-14) keeps a deposit at or above it,
        /// so a cell never says it has a resource and nothing to take.
        /// </summary>
        internal const float MinimumRichness = 0.05f;

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings, TerrainResourceRules? rules = null)
        {
            if (settings.ResourceDensity <= 0.0f)
                return;

            rules ??= TerrainResourceRules.Capture(settings);
            int wide = world.CellsWide;
            int high = world.CellsHigh;

            int definitionIndex = 0;
            foreach (TerrainResourceRules.Entry definition in rules.Entries)
            {
                // Every definition advances the index, so a stratum change on
                // one resource cannot shift every later resource's noise.
                int defSeed = settings.Seed + 70001 + (definitionIndex++ * 7919);
                if (definition.Stratum != ResourceStratum.Underground)
                    continue;

                // 0 = rare pockets, 1 = broad basins.
                float scale = Mathf.Clamp(definition.DepositScale, 0.0f, 1.0f);
                float threshold = Mathf.Lerp(0.86f, 0.58f, scale);

                for (int cellY = 0; cellY < high; cellY++)
                {
                    for (int cellX = 0; cellX < wide; cellX++)
                    {
                        int cell = (cellY * wide) + cellX;
                        if (!definition.Supports(world.CellTerrain[cell], world.CellRelief[cell]))
                            continue;

                        float n = FieldNoise(cellX, cellY, defSeed);
                        if (n <= threshold)
                            continue;

                        float richness = Mathf.Max(MinimumRichness, (n - threshold) / (1.0f - threshold));

                        // Where two fields overlap, the better claim wins -
                        // weight times local richness, so a rich rare deposit
                        // can displace the thin rim of a common one.
                        float score = definition.Weight * richness;
                        if (world.CellUndergroundResource[cell].Length > 0
                            && world.CellUndergroundRichness[cell] * rules.WeightOf(world.CellUndergroundResource[cell]) >= score)
                            continue;

                        world.CellUndergroundResource[cell] = definition.Id;
                        world.CellUndergroundRichness[cell] = richness;
                        world.CellUndergroundDepth[cell] = (byte)definition.Depth;
                    }
                }
            }
        }

        /// <summary>
        /// Two octaves of seeded value noise on the cell grid: a broad one that
        /// shapes the basin and a finer one that roughens its rim. Pure
        /// Hash01-lattice, so the same seed lays the same fields.
        /// </summary>
        private static float FieldNoise(int cellX, int cellY, int seed)
        {
            float broad = ValueNoise(cellX / FieldTiles, cellY / FieldTiles, seed);
            float fine = ValueNoise(cellX / (FieldTiles * 0.5f), cellY / (FieldTiles * 0.5f), seed + 331);
            return (broad * 0.72f) + (fine * 0.28f);
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            float ux = fx * fx * (3.0f - (2.0f * fx));
            float uy = fy * fy * (3.0f - (2.0f * fy));

            float a = TerrainGeometry.Hash01(x0, y0, seed);
            float b = TerrainGeometry.Hash01(x0 + 1, y0, seed);
            float c = TerrainGeometry.Hash01(x0, y0 + 1, seed);
            float d = TerrainGeometry.Hash01(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }
    }
}
