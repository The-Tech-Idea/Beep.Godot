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
        }

        private static readonly (string Name, Variant.Type Type)[] Layers =
        {
            (Cell.Terrain, Variant.Type.String),
            (Cell.Resource, Variant.Type.String),
            (Cell.Feature, Variant.Type.String),
            (Cell.Relief, Variant.Type.Int),
            (Cell.IsWater, Variant.Type.Bool),
            (Cell.Passable, Variant.Type.Bool),
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
            data.SetCustomData(Cell.Passable, !water && kind != "rock");
        }

        public static bool IsWaterKind(string terrainKind)
            => terrainKind is "deep_water" or "shallow_water" or "water";

        /// <summary>Physics and navigation layer indices, by what the ground IS.</summary>
        public enum Ground
        {
            /// <summary>Open land. Walkable.</summary>
            Land = 0,

            /// <summary>Sea, lake or river.</summary>
            Water = 1,

            /// <summary>Rock and cliffs - land, but not level enough to cross.</summary>
            Steep = 2,
        }

        /// <summary>
        /// Adds a physics and a navigation layer for EACH kind of ground, rather
        /// than one "solid" layer.
        ///
        /// WHOSE DECISION IT IS. Whether water stops a character is a question
        /// about the game, not about the map: one project wants a unit to swim,
        /// the next wants the shore to be a wall, and a third wants boats that do
        /// the opposite of both. A terrain engine that decides has taken that
        /// choice away, and taken it away invisibly - the map simply behaves, and
        /// the developer has to undo generated collision to disagree with it.
        ///
        /// So the map states what the ground IS and Godot's own layer masks say
        /// what that means. Water, steep ground and open land each collide on
        /// their own physics layer and navigate on their own navigation layer:
        ///
        ///   a walker    masks water + steep,   navigates Land
        ///   a swimmer   masks steep,           navigates Water
        ///   a boat      masks land + steep,    navigates Water
        ///   an airship  masks nothing,         navigates any
        ///
        /// None of which this addon needs to know about. It is the mechanism
        /// Godot already has for exactly this question.
        /// </summary>
        public static void DefineBody(TileSet tileSet, uint[]? collisionLayers = null)
        {
            // One physics and one navigation layer per Ground value, in order, so
            // the enum IS the index and no mapping table can drift from it.
            var grounds = System.Enum.GetValues<Ground>();

            for (int i = tileSet.GetPhysicsLayersCount(); i < grounds.Length; i++)
            {
                tileSet.AddPhysicsLayer();

                // Bit 1 is Godot's default for everything, so each ground gets a
                // distinct bit above it: a mask of 0 would collide with nothing
                // and a shared bit would make them indistinguishable.
                uint bit = collisionLayers is not null && i < collisionLayers.Length
                    ? collisionLayers[i]
                    : 1u << (i + 1);

                tileSet.SetPhysicsLayerCollisionLayer(i, bit);

                // The tiles are the world: they are collided WITH, and collide
                // with nothing themselves.
                tileSet.SetPhysicsLayerCollisionMask(i, 0);
            }

            for (int i = tileSet.GetNavigationLayersCount(); i < grounds.Length; i++)
                tileSet.AddNavigationLayer();
        }

        /// <summary>Which ground a terrain kind is, and so which layers it uses.</summary>
        public static Ground GroundOf(string terrainKind)
            => IsWaterKind(terrainKind) ? Ground.Water
                : terrainKind == "rock" ? Ground.Steep
                : Ground.Land;

        /// <summary>
        /// Gives a tile its body: a collision polygon AND a navigation polygon,
        /// both on the layer for the ground it is.
        ///
        /// Both, not one or the other. An earlier version gave water collision
        /// and no navigation, which silently decided that nothing could ever
        /// cross it - a swimming unit had nothing to path over even if its mask
        /// let it through. Every cell is navigable on its own ground's layer and
        /// solid on its own ground's layer; which of those a given agent honours
        /// is chosen by that agent's mask and navigation layers.
        /// </summary>
        public static void ShapeCell(TileData? data, string terrainKind, Vector2I cellSize)
        {
            if (data is null)
                return;

            // Tile polygons are measured from the tile's CENTRE, not its corner.
            var half = new Vector2(Mathf.Max(1, cellSize.X) * 0.5f, Mathf.Max(1, cellSize.Y) * 0.5f);
            Vector2[] square =
            {
                new(-half.X, -half.Y),
                new(half.X, -half.Y),
                new(half.X, half.Y),
                new(-half.X, half.Y),
            };

            int layer = (int)GroundOf(terrainKind);

            data.AddCollisionPolygon(layer);
            data.SetCollisionPolygonPoints(layer, 0, square);

            var navigation = new NavigationPolygon();
            navigation.SetVertices(square);
            navigation.AddPolygon(new[] { 0, 1, 2, 3 });
            data.SetNavigationPolygon(layer, navigation);
        }

        /// <summary>
        /// Every terrain kind the generator can produce, so a data layer can hold
        /// one tile for each. Ordered, so a kind's tile index is stable between
        /// runs and a saved TileSet stays valid.
        /// </summary>
        public static IReadOnlyList<string> Kinds { get; } = new[]
        {
            "deep_water", "shallow_water", "grass", "dry_grass", "desert", "sand",
            "tundra", "snow", "ice", "jungle", "swamp", "mud", "gravel", "rock",
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
