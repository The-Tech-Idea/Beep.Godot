using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Builds the TileSets the terrain draws with, and the per-cell facts they
    /// carry.
    ///
    /// Every terrain TileSet used to be assembled inline wherever it happened to
    /// be needed - five separate calls to new TileSet - which was survivable
    /// while a TileSet only had to hold tiles. It stops being survivable the
    /// moment they carry DATA: a custom data layer defined in five places is
    /// five chances for one of them to spell a name differently, and the failure
    /// is silent, because reading an undefined layer just returns nothing.
    ///
    /// WHAT A CELL CAN AND CANNOT TELL YOU. Custom data belongs to the TILE, not
    /// to the cell: every cell drawing the same tile answers the same way. That
    /// is exactly right for what a cell IS - its terrain, whether it is water,
    /// whether it can be walked - and it cannot express per-cell quantities like
    /// how much ore is left in a deposit. Those stay in GridCellDataComponent,
    /// which is keyed per cell and is the right owner for anything that changes
    /// during play.
    /// </summary>
    public static class TerrainTileSets
    {
        /// <summary>
        /// The custom data layers every terrain TileSet defines. Names are the
        /// contract with a game reading the map, so they live here rather than
        /// being written as string literals at each call site.
        /// </summary>
        public static class Cell
        {
            /// <summary>Terrain kind, as the generator names it: "grass", "rock".</summary>
            public const string Terrain = "terrain";

            /// <summary>Resource id on the cell, or empty.</summary>
            public const string Resource = "resource";

            /// <summary>Feature standing on the cell - "woods", "marsh" - or empty.</summary>
            public const string Feature = "feature";

            /// <summary>Elevation band, matching TerrainLayers: ground, hills, mountains.</summary>
            public const string Relief = "relief";

            /// <summary>Sea, lake or river.</summary>
            public const string IsWater = "is_water";

            /// <summary>Whether a unit can ordinarily enter. Water and rock cannot.</summary>
            public const string Passable = "passable";

            /// <summary>Landmass index the cell belongs to. 0 is water/no continent; land counts from 1.</summary>
            public const string Continent = "continent";

            /// <summary>True on a generator-recommended player start cell.</summary>
            public const string StartPosition = "start_position";

            /// <summary>Liquid-stratum resource id in the water column, or empty.</summary>
            public const string LiquidResource = "liquid_resource";

            /// <summary>Underground-stratum resource id beneath the cell, or empty.</summary>
            public const string UndergroundResource = "underground_resource";

            /// <summary>Underground richness 0..1 (banded), 0 where no deposit.</summary>
            public const string UndergroundRichness = "underground_richness";

            /// <summary>Underground depth band, as (int)ResourceDepth.</summary>
            public const string UndergroundDepth = "underground_depth";
        }

        private static readonly (string Name, Variant.Type Type)[] Layers =
        {
            (Cell.Terrain, Variant.Type.String),
            (Cell.Resource, Variant.Type.String),
            (Cell.Feature, Variant.Type.String),
            (Cell.Relief, Variant.Type.Int),
            (Cell.IsWater, Variant.Type.Bool),
            (Cell.Passable, Variant.Type.Bool),
            (Cell.Continent, Variant.Type.Int),
            (Cell.StartPosition, Variant.Type.Bool),
            (Cell.LiquidResource, Variant.Type.String),
            (Cell.UndergroundResource, Variant.Type.String),
            (Cell.UndergroundRichness, Variant.Type.Float),
            (Cell.UndergroundDepth, Variant.Type.Int),
        };

        /// <summary>
        /// A TileSet with the cell data layers already defined.
        ///
        /// Idempotent: a TileSet that already has a layer of a given name keeps
        /// the one it has, so this can be applied to an AUTHORED TileSet a
        /// developer made in the editor without duplicating their layers or
        /// disturbing the indices their tiles already reference.
        /// </summary>
        public static TileSet Create(Vector2I tileSize, bool isometric = false)
        {
            var tileSet = new TileSet { TileSize = tileSize };
            if (isometric)
            {
                tileSet.TileShape = TileSet.TileShapeEnum.Isometric;
                tileSet.TileLayout = TileSet.TileLayoutEnum.DiamondDown;
                tileSet.TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal;
            }

            DefineCellData(tileSet);
            return tileSet;
        }

        /// <summary>Adds any cell data layer this TileSet does not already have.</summary>
        public static void DefineCellData(TileSet tileSet)
        {
            foreach ((string name, Variant.Type type) in Layers)
            {
                if (tileSet.GetCustomDataLayerByName(name) >= 0)
                    continue;

                int index = tileSet.GetCustomDataLayersCount();
                tileSet.AddCustomDataLayer();
                tileSet.SetCustomDataLayerName(index, name);
                tileSet.SetCustomDataLayerType(index, type);
            }
        }

        /// <summary>
        /// Writes what a tile represents, so a game can ask a cell about itself.
        ///
        /// Relief, water and passability are DERIVED from the terrain kind rather
        /// than passed in, because they are not independent facts - rock is a
        /// mountain and is impassable whoever is describing it - and a caller
        /// free to disagree with TerrainLayers is a caller that eventually will.
        /// </summary>
        public static void Describe(
            TileData? data, string terrainKind, string resource = "", string feature = "")
        {
            if (data is null)
                return;

            string kind = terrainKind ?? string.Empty;
            bool water = IsWaterKind(kind);

            data.SetCustomData(Cell.Terrain, kind);
            data.SetCustomData(Cell.Resource, resource ?? string.Empty);
            data.SetCustomData(Cell.Feature, feature ?? string.Empty);
            data.SetCustomData(Cell.Relief, TerrainLayers.LevelForKind(kind));
            data.SetCustomData(Cell.IsWater, water);
            // This is generated ground classification, not the live agent's movement policy.
            data.SetCustomData(Cell.Passable, GroundOf(kind) == Ground.Land);
        }

        /// <summary>
        /// Writes the real relief band onto a tile that stands for one - the
        /// dedicated relief layer's tiles, one per TerrainRelief value.
        ///
        /// Kept separate from Describe, which still derives Cell.Relief from the
        /// terrain KIND via TerrainLayers.LevelForKind - a different unit
        /// (drawing z-order, not TerrainRelief's Flat/Hills/Mountains band) that
        /// existing callers already depend on. A cell's real relief band is read
        /// off THIS layer's tiles, sourced straight from the generator.
        /// </summary>
        public static void DescribeRelief(TileData? data, int relief)
        {
            if (data is null)
                return;

            data.SetCustomData(Cell.Relief, relief);
        }

        /// <summary>
        /// Writes the continent id onto a tile that stands for one landmass -
        /// the dedicated continent layer's tiles, one per id the map has.
        /// </summary>
        public static void DescribeContinent(TileData? data, int continent)
        {
            if (data is null)
                return;

            data.SetCustomData(Cell.Continent, continent);
        }

        /// <summary>Marks a tile as standing on a recommended start cell.</summary>
        public static void DescribeStart(TileData? data)
        {
            if (data is null)
                return;

            data.SetCustomData(Cell.StartPosition, true);
        }

        /// <summary>Writes a liquid-stratum resource id onto its tile.</summary>
        public static void DescribeLiquid(TileData? data, string id)
        {
            if (data is null)
                return;

            data.SetCustomData(Cell.LiquidResource, id ?? string.Empty);
        }

        /// <summary>
        /// Writes an underground deposit onto its tile: id, banded richness
        /// and the resource's authored depth band, so a published map answers
        /// without a catalogue lookup.
        /// </summary>
        public static void DescribeUnderground(TileData? data, string id, float richness, int depth)
        {
            if (data is null)
                return;

            data.SetCustomData(Cell.UndergroundResource, id ?? string.Empty);
            data.SetCustomData(Cell.UndergroundRichness, Mathf.Clamp(richness, 0.0f, 1.0f));
            data.SetCustomData(Cell.UndergroundDepth, depth);
        }

        public static bool IsWaterKind(string terrainKind)
            => terrainKind is "deep_water" or "shallow_water" or "water";

        /// <summary>
        /// Dry ground: a real terrain kind that is not water. The empty string is
        /// not land - it means no terrain was assigned - and writing that test by
        /// hand is how it came to be spelled twelve different ways across the
        /// engine, each free to disagree the next time a water kind is added.
        /// </summary>
        public static bool IsLandKind(string terrainKind)
            => terrainKind.Length > 0 && !IsWaterKind(terrainKind);

        /// <summary>Generated ground classification, independent of an agent's movement policy.</summary>
        public enum Ground
        {
            /// <summary>Open land. Walkable.</summary>
            Land = 0,

            /// <summary>Sea, lake or river.</summary>
            Water = 1,

            /// <summary>Rock and cliffs - land, but not level enough to cross.</summary>
            Steep = 2,
        }

        /// <summary>Classifies generated ground for descriptive tile metadata.</summary>
        public static Ground GroundOf(string terrainKind)
            => IsWaterKind(terrainKind) ? Ground.Water
                : terrainKind is "rock" or "lava" ? Ground.Steep
                : Ground.Land;

        /// <summary>
        /// Every terrain kind the generator can produce, so a data layer can hold
        /// one tile for each. Ordered, so a kind's tile index is stable between
        /// runs and a saved TileSet stays valid.
        /// </summary>
        public static IReadOnlyList<string> Kinds { get; } = new[]
        {
            "deep_water", "shallow_water", "grass", "dry_grass", "desert", "sand",
            "tundra", "snow", "ice", "jungle", "swamp", "mud", "gravel", "rock",
            // Appended, never inserted: a kind's position is its tile index in a
            // saved TileSet. The Lava preset produced this kind while nothing
            // listed it, so a lava map fell back to grass in the painted view
            // and was skipped entirely by the block view.
            "lava",
        };

        /// <summary>
        /// Saves a TileSet as a real asset.
        ///
        /// A TileSet built in code has no resource path, so the editor embeds a
        /// COPY of it into every scene that uses one - including any generated
        /// texture, inline. Saved once, scenes reference it, it opens in Godot's
        /// TileSet editor, and a developer can extend it with collision shapes or
        /// their own custom data without that work being overwritten next time a
        /// map is generated.
        /// </summary>
        public static Error Save(TileSet tileSet, string resPath)
        {
            if (string.IsNullOrWhiteSpace(resPath))
                return Error.InvalidParameter;

            string directory = resPath.GetBaseDir();
            if (!string.IsNullOrEmpty(directory) && !DirAccess.DirExistsAbsolute(directory))
            {
                Error made = DirAccess.MakeDirRecursiveAbsolute(directory);
                if (made != Error.Ok)
                    return made;
            }

            Error saved = ResourceSaver.Save(tileSet, resPath);
            if (saved != Error.Ok)
                GD.PushWarning($"Could not save TileSet '{resPath}': {saved}.");

            return saved;
        }
    }
}
