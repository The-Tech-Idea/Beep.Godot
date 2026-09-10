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

        /// <summary>Whether a small region of this kind may be dissolved into its neighbours when too
        /// small for the landmass (biome coherence). Unknown kinds are not absorbable.</summary>
        public bool Absorbable(string id) => For(id)?.Absorbable ?? false;

        /// <summary>Whether an absorbed region may become this kind. Unknown kinds are not targets.</summary>
        public bool AbsorbTarget(string id) => For(id)?.AbsorbTarget ?? false;

        private static TerrainKindCatalog? _standard;

        /// <summary>The addon's built-in terrain kinds and their meanings - the single source the
        /// hardcoded tables are being retired into.</summary>
        public static TerrainKindCatalog Standard => _standard ??= BuildStandard();

        private static TerrainKind Kind(string id, bool startable = true, bool absorbable = false, bool absorbTarget = false)
            => new() { Id = id, Startable = startable, Absorbable = absorbable, AbsorbTarget = absorbTarget };

        private static TerrainKindCatalog BuildStandard()
        {
            var catalog = new TerrainKindCatalog();
            // Order = the saved TileSet tile index (TerrainTileSets.Kinds), append-only.
            catalog.Kinds.Add(Kind("deep_water"));
            catalog.Kinds.Add(Kind("shallow_water"));
            catalog.Kinds.Add(Kind("grass", absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("dry_grass", absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("desert", absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("sand"));
            catalog.Kinds.Add(Kind("tundra", absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("snow", startable: false, absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("ice", startable: false));
            catalog.Kinds.Add(Kind("jungle", absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("swamp", absorbable: true, absorbTarget: true));
            catalog.Kinds.Add(Kind("mud"));
            catalog.Kinds.Add(Kind("gravel", absorbTarget: true));
            catalog.Kinds.Add(Kind("rock", startable: false, absorbTarget: true));
            catalog.Kinds.Add(Kind("lava", startable: false));
            return catalog;
        }
    }
}
