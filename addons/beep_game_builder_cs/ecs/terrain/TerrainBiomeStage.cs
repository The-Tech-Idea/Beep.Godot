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

        /// <summary>The fixed cutoffs, used when quotas are off.</summary>
        private static readonly MoistureBands Fixed = new(0.20f, 0.38f, 0.78f);

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
            MoistureBands bands = settings.UseBiomeQuotas
                ? Quotas(world, settings, fromOcean, beachSamples, fromLake, lakeShoreSamples)
                : Fixed;

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
        /// Cutoffs taken from the rainfall a map ACTUALLY has, so a named
        /// fraction of the land comes out desert whatever the map's size or
        /// noise settings.
        ///
        /// This is how Civilization does it - its climate generator works in
        /// "percent of land below the rainfall threshold" rather than in
        /// absolute rainfall - and the reason is the failure this replaces.
        /// Fixed cutoffs read a noise field whose spread changes with map size,
        /// frequency and octave count, so the same numbers gave 33% desert on
        /// one setting and 6% on another. Nothing in the parameters says
        /// "desert", so nobody can tell which they will get.
        /// </summary>
        private static MoistureBands Quotas(
            TerrainWorld world, TerrainGenerationSettings settings,
            int[] fromOcean, int beachSamples, int[] fromLake, int lakeShoreSamples)
        {
            // Only the cells the rainfall table actually decides.
            var moisture = new List<float>();
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    if (world.Land[index] && EarlyKind(world, settings, fromOcean, beachSamples, fromLake, lakeShoreSamples, index, x, y) is null)
                        moisture.Add(world.Moisture[index]);
                }
            }

            if (moisture.Count == 0)
                return Fixed;

            moisture.Sort();

            float desert = Mathf.Clamp(settings.DesertFraction, 0.0f, 1.0f);
            float dry = Mathf.Clamp(settings.DryGrassFraction, 0.0f, 1.0f - desert);
            float swamp = Mathf.Clamp(settings.SwampFraction, 0.0f, 1.0f - desert - dry);

            return new MoistureBands(
                At(moisture, desert),
                At(moisture, desert + dry),
                At(moisture, 1.0f - swamp));

            static float At(List<float> sorted, float fraction)
            {
                int at = Mathf.Clamp(
                    Mathf.RoundToInt(fraction * (sorted.Count - 1)), 0, sorted.Count - 1);
                return sorted[at];
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
