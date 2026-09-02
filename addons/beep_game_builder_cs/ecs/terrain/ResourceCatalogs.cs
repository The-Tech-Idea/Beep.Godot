using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The resource sets the addon ships, as catalogs both the generator and the
    /// economy can read.
    ///
    /// These were three private arrays inside the generation stage, so the game
    /// side could not see them and a resource the map placed was a string nobody
    /// had defined. The data is unchanged - same ids, categories, ground and
    /// weights - but it is now the shared type, so a wallet, a HUD and a resource
    /// node can ask what "crude_oil" is instead of hoping.
    ///
    /// A game that wants its own economy authors a ResourceCatalog and assigns it
    /// to the generator rather than editing this file.
    /// </summary>
    public static class ResourceCatalogs
    {
        private static ResourceCatalog? _historical;
        private static ResourceCatalog? _oilAndGas;
        private static ResourceCatalog? _space;

        public static ResourceCatalog For(ResourceSet set) => set switch
        {
            ResourceSet.OilAndGas => OilAndGas,
            ResourceSet.SpaceExploration => Space,
            _ => Historical,
        };

        /// <summary>Food, luxuries and strategics, as a historical 4X uses them.</summary>
        public static ResourceCatalog Historical => _historical ??= Build("Historical", new[]
        {
            // Bonus - food and early production.
            ("wheat", "Wheat", ResourceCategory.Bonus, new[] { "grass", "dry_grass" }, 1.0f),
            ("cattle", "Cattle", ResourceCategory.Bonus, new[] { "grass" }, 0.9f),
            ("banana", "Banana", ResourceCategory.Bonus, new[] { "jungle" }, 0.9f),
            ("deer", "Deer", ResourceCategory.Bonus, new[] { "tundra" }, 1.0f),
            ("fish", "Fish", ResourceCategory.Bonus, new[] { "shallow_water" }, 1.1f),
            ("whale", "Whale", ResourceCategory.Bonus, new[] { "deep_water" }, 0.35f),
            ("stone", "Stone", ResourceCategory.Bonus, new[] { "gravel", "rock" }, 0.8f),

            // Luxury - the things worth trading for.
            ("gems", "Gems", ResourceCategory.Luxury, new[] { "jungle" }, 0.7f),
            ("spices", "Spices", ResourceCategory.Luxury, new[] { "jungle", "swamp" }, 0.7f),
            ("wine", "Wine", ResourceCategory.Luxury, new[] { "grass", "dry_grass" }, 0.7f),
            ("furs", "Furs", ResourceCategory.Luxury, new[] { "tundra", "snow" }, 0.7f),
            ("incense", "Incense", ResourceCategory.Luxury, new[] { "desert" }, 0.7f),
            ("ivory", "Ivory", ResourceCategory.Luxury, new[] { "dry_grass" }, 0.5f),
            ("silver", "Silver", ResourceCategory.Luxury, new[] { "desert", "tundra" }, 0.5f),

            // Strategic - what an army runs on.
            ("horses", "Horses", ResourceCategory.Strategic, new[] { "grass", "dry_grass" }, 1.0f),
            ("iron", "Iron", ResourceCategory.Strategic, new[] { "gravel", "rock", "dry_grass" }, 1.0f),
            ("coal", "Coal", ResourceCategory.Strategic, new[] { "gravel", "rock" }, 0.8f),
            ("oil", "Oil", ResourceCategory.Strategic, new[] { "desert", "swamp", "deep_water" }, 0.45f),
            ("aluminium", "Aluminium", ResourceCategory.Strategic, new[] { "desert", "tundra" }, 0.6f),
            ("uranium", "Uranium", ResourceCategory.Strategic, new[] { "gravel", "rock", "desert", "tundra" }, 0.35f),
        });

        /// <summary>
        /// Hydrocarbons follow the geology that traps them: sands and evaporite
        /// basins onshore, continental shelf offshore, heavy oil in cold bogs.
        /// </summary>
        public static ResourceCatalog OilAndGas => _oilAndGas ??= Build("Oil And Gas", new[]
        {
            ("crude_oil", "Crude Oil", ResourceCategory.Strategic, new[] { "desert", "dry_grass", "swamp" }, 1.2f),
            ("offshore_oil", "Offshore Oil", ResourceCategory.Strategic, new[] { "deep_water" }, 0.9f),
            ("natural_gas", "Natural Gas", ResourceCategory.Strategic, new[] { "desert", "tundra", "dry_grass" }, 1.1f),
            ("offshore_gas", "Offshore Gas", ResourceCategory.Strategic, new[] { "shallow_water", "deep_water" }, 0.8f),
            ("shale", "Shale", ResourceCategory.Strategic, new[] { "gravel", "rock", "dry_grass" }, 0.9f),
            ("oil_sands", "Oil Sands", ResourceCategory.Strategic, new[] { "tundra", "swamp" }, 0.7f),
            ("condensate", "Condensate", ResourceCategory.Bonus, new[] { "desert", "shallow_water" }, 0.6f),
            ("helium", "Helium", ResourceCategory.Luxury, new[] { "desert", "rock" }, 0.35f),
            ("sulphur", "Sulphur", ResourceCategory.Bonus, new[] { "swamp", "desert" }, 0.6f),
            ("salt_dome", "Salt Dome", ResourceCategory.Bonus, new[] { "desert", "shallow_water" }, 0.5f),
            ("brine", "Brine", ResourceCategory.Bonus, new[] { "shallow_water", "desert" }, 0.5f),
            ("coalbed_methane", "Coalbed Methane", ResourceCategory.Strategic, new[] { "gravel", "rock" }, 0.7f),
        });

        /// <summary>
        /// Off-world prospecting. Volatiles sit where it is cold enough to keep
        /// them, metals in exposed rock and regolith. Water ice leads because on
        /// any real mission it is the resource the others depend on.
        /// </summary>
        public static ResourceCatalog Space => _space ??= Build("Space Exploration", new[]
        {
            ("water_ice", "Water Ice", ResourceCategory.Strategic, new[] { "snow", "ice", "tundra" }, 1.3f),
            ("ammonia_ice", "Ammonia Ice", ResourceCategory.Bonus, new[] { "snow", "ice" }, 0.7f),
            ("methane_ice", "Methane Ice", ResourceCategory.Strategic, new[] { "snow", "ice" }, 0.7f),
            ("helium3", "Helium-3", ResourceCategory.Strategic, new[] { "gravel", "desert" }, 0.8f),
            ("regolith", "Regolith", ResourceCategory.Bonus, new[] { "gravel", "desert", "dry_grass" }, 1.2f),
            ("silicates", "Silicates", ResourceCategory.Bonus, new[] { "desert", "gravel" }, 0.9f),
            ("iron_ore", "Iron Ore", ResourceCategory.Strategic, new[] { "rock", "gravel" }, 1.0f),
            ("titanium", "Titanium", ResourceCategory.Strategic, new[] { "rock" }, 0.7f),
            ("rare_earths", "Rare Earths", ResourceCategory.Luxury, new[] { "rock", "gravel" }, 0.6f),
            ("platinum", "Platinum", ResourceCategory.Luxury, new[] { "rock" }, 0.45f),
            ("thorium", "Thorium", ResourceCategory.Strategic, new[] { "rock", "desert" }, 0.4f),
            ("deuterium", "Deuterium", ResourceCategory.Strategic, new[] { "deep_water", "shallow_water" }, 0.6f),
        });

        /// <summary>Every shipped resource, for resolving an id off a saved map.</summary>
        public static ResourceDefinition? FindAnywhere(string id)
            => Historical.Find(id) ?? OilAndGas.Find(id) ?? Space.Find(id);

        private static ResourceCatalog Build(
            string name,
            (string Id, string DisplayName, ResourceCategory Category, string[] Terrain, float Weight)[] entries)
        {
            var catalog = new ResourceCatalog { CatalogName = name };
            foreach ((string id, string displayName, ResourceCategory category, string[] terrain, float weight) in entries)
            {
                var definition = new ResourceDefinition
                {
                    Id = id,
                    DisplayName = displayName,
                    Category = category,
                    Weight = weight,
                };
                foreach (string kind in terrain)
                    definition.TerrainKinds.Add(kind);
                catalog.Resources.Add(definition);
            }
            return catalog;
        }
    }
}
