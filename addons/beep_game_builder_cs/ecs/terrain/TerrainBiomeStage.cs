using Godot;
using System.Collections.Generic;

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
        /// <summary>Rainfall cutoffs between desert, dry grass, grass and swamp.</summary>
        private readonly record struct MoistureBands(float Desert, float DryGrass, float Swamp);

        /// <summary>
        /// The rainfall cutoffs. A Whittaker diagram is a LOOKUP - a pair of
        /// temperature and moisture names a biome - so the cutoffs are fixed
        /// points on that diagram, the same way Dwarf Fortress and Minecraft use
        /// it.
        ///
        /// A quota pass used to replace these with percentiles of the map's own
        /// moisture, to guarantee each biome a share. It did the opposite:
        /// measured on a 64x40 map it collapsed every land biome into grass -
        /// hot maps lost desert entirely (108 tiles to 0) and temperate maps lost
        /// dry grass (538 to 0). It was also a second owner of a decision this
        /// table already makes, and TerrainWorldComponent turned it on for every
        /// world it built, so the collapse was the normal case rather than an
        /// opt-in.
        /// </summary>
        private static readonly MoistureBands Bands = new(0.20f, 0.38f, 0.78f);

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            // Ocean sand is assigned after topology cleanup from an inward
            // distance contour. Keep the underlying biome intact through reduction.

            // Both shore types are applied after reduction, retaining this
            // underlying biome for continuous distance-based rendering.

            MoistureBands bands = Bands;

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    world.Terrain[index] = world.Land[index]
                        ? LandKind(world, settings, bands, index)
                        : WaterKind(world, x, y);
                }
            }
        }

        /// <summary>
        /// Water split into shallow and deep. Lakes and rivers are always
        /// shallow fresh water; open sea is deep only clear of the shelf, so
        /// the painter gets a real continental shelf instead of one flat blue.
        /// </summary>
        private static string WaterKind(TerrainGenerationBuffer world, int x, int y)
        {
            WaterBody body = world.Water[world.Index(x, y)];

            // Lakes and rivers are shallow fresh water; only the open sea gets
            // the deep tone, and only where it is clear of the shelf.
            if (body is WaterBody.Lake or WaterBody.River)
                return "shallow_water";

            return TouchesLand(world, x, y) ? "shallow_water" : "deep_water";
        }

        private static string LandKind(
            TerrainGenerationBuffer world, TerrainGenerationSettings settings, MoistureBands bands,
            int index)
        {
            string? early = EarlyKind(world, settings, index);
            if (early is not null)
                return early;

            float moisture = world.Moisture[index];
            if (moisture <= bands.Desert)
                return "desert";
            if (moisture <= bands.DryGrass)
                return "dry_grass";
            if (moisture >= bands.Swamp)
                return "swamp";

            return "grass";
        }

        /// <summary>
        /// Preset, shore and temperature decisions before rainfall classification.
        /// Null lets the cell fall through to the moisture table.
        /// </summary>
        private static string? EarlyKind(
            TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            int index)
        {
            // An explicitly themed preset overrides climate entirely, so the
            // preset dropdown still means what it says.
            string? themed = ThemedKind(settings.Preset, world.Elevation[index]);
            if (themed is not null)
                return themed;

            float temperature = world.Temperature[index];

            // Relief is independent of ground cover. Hills and mountains keep
            // their biome; the relief/object renderer supplies exposed rocks.
            if (!settings.UseClimateBiomeMaps)
                return PlainGround(settings.Preset);

            // Cold dominates: nothing grows regardless of rainfall.
            if (temperature < 0.16f)
                return "snow";
            if (temperature < 0.30f)
                return "tundra";

            // Hills deliberately fall through to the biome table. Their relief
            // is carried by the hillshade, so a grassland hill stays grassland
            // instead of being flattened into grey rock.
            if (temperature > 0.72f && world.Moisture[index] > 0.58f)
                return "jungle";

            return null;
        }

        /// <summary>The single ground type a preset uses when climate is off.</summary>
        private static string PlainGround(TerrainPreset preset) => preset switch
        {
            TerrainPreset.Desert => "desert",
            TerrainPreset.Sand => "sand",
            TerrainPreset.Rock => "gravel",
            TerrainPreset.Swamp => "swamp",
            TerrainPreset.Snow => "snow",
            TerrainPreset.Ice => "ice",
            _ => "grass",
        };

        /// <summary>
        /// Preset-driven terrain for the themed presets. Returns null for the
        /// climate-driven presets so the biome table decides.
        /// </summary>
        internal static string? ThemedKind(TerrainPreset preset, float elevation)
        {
            return preset switch
            {
                TerrainPreset.Desert => elevation >= 0.72f ? "rock" : "desert",
                TerrainPreset.Sand => elevation >= 0.78f ? "rock" : "sand",
                TerrainPreset.Rock => elevation >= 0.55f ? "rock" : "gravel",
                TerrainPreset.Lava => elevation >= 0.58f ? "lava" : "rock",
                TerrainPreset.Ice => elevation >= 0.54f ? "snow" : "ice",
                TerrainPreset.Snow => elevation >= 0.62f ? "rock" : "snow",
                TerrainPreset.Swamp => elevation >= 0.70f ? "gravel" : "swamp",
                _ => null,
            };
        }

        private static bool TouchesLand(TerrainGenerationBuffer world, int x, int y)
        {
            foreach (int neighbour in TerrainGeometry.Neighbours(x, y, world.Width, world.Height))
            {
                if (world.Land[neighbour])
                    return true;
            }
            return false;
        }

    }
}
