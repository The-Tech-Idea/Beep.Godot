using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The SHAPE of a world: how its land is arranged, and how tall it is.
    ///
    /// Shape and climate are separate axes because they genuinely are: an
    /// archipelago can be frozen or tropical, and a desert can be one continent
    /// or a thousand islands. Combining them into single "world types" forced a
    /// choice nobody meant to make - picking "Frozen World" also decided you
    /// wanted one landmass, and picking "Archipelago" decided you wanted a
    /// temperate one. Twelve combined presets covered twelve of the thirty-five
    /// combinations these two lists give, and hid the other twenty-three.
    /// </summary>
    public enum TerrainShape
    {
        Continents,
        Pangaea,
        Archipelago,
        IslandChain,
        OceanWorld,
    }

    /// <summary>How the land of a world is laid out. Nothing here decides weather.</summary>
    public readonly record struct TerrainShapeDefinition(
        string DisplayName,
        GridTerrainGeneratorComponent.LandformMode Landform,
        float LandCoverage,
        int IslandCount,
        float SeaCoverage,
        float HillsFraction,
        float MountainsFraction,
        int StartPositions);

    public static class TerrainShapePresets
    {
        private static readonly Dictionary<TerrainShape, TerrainShapeDefinition> Catalogue = new()
        {
            // The default 4X arrangement: several landmasses, moderate relief.
            [TerrainShape.Continents] = new(
                "Continents", GridTerrainGeneratorComponent.LandformMode.Mainland,
                0.42f, 4, 0.12f, 0.16f, 0.07f, 6),

            [TerrainShape.Pangaea] = new(
                "Pangaea", GridTerrainGeneratorComponent.LandformMode.Island,
                0.66f, 1, 0.10f, 0.18f, 0.09f, 8),

            [TerrainShape.Archipelago] = new(
                "Archipelago", GridTerrainGeneratorComponent.LandformMode.Archipelago,
                0.30f, 7, 0.14f, 0.14f, 0.05f, 6),

            [TerrainShape.IslandChain] = new(
                "Island Chain", GridTerrainGeneratorComponent.LandformMode.Archipelago,
                0.22f, 9, 0.16f, 0.12f, 0.04f, 5),

            // Mostly sea. Start positions drop because there is little to stand on.
            [TerrainShape.OceanWorld] = new(
                "Ocean World", GridTerrainGeneratorComponent.LandformMode.Archipelago,
                0.14f, 11, 0.20f, 0.10f, 0.03f, 4),

        };

        /// <summary>The shapes in menu order.</summary>
        public static readonly TerrainShape[] Order =
        {
            TerrainShape.Continents,
            TerrainShape.Pangaea,
            TerrainShape.Archipelago,
            TerrainShape.IslandChain,
            TerrainShape.OceanWorld,
        };

        public static TerrainShapeDefinition Get(TerrainShape shape)
            => Catalogue.TryGetValue(shape, out TerrainShapeDefinition found)
                ? found
                : Catalogue[TerrainShape.Continents];

        public static string[] DisplayNames()
        {
            var names = new string[Order.Length];
            for (int i = 0; i < Order.Length; i++)
                names[i] = Get(Order[i]).DisplayName;
            return names;
        }
    }
}
