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
        GridTerrainGeneratorComponent.LandformMode Landform,
        float LandmassScale,
        int ArchipelagoIslandCount,
        int TopologySamplesPerCell,
        float ErosionStrength,
        float BeachWidth,
        float Dryness,
        float FeatureFrequencyMultiplier,
        float LakeCoverage,
        float LakeFrequencyMultiplier,
        float LakeShoreWidth,
        float RiverDensity,
        int StartPositionCount,
        float ResourceDensity,
        ResourceSet ResourceSet,
        float HillsFraction,
        float MountainsFraction,
        float HillshadeStrength,
        float FeatureDensity,
        bool UseClimateBiomeMaps,
        bool UseScaleRules,
        bool UseBiomeQuotas,
        int BiomeCoherencePasses,
        float MinBiomeRegionFraction,
        int BiomeCoherenceKeep,
        float DesertFraction,
        float DryGrassFraction,
        float SwampFraction,
        float OceanMarginTiles,
        float CoastlineRaggedness,
        float AltitudeCooling,
        float ClimateLatitudeSpan,
        float ClimateLatitudeCentre,
        float TemperatureFrequencyMultiplier,
        float MoistureFrequencyMultiplier,
        float FertilityFrequencyMultiplier)
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
            GridTerrainGeneratorComponent.LandformMode.Island => 1,
            GridTerrainGeneratorComponent.LandformMode.Archipelago => Mathf.Max(2, ArchipelagoIslandCount),

            // Mainland means a few CONTINENTS, not one - the word is plural, and
            // a single mass filling the map is the thing this generator was
            // rewritten to stop producing. It takes the requested count like any
            // other mode; continents are simply fewer and larger, which is the
            // land coverage talking, not a separate rule.
            _ => Mathf.Clamp(ArchipelagoIslandCount, 2, 6),
        };
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
            ["start_position_count"] = StartPositionCount,
            ["feature_count"] = FeatureCount,
            ["samples_per_cell"] = SamplesPerCell,
            ["field_width"] = FieldWidth,
            ["field_height"] = FieldHeight,
            ["generation_milliseconds"] = GenerationMilliseconds,
        };
    }
}
