using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// What each cell of the generated map IS, as recipe metadata a game can ask
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
    /// Runtime queries read the published field directly, without duplicating
    /// its arrays or allocating invisible tiles. MaterializeTileLayers enables
    /// an explicit native TileData view for authoring/export and direct TileSet
    /// inspection. Those optional layers are invisible and physically inert.
    ///
    /// NINE layers, not one, because the facts vary independently: a cell's
    /// terrain, the resource on it, what swims in its water, what lies under
    /// it, the feature standing on it, its relief band, the continent it
    /// belongs to, whether it is a recommended start and which start's area
    /// it is reserved for are separate choices,
    /// and one tile can only carry one set of values. Split this way each
    /// layer holds one tile per distinct value; combined, it would need a
    /// tile per combination.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainDataLayersComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);

        /// <summary>
        /// Size of metadata atlas tiles. Logical cell queries do not depend on the view's projection.
        /// </summary>
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

        [Export] public bool RefreshOnReady { get; set; } = true;
        /// <summary>Explicit native TileData authoring/export view. Runtime queries need no tiles.</summary>
        [Export] public bool MaterializeTileLayers { get; set; }

        private GeneratedTerrainField? _field;
        private Vector2I _publishedOrigin;
        private Vector2I _publishedSize;
        private readonly HashSet<Vector2I> _publishedStarts = new();
        // The same starts in the generator's order, so StartCells()[k] is start k. The set above
        // answers IsStartPositionAt; a set alone lost the order, and the index is what a start
        // area, a faction assignment and the camera's framing all key on.
        private readonly List<Vector2I> _publishedStartOrder = new();
        private bool _materialized;

        private TerrainGeneratorComponent? _generator;
        /// <summary>Stable identity of the published underground map, including absolute bounds.</summary>
        public string UndergroundIdentity { get; private set; } = "";
        private TileMapLayer? _terrain;
        private TileMapLayer? _resources;
        private TileMapLayer? _features;
        private TileMapLayer? _relief;
        private TileMapLayer? _continents;
        private TileMapLayer? _starts;
        private TileMapLayer? _liquid;
        private TileMapLayer? _underground;
        private TileMapLayer? _startAreas;

        /// <summary>Value to tile column, per layer, so a fill is a lookup.</summary>
        private readonly Dictionary<string, int> _terrainTiles = new();
        private readonly Dictionary<string, int> _resourceTiles = new();
        private readonly Dictionary<string, int> _featureTiles = new();
        private readonly Dictionary<string, int> _reliefTiles = new();
        private readonly Dictionary<string, int> _continentTiles = new();
        private readonly Dictionary<string, int> _startTiles = new();
        private readonly Dictionary<string, int> _liquidTiles = new();
        private readonly Dictionary<string, int> _undergroundTiles = new();
        private readonly Dictionary<string, int> _startAreaTiles = new();

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
            _generator = TerrainGeneratorPath.IsEmpty
                ? null
                : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
            if (_generator is null)
            {
                _field = null;
                _publishedStarts.Clear();
                _publishedStartOrder.Clear();
                ClearLayers();
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; no cell data was written.");
                return;
            }
            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            // Resolve once. Runtime metadata binds directly; optional native
            // materialization scans this field without repeated settings checks.
            GeneratedTerrainField field = _generator.ResolveField();

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            _field = field;
            _publishedOrigin = BoundsOrigin;
            _publishedSize = size;
            _publishedStarts.Clear();
            _publishedStartOrder.Clear();
            foreach (Vector2I start in field.StartPositions)
                if (start.X >= 0 && start.Y >= 0 && start.X < size.X && start.Y < size.Y)
                {
                    _publishedStarts.Add(BoundsOrigin + start);
                    _publishedStartOrder.Add(BoundsOrigin + start);
                }
            _materialized = MaterializeTileLayers;
            if (!_materialized)
            {
                ReleaseLayers();
                UpdateUndergroundIdentity(field, size);
                return;
            }
            Vector2I cell = new(Mathf.Max(1, TileSize), Mathf.Max(1, TileSize));

            // The values actually present on THIS map, so a tile is only made for
            // something that exists rather than for every id the catalogue knows.
            var resources = new SortedSet<string>();
            var features = new SortedSet<string>();
            var reliefs = new SortedSet<string>();
            var liquids = new SortedSet<string>();
            var undergrounds = new SortedSet<string>();
            // Numeric so 2 sorts before 10; painted as strings like the rest.
            var continentIds = new SortedSet<int>();
            var startAreaIds = new SortedSet<int>();
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

                    int startArea = field.StartAreaAtCell(at);
                    if (startArea > 0)
                        startAreaIds.Add(startArea);

                    string liquid = field.LiquidResourceAtCell(at);
                    if (liquid.Length > 0)
                        liquids.Add(liquid);

                    // Underground tiles carry (id, richness band, depth) as
                    // one value, because a tile holds one set of custom data:
                    // richness is banded so a continuous field does not need
                    // a tile per distinct float.
                    string undergroundId = field.UndergroundResourceAtCell(at);
                    if (undergroundId.Length > 0)
                        undergrounds.Add(UndergroundKey(
                            undergroundId,
                            RichnessBand(field.UndergroundRichnessAtCell(at)),
                            field.UndergroundDepthAtCell(at)));
                }
            }
            var continents = new List<string>();
            foreach (int id in continentIds)
                continents.Add(id.ToString());
            var startAreas = new List<string>();
            foreach (int id in startAreaIds)
                startAreas.Add(id.ToString());
            var startIndices = new List<string>();
            for (int index = 0; index < _publishedStartOrder.Count; index++)
                startIndices.Add(index.ToString());

            // The kinds this map HAS, from the engine - not every kind the
            // catalogue knows. A tile per absent biome is a tile nothing ever
            // points at.
            _terrain = EnsureLayer("TerrainData", _terrain, cell, new List<string>(_generator.TerrainKindsPresent()), _terrainTiles,
                (data, value) => TerrainTileSets.Describe(data, value));
            _resources = EnsureLayer("ResourceData", _resources, cell, new List<string>(resources), _resourceTiles,
                (data, value) => TerrainTileSets.Describe(data, string.Empty, resource: value));
            _features = EnsureLayer("FeatureData", _features, cell, new List<string>(features), _featureTiles,
                (data, value) => TerrainTileSets.Describe(data, string.Empty, feature: value));
            _relief = EnsureLayer("ReliefData", _relief, cell, new List<string>(reliefs), _reliefTiles,
                (data, value) => TerrainTileSets.DescribeRelief(data, int.Parse(value)));
            _continents = EnsureLayer("ContinentData", _continents, cell, continents, _continentTiles,
                (data, value) => TerrainTileSets.DescribeContinent(data, int.Parse(value)));
            // One tile per start index, so a materialised start cell says WHICH start it is.
            _starts = EnsureLayer("StartData", _starts, cell, startIndices, _startTiles,
                (data, value) => TerrainTileSets.DescribeStart(data, int.Parse(value)));
            _startAreas = EnsureLayer("StartAreaData", _startAreas, cell, startAreas, _startAreaTiles,
                (data, value) => TerrainTileSets.DescribeStartArea(data, int.Parse(value)));
            _liquid = EnsureLayer("LiquidData", _liquid, cell, new List<string>(liquids), _liquidTiles,
                (data, value) => TerrainTileSets.DescribeLiquid(data, value));
            _underground = EnsureLayer("UndergroundData", _underground, cell, new List<string>(undergrounds), _undergroundTiles,
                (data, value) =>
                {
                    (string id, int band, int depth) = ParseUndergroundKey(value);
                    TerrainTileSets.DescribeUnderground(data, id, RichnessOfBand(band), depth);
                });

            ClearLayers();

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

                    int startArea = field.StartAreaAtCell(at);
                    if (startArea > 0)
                        Paint(_startAreas, _startAreaTiles, at, startArea.ToString());

                    Paint(_liquid, _liquidTiles, at, field.LiquidResourceAtCell(at));

                    string undergroundId = field.UndergroundResourceAtCell(at);
                    if (undergroundId.Length > 0)
                        Paint(_underground, _undergroundTiles, at, UndergroundKey(
                            undergroundId,
                            RichnessBand(field.UndergroundRichnessAtCell(at)),
                            field.UndergroundDepthAtCell(at)));
                }
            }
            UpdateUndergroundIdentity(field, size);

            for (int index = 0; index < _publishedStartOrder.Count; index++)
                Paint(_starts, _startTiles, _publishedStartOrder[index] - BoundsOrigin, index.ToString());
        }

        private void Paint(
            TileMapLayer layer, Dictionary<string, int> tiles, Vector2I cell, string value)
        {
            if (value.Length > 0 && tiles.TryGetValue(value, out int column))
                layer.SetCell(BoundsOrigin + cell, 0, new Vector2I(column, 0));
        }

        private void ClearLayers()
        {
            UndergroundIdentity = "";
            foreach (var layer in new[] { _terrain, _resources, _features, _relief, _continents, _starts, _liquid, _underground, _startAreas })
                if (GodotObject.IsInstanceValid(layer)) layer!.Clear();
        }

        private void ReleaseLayers()
        {
            foreach (string name in new[] { "TerrainData", "ResourceData", "FeatureData", "ReliefData",
                         "ContinentData", "StartData", "LiquidData", "UndergroundData", "StartAreaData" })
                if (GetNodeOrNull<TileMapLayer>(name) is { } layer)
                {
                    RemoveChild(layer);
                    layer.QueueFree();
                }
            _terrain = _resources = _features = _relief = _continents = _starts = _liquid = _underground = _startAreas = null;
            foreach (var table in new[] { _terrainTiles, _resourceTiles, _featureTiles, _reliefTiles,
                         _continentTiles, _startTiles, _liquidTiles, _undergroundTiles, _startAreaTiles }) table.Clear();
        }

        private void UpdateUndergroundIdentity(GeneratedTerrainField field, Vector2I size)
            => UndergroundIdentity = field.UndergroundIdentity(BoundsOrigin, size);

        private bool HasPublishedCell(Vector2I cell)
        {
            Vector2I local = cell - _publishedOrigin;
            return _field is not null && local.X >= 0 && local.Y >= 0
                && local.X < _publishedSize.X && local.Y < _publishedSize.Y;
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
            System.Action<TileData?, string> describe)
        {
            TileMapLayer layer = existing is not null && GodotObject.IsInstanceValid(existing)
                ? existing
                : TerrainAuthoring.EnsureLayer(this, name);

            // Recipe metadata must not create a second physical or navigable world.
            layer.Visible = false;
            layer.CollisionEnabled = false;
            layer.NavigationEnabled = false;

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
        // Queries use the published field by default. With MaterializeTileLayers,
        // they read the optional native TileData view, also exposed below.
        //
        //     var data := $CellData/TerrainData.get_cell_tile_data(cell)
        //     if data: print(data.get_custom_data("terrain"))

        /// <summary>
        /// The terrain kind the GENERATOR gave a cell, or empty when outside the
        /// map. Named for what it is: the recipe's answer, regenerated from the
        /// axes and seed, not the live map. The live kind - what the player has
        /// cleared, flooded or built over, and what the save carries - is
        /// GridCellDataComponent's alone, and no grid rule reads this for it.
        /// </summary>
        public string GeneratedTerrainAt(Vector2I cell) => _materialized
            ? Read(_terrain, cell, TerrainTileSets.Cell.Terrain).AsString()
            : HasPublishedCell(cell) ? _field!.TerrainAtCell(cell - _publishedOrigin) : "";

        /// <summary>Resource id on a cell, or empty where there is none.</summary>
        public string ResourceAt(Vector2I cell) => _materialized
            ? Read(_resources, cell, TerrainTileSets.Cell.Resource).AsString()
            : HasPublishedCell(cell) ? _field!.ResourceAtCell(cell - _publishedOrigin) : "";

        /// <summary>Feature on a cell - "woods", "marsh" - or empty.</summary>
        public string FeatureAt(Vector2I cell) => _materialized
            ? Read(_features, cell, TerrainTileSets.Cell.Feature).AsString()
            : HasPublishedCell(cell) ? _field!.FeatureAtCell(cell - _publishedOrigin) : "";

        /// <summary>
        /// Relief band at a cell - Flat, Hills or Mountains, as TerrainRelief
        /// numbers it. Its own layer, sourced straight from the generator's
        /// ReliefAt, rather than the terrain layer's Cell.Relief - which is a
        /// different unit, TerrainLayers' drawing z-order derived from kind.
        /// </summary>
        public int ReliefAt(Vector2I cell) => _materialized
            ? Read(_relief, cell, TerrainTileSets.Cell.Relief).AsInt32()
            : HasPublishedCell(cell) ? (int)_field!.ReliefAtCell(cell - _publishedOrigin) : 0;

        /// <summary>Whether the cell is sea, lake or river. A fact about the map.</summary>
        public bool IsWaterAt(Vector2I cell) => _materialized
            ? Read(_terrain, cell, TerrainTileSets.Cell.IsWater).AsBool()
            : TerrainTileSets.IsWaterKind(GeneratedTerrainAt(cell));

        /// <summary>Generated ground's conventional passability, not live navigation rules.</summary>
        public bool PassableAt(Vector2I cell) => _materialized
            ? Read(_terrain, cell, TerrainTileSets.Cell.Passable).AsBool()
            : HasPublishedCell(cell) && TerrainTileSets.GroundOf(GeneratedTerrainAt(cell)) == TerrainTileSets.Ground.Land;

        /// <summary>
        /// Landmass index the cell belongs to. 0 for water, off-map, and
        /// unbuilt layers alike - land continents count from 1, so 0 always
        /// means "no continent" rather than a real id.
        /// </summary>
        public int ContinentAt(Vector2I cell) => _materialized
            ? Read(_continents, cell, TerrainTileSets.Cell.Continent).AsInt32()
            : HasPublishedCell(cell) ? _field!.ContinentAtCell(cell - _publishedOrigin) : 0;

        /// <summary>Whether the generator recommended this cell as a player start.</summary>
        public bool IsStartPositionAt(Vector2I cell) => _materialized
            ? Read(_starts, cell, TerrainTileSets.Cell.StartPosition).AsBool() : _publishedStarts.Contains(cell);

        /// <summary>
        /// Every recommended start cell in absolute map coordinates, IN START ORDER: element k is
        /// start k in both modes (the materialised layer's tiles carry their start index). The
        /// order used to be a HashSet's in the runtime mode and GetUsedCells' in the materialised
        /// one - neither is the generator's - so "the second player's start" had no stable answer.
        /// </summary>
        public Godot.Collections.Array<Vector2I> StartCells()
        {
            if (!_materialized) return new Godot.Collections.Array<Vector2I>(_publishedStartOrder);
            var ordered = new Godot.Collections.Array<Vector2I>();
            if (_starts is null || !GodotObject.IsInstanceValid(_starts)) return ordered;
            var byIndex = new SortedDictionary<int, Vector2I>();
            foreach (Vector2I cell in _starts.GetUsedCells())
                byIndex[Read(_starts, cell, TerrainTileSets.Cell.StartIndex).AsInt32()] = cell;
            foreach (Vector2I cell in byIndex.Values) ordered.Add(cell);
            return ordered;
        }

        /// <summary>
        /// Which start's generated area a cell is reserved for: 0 none (or no start areas),
        /// k+1 start k. The recipe's reservation, as GridCellDataComponent.GetStartArea carries it
        /// on the live cells.
        /// </summary>
        public int StartAreaAt(Vector2I cell) => _materialized
            ? Read(_startAreas, cell, TerrainTileSets.Cell.StartArea).AsInt32()
            : HasPublishedCell(cell) ? _field!.StartAreaAtCell(cell - _publishedOrigin) : 0;

        /// <summary>
        /// Distance from a cell to the nearest start, in whole cells - or -1 off the published map and
        /// on a map that measured none (FEAT-14; the generator's StartDistanceScaling). A generated fact
        /// a game reads like the resources: the far country's danger as well as its richness.
        ///
        /// Read from the published field in BOTH modes, never materialised as tiles: every distinct
        /// distance would be a tile of its own - hundreds on an ordinary map - for a dense fact the
        /// recipe regenerates, which is exactly what the live cells do not store either.
        /// </summary>
        public int StartDistanceAt(Vector2I cell)
            => HasPublishedCell(cell) ? _field!.StartDistanceAtCell(cell - _publishedOrigin) : -1;

        /// <summary>The liquid-stratum resource in a water cell - fish and kin - or empty.</summary>
        public string LiquidResourceAt(Vector2I cell) => _materialized
            ? Read(_liquid, cell, TerrainTileSets.Cell.LiquidResource).AsString()
            : HasPublishedCell(cell) ? _field!.LiquidResourceAtCell(cell - _publishedOrigin) : "";

        /// <summary>The underground resource beneath a cell, or empty. Invisible on the map.</summary>
        public string UndergroundResourceAt(Vector2I cell) => _materialized
            ? Read(_underground, cell, TerrainTileSets.Cell.UndergroundResource).AsString()
            : HasPublishedCell(cell) ? _field!.UndergroundResourceAtCell(cell - _publishedOrigin) : "";

        /// <summary>Underground richness 0..1 (banded) where a deposit exists, else 0.</summary>
        public float UndergroundRichnessAt(Vector2I cell) => _materialized
            ? Read(_underground, cell, TerrainTileSets.Cell.UndergroundRichness).AsSingle()
            : UndergroundResourceAt(cell).Length > 0
                ? RichnessOfBand(RichnessBand(_field!.UndergroundRichnessAtCell(cell - _publishedOrigin))) : 0f;

        /// <summary>Underground depth band, as (int)ResourceDepth; check the id first.</summary>
        public int UndergroundDepthAt(Vector2I cell) => _materialized
            ? Read(_underground, cell, TerrainTileSets.Cell.UndergroundDepth).AsInt32()
            : UndergroundResourceAt(cell).Length > 0 ? _field!.UndergroundDepthAtCell(cell - _publishedOrigin) : 0;

        /// <summary>Richness banded to four steps, so a field needs four tiles per id, not one per float.</summary>
        private static int RichnessBand(float richness)
            => TerrainUndergroundIdentity.RichnessBand(richness);

        private static float RichnessOfBand(int band) => (band + 0.5f) / 4.0f;

        private static string UndergroundKey(string id, int band, int depth) => $"{id}|{band}|{depth}";

        private static (string Id, int Band, int Depth) ParseUndergroundKey(string key)
        {
            string[] parts = key.Split('|');
            string id = parts.Length > 0 ? parts[0] : "";
            int band = parts.Length > 1 && int.TryParse(parts[1], out int b) ? b : 0;
            int depth = parts.Length > 2 && int.TryParse(parts[2], out int d) ? d : 0;
            return (id, band, depth);
        }

        private static Variant Read(TileMapLayer? layer, Vector2I cell, string field)
        {
            TileData? data = layer?.GetCellTileData(cell);
            return data is null ? default : data.GetCustomData(field);
        }

        /// <summary>Generated terrain metadata only. No collision or navigation body.</summary>
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

        /// <summary>Liquid-stratum resources, on the water cells that hold one.</summary>
        public TileMapLayer? LiquidLayer => _liquid;

        /// <summary>Underground deposits - id, richness band, depth - on the cells above one.</summary>
        public TileMapLayer? UndergroundLayer => _underground;
    }
}
