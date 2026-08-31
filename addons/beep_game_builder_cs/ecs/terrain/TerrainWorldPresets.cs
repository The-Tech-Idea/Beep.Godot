using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A named world type. Picking one sets every generation dial at once.
    ///
    /// These exist because the dials are not independent: 20% land only reads as
    /// an archipelago if the landform is archipelago too, snow only appears if
    /// the climate allows it, and asking for oil resources on a temperate
    /// grassland yields almost nothing because the hydrocarbon catalogue wants
    /// desert, bog and shelf. A preset is a combination already known to hold
    /// together, so a developer starts from a working world rather than from
    /// thirty sliders at their defaults.
    /// </summary>
    public enum TerrainWorldPreset
    {
        Continents = 0,
        Pangaea = 1,
        Archipelago = 2,
        IslandChain = 3,
        OceanWorld = 4,
        Highlands = 5,
        GreatPlains = 6,
        DesertWorld = 7,
        FrozenWorld = 8,
        Wetlands = 9,
        OilFrontier = 10,
        BarrenMoon = 11,
    }

    /// <summary>
    /// Every dial a world type sets. Deliberately a full description rather than
    /// a patch: a preset that only overrode some fields would inherit the rest
    /// from whatever was selected before it, so switching A to B to A would not
    /// come back to the same world.
    /// </summary>
    public readonly record struct TerrainWorldDefinition(
        string DisplayName,
        GridTerrainGeneratorComponent.LandformMode Landform,
        TerrainPreset Climate,
        float LandCoverage,
        int IslandCount,
        float SeaCoverage,
        float LakeCoverage,
        float RiverDensity,
        float HillsFraction,
        float MountainsFraction,
        float SwampCoverage,
        float SnowCoverage,
        float IceCoverage,
        float Dryness,
        float FeatureDensity,
        float ResourceDensity,
        ResourceSet Resources,
        int StartPositions);

    /// <summary>The catalogue of world types the generator can be set to.</summary>
    public static class TerrainWorldPresets
    {
        private static readonly Dictionary<TerrainWorldPreset, TerrainWorldDefinition> Catalogue = new()
        {
            // The default 4X shape: several landmasses, temperate, everything in
            // moderate supply.
            [TerrainWorldPreset.Continents] = new(
                "Continents", GridTerrainGeneratorComponent.LandformMode.Mainland, TerrainPreset.Grassland,
                0.42f, 4, 0.12f, 0.05f, 1.0f, 0.16f, 0.07f, 0.02f, 0.04f, 0.02f, 0.25f, 1.0f, 1.0f,
                ResourceSet.Historical, 6),

            [TerrainWorldPreset.Pangaea] = new(
                "Pangaea", GridTerrainGeneratorComponent.LandformMode.Island, TerrainPreset.Grassland,
                0.66f, 1, 0.10f, 0.06f, 1.2f, 0.18f, 0.09f, 0.04f, 0.05f, 0.02f, 0.28f, 1.0f, 1.0f,
                ResourceSet.Historical, 8),

            [TerrainWorldPreset.Archipelago] = new(
                "Archipelago", GridTerrainGeneratorComponent.LandformMode.Archipelago, TerrainPreset.Grassland,
                0.30f, 7, 0.14f, 0.03f, 0.7f, 0.14f, 0.05f, 0.05f, 0.02f, 0.01f, 0.22f, 1.2f, 1.0f,
                ResourceSet.Historical, 6),

            [TerrainWorldPreset.IslandChain] = new(
                "Island Chain", GridTerrainGeneratorComponent.LandformMode.Archipelago, TerrainPreset.Grassland,
                0.22f, 9, 0.16f, 0.02f, 0.5f, 0.12f, 0.04f, 0.06f, 0.0f, 0.0f, 0.20f, 1.3f, 1.1f,
                ResourceSet.Historical, 5),

            // Mostly sea. Start positions drop because there is little to stand on.
            [TerrainWorldPreset.OceanWorld] = new(
                "Ocean World", GridTerrainGeneratorComponent.LandformMode.Archipelago, TerrainPreset.Grassland,
                0.14f, 11, 0.20f, 0.01f, 0.4f, 0.10f, 0.03f, 0.04f, 0.0f, 0.0f, 0.18f, 1.2f, 1.2f,
                ResourceSet.Historical, 4),

            // Relief is the point, so hills and mountains are pushed well up and
            // rivers follow from the extra elevation.
            [TerrainWorldPreset.Highlands] = new(
                "Highlands", GridTerrainGeneratorComponent.LandformMode.Island, TerrainPreset.Grassland,
                0.52f, 1, 0.10f, 0.07f, 1.6f, 0.34f, 0.22f, 0.02f, 0.10f, 0.03f, 0.30f, 0.9f, 1.1f,
                ResourceSet.Historical, 6),

            [TerrainWorldPreset.GreatPlains] = new(
                "Great Plains", GridTerrainGeneratorComponent.LandformMode.Island, TerrainPreset.Grassland,
                0.58f, 1, 0.08f, 0.04f, 0.9f, 0.05f, 0.01f, 0.02f, 0.02f, 0.0f, 0.35f, 0.8f, 1.0f,
                ResourceSet.Historical, 7),

            [TerrainWorldPreset.DesertWorld] = new(
                "Desert World", GridTerrainGeneratorComponent.LandformMode.Island, TerrainPreset.Desert,
                0.55f, 1, 0.10f, 0.01f, 0.3f, 0.18f, 0.08f, 0.0f, 0.0f, 0.0f, 0.85f, 0.35f, 0.9f,
                ResourceSet.Historical, 5),

            // An ice age, not a dead iceball. The Ice climate turns every land
            // tile to snow, ice or rock - and start positions exclude all three,
            // so the world generated with nowhere to begin. A temperate base
            // with heavy snow and ice cover gives frozen poles and a habitable
            // middle, which is what the name is actually promising.
            [TerrainWorldPreset.FrozenWorld] = new(
                "Frozen World", GridTerrainGeneratorComponent.LandformMode.Island, TerrainPreset.Grassland,
                0.48f, 1, 0.14f, 0.04f, 0.5f, 0.20f, 0.10f, 0.0f, 0.44f, 0.16f, 0.20f, 0.35f, 0.9f,
                ResourceSet.Historical, 5),

            [TerrainWorldPreset.Wetlands] = new(
                "Wetlands", GridTerrainGeneratorComponent.LandformMode.Mainland, TerrainPreset.Swamp,
                0.46f, 3, 0.12f, 0.22f, 2.0f, 0.06f, 0.01f, 0.42f, 0.0f, 0.0f, 0.08f, 1.4f, 1.0f,
                ResourceSet.Historical, 6),

            // Hydrocarbons want desert, bog and continental shelf, so the world
            // is shaped to actually produce them rather than just relabelled.
            [TerrainWorldPreset.OilFrontier] = new(
                "Oil Frontier", GridTerrainGeneratorComponent.LandformMode.Mainland, TerrainPreset.Desert,
                0.44f, 3, 0.14f, 0.06f, 0.5f, 0.14f, 0.05f, 0.18f, 0.03f, 0.0f, 0.70f, 0.40f, 1.5f,
                ResourceSet.OilAndGas, 6),

            // No vegetation at all: an airless body has none, and drawing trees
            // on it would be the map lying about the world.
            [TerrainWorldPreset.BarrenMoon] = new(
                "Barren Moon", GridTerrainGeneratorComponent.LandformMode.Island, TerrainPreset.Rock,
                0.72f, 1, 0.04f, 0.0f, 0.0f, 0.40f, 0.26f, 0.0f, 0.12f, 0.06f, 0.95f, 0.0f, 1.6f,
                ResourceSet.SpaceExploration, 4),
        };

        /// <summary>The presets in menu order, for building a dropdown.</summary>
        public static readonly TerrainWorldPreset[] Order =
        {
            TerrainWorldPreset.Continents,
            TerrainWorldPreset.Pangaea,
            TerrainWorldPreset.Archipelago,
            TerrainWorldPreset.IslandChain,
            TerrainWorldPreset.OceanWorld,
            TerrainWorldPreset.Highlands,
            TerrainWorldPreset.GreatPlains,
            TerrainWorldPreset.DesertWorld,
            TerrainWorldPreset.FrozenWorld,
            TerrainWorldPreset.Wetlands,
            TerrainWorldPreset.OilFrontier,
            TerrainWorldPreset.BarrenMoon,
        };

        public static TerrainWorldDefinition Get(TerrainWorldPreset preset)
            => Catalogue.TryGetValue(preset, out TerrainWorldDefinition definition)
                ? definition
                : Catalogue[TerrainWorldPreset.Continents];

        /// <summary>Display names in <see cref="Order"/>, for a dropdown.</summary>
        public static string[] DisplayNames()
        {
            var names = new string[Order.Length];
            for (int i = 0; i < Order.Length; i++)
                names[i] = Get(Order[i]).DisplayName;
            return names;
        }
    }
}
