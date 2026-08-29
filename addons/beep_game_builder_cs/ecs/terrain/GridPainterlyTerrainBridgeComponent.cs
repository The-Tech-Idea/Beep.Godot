using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Converts GridCellDataComponent and GridRoadComponent state into typed
    /// PainterlyTerrainComponent samples. Terrain, water, and gameplay remain
    /// separate render layers; TileMapLayer can own authored transitions.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridPainterlyTerrainBridgeComponent : Node
    {
        [Export] public NodePath PainterlyTerrainPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath RoadPath { get; set; } = new("");
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public bool UsePainterDimensions { get; set; } = true;
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 64);
        [Export] public int TileSize { get; set; } = 64;
        [Export] public string DefaultTerrainKind { get; set; } = "grass";

        [ExportGroup("Rendering")]
        [Export] public bool AutoRebuildOnReady { get; set; } = true;
        [Export] public bool AutoRebuildOnChanges { get; set; } = true;
        [Export] public bool GenerateBeforeFirstRebuild { get; set; } = false;
        [Export] public bool RebuildInEditor { get; set; } = false;
        [Export] public bool UseContinuousBiomeRendering { get; set; } = false;
        [Export(PropertyHint.Range, "0,2,0.01")] public float ChangeDebounceSeconds { get; set; } = 0.08f;
        [Export] public bool PaintRoads { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RoadBlendStrength { get; set; } = 0.55f;
        [Export] public bool PaintGroundDetails { get; set; } = false;
        [Export(PropertyHint.Range, "0,1,0.01")] public float GroundDetailMask { get; set; } = 1.0f;

        private PainterlyTerrainComponent? _terrain;
        private GridCellDataComponent? _cells;
        private GridRoadComponent? _roads;
        private GridTerrainGeneratorComponent? _generator;
        private GridCellDataComponent? _connectedCells;
        private GridRoadComponent? _connectedRoads;
        private GridTerrainGeneratorComponent? _connectedGenerator;
        private bool _rebuildPending;
        private float _rebuildDelay;
        private bool _attemptedInitialGeneration;
        private bool _suppressRebuildRequests;

        public Vector2I EffectiveBoundsSize => new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
        public int EffectiveTileSize => Mathf.Max(1, TileSize);
        public float EffectiveChangeDebounceSeconds => Mathf.Max(0f, float.IsFinite(ChangeDebounceSeconds) ? ChangeDebounceSeconds : 0f);

        public override void _Ready()
        {
            ResolveReferences();
            ConnectSignals();
            SetProcess(false);
            UpdateConfigurationWarnings();

            if (AutoRebuildOnReady)
                RequestRebuild(immediate: true);
        }

        public override void _ExitTree()
        {
            DisconnectSignals();
        }

        public override void _Process(double delta)
        {
            if (!_rebuildPending)
            {
                SetProcess(false);
                return;
            }

            if (!double.IsFinite(delta) || delta < 0.0)
                return;

            _rebuildDelay = Mathf.Max(0f, _rebuildDelay - (float)Math.Min(delta, 86400.0));
            if (_rebuildDelay > 0f)
                return;

            _rebuildPending = false;
            SetProcess(false);
            RebuildTerrain();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (PainterlyTerrainPath.IsEmpty)
                return new[] { "PainterlyTerrainPath should point to a PainterlyTerrainComponent." };
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            if (PaintRoads && RoadPath.IsEmpty)
                return new[] { "RoadPath should point to a GridRoadComponent when PaintRoads is enabled." };
            if (GenerateBeforeFirstRebuild && TerrainGeneratorPath.IsEmpty)
                return new[] { "TerrainGeneratorPath should point to a GridTerrainGeneratorComponent when GenerateBeforeFirstRebuild is enabled." };
            return Array.Empty<string>();
        }

        public void RequestRebuild(bool immediate = false)
        {
            if (Engine.IsEditorHint() && !RebuildInEditor)
                return;

            if (immediate || EffectiveChangeDebounceSeconds <= 0f)
            {
                _rebuildPending = false;
                SetProcess(false);
                RebuildTerrain();
                return;
            }

            _rebuildPending = true;
            _rebuildDelay = EffectiveChangeDebounceSeconds;
            SetProcess(true);
        }

        public void RebuildTerrain()
        {
            ResolveReferences();
            ConnectSignals();
            if (_terrain == null)
                return;

            GenerateInitialTerrainIfNeeded();

            Vector2I size = UsePainterDimensions
                ? new Vector2I(Mathf.Max(1, _terrain.WidthTiles), Mathf.Max(1, _terrain.HeightTiles))
                : EffectiveBoundsSize;
            int tileSize = UsePainterDimensions ? Mathf.Max(1, _terrain.TileSize) : EffectiveTileSize;

            if (!UsePainterDimensions)
            {
                _terrain.WidthTiles = size.X;
                _terrain.HeightTiles = size.Y;
                _terrain.TileSize = tileSize;
            }

            if (UseContinuousBiomeRendering && _generator != null)
            {
                // Resolved once for the whole pass: this sampler runs per pixel,
                // and going back through the generator each time made the field
                // lookup dwarf the painting.
                GeneratedTerrainField field = _generator.ResolveField();
                ResetSampleMemo();
                _terrain.RenderFromTerrainPaintContinuousSampler(
                    size.X, size.Y, at => SampleWithField(at, field), tileSize);
                return;
            }

            _terrain.RenderFromTerrainPaintSampler(size.X, size.Y, SampleTerrainCell, tileSize);
        }

        public PainterlyTerrainComponent.PaintSample SampleCell(Vector2I localCell)
        {
            PainterlyTerrainComponent.TerrainPaintSample sample = SampleTerrainCell(localCell);
            return new PainterlyTerrainComponent.PaintSample(
                sample.BaseColour,
                sample.Effect,
                sample.WaterEdgeAmount,
                sample.TerrainKind);
        }


        // Per-pass memo. The painter walks millions of pixels but only thousands
        // of cells, and consecutive pixels almost always share both the cell and
        // the terrain kind. Normalizing a kind allocates four strings, so doing
        // it per pixel rather than per distinct kind was the largest remaining
        // cost in a render; cell flags are a node lookup with the same shape.
        private string? _memoRawKind;
        private string _memoKind = "grass";
        private string? _memoEffectKind;
        private PainterlyTerrainComponent.TerrainPaintEffect _memoEffect;
        private Vector2I _memoCell = new(int.MinValue, int.MinValue);
        private int _memoFlags;

        /// <summary>
        /// Drops the per-pass memo. Called before a render so cell flags changed
        /// since the last one cannot be served from the previous pass.
        /// </summary>
        private void ResetSampleMemo()
        {
            _memoRawKind = null;
            _memoEffectKind = null;
            _memoCell = new Vector2I(int.MinValue, int.MinValue);
        }

        private string NormalizeMemoized(string value)
        {
            if (ReferenceEquals(value, _memoRawKind))
                return _memoKind;
            _memoRawKind = value;
            _memoKind = Normalize(value);
            return _memoKind;
        }

        private PainterlyTerrainComponent.TerrainPaintEffect EffectForMemoized(string terrainKind)
        {
            if (ReferenceEquals(terrainKind, _memoEffectKind))
                return _memoEffect;
            _memoEffectKind = terrainKind;
            _memoEffect = EffectFor(terrainKind);
            return _memoEffect;
        }

        private int FlagsForMemoized(Vector2I cell)
        {
            if (cell == _memoCell)
                return _memoFlags;
            _memoCell = cell;
            _memoFlags = _cells?.GetFlags(cell) ?? 0;
            return _memoFlags;
        }

        public PainterlyTerrainComponent.TerrainPaintSample SampleTerrainCell(Vector2I localCell)
        {
            Vector2I cell = BoundsOrigin + localCell;
            string terrainKind = Normalize(_cells?.GetTerrainKind(cell) ?? DefaultTerrainKind);
            int flags = _cells?.GetFlags(cell) ?? 0;
            float shade = _generator?.ShadeAtCell(localCell) ?? 1.0f;
            return CreateSample(cell, terrainKind, flags, shade, null, -1.0f);
        }

        public PainterlyTerrainComponent.TerrainPaintSample SampleTerrainAt(Vector2 localPosition)
            => SampleWithField(localPosition, _generator?.ResolveField());

        /// <summary>
        /// Samples one position against an already-resolved field. Deliberately
        /// not an overload of SampleTerrainAt: Godot exposes script methods by
        /// name and keeps only one per name, so overloading would make the
        /// public accessor unreachable from GDScript.
        /// </summary>
        private PainterlyTerrainComponent.TerrainPaintSample SampleWithField(
            Vector2 localPosition,
            GeneratedTerrainField? field)
        {
            Vector2I localCell = new(Mathf.FloorToInt(localPosition.X), Mathf.FloorToInt(localPosition.Y));
            Vector2I cell = BoundsOrigin + localCell;
            string terrainKind = NormalizeMemoized(field != null
                ? field.TerrainAtPosition(localPosition)
                : _cells?.GetTerrainKind(cell) ?? DefaultTerrainKind);
            int flags = FlagsForMemoized(cell);
            float shade = field?.ShadeAtPosition(localPosition) ?? 1.0f;
            Color? blended = field?.BlendedBaseColour(localPosition, ColourLookup);
            // Coverage rather than a yes/no test, so the shoreline fades instead
            // of stepping from sample to sample.
            float waterFraction = field?.WaterFractionAtPosition(localPosition) ?? -1.0f;
            return CreateSample(cell, terrainKind, flags, shade, blended, waterFraction);
        }

        private PainterlyTerrainComponent.TerrainPaintSample CreateSample(
            Vector2I cell,
            string terrainKind,
            int flags,
            float shade,
            Color? blendedColour,
            float waterFraction)
        {
            PainterlyTerrainComponent.TerrainPaintEffect effect = EffectForMemoized(terrainKind);
            string roadKind = PaintRoads && _roads != null && _roads.HasRoad(cell)
                ? _roads.GetRoadKind(cell)
                : "";

            bool isWaterKind = effect == PainterlyTerrainComponent.TerrainPaintEffect.Water;

            // Without coverage (the discrete-cell path) fall back to the hard
            // in-or-out test, which is all that path can know.
            float coverage = waterFraction >= 0.0f
                ? Mathf.Clamp(waterFraction, 0.0f, 1.0f)
                : isWaterKind ? 1.0f : 0.0f;

            // Any coverage at all means the water layer must draw here, even on
            // a land sample: that partial alpha over the land colour is what
            // turns a stepped shoreline into a curve.
            if (coverage > 0.0f)
                effect = PainterlyTerrainComponent.TerrainPaintEffect.Water;

            return new PainterlyTerrainComponent.TerrainPaintSample(
                terrainKind,
                // Water is intentionally a transparent overlay over the terrain
                // below it, rather than a second opaque base colour.
                isWaterKind
                    ? ColourFor("grass")
                    : Shaded(blendedColour ?? ColourFor(terrainKind), shade),
                effect,
                coverage,
                coverage > 0.0f ? 0.25f : 0.0f,
                PaintGroundDetails ? Mathf.Clamp(GroundDetailMask, 0.0f, 1.0f) : 0.0f,
                0.0f,
                flags,
                roadKind);
        }

        private void ResolveReferences()
        {
            if (_terrain == null || !GodotObject.IsInstanceValid(_terrain))
                _terrain = !PainterlyTerrainPath.IsEmpty
                    ? GetNodeOrNull<PainterlyTerrainComponent>(PainterlyTerrainPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<PainterlyTerrainComponent>(GetTree()?.CurrentScene) : null;

            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_roads == null || !GodotObject.IsInstanceValid(_roads))
                _roads = !RoadPath.IsEmpty
                    ? GetNodeOrNull<GridRoadComponent>(RoadPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene) : null;

            if (_generator == null || !GodotObject.IsInstanceValid(_generator))
                _generator = !TerrainGeneratorPath.IsEmpty
                    ? GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridTerrainGeneratorComponent>(GetTree()?.CurrentScene) : null;
        }

        private void ConnectSignals()
        {
            if (_connectedCells == _cells && _connectedRoads == _roads && _connectedGenerator == _generator)
                return;

            DisconnectSignals();

            if (_cells != null)
            {
                _cells.CellChanged += OnCellChanged;
                _cells.CellsChanged += OnCellsChanged;
                _connectedCells = _cells;
            }
            if (_roads != null)
            {
                _roads.RoadChanged += OnRoadChanged;
                _roads.RoadsChanged += OnRoadsChanged;
                _connectedRoads = _roads;
            }
            if (_generator != null)
            {
                _generator.TerrainGenerated += OnTerrainGenerated;
                _connectedGenerator = _generator;
            }
        }

        private void DisconnectSignals()
        {
            if (_connectedCells != null && GodotObject.IsInstanceValid(_connectedCells))
            {
                _connectedCells.CellChanged -= OnCellChanged;
                _connectedCells.CellsChanged -= OnCellsChanged;
            }
            if (_connectedRoads != null && GodotObject.IsInstanceValid(_connectedRoads))
            {
                _connectedRoads.RoadChanged -= OnRoadChanged;
                _connectedRoads.RoadsChanged -= OnRoadsChanged;
            }
            if (_connectedGenerator != null && GodotObject.IsInstanceValid(_connectedGenerator))
                _connectedGenerator.TerrainGenerated -= OnTerrainGenerated;

            _connectedCells = null;
            _connectedRoads = null;
            _connectedGenerator = null;
        }

        private void GenerateInitialTerrainIfNeeded()
        {
            if (!GenerateBeforeFirstRebuild || _attemptedInitialGeneration)
                return;

            _attemptedInitialGeneration = true;
            if (_generator == null)
                return;

            _suppressRebuildRequests = true;
            try
            {
                _generator.GenerateTerrain();
            }
            finally
            {
                _suppressRebuildRequests = false;
            }
        }

        private void OnCellChanged(int x, int y)
        {
            if (AutoRebuildOnChanges && !_suppressRebuildRequests)
                RequestRebuild();
        }

        private void OnRoadChanged(int x, int y, string kind, bool hasRoad)
        {
            if (AutoRebuildOnChanges && PaintRoads && !_suppressRebuildRequests)
                RequestRebuild();
        }

        private void OnCellsChanged()
        {
            if (AutoRebuildOnChanges && !_suppressRebuildRequests)
                RequestRebuild();
        }

        private void OnRoadsChanged()
        {
            if (AutoRebuildOnChanges && PaintRoads && !_suppressRebuildRequests)
                RequestRebuild();
        }

        private void OnTerrainGenerated(int cellCount)
        {
            if (AutoRebuildOnChanges && !_suppressRebuildRequests)
                RequestRebuild();
        }

        /// <summary>
        /// Applies the generator's hillshade to a base colour. Relief lives here
        /// rather than in the terrain kind, so a grassland hill stays green.
        /// </summary>
        private static Color Shaded(Color colour, float shade)
        {
            if (Mathf.IsEqualApprox(shade, 1.0f))
                return colour;

            float factor = Mathf.Clamp(shade, 0.0f, 2.0f);
            return new Color(
                Mathf.Clamp(colour.R * factor, 0.0f, 1.0f),
                Mathf.Clamp(colour.G * factor, 0.0f, 1.0f),
                Mathf.Clamp(colour.B * factor, 0.0f, 1.0f),
                colour.A);
        }

        /// <summary>
        /// Cached so the per-pixel blend does not allocate a delegate on every
        /// sample during a render.
        /// </summary>
        private static readonly Func<string, Color> ColourLookup = ColourFor;

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "grass" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private static PainterlyTerrainComponent.TerrainPaintEffect EffectFor(string terrainKind)
            => terrainKind switch
            {
                "water" or "sea" or "ocean" or "shallow_water" or "deep_water" => PainterlyTerrainComponent.TerrainPaintEffect.Water,
                "ice" => PainterlyTerrainComponent.TerrainPaintEffect.Ice,
                "lava" => PainterlyTerrainComponent.TerrainPaintEffect.Lava,
                _ => PainterlyTerrainComponent.TerrainPaintEffect.None
            };

        private static Color ColourFor(string terrainKind)
            => terrainKind switch
            {
                "desert" => new Color(0.62f, 0.43f, 0.21f),
                "sand" or "beach" => new Color(0.67f, 0.56f, 0.34f),
                "dirt" or "soil" or "earth" => new Color(0.42f, 0.29f, 0.16f),
                "mud" or "swamp" => new Color(0.18f, 0.28f, 0.17f),
                // Hills read lighter than mountains so relief is legible.
                "gravel" => new Color(0.47f, 0.46f, 0.40f),
                "rock" or "stone" => new Color(0.35f, 0.36f, 0.33f),
                // Climate biomes from the temperature x moisture table.
                "tundra" => new Color(0.52f, 0.51f, 0.42f),
                "dry_grass" or "plains" => new Color(0.55f, 0.60f, 0.24f),
                "jungle" => new Color(0.16f, 0.42f, 0.14f),
                "snow" => new Color(0.78f, 0.82f, 0.80f),
                "ice" => new Color(0.62f, 0.80f, 0.86f),
                "water" or "sea" or "ocean" => new Color(0.05f, 0.34f, 0.50f),
                "shallow_water" => new Color(0.12f, 0.48f, 0.58f),
                "deep_water" => new Color(0.03f, 0.18f, 0.36f),
                "lava" => new Color(0.35f, 0.10f, 0.04f),
                _ => new Color(0.34f, 0.62f, 0.12f),
            };

        private static Color RoadColour(string roadKind)
            => Normalize(roadKind) switch
            {
                "stone" or "stone_path" or "paved" => new Color(0.43f, 0.40f, 0.34f),
                "sand" or "sand_path" => new Color(0.60f, 0.49f, 0.30f),
                "asphalt" => new Color(0.18f, 0.18f, 0.17f),
                _ => new Color(0.48f, 0.34f, 0.19f),
            };
    }
}
