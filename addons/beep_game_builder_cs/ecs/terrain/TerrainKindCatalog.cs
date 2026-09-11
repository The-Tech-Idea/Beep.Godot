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

        /// <summary>The kind's terrain-layer level (Sea/Ground/Hills/Mountains) when relief is unknown.
        /// An unknown kind is flat Ground - the same default the hardcoded switch gave.</summary>
        public int Level(string id) => For(id)?.Level ?? TerrainLayers.Ground;

        /// <summary>The kind's generated ground class (Land/Water/Steep). An unknown kind is Land -
        /// the same permissive default the hardcoded classifier gave. Note this answers for CANONICAL
        /// ids only; the bare "water" alias is resolved by <see cref="TerrainTileSets.GroundOf"/>.</summary>
        public TerrainTileSets.Ground GroundOf(string id) => For(id)?.Class ?? TerrainTileSets.Ground.Land;

        /// <summary>Whether this kind is water. Canonical ids only (deep_water, shallow_water); the
        /// bare "water" alias is handled by <see cref="TerrainTileSets.IsWaterKind"/>. Unknown = dry.</summary>
        public bool IsWater(string id) => For(id)?.Class == TerrainTileSets.Ground.Water;

        /// <summary>Whether a start position may be placed on this kind. An unknown kind is startable -
        /// the same permissive default the hardcoded check gave a kind it did not name.</summary>
        public bool Startable(string id) => For(id)?.Startable ?? true;

        /// <summary>Whether this kind is a rainfall biome - one the coherence majority filter smooths.
        /// Unknown kinds are not rainfall biomes.</summary>
        public bool Rainfall(string id) => For(id)?.Rainfall ?? false;

        private List<string>? _rainfallKinds;
        /// <summary>The rainfall biomes in catalog order - the coherence smoother's dense vote index maps
        /// a kind to its position here and back. The ORDER does not affect the smoothed result (the vote
        /// is decided by neighbour counts and scan order, not by this index), only the compact byte
        /// encoding a pass uses internally. Built from the Rainfall flag; do not mutate.</summary>
        public IReadOnlyList<string> RainfallKinds => _rainfallKinds ??= BuildList(k => k.Rainfall);

        /// <summary>Whether a small region of this kind may be dissolved into its neighbours when too
        /// small for the landmass (biome coherence). Unknown kinds are not absorbable.</summary>
        public bool Absorbable(string id) => For(id)?.Absorbable ?? false;

        /// <summary>Whether an absorbed region may become this kind. Unknown kinds are not targets.</summary>
        public bool AbsorbTarget(string id) => For(id)?.AbsorbTarget ?? false;

        /// <summary>Whether this kind is a peak material (what high ground is made of).</summary>
        public bool PeakMaterial(string id) => For(id)?.PeakMaterial ?? false;

        /// <summary>Whether the grid blocks building/roading/spawning/scattering on this kind by default.
        /// Canonical ids only (the water/sea/ocean aliases are added by <see cref="GridTerrainRules"/>).
        /// Unknown = not blocked.</summary>
        public bool BlockedByDefault(string id) => For(id)?.BlockedByDefault ?? false;

        /// <summary>The prop palette a scatter places on this kind ("grass"/"desert"/"mud"/"rock"/"water"),
        /// or "" for no props. Canonical ids only; the scatter normalizes its own aliases and applies the
        /// water opt-in. Unknown = no props.</summary>
        public string PropPalette(string id) => For(id)?.PropPalette ?? string.Empty;

        /// <summary>The painted view's shader material slot for this kind, when the catalog names it.
        /// Returns false for an unknown kind so a caller can apply its own fallback (the painted renderer
        /// falls back to grass's slot for a cell, or to the cell's own slot for its shore inland).</summary>
        public bool TryMaterialSlot(string id, out int slot)
        {
            if (For(id) is { } kind) { slot = kind.MaterialSlot; return true; }
            slot = 0;
            return false;
        }

        /// <summary>Which feature this kind carries ("jungle"/"marsh"/"oasis"/"woods"), or "" for none.
        /// "woods" means woods-CAPABLE; the feature stage still gates it on temperature and ranking.
        /// Unknown = no feature.</summary>
        public string FeatureEligibility(string id) => For(id)?.FeatureEligibility ?? string.Empty;

        private HashSet<string>? _peakMaterialKinds;
        /// <summary>The peak-material kinds as a set - passed to the scale stage's exclude filters and
        /// iterated by the coherence absorb pass. Built from the PeakMaterial flag; do not mutate.</summary>
        public HashSet<string> PeakMaterialKinds => _peakMaterialKinds ??= BuildSet(k => k.PeakMaterial);

        private HashSet<string>? _notLakeBedKinds;
        /// <summary>The kinds a drained lake bed must not become, as a set. From NotLakeBed; do not mutate.</summary>
        public HashSet<string> NotLakeBedKinds => _notLakeBedKinds ??= BuildSet(k => k.NotLakeBed);

        private HashSet<string> BuildSet(System.Func<TerrainKind, bool> predicate)
        {
            var set = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (TerrainKind kind in Kinds)
                if (kind is not null && !string.IsNullOrEmpty(kind.Id) && predicate(kind))
                    set.Add(kind.Id);
            return set;
        }

        private List<string> BuildList(System.Func<TerrainKind, bool> predicate)
        {
            var list = new List<string>();
            foreach (TerrainKind kind in Kinds)
                if (kind is not null && !string.IsNullOrEmpty(kind.Id) && predicate(kind))
                    list.Add(kind.Id);
            return list;
        }

        private static TerrainKindCatalog? _standard;

        /// <summary>The addon's built-in terrain kinds and their meanings - the single source the
        /// hardcoded tables are being retired into.</summary>
        public static TerrainKindCatalog Standard => _standard ??= BuildStandard();

        private static TerrainKind Kind(string id, bool startable = true, bool rainfall = false,
            bool absorbable = false, bool absorbTarget = false, bool peakMaterial = false,
            bool notLakeBed = false, int level = TerrainLayers.Ground,
            TerrainTileSets.Ground @class = TerrainTileSets.Ground.Land,
            bool blockedByDefault = false, string propPalette = "", int materialSlot = 0,
            string featureEligibility = "")
            => new()
            {
                Id = id, Startable = startable, Rainfall = rainfall, Absorbable = absorbable,
                AbsorbTarget = absorbTarget, PeakMaterial = peakMaterial, NotLakeBed = notLakeBed,
                Level = level, Class = @class, BlockedByDefault = blockedByDefault,
                PropPalette = propPalette, MaterialSlot = materialSlot, FeatureEligibility = featureEligibility,
            };

        private static TerrainKindCatalog BuildStandard()
        {
            var catalog = new TerrainKindCatalog();
            // Order = the saved TileSet tile index (TerrainTileSets.Kinds), append-only.
            catalog.Kinds.Add(Kind("deep_water", level: TerrainLayers.Sea, @class: TerrainTileSets.Ground.Water, blockedByDefault: true, materialSlot: 12));
            catalog.Kinds.Add(Kind("shallow_water", level: TerrainLayers.Sea, @class: TerrainTileSets.Ground.Water, blockedByDefault: true, propPalette: "water", materialSlot: 11));
            catalog.Kinds.Add(Kind("grass", rainfall: true, absorbable: true, absorbTarget: true, propPalette: "grass", materialSlot: 0, featureEligibility: "woods"));
            catalog.Kinds.Add(Kind("dry_grass", rainfall: true, absorbable: true, absorbTarget: true, propPalette: "grass", materialSlot: 1, featureEligibility: "woods"));
            catalog.Kinds.Add(Kind("desert", rainfall: true, absorbable: true, absorbTarget: true, propPalette: "desert", materialSlot: 2, featureEligibility: "oasis"));
            catalog.Kinds.Add(Kind("sand", notLakeBed: true, propPalette: "desert", materialSlot: 3));
            catalog.Kinds.Add(Kind("tundra", absorbable: true, absorbTarget: true, propPalette: "rock", materialSlot: 4, featureEligibility: "woods"));
            catalog.Kinds.Add(Kind("snow", startable: false, absorbable: true, absorbTarget: true, peakMaterial: true, notLakeBed: true, propPalette: "rock", materialSlot: 5));
            catalog.Kinds.Add(Kind("ice", startable: false, propPalette: "rock", materialSlot: 6));
            catalog.Kinds.Add(Kind("jungle", rainfall: true, absorbable: true, absorbTarget: true, propPalette: "grass", materialSlot: 7, featureEligibility: "jungle"));
            catalog.Kinds.Add(Kind("swamp", rainfall: true, absorbable: true, absorbTarget: true, propPalette: "mud", materialSlot: 8, featureEligibility: "marsh"));
            catalog.Kinds.Add(Kind("mud", propPalette: "mud", materialSlot: 8));
            catalog.Kinds.Add(Kind("gravel", absorbTarget: true, peakMaterial: true, notLakeBed: true, level: TerrainLayers.Hills, propPalette: "rock", materialSlot: 9));
            catalog.Kinds.Add(Kind("rock", startable: false, absorbTarget: true, peakMaterial: true, notLakeBed: true, level: TerrainLayers.Mountains, @class: TerrainTileSets.Ground.Steep, propPalette: "rock", materialSlot: 10));
            catalog.Kinds.Add(Kind("lava", startable: false, @class: TerrainTileSets.Ground.Steep, blockedByDefault: true, materialSlot: 13));
            return catalog;
        }
    }
}
