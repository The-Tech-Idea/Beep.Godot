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

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            // Distance to the OPEN SEA, in samples. A beach belongs to the
            // ocean; a lake shore is a different thing with its own width.
            var ocean = new bool[world.Count];
            for (int index = 0; index < world.Count; index++)
                ocean[index] = world.Water[index] == WaterBody.Ocean;

            int[] fromOcean = TerrainGeometry.DistanceTo(ocean, world.Width, world.Height);

            // BeachWidth is in TILES, so it has to be converted to the sample
            // grid the field is measured on. Before this the beach was whatever
            // touched the sea - ONE SAMPLE - which is an eighth of a tile, and
            // never survived the majority reduction to tiles. The map had a
            // beach setting, a beach rule, and no beaches.
            int beachSamples = Mathf.RoundToInt(
                Mathf.Max(0.0f, settings.BeachWidth) * world.SamplesPerCell);

            // A lake gets its own shore, measured the same way but from the lake
            // rather than the sea. Sharing the ocean's field would put a beach
            // round every pond at whatever width the coast uses, and the two are
            // not the same thing: a sea beach is surf-built and wide, a lake
            // shore is a thin rim.
            var lakes = new bool[world.Count];
            for (int index = 0; index < world.Count; index++)
                lakes[index] = world.Water[index] == WaterBody.Lake;

            int[] fromLake = TerrainGeometry.DistanceTo(lakes, world.Width, world.Height);
            int lakeShoreSamples = Mathf.RoundToInt(
                Mathf.Max(0.0f, settings.LakeShoreWidth) * world.SamplesPerCell);

            // After the beach field, because the quota counts only the cells the
            // rainfall table decides - and a beach is decided before it.
            MoistureBands bands = Bands;

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    world.Terrain[index] = world.Land[index]
                        ? LandKind(world, settings, bands, fromOcean, beachSamples, fromLake, lakeShoreSamples, index, x, y)
                        : WaterKind(world, x, y);
                }
            }
        }

        /// <summary>
        /// Water split into shallow and deep. Lakes and rivers are always
        /// shallow fresh water; open sea is deep only clear of the shelf, so
        /// the painter gets a real continental shelf instead of one flat blue.
        /// </summary>
        private static string WaterKind(TerrainWorld world, int x, int y)
        {
            WaterBody body = world.Water[world.Index(x, y)];

            // Lakes and rivers are shallow fresh water; only the open sea gets
            // the deep tone, and only where it is clear of the shelf.
            if (body is WaterBody.Lake or WaterBody.River)
                return "shallow_water";

            return TouchesLand(world, x, y) ? "shallow_water" : "deep_water";
        }

        private static string LandKind(
            TerrainWorld world, TerrainGenerationSettings settings, MoistureBands bands,
            int[] fromOcean, int beachSamples, int[] fromLake, int lakeShoreSamples,
            int index, int x, int y)
        {
            string? early = EarlyKind(world, settings, fromOcean, beachSamples, fromLake, lakeShoreSamples, index, x, y);
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
        /// Everything decided BEFORE rainfall: a themed preset, the beach, the
        /// peaks, and cold that nothing grows through. Null where the cell falls
        /// through to the rainfall table.
        ///
        /// It is one method, and both the classifier and the quota call it. A
        /// quota counted over all land instead would be a fraction of a
        /// population the table never sees - ask for 30% dry grassland, get 24%,
        /// with nothing to say where the rest went.
        /// </summary>
        private static string? EarlyKind(
            TerrainWorld world, TerrainGenerationSettings settings,
            int[] fromOcean, int beachSamples, int[] fromLake, int lakeShoreSamples,
            int index, int x, int y)
        {
            // An explicitly themed preset overrides climate entirely, so the
            // preset dropdown still means what it says.
            string? themed = ThemedKind(world, settings, index);
            if (themed is not null)
                return themed;

            // A beach of the requested width wherever flat land meets the sea.
            if (beachSamples > 0
                && world.Relief[index] == TerrainRelief.Flat
                && fromOcean[index] <= beachSamples)
            {
                return "sand";
            }

            // The lake's own rim.
            if (lakeShoreSamples > 0
                && world.Relief[index] == TerrainRelief.Flat
                && fromLake[index] <= lakeShoreSamples)
            {
                return "sand";
            }

            float temperature = world.Temperature[index];

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
        private static string? ThemedKind(TerrainWorld world, TerrainGenerationSettings settings, int index)
        {
            float elevation = world.Elevation[index];
            return settings.Preset switch
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

        private static bool TouchesLand(TerrainWorld world, int x, int y)
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
