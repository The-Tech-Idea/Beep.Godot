using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Complete, immutable input to world generation. Generation is a pure
    /// function of this value, so two equal settings always produce an identical
    /// world and the field can be cached on equality alone.
    /// </summary>
    internal readonly record struct TerrainGenerationSettings(
        Vector2I Origin,
        Vector2I Size,
        PainterlyTerrainComponent.TerrainMode Mode,
        PainterlyTerrainComponent.TerrainPreset Preset,
        int Seed,
        FastNoiseLite.NoiseTypeEnum NoiseType,
        FastNoiseLite.FractalTypeEnum FractalType,
        float Frequency,
        int Octaves,
        float Lacunarity,
        float Gain,
        GridTerrainGeneratorComponent.LandformMode Landform,
        float LandmassScale,
        float SeaCoverage,
        int ArchipelagoIslandCount,
        int TopologySamplesPerCell,
        float BeachWidth,
        float RockLevel,
        float Dryness,
        float SwampCoverage,
        float SnowCoverage,
        float IceCoverage,
        float FeatureFrequencyMultiplier,
        float LakeCoverage,
        float LakeFrequencyMultiplier,
        float LakeShoreWidth,
        float RiverDensity,
        int StartPositionCount,
        float ResourceDensity,
        float HillsFraction,
        float MountainsFraction,
        float HillshadeStrength,
        float FeatureDensity,
        bool UseClimateBiomeMaps,
        float TemperatureFrequencyMultiplier,
        float MoistureFrequencyMultiplier,
        float FertilityFrequencyMultiplier,
        float ColdThreshold,
        float WetlandThreshold,
        float FertilityThreshold)
    {
        /// <summary>Fraction of the map that must end up as land.</summary>
        public float TargetLandCoverage => Landform == GridTerrainGeneratorComponent.LandformMode.Mainland
            ? 1.0f - SeaCoverage
            : LandmassScale;

        /// <summary>How many separate landmasses the mode asks for; 0 means unconstrained.</summary>
        public int RequestedLandmassCount => Landform switch
        {
            GridTerrainGeneratorComponent.LandformMode.Island => 1,
            GridTerrainGeneratorComponent.LandformMode.Archipelago => Mathf.Max(2, ArchipelagoIslandCount),
            _ => 0,
        };
    }

    /// <summary>Measured outcome of one generation run, surfaced to tooling.</summary>
    internal readonly record struct TerrainGenerationDiagnostics(
        float TargetLandCoverage,
        float LandFootprintCoverage,
        float SolidLandCoverage,
        float OceanCoverage,
        float LakeCoverage,
        float RiverCoverage,
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
