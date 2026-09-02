using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// What kind of world to build. Owned by the generation layer, not by a
    /// renderer: the pipeline decides terrain, and nothing about that decision
    /// should depend on which component happens to draw the result.
    /// </summary>
    public enum TerrainMode
    {
        Plain,
        ProceduralNoise,
    }

    /// <summary>The broad climate a map is generated around.</summary>
    public enum TerrainPreset
    {
        Grassland,
        Desert,
        Sand,
        Ice,
        Sea,
        Rock,
        Lava,
        Swamp,
        Snow,
    }

    /// <summary>
    /// Complete, immutable input to world generation. Generation is a pure
    /// function of this value, so two equal settings always produce an identical
    /// world and the field can be cached on equality alone.
    /// </summary>
    internal readonly record struct TerrainGenerationSettings(
        Vector2I Origin,
        Vector2I Size,
        TerrainMode Mode,
        TerrainPreset Preset,
        int Seed,
        FastNoiseLite.NoiseTypeEnum NoiseType,
        FastNoiseLite.FractalTypeEnum FractalType,
        float Frequency,
        int Octaves,
        float Lacunarity,
        float Gain,
        TerrainGeneratorComponent.LandformMode Landform,
        float LandmassScale,
        int ArchipelagoIslandCount,
        int TopologySamplesPerCell,
        float ErosionStrength,
        float BeachWidth,
        float FeatureFrequencyMultiplier,
        float LakeCoverage,
        float LakeFrequencyMultiplier,
        float LakeShoreWidth,
        float RiverDensity,
        int StartPositionCount,
        float ResourceDensity,
        ResourceSet ResourceSet,
        // The authored catalog, when a game supplies one. Null means the
        // ResourceSet axis picks a shipped catalog instead.
        ResourceCatalog? ResourceCatalog,
        float HillsFraction,
        float MountainsFraction,
        float HillshadeStrength,
        float FeatureDensity,
        bool UseClimateBiomeMaps,
        bool UseScaleRules,
        int BiomeCoherencePasses,
        float MinBiomeRegionFraction,
        int BiomeCoherenceKeep,
        float OceanMarginTiles,
        float CoastlineRaggedness,
        float AltitudeCooling,
        float ClimateLatitudeSpan,
        float ClimateLatitudeCentre,
        float TemperatureFrequencyMultiplier,
        float MoistureFrequencyMultiplier)
    {
        /// <summary>
        /// Fraction of the map that must end up as land.
        ///
        /// LandmassScale owns this for every mode. Mainland used to read
        /// 1 - SeaCoverage instead, so two settings decided one fact and only
        /// one was listened to: the Continents map type asks for 42% land and
        /// produced 72%, its own LandCoverage quietly ignored while a sea
        /// coverage of 0.12 answered in its place.
        /// </summary>
        public float TargetLandCoverage => LandmassScale;

        /// <summary>
        /// How many separate landmasses the map should have. This is the one
        /// place that decides it - the landmass stage asks here rather than
        /// keeping its own copy, which is how the two came to disagree: this
        /// said Mainland wanted no particular number while the stage said three,
        /// and neither honoured the count the caller had actually set.
        /// </summary>
        public int RequestedLandmassCount => Landform switch
        {
            TerrainGeneratorComponent.LandformMode.Island => 1,
            TerrainGeneratorComponent.LandformMode.Archipelago => Mathf.Max(2, ArchipelagoIslandCount),

            // Mainland means a few CONTINENTS, not one - the word is plural, and
            // a single mass filling the map is the thing this generator was
            // rewritten to stop producing. It takes half the Archipelago count,
            // rounded up: fewer, larger masses than an archipelago of the same
            // island count, rather than the same number of them - which is what
            // reading ArchipelagoIslandCount unscaled produced, so a Mainland
            // map and an Archipelago map at the same setting came back with the
            // same landmass count and only their land coverage told them apart.
            _ => Mathf.Clamp((ArchipelagoIslandCount + 1) / 2, 2, 6),
        };

        /// <summary>
        /// How many start positions the map should have. The one place that
        /// clamps it, for the same reason RequestedLandmassCount is the one
        /// place that decides a landmass count: TerrainStartPositionStage reads
        /// this rather than re-clamping StartPositionCount itself, so the
        /// number a diagnostic reports as "requested" can never drift from the
        /// number the stage actually aimed for.
        /// </summary>
        public int RequestedStartPositionCount => Mathf.Clamp(StartPositionCount, 0, 24);
    }

    /// <summary>
    /// Measured outcome of one generation run, surfaced to tooling.
    ///
    /// RequestedLandmassCount sits beside LandComponentCount deliberately. A
    /// map can be asked for twelve islands and come back with eleven - twelve
    /// masses with a channel between each pair genuinely do not fit on a small
    /// map, and the generator's honest answer is to grow the ones that fit and
    /// give the rest their share. Reporting only what was achieved makes that
    /// indistinguishable from success, which is the caller being told a number
    /// it has no way to check.
    /// </summary>
    internal readonly record struct TerrainGenerationDiagnostics(
        float TargetLandCoverage,
        float LandFootprintCoverage,
        float SolidLandCoverage,
        float OceanCoverage,
        float LakeCoverage,
        float RiverCoverage,
        int RequestedLandmassCount,
        int LandComponentCount,
        int ContinentCount,
        int ResourceCount,
        int LiquidResourceCount,
        int UndergroundCellCount,
        int RequestedStartPositionCount,
        int StartPositionCount,
        int FeatureCount,
        int SamplesPerCell,
        int FieldWidth,
        int FieldHeight,
        long GenerationMilliseconds)
    {
        public Godot.Collections.Dictionary ToDictionary() => new()
        {
            ["target_land_coverage"] = TargetLandCoverage,
            ["land_footprint_coverage"] = LandFootprintCoverage,
            ["solid_land_coverage"] = SolidLandCoverage,
            ["ocean_coverage"] = OceanCoverage,
            ["lake_coverage"] = LakeCoverage,
            ["river_coverage"] = RiverCoverage,
            ["requested_landmass_count"] = RequestedLandmassCount,
            ["land_component_count"] = LandComponentCount,
            ["continent_count"] = ContinentCount,
            ["resource_count"] = ResourceCount,
            ["liquid_resource_count"] = LiquidResourceCount,
            ["underground_cell_count"] = UndergroundCellCount,
            ["requested_start_position_count"] = RequestedStartPositionCount,
            ["start_position_count"] = StartPositionCount,
            ["feature_count"] = FeatureCount,
            ["samples_per_cell"] = SamplesPerCell,
            ["field_width"] = FieldWidth,
            ["field_height"] = FieldHeight,
            ["generation_milliseconds"] = GenerationMilliseconds,
        };
    }
}
