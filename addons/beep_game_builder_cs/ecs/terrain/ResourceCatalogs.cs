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
        public static ResourceCatalog Historical => _historical ??= Describe(Stratify(Build("Historical", new[]
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
        }), liquid: new[] { "fish", "whale" }, underground: new[]
        {
            ("iron", ResourceDepth.Shallow),
            ("coal", ResourceDepth.Shallow),
            ("gems", ResourceDepth.Mid),
            ("silver", ResourceDepth.Mid),
            ("oil", ResourceDepth.Mid),
            ("aluminium", ResourceDepth.Mid),
            ("uranium", ResourceDepth.Deep),
        }),
            fluids: new[] { "oil" },
            gases: System.Array.Empty<string>(),
            tags: new[]
            {
                ("food", new[] { "wheat", "cattle", "banana", "deer", "fish", "whale" }),
                ("metal", new[] { "iron", "silver", "aluminium" }),
                ("fuel", new[] { "coal", "oil", "uranium" }),
            });

        /// <summary>
        /// Hydrocarbons follow the geology that traps them: sands and evaporite
        /// basins onshore, continental shelf offshore, heavy oil in cold bogs.
        /// </summary>
        public static ResourceCatalog OilAndGas => _oilAndGas ??= Describe(Stratify(Build("Oil And Gas", new[]
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
        }), liquid: System.Array.Empty<string>(), underground: new[]
        {
            // The whole point of a licence block: what is UNDER it. Depth is
            // the tech ladder - sands and shale first, conventional fields
            // next, offshore deepest.
            ("oil_sands", ResourceDepth.Shallow),
            ("shale", ResourceDepth.Shallow),
            ("coalbed_methane", ResourceDepth.Shallow),
            ("crude_oil", ResourceDepth.Mid),
            ("natural_gas", ResourceDepth.Mid),
            ("condensate", ResourceDepth.Mid),
            ("offshore_gas", ResourceDepth.Deep),
            ("offshore_oil", ResourceDepth.Deep),
            ("helium", ResourceDepth.Deep),
        }),
            fluids: new[] { "crude_oil", "offshore_oil", "condensate", "brine" },
            gases: new[] { "natural_gas", "offshore_gas", "coalbed_methane", "helium" },
            tags: new[]
            {
                ("hydrocarbon", new[] { "crude_oil", "offshore_oil", "natural_gas", "offshore_gas", "shale", "oil_sands", "condensate", "coalbed_methane" }),
            });

        /// <summary>
        /// Off-world prospecting. Volatiles sit where it is cold enough to keep
        /// them, metals in exposed rock and regolith. Water ice leads because on
        /// any real mission it is the resource the others depend on.
        /// </summary>
        public static ResourceCatalog Space => _space ??= Describe(Stratify(Build("Space Exploration", new[]
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
        }), liquid: new[] { "deuterium" }, underground: new[]
        {
            // The colony loop: near-surface ices the base lives on, metals
            // below them, the precious stuff deep.
            ("water_ice", ResourceDepth.Shallow),
            ("ammonia_ice", ResourceDepth.Shallow),
            ("methane_ice", ResourceDepth.Shallow),
            ("iron_ore", ResourceDepth.Shallow),
            ("titanium", ResourceDepth.Mid),
            ("rare_earths", ResourceDepth.Mid),
            ("thorium", ResourceDepth.Deep),
            ("platinum", ResourceDepth.Deep),
        }),
            fluids: System.Array.Empty<string>(),
            gases: new[] { "helium3" },
            tags: new[]
            {
                ("ice", new[] { "water_ice", "ammonia_ice", "methane_ice" }),
                ("metal", new[] { "iron_ore", "titanium", "platinum" }),
            });

        /// <summary>Every shipped resource, for resolving an id off a saved map.</summary>
        public static ResourceDefinition? FindAnywhere(string id)
            => Historical.Find(id) ?? OilAndGas.Find(id) ?? Space.Find(id);

        /// <summary>
        /// Assigns strata after Build so the tables above stay readable. Liquid
        /// ids float in the water column and are worked by boats; underground
        /// ids become invisible fields at their depth band, extracted by
        /// buildings. Everything unnamed stays a surface walk-up resource.
        /// </summary>
        private static ResourceCatalog Stratify(
            ResourceCatalog catalog, string[] liquid, (string Id, ResourceDepth Depth)[] underground)
        {
            foreach (string id in liquid)
            {
                if (catalog.Find(id) is not { } definition)
                    continue;
                definition.Stratum = ResourceStratum.Liquid;
                // Water work is its own trade: fish jobs get their own kind,
                // so boats claim them (AllowedJobKinds = ["fish"]) and land
                // workers can be kept off water they cannot path to.
                definition.GatherJobKind = "fish";
            }

            foreach ((string id, ResourceDepth depth) in underground)
            {
                if (catalog.Find(id) is not { } definition)
                    continue;
                definition.Stratum = ResourceStratum.Underground;
                definition.Depth = depth;
                definition.Extraction = ResourceExtraction.Extractor;
            }

            return catalog;
        }

        /// <summary>
        /// Physical forms and free-form tags, assigned the same way. A Fluid
        /// or Gas underground deposit drains as one connected reservoir.
        /// </summary>
        private static ResourceCatalog Describe(
            ResourceCatalog catalog,
            string[] fluids,
            string[] gases,
            (string Tag, string[] Ids)[] tags)
        {
            foreach (string id in fluids)
            {
                if (catalog.Find(id) is { } definition)
                    definition.Form = ResourceForm.Fluid;
            }

            foreach (string id in gases)
            {
                if (catalog.Find(id) is { } definition)
                    definition.Form = ResourceForm.Gas;
            }

            foreach ((string tag, string[] ids) in tags)
            {
                foreach (string id in ids)
                {
                    if (catalog.Find(id) is { } definition && !definition.HasTag(tag))
                        definition.Tags.Add(tag);
                }
            }

            return catalog;
        }

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
