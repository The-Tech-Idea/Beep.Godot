using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The set of terrain kinds a world uses and what each one means (DUP-13). Generation and the
    /// views look kinds up here rather than in their own hardcoded tables, so the "lava field painted
    /// as grass" hunt - editing five tables to add one kind - happens in exactly one place. <see
    /// cref="Standard"/> is the addon's built-in set; a game authors its own catalog (or extends the
    /// standard one) to add a kind without editing the addon. Catalog ORDER is the saved TileSet tile
    /// index and is append-only.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainKindCatalog : Resource
    {
        [Export] public Godot.Collections.Array<TerrainKind> Kinds { get; set; } = new();

        private Dictionary<string, TerrainKind>? _byId;

        private Dictionary<string, TerrainKind> ById()
        {
            if (_byId is { } cached && cached.Count == Kinds.Count)
                return cached;
            var map = new Dictionary<string, TerrainKind>(System.StringComparer.Ordinal);
            foreach (TerrainKind kind in Kinds)
                if (kind is not null && !string.IsNullOrEmpty(kind.Id))
                    map[kind.Id] = kind;
            _byId = map;
            return map;
        }

        /// <summary>The kind with this id, or null when the catalog does not define it.</summary>
        public TerrainKind? For(string id) => ById().GetValueOrDefault(id);

        /// <summary>Whether a start position may be placed on this kind. An unknown kind is startable -
        /// the same permissive default the hardcoded check gave a kind it did not name.</summary>
        public bool Startable(string id) => For(id)?.Startable ?? true;

        private static TerrainKindCatalog? _standard;

        /// <summary>The addon's built-in terrain kinds and their meanings - the single source the
        /// hardcoded tables are being retired into.</summary>
        public static TerrainKindCatalog Standard => _standard ??= BuildStandard();

        private static TerrainKind Kind(string id, bool startable) => new() { Id = id, Startable = startable };

        private static TerrainKindCatalog BuildStandard()
        {
            var catalog = new TerrainKindCatalog();
            // Order = the saved TileSet tile index (TerrainTileSets.Kinds), append-only.
            catalog.Kinds.Add(Kind("deep_water", startable: true));
            catalog.Kinds.Add(Kind("shallow_water", startable: true));
            catalog.Kinds.Add(Kind("grass", startable: true));
            catalog.Kinds.Add(Kind("dry_grass", startable: true));
            catalog.Kinds.Add(Kind("desert", startable: true));
            catalog.Kinds.Add(Kind("sand", startable: true));
            catalog.Kinds.Add(Kind("tundra", startable: true));
            catalog.Kinds.Add(Kind("snow", startable: false));
            catalog.Kinds.Add(Kind("ice", startable: false));
            catalog.Kinds.Add(Kind("jungle", startable: true));
            catalog.Kinds.Add(Kind("swamp", startable: true));
            catalog.Kinds.Add(Kind("mud", startable: true));
            catalog.Kinds.Add(Kind("gravel", startable: true));
            catalog.Kinds.Add(Kind("rock", startable: false));
            catalog.Kinds.Add(Kind("lava", startable: false));
            return catalog;
        }
    }
}
