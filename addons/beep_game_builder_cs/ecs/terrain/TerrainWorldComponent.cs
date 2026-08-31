using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Which projection a world is drawn in. Public and owned here, because a
    /// game chooses it as readily as a lab does.
    /// </summary>
    public enum TerrainProjection
    {
        Painted = 0,
        Tiles = 1,
        Isometric = 2,
    }

    /// <summary>
    /// Creates a world and draws it. THE map/world creation component.
    ///
    /// Everything a game needs to make a map is here and nothing a lab needs is:
    /// no controls, no panel, no camera. A developer builds their own creation
    /// screen by dropping this in, setting the axes, and calling Build - or by
    /// setting BuildOnReady and writing no code at all.
    ///
    /// This existed only inside the lab's scene controller, which meant the one
    /// piece worth reusing was welded to a particular panel of OptionButtons in
    /// a particular test scene. Every demo then reimplemented the same three
    /// steps - generate, rebuild the renderers, report - as another hundred-line
    /// controller, and each copy drifted: one reported continents, another
    /// landmasses, a third framed the map by scaling the world while the rest
    /// moved a camera.
    ///
    /// The axes are exported, so a scene configures a world the way it
    /// configures anything else. TerrainMapSetup owns what each axis means and
    /// GridTerrainGeneratorComponent.ApplyMapSetup owns how it reaches the
    /// generator; this component owns neither, it just carries the choice.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainWorldComponent : Node
    {
        /// <summary>Raised after a world is generated and drawn.</summary>
        [Signal] public delegate void WorldBuiltEventHandler(Vector2I size);

        [ExportGroup("Pipeline")]
        [Export] public NodePath GeneratorPath { get; set; } = new("");

        [ExportGroup("Renderers")]
        /// <summary>
        /// Each renderer is optional. A scene wires only the projections it
        /// offers, and a missing one simply cannot be selected - rather than
        /// being half-drawn, which is what a shared "draw everything" path does
        /// when a renderer is absent.
        /// </summary>
        [Export] public NodePath SplatRendererPath { get; set; } = new("");
        [Export] public NodePath TileRendererPath { get; set; } = new("");
        [Export] public NodePath IsoRendererPath { get; set; } = new("");
        [Export] public NodePath FeaturesPath { get; set; } = new("");
        [Export] public NodePath IsoFeaturesPath { get; set; } = new("");
        [Export] public NodePath MapOverlayPath { get; set; } = new("");

        /// <summary>
        /// Relief objects and resource icons, both drawn on the square grid and
        /// so belonging to the flat projections.
        /// </summary>
        [Export] public NodePath ReliefRendererPath { get; set; } = new("");
        [Export] public NodePath ResourceRendererPath { get; set; } = new("");

        [ExportGroup("World")]
        [Export] public TerrainShape MapType { get; set; } = TerrainShape.Continents;
        [Export] public TerrainMapSize MapSize { get; set; } = TerrainMapSize.Standard;
        [Export] public TerrainWorldAge WorldAge { get; set; } = TerrainWorldAge.Mature;
        [Export] public TerrainTemperature Temperature { get; set; } = TerrainTemperature.Temperate;
        [Export] public TerrainRainfall Rainfall { get; set; } = TerrainRainfall.Normal;
        [Export] public TerrainSeaLevel SeaLevel { get; set; } = TerrainSeaLevel.Normal;
        [Export] public TerrainResourceLevel ResourceLevel { get; set; } = TerrainResourceLevel.Normal;
        [Export] public ResourceSet Resources { get; set; } = ResourceSet.Historical;
        [Export] public int Seed { get; set; } = 31415;

        [ExportGroup("Drawing")]
        [Export] public TerrainProjection Projection { get; set; } = TerrainProjection.Painted;

        /// <summary>
        /// Build the world once the scene is ready. This is what lets a demo be
        /// a configured node rather than a controller script.
        /// </summary>
        [Export] public bool BuildOnReady { get; set; } = true;

        /// <summary>
        /// Generates the map IN THE EDITOR, and saves it with the scene.
        ///
        /// This is what makes the addon a map-authoring tool rather than only a
        /// runtime one: press it and the renderers build their layers as
        /// children of this scene, owned by it, so they persist. A developer can
        /// then hand-edit the result - move a tile, place a building - and keep
        /// it.
        ///
        /// BuildOnReady is deliberately NOT honoured in the editor: regenerating
        /// every time a scene is opened would be slow and would overwrite an
        /// authored map with a fresh one. This button is the design-time trigger,
        /// and until it existed every component was [Tool] and inert - loaded in
        /// the editor, showing settings that could not be applied to anything.
        /// </summary>
        [ExportToolButton("Generate map")]
        public Callable GenerateMap => Callable.From(Build);

        private GridTerrainGeneratorComponent? _generator;
        private GridSplatTerrainRendererComponent? _splat;
        private GridBiomeTileMapRendererComponent? _tiles;
        private GridIsoTileMapRendererComponent? _iso;
        private GridTerrainFeatureRendererComponent? _features;
        private GridIsoFeatureRendererComponent? _isoFeatures;
        private GridTerrainMapOverlayComponent? _overlay;
        private GridTerrainReliefRendererComponent? _relief;
        private GridTerrainResourceRendererComponent? _resources;
        private Node2D? _splatNode;
        private Node2D? _overlayNode;

        /// <summary>The size of the world last built, in tiles.</summary>
        public Vector2I BuiltSize { get; private set; }

        public override void _Ready()
        {
            if (BuildOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Build));
        }

        public override string[] _GetConfigurationWarnings()
            => GeneratorPath.IsEmpty
                ? new[] { "GeneratorPath should point to a GridTerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>
        /// Generates the configured world and draws it in the configured
        /// projection.
        ///
        /// The size comes from the size AXIS rather than from a renderer's own
        /// bounds, and is then pushed to every renderer - so the projections
        /// cannot end up drawing different extents of the same world.
        /// </summary>
        public void Build()
        {
            Resolve();
            if (_generator is null)
            {
                GD.PushWarning($"[{Name}] no GridTerrainGeneratorComponent at GeneratorPath; no world was created.");
                return;
            }

            Vector2I size = TerrainMapSetup.BoundsFor(MapSize);
            BuiltSize = size;

            _generator.BoundsSize = size;
            _generator.Seed = Mathf.Max(0, Seed);
            _generator.ApplyMapSetup(
                (int)MapType, (int)WorldAge, (int)Temperature,
                (int)Rainfall, (int)SeaLevel, (int)ResourceLevel);
            _generator.ResourceSet = Resources;

            // The climate model, the scale rules and the biome quotas are what
            // make the axes mean what TerrainMapSetup says they mean; a world
            // built without them would answer to the same dials differently.
            _generator.UseClimateBiomeMaps = true;
            _generator.UseScaleRules = true;
            _generator.UseBiomeQuotas = true;

            _generator.GenerateTerrain();
            Draw(size);

            EmitSignal(SignalName.WorldBuilt, size);
        }

        /// <summary>The generator's own report on the world it just made.</summary>
        public Godot.Collections.Dictionary Diagnostics()
        {
            Resolve();
            return _generator?.GetGenerationDiagnostics() ?? new Godot.Collections.Dictionary();
        }

        /// <summary>
        /// One line describing the built world. Every figure comes from the
        /// generator's diagnostics, so a caller's status text cannot disagree
        /// with the map it labels - which is how one demo came to report
        /// continents while another reported landmasses for the same field.
        /// </summary>
        public string StatusLine()
        {
            Resolve();
            if (_generator is null)
                return string.Empty;

            Godot.Collections.Dictionary d = _generator.GetGenerationDiagnostics();
            return string.Format(
                "{0} x {1}  |  {2}  |  land {3:P0}  ocean {4:P0}  lakes {5:P0}  rivers {6:P1}"
                + "  |  {7} of {8} landmasses  |  {9} resources  {10} starts  |  {11} ms",
                BuiltSize.X, BuiltSize.Y,
                LandformName(_generator.Landform),
                d["land_footprint_coverage"].AsSingle(),
                d["ocean_coverage"].AsSingle(),
                d["lake_coverage"].AsSingle(),
                d["river_coverage"].AsSingle(),
                d["land_component_count"].AsInt32(),
                d["requested_landmass_count"].AsInt32(),
                d["resource_count"].AsInt32(),
                d["start_position_count"].AsInt32(),
                d["generation_milliseconds"].AsInt64());
        }

        private static string LandformName(GridTerrainGeneratorComponent.LandformMode landform)
            => landform switch
            {
                GridTerrainGeneratorComponent.LandformMode.Island => "island",
                GridTerrainGeneratorComponent.LandformMode.Archipelago => "archipelago",
                _ => "mainland",
            };

        private void Resolve()
        {
            if (_generator is null || !GodotObject.IsInstanceValid(_generator))
                _generator = GeneratorPath.IsEmpty ? null : GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);

            _splat ??= GetNodeOrNull<GridSplatTerrainRendererComponent>(SplatRendererPath);
            _splatNode ??= GetNodeOrNull<Node2D>(SplatRendererPath);
            _tiles ??= GetNodeOrNull<GridBiomeTileMapRendererComponent>(TileRendererPath);
            _iso ??= GetNodeOrNull<GridIsoTileMapRendererComponent>(IsoRendererPath);
            _features ??= GetNodeOrNull<GridTerrainFeatureRendererComponent>(FeaturesPath);
            _isoFeatures ??= GetNodeOrNull<GridIsoFeatureRendererComponent>(IsoFeaturesPath);
            _overlay ??= GetNodeOrNull<GridTerrainMapOverlayComponent>(MapOverlayPath);
            _relief ??= GetNodeOrNull<GridTerrainReliefRendererComponent>(ReliefRendererPath);
            _resources ??= GetNodeOrNull<GridTerrainResourceRendererComponent>(ResourceRendererPath);
            _overlayNode ??= GetNodeOrNull<Node2D>(MapOverlayPath);
        }
    }
}
