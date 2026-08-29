using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>What a resource is for, in the Civilization sense.</summary>
    internal enum ResourceCategory : byte
    {
        Bonus = 0,
        Luxury = 1,
        Strategic = 2,
    }

    /// <summary>
    /// One placeable resource and the ground it belongs on.
    /// </summary>
    internal readonly record struct ResourceDefinition(
        string Id,
        ResourceCategory Category,
        string[] Terrain,
        float Weight,
        TerrainRelief? RequiredRelief = null);

    /// <summary>
    /// Scatters resources across the map, each only on terrain that would
    /// actually produce it - fish in the sea, deer on tundra, gems in jungle,
    /// iron in the hills.
    ///
    /// Resources are chosen per GAMEPLAY CELL rather than per sample, because a
    /// resource is something a tile has. Placement is spaced so a map does not
    /// end up with a single corner holding everything, and is driven entirely by
    /// the seeded hash, so the same seed lays out the same resources.
    /// </summary>
    internal static class TerrainResourceStage
    {
        /// <summary>Share of eligible LAND tiles that receive a resource.</summary>
        private const float Density = 0.085f;

        /// <summary>
        /// Water carries far fewer resources than land. Without this the sea -
        /// which is most of the map - ends up holding most of the resources,
        /// and the ocean reads as a field of markers.
        /// </summary>
        private const float WaterDensityScale = 0.22f;

        /// <summary>Tiles that must separate two of the same resource.</summary>
        private const int SameResourceSpacing = 4;

        private static readonly ResourceDefinition[] Catalogue =
        {
            // Bonus - food and early production.
            new("wheat", ResourceCategory.Bonus, new[] { "grass", "dry_grass" }, 1.0f),
            new("cattle", ResourceCategory.Bonus, new[] { "grass" }, 0.9f),
            new("banana", ResourceCategory.Bonus, new[] { "jungle" }, 0.9f),
            new("deer", ResourceCategory.Bonus, new[] { "tundra" }, 1.0f),
            new("fish", ResourceCategory.Bonus, new[] { "shallow_water" }, 1.1f),
            new("whale", ResourceCategory.Bonus, new[] { "deep_water" }, 0.35f),
            new("stone", ResourceCategory.Bonus, new[] { "gravel", "rock" }, 0.8f),

            // Luxury - the things worth trading for.
            new("gems", ResourceCategory.Luxury, new[] { "jungle" }, 0.7f),
            new("spices", ResourceCategory.Luxury, new[] { "jungle", "swamp" }, 0.7f),
            new("wine", ResourceCategory.Luxury, new[] { "grass", "dry_grass" }, 0.7f),
            new("furs", ResourceCategory.Luxury, new[] { "tundra", "snow" }, 0.7f),
            new("incense", ResourceCategory.Luxury, new[] { "desert" }, 0.7f),
            new("ivory", ResourceCategory.Luxury, new[] { "dry_grass" }, 0.5f),
            new("silver", ResourceCategory.Luxury, new[] { "desert", "tundra" }, 0.5f),

            // Strategic - what an army runs on.
            new("horses", ResourceCategory.Strategic, new[] { "grass", "dry_grass" }, 1.0f),
            new("iron", ResourceCategory.Strategic, new[] { "gravel", "rock", "dry_grass" }, 1.0f),
            new("coal", ResourceCategory.Strategic, new[] { "gravel", "rock" }, 0.8f),
            new("oil", ResourceCategory.Strategic, new[] { "desert", "swamp", "deep_water" }, 0.45f),
            new("aluminium", ResourceCategory.Strategic, new[] { "desert", "tundra" }, 0.6f),
            new("uranium", ResourceCategory.Strategic, new[] { "gravel", "rock", "desert", "tundra" }, 0.35f),
        };

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            if (settings.ResourceDensity <= 0.0f)
                return;

            int wide = world.CellsWide;
            int high = world.CellsHigh;
            var placed = new List<(Vector2I Cell, string Id)>();

            for (int cellY = 0; cellY < high; cellY++)
            {
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    int cell = (cellY * wide) + cellX;
                    string terrain = world.CellTerrain[cell];
                    bool isLand = world.CellWater[cell] == WaterBody.None;

                    float density = (isLand ? Density : Density * WaterDensityScale) * settings.ResourceDensity;
                    if (Hash01(settings.Seed + 63601, cellX, cellY) > density)
                        continue;

                    string chosen = Choose(terrain, world.CellRelief[cell], settings.Seed, cellX, cellY);
                    if (chosen.Length == 0)
                        continue;

                    if (!FarEnough(placed, cellX, cellY, chosen))
                        continue;

                    world.Resource[cell] = chosen;
                    placed.Add((new Vector2I(cellX, cellY), chosen));
                }
            }
        }

        /// <summary>
        /// Picks among everything this terrain supports, weighted, using a hash
        /// so the choice is stable for a seed.
        /// </summary>
        private static string Choose(string terrain, TerrainRelief relief, int seed, int cellX, int cellY)
        {
            float total = 0.0f;
            foreach (ResourceDefinition definition in Catalogue)
            {
                if (Supports(definition, terrain, relief))
                    total += definition.Weight;
            }
            if (total <= 0.0f)
                return string.Empty;

            float roll = Hash01(seed + 63611, cellX, cellY) * total;
            foreach (ResourceDefinition definition in Catalogue)
            {
                if (!Supports(definition, terrain, relief))
                    continue;
                roll -= definition.Weight;
                if (roll <= 0.0f)
                    return definition.Id;
            }
            return string.Empty;
        }

        private static bool Supports(ResourceDefinition definition, string terrain, TerrainRelief relief)
        {
            if (definition.RequiredRelief is { } required && relief != required)
                return false;

            foreach (string allowed in definition.Terrain)
            {
                if (allowed == terrain)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Keeps copies of one resource apart, so a map does not end up with all
        /// its iron in a single valley.
        /// </summary>
        private static bool FarEnough(List<(Vector2I Cell, string Id)> placed, int cellX, int cellY, string id)
        {
            foreach ((Vector2I cell, string existing) in placed)
            {
                if (existing != id)
                    continue;
                int dx = cell.X - cellX;
                int dy = cell.Y - cellY;
                if ((dx * dx) + (dy * dy) < SameResourceSpacing * SameResourceSpacing)
                    return false;
            }
            return true;
        }

        public static ResourceCategory CategoryOf(string id)
        {
            foreach (ResourceDefinition definition in Catalogue)
            {
                if (definition.Id == id)
                    return definition.Category;
            }
            return ResourceCategory.Bonus;
        }

        private static float Hash01(int seed, int x, int y)
        {
            uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215.0f;
        }
    }
}
