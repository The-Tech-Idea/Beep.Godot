using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Which catalogue a map draws its resources from. A map is a setting, and
    /// the setting decides what is worth digging up: a lunar survey has no
    /// cattle and an oilfield has no ivory. Rolling one catalogue and filtering
    /// afterwards would leave whole terrains barren, so each set is its own
    /// weighted list.
    /// </summary>
    public enum ResourceSet
    {
        /// <summary>Food, luxuries and strategics, as a historical 4X uses them.</summary>
        Historical = 0,

        /// <summary>Hydrocarbon extraction: what a licence block is bought for.</summary>
        OilAndGas = 1,

        /// <summary>Off-world prospecting: volatiles, regolith and refractory metals.</summary>
        SpaceExploration = 2,
    }

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

        private static ResourceDefinition[] CatalogueFor(ResourceSet set) => set switch
        {
            ResourceSet.OilAndGas => OilAndGasCatalogue,
            ResourceSet.SpaceExploration => SpaceCatalogue,
            _ => HistoricalCatalogue,
        };

        /// <summary>
        /// Hydrocarbons follow the geology that traps them: sands and evaporite
        /// basins onshore, continental shelf offshore, heavy oil in cold bogs.
        /// Categories are reused - Bonus is what a field yields directly, Luxury
        /// what is worth exporting, Strategic what the plant cannot run without.
        /// </summary>
        private static readonly ResourceDefinition[] OilAndGasCatalogue =
        {
            new("crude_oil", ResourceCategory.Strategic, new[] { "desert", "dry_grass", "swamp" }, 1.2f),
            new("offshore_oil", ResourceCategory.Strategic, new[] { "deep_water" }, 0.9f),
            new("natural_gas", ResourceCategory.Strategic, new[] { "desert", "tundra", "dry_grass" }, 1.1f),
            new("offshore_gas", ResourceCategory.Strategic, new[] { "shallow_water", "deep_water" }, 0.8f),
            new("shale", ResourceCategory.Strategic, new[] { "gravel", "rock", "dry_grass" }, 0.9f),
            new("oil_sands", ResourceCategory.Strategic, new[] { "tundra", "swamp" }, 0.7f),
            new("condensate", ResourceCategory.Bonus, new[] { "desert", "shallow_water" }, 0.6f),
            new("helium", ResourceCategory.Luxury, new[] { "desert", "rock" }, 0.35f),
            new("sulphur", ResourceCategory.Bonus, new[] { "swamp", "desert" }, 0.6f),
            new("salt_dome", ResourceCategory.Bonus, new[] { "desert", "shallow_water" }, 0.5f),
            new("brine", ResourceCategory.Bonus, new[] { "shallow_water", "desert" }, 0.5f),
            new("coalbed_methane", ResourceCategory.Strategic, new[] { "gravel", "rock" }, 0.7f),
        };

        /// <summary>
        /// Off-world prospecting. Volatiles sit where it is cold enough to keep
        /// them, metals in exposed rock and regolith. Water ice leads because on
        /// any real mission it is the resource the others depend on.
        /// </summary>
        private static readonly ResourceDefinition[] SpaceCatalogue =
        {
            new("water_ice", ResourceCategory.Strategic, new[] { "snow", "ice", "tundra" }, 1.3f),
            new("ammonia_ice", ResourceCategory.Bonus, new[] { "snow", "ice" }, 0.7f),
            new("methane_ice", ResourceCategory.Strategic, new[] { "snow", "ice" }, 0.7f),
            new("helium3", ResourceCategory.Strategic, new[] { "gravel", "desert" }, 0.8f),
            new("regolith", ResourceCategory.Bonus, new[] { "gravel", "desert", "dry_grass" }, 1.2f),
            new("silicates", ResourceCategory.Bonus, new[] { "desert", "gravel" }, 0.9f),
            new("iron_ore", ResourceCategory.Strategic, new[] { "rock", "gravel" }, 1.0f),
            new("titanium", ResourceCategory.Strategic, new[] { "rock" }, 0.7f),
            new("rare_earths", ResourceCategory.Luxury, new[] { "rock", "gravel" }, 0.6f),
            new("platinum", ResourceCategory.Luxury, new[] { "rock" }, 0.45f),
            new("thorium", ResourceCategory.Strategic, new[] { "rock", "desert" }, 0.4f),
            new("deuterium", ResourceCategory.Strategic, new[] { "deep_water", "shallow_water" }, 0.6f),
        };

        private static readonly ResourceDefinition[] HistoricalCatalogue =
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

            ResourceDefinition[] catalogue = CatalogueFor(settings.ResourceSet);
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

                    string chosen = Choose(catalogue, terrain, world.CellRelief[cell], settings.Seed, cellX, cellY);
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
        private static string Choose(
            ResourceDefinition[] catalogue, string terrain, TerrainRelief relief, int seed, int cellX, int cellY)
        {
            float total = 0.0f;
            foreach (ResourceDefinition definition in catalogue)
            {
                if (Supports(definition, terrain, relief))
                    total += definition.Weight;
            }
            if (total <= 0.0f)
                return string.Empty;

            float roll = Hash01(seed + 63611, cellX, cellY) * total;
            foreach (ResourceDefinition definition in catalogue)
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

        /// <summary>
        /// Searches every catalogue, because a saved map may carry ids from a set
        /// the generator is no longer configured for.
        /// </summary>
        public static ResourceCategory CategoryOf(string id)
        {
            foreach (ResourceDefinition definition in AllDefinitions())
            {
                if (definition.Id == id)
                    return definition.Category;
            }
            return ResourceCategory.Bonus;
        }

        private static IEnumerable<ResourceDefinition> AllDefinitions()
        {
            foreach (ResourceDefinition d in HistoricalCatalogue) yield return d;
            foreach (ResourceDefinition d in OilAndGasCatalogue) yield return d;
            foreach (ResourceDefinition d in SpaceCatalogue) yield return d;
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
