using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Detached generation input. Capture on the main thread before dispatching work.</summary>
internal sealed class TerrainResourceRules
{
    internal sealed class Entry
    {
        private readonly string[] _terrain;
        public string Id { get; }
        public ResourceStratum Stratum { get; }
        public ResourceDepth Depth { get; }
        /// <summary>The authored category, captured so a worker never asks the catalog resource.</summary>
        public ResourceCategory Category { get; }
        public float Weight { get; }
        public float DepositScale { get; }
        private readonly bool _requiresRelief;
        private readonly TerrainRelief _relief;

        public Entry(ResourceDefinition definition)
        {
            Id = definition.Id;
            Stratum = definition.Stratum;
            Depth = definition.Depth;
            Category = definition.Category;
            Weight = definition.Weight;
            DepositScale = definition.DepositScale;
            _requiresRelief = definition.RequiresRelief;
            _relief = (TerrainRelief)definition.RequiredRelief;
            _terrain = new string[definition.TerrainKinds.Count];
            for (int i = 0; i < _terrain.Length; i++) _terrain[i] = definition.TerrainKinds[i];
        }

        public bool Supports(string terrain, TerrainRelief relief)
        {
            if (_requiresRelief && relief != _relief) return false;
            foreach (string allowed in _terrain)
                if (allowed == terrain) return true;
            return false;
        }
    }

    public IReadOnlyList<Entry> Entries { get; }
    private readonly Dictionary<string, float> _weights = new(StringComparer.Ordinal);

    private TerrainResourceRules(List<Entry> entries)
    {
        Entries = entries.AsReadOnly();
        // ResourceCatalog.Find returns the first matching definition.
        foreach (Entry entry in entries) _weights.TryAdd(entry.Id, entry.Weight);
    }

    public float WeightOf(string id) => _weights.TryGetValue(id, out float weight) ? weight : 1f;

    /// <summary>The first catalog entry with this id (ResourceCatalog.Find's rule), or null.</summary>
    public Entry? Find(string id)
    {
        foreach (Entry entry in Entries)
            if (entry.Id == id) return entry;
        return null;
    }

    public static TerrainResourceRules Capture(TerrainGenerationSettings settings)
    {
        var entries = new List<Entry>();
        if (settings.ResourceDensity > 0)
        {
            ResourceCatalog catalog = settings.ResourceCatalog ?? ResourceCatalogs.For(settings.ResourceSet);
            foreach (ResourceDefinition? definition in catalog.Resources)
                if (definition is not null) entries.Add(new Entry(definition));
        }
        return new TerrainResourceRules(entries);
    }
}
