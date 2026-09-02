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

        /// <summary>
        /// The catalog the world was configured with, or the shipped set the
        /// ResourceSet axis names. One object, and the game side reads the same
        /// one - which is what stops the map generating a resource the economy
        /// has never heard of.
        /// </summary>
        private static ResourceCatalog CatalogueFor(TerrainGenerationSettings settings)
            => settings.ResourceCatalog ?? ResourceCatalogs.For(settings.ResourceSet);


        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            if (settings.ResourceDensity <= 0.0f)
                return;

            ResourceCatalog catalogue = CatalogueFor(settings);
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
                    if (TerrainGeometry.Hash01(cellX, cellY, settings.Seed + 63601) > density)
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
            ResourceCatalog catalogue, string terrain, TerrainRelief relief, int seed, int cellX, int cellY)
        {
            float total = 0.0f;
            foreach (ResourceDefinition definition in catalogue.Resources)
            {
                if (Supports(definition, terrain, relief))
                    total += definition.Weight;
            }
            if (total <= 0.0f)
                return string.Empty;

            float roll = TerrainGeometry.Hash01(cellX, cellY, seed + 63611) * total;
            foreach (ResourceDefinition definition in catalogue.Resources)
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
            if (definition.RequiresRelief && relief != (TerrainRelief)definition.RequiredRelief)
                return false;

            foreach (string allowed in definition.TerrainKinds)
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
            => ResourceCatalogs.FindAnywhere(id)?.Category ?? ResourceCategory.Bonus;

    }
}
