using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Turns elevation, temperature and moisture into the final terrain kind.
    ///
    /// The land table is a Whittaker-style temperature x moisture matrix, the
    /// standard way biomes are assigned: cold gives snow then tundra regardless
    /// of rainfall, and within the temperate and hot bands rainfall decides
    /// between desert, plains, grassland and jungle.
    ///
    /// Water is split into shallow and deep by whether it touches land, so the
    /// painter gets a real continental shelf instead of one flat blue.
    /// </summary>
    internal static class TerrainBiomeStage
    {
        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    world.Terrain[index] = world.Land[index]
                        ? LandKind(world, settings, index, x, y)
                        : WaterKind(world, x, y);
                }
            }
        }

        private static string WaterKind(TerrainWorld world, int x, int y)
        {
            WaterBody body = world.Water[world.Index(x, y)];

            // Lakes and rivers are shallow fresh water; only the open sea gets
            // the deep tone, and only where it is clear of the shelf.
            if (body is WaterBody.Lake or WaterBody.River)
                return "shallow_water";

            return TouchesLand(world, x, y) ? "shallow_water" : "deep_water";
        }

        private static string LandKind(TerrainWorld world, TerrainGenerationSettings settings, int index, int x, int y)
        {
            // An explicitly themed preset overrides climate entirely, so the
            // preset dropdown still means what it says.
            string? themed = ThemedKind(world, settings, index);
            if (themed is not null)
                return themed;

            // A narrow beach wherever land meets the sea.
            if (world.Relief[index] == TerrainRelief.Flat && TouchesOcean(world, x, y))
                return "sand";

            float temperature = world.Temperature[index];
            float moisture = world.Moisture[index];

            // Mountains are their own terrain, as in Civilization, and take a
            // snow cap where it is cold enough for one to persist.
            if (world.Relief[index] == TerrainRelief.Mountains)
                return settings.UseClimateBiomeMaps && temperature < 0.42f ? "snow" : "rock";

            // A game that does not want climate biomes gets plain terrain from
            // its preset instead: one ground type, a shore, and rock on the
            // heights. Turning the climate model off must not leave the map
            // half-classified.
            if (!settings.UseClimateBiomeMaps)
                return world.Relief[index] == TerrainRelief.Hills ? "gravel" : PlainGround(settings.Preset);

            // Cold dominates: nothing grows regardless of rainfall.
            if (temperature < 0.16f)
                return "snow";
            if (temperature < 0.30f)
                return "tundra";

            // Hills deliberately fall through to the biome table. Their relief
            // is carried by the hillshade, so a grassland hill stays grassland
            // instead of being flattened into grey rock.
            if (temperature > 0.72f && moisture > 0.58f)
                return "jungle";
            if (moisture < 0.20f)
                return "desert";
            if (moisture < 0.38f)
                return "dry_grass";
            if (moisture > 0.78f)
                return "swamp";

            return "grass";
        }

        /// <summary>The single ground type a preset uses when climate is off.</summary>
        private static string PlainGround(PainterlyTerrainComponent.TerrainPreset preset) => preset switch
        {
            PainterlyTerrainComponent.TerrainPreset.Desert => "desert",
            PainterlyTerrainComponent.TerrainPreset.Sand => "sand",
            PainterlyTerrainComponent.TerrainPreset.Rock => "gravel",
            PainterlyTerrainComponent.TerrainPreset.Swamp => "swamp",
            PainterlyTerrainComponent.TerrainPreset.Snow => "snow",
            PainterlyTerrainComponent.TerrainPreset.Ice => "ice",
            _ => "grass",
        };

        /// <summary>
        /// Preset-driven terrain for the themed presets. Returns null for the
        /// climate-driven presets so the biome table decides.
        /// </summary>
        private static string? ThemedKind(TerrainWorld world, TerrainGenerationSettings settings, int index)
        {
            float elevation = world.Elevation[index];
            return settings.Preset switch
            {
                PainterlyTerrainComponent.TerrainPreset.Desert => elevation >= 0.72f ? "rock" : "desert",
                PainterlyTerrainComponent.TerrainPreset.Sand => elevation >= 0.78f ? "rock" : "sand",
                PainterlyTerrainComponent.TerrainPreset.Rock => elevation >= 0.55f ? "rock" : "gravel",
                PainterlyTerrainComponent.TerrainPreset.Lava => elevation >= 0.58f ? "lava" : "rock",
                PainterlyTerrainComponent.TerrainPreset.Ice => elevation >= 0.54f ? "snow" : "ice",
                PainterlyTerrainComponent.TerrainPreset.Snow => elevation >= 0.62f ? "rock" : "snow",
                PainterlyTerrainComponent.TerrainPreset.Swamp => elevation >= 0.70f ? "gravel" : "swamp",
                _ => null,
            };
        }

        private static bool TouchesLand(TerrainWorld world, int x, int y)
        {
            foreach (int neighbour in TerrainGeometry.Neighbours(x, y, world.Width, world.Height))
            {
                if (world.Land[neighbour])
                    return true;
            }
            return false;
        }

        private static bool TouchesOcean(TerrainWorld world, int x, int y)
        {
            foreach (int neighbour in TerrainGeometry.Neighbours(x, y, world.Width, world.Height))
            {
                if (world.Water[neighbour] == WaterBody.Ocean)
                    return true;
            }
            return false;
        }
    }
}
