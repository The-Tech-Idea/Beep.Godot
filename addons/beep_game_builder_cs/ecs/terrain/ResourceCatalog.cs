using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// THE set of resources a world uses - read by the generator to decide what
    /// the map holds, and by the economy to know what those things are worth.
    ///
    /// One catalog, both systems. The generator asks it which resources belong on
    /// a terrain kind; a resource node asks it how much a deposit holds and what
    /// gathering pays; a HUD asks it for a display name. Because they
    /// ask the same object, a map cannot generate a resource the game has never
    /// heard of, which is exactly what happened while each side kept its own
    /// list.
    ///
    /// Assign one on the generator to change what a world can contain. A game
    /// that wants ore and lumber instead of a 4X resource table authors its own
    /// catalog rather than editing the addon.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ResourceCatalog : Resource
    {
        [Export] public string CatalogName { get; set; } = "Resources";

        [Export] public Godot.Collections.Array<ResourceDefinition> Resources { get; set; } = new();

        // Ids normalised to definitions, built lazily and rebuilt when the resource
        // count changes (an authored catalog is loaded once; a runtime add/remove
        // moves the count). Keyed by GridIds.Normalize so a cost that says "Crude Oil"
        // and a map that carries "crude_oil" resolve to the one definition instead of
        // reading as two - the ordinal == scan this replaces matched neither.
        private System.Collections.Generic.Dictionary<string, ResourceDefinition>? _byId;
        private int _indexedCount = -1;

        private System.Collections.Generic.Dictionary<string, ResourceDefinition> Index()
        {
            if (_byId is not null && _indexedCount == Resources.Count)
                return _byId;

            var map = new System.Collections.Generic.Dictionary<string, ResourceDefinition>();
            foreach (ResourceDefinition? definition in Resources)
            {
                if (definition is null)
                    continue;
                string key = GridIds.Normalize(definition.Id);
                // First wins, matching the linear scan this replaces.
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                    map[key] = definition;
            }
            _byId = map;
            _indexedCount = Resources.Count;
            return _byId;
        }

        /// <summary>The definition for an id, or null when the catalog has none. Case- and separator-insensitive.</summary>
        public ResourceDefinition? Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return Index().TryGetValue(GridIds.Normalize(id), out ResourceDefinition? definition) ? definition : null;
        }

        public bool Contains(string id) => Find(id) is not null;

        /// <summary>
        /// The category for an id. Returns Bonus for an unknown id rather than
        /// throwing: a SAVED map can carry ids from a catalog the generator is no
        /// longer configured for, and a colour on an overlay is not worth failing
        /// a load over. Callers that need to know whether the id is real should
        /// ask Contains.
        /// </summary>
        public ResourceCategory CategoryOf(string id) => Find(id)?.Category ?? ResourceCategory.Bonus;

        public string DisplayNameOf(string id)
        {
            ResourceDefinition? definition = Find(id);
            return definition is null || string.IsNullOrEmpty(definition.DisplayName)
                ? id
                : definition.DisplayName;
        }

        /// <summary>Every resource that can occur on a terrain kind.</summary>
        public Godot.Collections.Array<ResourceDefinition> ForTerrain(string terrainKind)
        {
            var matches = new Godot.Collections.Array<ResourceDefinition>();
            if (string.IsNullOrEmpty(terrainKind))
                return matches;

            foreach (ResourceDefinition? definition in Resources)
            {
                if (definition is null)
                    continue;
                foreach (string kind in definition.TerrainKinds)
                {
                    if (kind == terrainKind)
                    {
                        matches.Add(definition);
                        break;
                    }
                }
            }
            return matches;
        }
    }
}
