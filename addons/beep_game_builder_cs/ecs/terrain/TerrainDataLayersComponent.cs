using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// What each cell of the generated map IS, as real tile data a game can ask
    /// about: terrain, resource, feature, relief, water and passability.
    ///
    /// WHY A LAYER OF ITS OWN rather than reading the layers that draw the map.
    /// The tile view spreads its ground across fourteen biome layers, so finding
    /// a cell means trying each until one has a tile; the isometric view stacks
    /// its own; and the painted view has no terrain tiles at all - it is one
    /// shaded surface. A game written against the drawing layers would therefore
    /// have to know which VIEW is on screen, and would stop working when the
    /// player switched. These layers are the same whichever view draws.
    ///
    /// They are invisible and contribute nothing to the picture. That is the
    /// point: they are the map's data, kept in the form Godot already has for
    /// per-tile data, so a developer uses get_cell_tile_data and the TileSet
    /// editor rather than an API peculiar to this addon.
    ///
    /// SIX layers, not one, because the facts vary independently: a cell's
    /// terrain, the resource on it, the feature standing on it, its relief
    /// band, the continent it belongs to and whether it is a recommended start
    /// are separate choices, and one tile can only carry one set of values.
    /// Split this way each layer holds one tile per distinct value; combined,
    /// it would need a tile per combination.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainDataLayersComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);

        /// <summary>
        /// Must match the cell size of the view being queried, so a cell here is
        /// the same cell there.
        /// </summary>
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

        [Export] public bool RefreshOnReady { get; set; } = true;

        [ExportGroup("Body")]
        /// <summary>
        /// Give every cell a collision shape and a navigation polygon on the
        /// layer for the GROUND it is - land, water or steep - so a game can
        /// decide what that means with ordinary collision masks.
        ///
        /// Nothing here decides whether water stops anyone. Off entirely for a
        /// map that is only ever looked at, since both cost memory per tile.
        /// </summary>
        [Export] public bool GenerateCollision { get; set; } = true;
        [Export] public bool GenerateNavigation { get; set; } = true;

        /// <summary>Collision bit for open land.</summary>
        [Export(PropertyHint.Layers2DPhysics)] public uint LandCollisionLayer { get; set; } = 2;

        /// <summary>Collision bit for sea, lake and river.</summary>
        [Export(PropertyHint.Layers2DPhysics)] public uint WaterCollisionLayer { get; set; } = 4;

        /// <summary>Collision bit for rock and cliffs.</summary>
        [Export(PropertyHint.Layers2DPhysics)] public uint SteepCollisionLayer { get; set; } = 8;

        private TerrainGeneratorComponent? _generator;
        private TileMapLayer? _terrain;
        private TileMapLayer? _resources;
        private TileMapLayer? _features;
        private TileMapLayer? _relief;
        private TileMapLayer? _continents;
        private TileMapLayer? _starts;

        /// <summary>Value to tile column, per layer, so a fill is a lookup.</summary>
        private readonly Dictionary<string, int> _terrainTiles = new();
        private readonly Dictionary<string, int> _resourceTiles = new();
        private readonly Dictionary<string, int> _featureTiles = new();
        private readonly Dictionary<string, int> _reliefTiles = new();
        private readonly Dictionary<string, int> _continentTiles = new();
        private readonly Dictionary<string, int> _startTiles = new();

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>Rewrites every cell's data from the generator.</summary>
        public void Rebuild()
        {
            _generator ??= TerrainGeneratorPath.IsEmpty
                ? null
                : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
            if (_generator is null)
            {
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; no cell data was written.");
                return;
            }
            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            // Resolved ONCE per rebuild rather than once per cell: this method
            // scans the whole map twice, and every public per-cell accessor on
            // the generator otherwise rebuilds and compares its ~30-field
            // settings record before returning this same cached field.
            GeneratedTerrainField field = _generator.ResolveField();

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            Vector2I cell = new(Mathf.Max(1, TileSize), Mathf.Max(1, TileSize));

            // The values actually present on THIS map, so a tile is only made for
            // something that exists rather than for every id the catalogue knows.
            var resources = new SortedSet<string>();
            var features = new SortedSet<string>();
            var reliefs = new SortedSet<string>();
            // Numeric so 2 sorts before 10; painted as strings like the rest.
            var continentIds = new SortedSet<int>();
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var at = new Vector2I(x, y);
                    string resource = field.ResourceAtCell(at);
                    if (resource.Length > 0)
                        resources.Add(resource);

                    string feature = field.FeatureAtCell(at);
                    if (feature.Length > 0)
                        features.Add(feature);

                    reliefs.Add(((int)field.ReliefAtCell(at)).ToString());

                    // 0 is water/no continent; those cells simply get no tile,
                    // so ContinentAt reads 0 there without a tile existing.
                    int continent = field.ContinentAtCell(at);
                    if (continent > 0)
                        continentIds.Add(continent);
                }
            }
            var continents = new List<string>();
            foreach (int id in continentIds)
                continents.Add(id.ToString());

            // The kinds this map HAS, from the engine - not every kind the
            // catalogue knows. A tile per absent biome is a tile nothing ever
            // points at.
            _terrain = EnsureLayer("TerrainData", _terrain, cell, new List<string>(_generator.TerrainKindsPresent()), _terrainTiles,
                (data, value) =>
                {
                    TerrainTileSets.Describe(data, value);
                    if (GenerateCollision || GenerateNavigation)
                        TerrainTileSets.ShapeCell(data, value, cell);
                },
                body: GenerateCollision || GenerateNavigation);
            _resources = EnsureLayer("ResourceData", _resources, cell, new List<string>(resources), _resourceTiles,
                (data, value) => TerrainTileSets.Describe(data, string.Empty, resource: value));
            _features = EnsureLayer("FeatureData", _features, cell, new List<string>(features), _featureTiles,
                (data, value) => TerrainTileSets.Describe(data, string.Empty, feature: value));
            _relief = EnsureLayer("ReliefData", _relief, cell, new List<string>(reliefs), _reliefTiles,
                (data, value) => TerrainTileSets.DescribeRelief(data, int.Parse(value)));
            _continents = EnsureLayer("ContinentData", _continents, cell, continents, _continentTiles,
                (data, value) => TerrainTileSets.DescribeContinent(data, int.Parse(value)));
            _starts = EnsureLayer("StartData", _starts, cell, new List<string> { "start" }, _startTiles,
                (data, _) => TerrainTileSets.DescribeStart(data));

            _terrain.Clear();
            _resources.Clear();
            _features.Clear();
            _relief.Clear();
            _continents.Clear();
            _starts.Clear();

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var at = new Vector2I(x, y);
                    Paint(_terrain, _terrainTiles, at, field.TerrainAtCell(at));
                    Paint(_resources, _resourceTiles, at, field.ResourceAtCell(at));
                    Paint(_features, _featureTiles, at, field.FeatureAtCell(at));
                    Paint(_relief, _reliefTiles, at, ((int)field.ReliefAtCell(at)).ToString());

                    int continent = field.ContinentAtCell(at);
                    if (continent > 0)
                        Paint(_continents, _continentTiles, at, continent.ToString());
                }
            }

            foreach (Vector2I start in field.StartPositions)
            {
                if (start.X >= 0 && start.Y >= 0 && start.X < size.X && start.Y < size.Y)
                    Paint(_starts, _startTiles, start, "start");
            }
        }

        private static void Paint(
            TileMapLayer layer, Dictionary<string, int> tiles, Vector2I cell, string value)
        {
            if (value.Length > 0 && tiles.TryGetValue(value, out int column))
                layer.SetCell(cell, 0, new Vector2I(column, 0));
        }

        /// <summary>
        /// One tile per distinct value, in a strip. The atlas is transparent -
        /// these layers never draw - so the texture exists only because a
        /// TileSetAtlasSource must have one.
        /// </summary>
        private TileMapLayer EnsureLayer(
            string name,
            TileMapLayer? existing,
            Vector2I cell,
            IReadOnlyList<string> values,
            Dictionary<string, int> tiles,
            System.Action<TileData?, string> describe,
            bool body = false)
        {
            TileMapLayer layer = existing is not null && GodotObject.IsInstanceValid(existing)
                ? existing
                : TerrainAuthoring.EnsureLayer(this, name);

            // Data, not decoration - but collision and navigation are served
            // from a hidden layer just as well as a visible one.
            layer.Visible = false;
            layer.CollisionEnabled = GenerateCollision;
            layer.NavigationEnabled = GenerateNavigation;

            tiles.Clear();
            int count = Mathf.Max(1, values.Count);
            var image = Image.CreateEmpty(cell.X * count, cell.Y, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);

            var source = new TileSetAtlasSource
            {
                Texture = ImageTexture.CreateFromImage(image),
                TextureRegionSize = cell,
            };

            TileSet tileSet = TerrainTileSets.Create(cell);
            if (body)
            {
                TerrainTileSets.DefineBody(
                    tileSet,
                    new[] { LandCollisionLayer, WaterCollisionLayer, SteepCollisionLayer });
            }
            for (int i = 0; i < values.Count; i++)
            {
                var coords = new Vector2I(i, 0);
                source.CreateTile(coords);
                tiles[values[i]] = i;
            }

            tileSet.AddSource(source, 0);
            layer.TileSet = tileSet;

            // Describing has to happen AFTER the source is on the TileSet: a
            // tile's data is owned by the TileSet, and an atlas that is not yet
            // attached to one has no data to write to.
            for (int i = 0; i < values.Count; i++)
                describe(source.GetTileData(new Vector2I(i, 0), 0), values[i]);

            return layer;
        }

        // ---- reading a cell -------------------------------------------------
        //
        // Convenience over get_cell_tile_data(cell).get_custom_data(name), which
        // is what these do and what a developer can equally call themselves - the
        // layers are public below for exactly that. They ANSWER questions; none
        // of them acts on the answer.
        //
        //     var data := $CellData/TerrainData.get_cell_tile_data(cell)
        //     if data: print(data.get_custom_data("terrain"))

        /// <summary>Terrain kind at a cell, or empty when outside the map.</summary>
        public string TerrainAt(Vector2I cell) => Read(_terrain, cell, TerrainTileSets.Cell.Terrain).AsString();

        /// <summary>Resource id on a cell, or empty where there is none.</summary>
        public string ResourceAt(Vector2I cell) => Read(_resources, cell, TerrainTileSets.Cell.Resource).AsString();

        /// <summary>Feature on a cell - "woods", "marsh" - or empty.</summary>
        public string FeatureAt(Vector2I cell) => Read(_features, cell, TerrainTileSets.Cell.Feature).AsString();

        /// <summary>
        /// Relief band at a cell - Flat, Hills or Mountains, as TerrainRelief
        /// numbers it. Its own layer, sourced straight from the generator's
        /// ReliefAt, rather than the terrain layer's Cell.Relief - which is a
        /// different unit, TerrainLayers' drawing z-order derived from kind.
        /// </summary>
        public int ReliefAt(Vector2I cell) => Read(_relief, cell, TerrainTileSets.Cell.Relief).AsInt32();

        /// <summary>Whether the cell is sea, lake or river. A fact about the map.</summary>
        public bool IsWaterAt(Vector2I cell) => Read(_terrain, cell, TerrainTileSets.Cell.IsWater).AsBool();

        /// <summary>
        /// The CONVENTIONAL default for whether a cell can be entered on foot -
        /// not water, not rock.
        ///
        /// Read it or ignore it. Nothing in the addon acts on it: collision and
        /// navigation are generated per GROUND KIND on their own layers, so
        /// whether a swimmer crosses water, or a climber crosses rock, is decided
        /// by that agent's collision mask and navigation layers and never here. A
        /// game whose rules differ from this default is not fighting anything -
        /// it simply reads terrain or is_water instead.
        /// </summary>
        public bool PassableAt(Vector2I cell) => Read(_terrain, cell, TerrainTileSets.Cell.Passable).AsBool();

        /// <summary>
        /// Landmass index the cell belongs to. 0 for water, off-map, and
        /// unbuilt layers alike - land continents count from 1, so 0 always
        /// means "no continent" rather than a real id.
        /// </summary>
        public int ContinentAt(Vector2I cell) => Read(_continents, cell, TerrainTileSets.Cell.Continent).AsInt32();

        /// <summary>Whether the generator recommended this cell as a player start.</summary>
        public bool IsStartPositionAt(Vector2I cell) => Read(_starts, cell, TerrainTileSets.Cell.StartPosition).AsBool();

        /// <summary>
        /// Every recommended start cell, read off the painted start layer so it
        /// answers from the same data a get_used_cells caller would see.
        /// </summary>
        public Godot.Collections.Array<Vector2I> StartCells()
            => _starts is not null && GodotObject.IsInstanceValid(_starts)
                ? _starts.GetUsedCells()
                : new Godot.Collections.Array<Vector2I>();

        private static Variant Read(TileMapLayer? layer, Vector2I cell, string field)
        {
            TileData? data = layer?.GetCellTileData(cell);
            return data is null ? default : data.GetCustomData(field);
        }

        /// <summary>Terrain kind, water; also the collision and navigation body.</summary>
        public TileMapLayer? TerrainLayer => _terrain;

        /// <summary>Resource ids, on the cells that have one.</summary>
        public TileMapLayer? ResourceLayer => _resources;

        /// <summary>Features - woods, marsh - on the cells that have one.</summary>
        public TileMapLayer? FeatureLayer => _features;

        /// <summary>Relief band - Flat, Hills, Mountains - per TerrainRelief.</summary>
        public TileMapLayer? ReliefLayer => _relief;

        /// <summary>Continent ids, on land cells only.</summary>
        public TileMapLayer? ContinentLayer => _continents;

        /// <summary>Recommended start cells, and nothing else.</summary>
        public TileMapLayer? StartLayer => _starts;
    }
}
