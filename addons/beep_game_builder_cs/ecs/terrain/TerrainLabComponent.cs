using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The terrain lab's UI: binds a panel of controls to a
    /// <see cref="TerrainWorldComponent"/> and reports what it built.
    ///
    /// THIS IS ONLY UI. It knows about OptionButtons, a status label and a
    /// preview node to pan and zoom. It does not know what a splat renderer is,
    /// which projections exist, how a world is generated or how a map is drawn -
    /// all of that belongs to the world component, so a developer can build a
    /// completely different creation screen on the same component, or none at
    /// all and just configure the node.
    ///
    /// It used to be the other way round: the only code that could create a world
    /// lived in a scene controller in tests/examples, welded to one particular
    /// panel. Every demo that wanted a map reimplemented the same three steps.
    ///
    /// Split by concern: this file resolves and populates the controls,
    /// .Navigation frames and moves the preview.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainLabComponent : Node
    {
        /// <summary>The world this panel drives. Everything else here is a control.</summary>
        [Export] public NodePath WorldPath { get; set; } = new("");

        /// <summary>The node holding the renderers, panned and zoomed as one.</summary>
        [Export] public NodePath PreviewPath { get; set; } = new("");

        [ExportGroup("Setup Controls")]
        [Export] public NodePath MapTypePath { get; set; } = new("");
        [Export] public NodePath MapSizePath { get; set; } = new("");
        [Export] public NodePath WorldAgePath { get; set; } = new("");
        [Export] public NodePath TemperaturePath { get; set; } = new("");
        [Export] public NodePath RainfallPath { get; set; } = new("");
        [Export] public NodePath SeaLevelPath { get; set; } = new("");
        [Export] public NodePath ResourceLevelPath { get; set; } = new("");
        [Export] public NodePath ResourceSetPath { get; set; } = new("");
        [Export] public NodePath SeedPath { get; set; } = new("");
        [Export] public NodePath ViewPath { get; set; } = new("");

        [ExportGroup("Actions")]
        [Export] public NodePath GenerateButtonPath { get; set; } = new("");
        [Export] public NodePath RandomSeedButtonPath { get; set; } = new("");
        [Export] public NodePath ResetViewButtonPath { get; set; } = new("");
        [Export] public NodePath StatusPath { get; set; } = new("");

        // There are deliberately no paths here for relief, rivers, resource
        // density, lake size, beach width, frequency, octaves, landform, or raw
        // width and height. Nineteen such exports and their fields were resolved
        // and never read: the map-setup AXES own those facts, and ApplyMapSetup
        // derives every one of them from the chosen world. Three of the orphans
        // were CheckButtons wired to regenerate, so they looked like working
        // controls while the values they claimed to own were hardcoded.

        [ExportGroup("Preview Navigation")]
        /// <summary>
        /// Low enough for the ISOMETRIC view. Its cells are 111x64 against the
        /// flat view's 64px tile, so a map that fits the panel flat needs roughly
        /// a third of the zoom in isometric - a 96x60 map fits at 0.106, and a
        /// floor of 0.15 meant it simply could not be zoomed out far enough.
        /// </summary>
        [Export] public float MinimumZoom { get; set; } = 0.04f;
        [Export] public float MaximumZoom { get; set; } = 3.0f;
        [Export] public float ZoomStep { get; set; } = 1.15f;

        private TerrainWorldComponent? _world;
        private Node2D? _preview;

        private OptionButton? _mapType;
        private OptionButton? _mapSize;
        private OptionButton? _worldAge;
        private OptionButton? _temperature;
        private OptionButton? _rainfall;
        private OptionButton? _seaLevel;
        private OptionButton? _resourceLevel;
        private OptionButton? _resourceSet;
        private OptionButton? _view;
        private SpinBox? _seed;
        private Label? _status;

        private bool _isPanning;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                return;

            ResolveNodes();
            PopulateOptions();

            // Reframe when the window changes size. The preview's zoom and
            // position are computed FROM the viewport, so after a resize they
            // describe a viewport that no longer exists - which is why enlarging
            // the window to see more of the map left it off to one side.
            GetViewport().SizeChanged += ResetPreviewView;

            GetNodeOrNull<Button>(GenerateButtonPath)?.Pressed += Generate;
            GetNodeOrNull<Button>(RandomSeedButtonPath)?.Pressed += RandomizeSeed;
            GetNodeOrNull<Button>(ResetViewButtonPath)?.Pressed += ResetPreviewView;

            foreach (OptionButton? axis in new[]
                     { _mapType, _mapSize, _worldAge, _temperature, _rainfall,
                       _seaLevel, _resourceLevel, _resourceSet })
            {
                if (axis is not null)
                    axis.ItemSelected += _ => Generate();
            }

            // Changing the projection reframes as well as redraws: they have
            // different footprints and different origins.
            if (_view is not null)
            {
                _view.ItemSelected += _ =>
                {
                    Generate();
                    ResetPreviewView();
                };
            }

            CallDeferred(nameof(Generate));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (WorldPath.IsEmpty)
                return new[] { "WorldPath should point to a TerrainWorldComponent." };
            if (PreviewPath.IsEmpty)
                return new[] { "PreviewPath should point to the Node2D holding the renderers." };
            return System.Array.Empty<string>();
        }

        /// <summary>
        /// Reads the controls onto the world component and builds.
        ///
        /// Every line here is a control being copied onto a property. There is
        /// no generation logic to get wrong, because none of it lives here.
        /// </summary>
        public void Generate()
        {
            if (_world is null)
                return;

            _world.MapType = (TerrainShape)Selected(_mapType, (int)_world.MapType);
            _world.MapSize = (TerrainMapSize)Selected(_mapSize, (int)_world.MapSize);
            _world.WorldAge = (TerrainWorldAge)Selected(_worldAge, (int)_world.WorldAge);
            _world.Temperature = (TerrainTemperature)Selected(_temperature, (int)_world.Temperature);
            _world.Rainfall = (TerrainRainfall)Selected(_rainfall, (int)_world.Rainfall);
            _world.SeaLevel = (TerrainSeaLevel)Selected(_seaLevel, (int)_world.SeaLevel);
            _world.ResourceLevel = (TerrainResourceLevel)Selected(_resourceLevel, (int)_world.ResourceLevel);
            _world.Resources = (ResourceSet)Selected(_resourceSet, (int)_world.Resources);
            _world.Projection = (TerrainProjection)Selected(_view, (int)_world.Projection);
            if (_seed is not null)
                _world.Seed = Mathf.Clamp((int)_seed.Value, 0, int.MaxValue);

            _world.Build();

            _status?.SetText(_world.StatusLine());

            // First build only: a preview still at its default scale has never
            // been framed.
            if (_preview?.Scale == Vector2.One)
                ResetPreviewView();
        }

        /// <summary>A chooser's selection, or the world's current value when it is absent.</summary>
        private static int Selected(OptionButton? option, int fallback)
            => option is null ? fallback : Mathf.Max(0, option.Selected);

        private void RandomizeSeed()
        {
            if (_seed is null)
                return;

            _seed.Value = GD.Randi() % int.MaxValue;
            Generate();
        }

        private void ResolveNodes()
        {
            _world = GetNodeOrNull<TerrainWorldComponent>(WorldPath);
            _preview = GetNodeOrNull<Node2D>(PreviewPath);

            _mapType = GetNodeOrNull<OptionButton>(MapTypePath);
            _mapSize = GetNodeOrNull<OptionButton>(MapSizePath);
            _worldAge = GetNodeOrNull<OptionButton>(WorldAgePath);
            _temperature = GetNodeOrNull<OptionButton>(TemperaturePath);
            _rainfall = GetNodeOrNull<OptionButton>(RainfallPath);
            _seaLevel = GetNodeOrNull<OptionButton>(SeaLevelPath);
            _resourceLevel = GetNodeOrNull<OptionButton>(ResourceLevelPath);
            _resourceSet = GetNodeOrNull<OptionButton>(ResourceSetPath);
            _view = GetNodeOrNull<OptionButton>(ViewPath);
            _seed = GetNodeOrNull<SpinBox>(SeedPath);
            _status = GetNodeOrNull<Label>(StatusPath);
        }

        /// <summary>Fills a chooser once, and selects its default.</summary>
        private static void Fill(OptionButton? option, string[] names, int selected = 0)
        {
            if (option is null || option.ItemCount > 0)
                return;

            foreach (string name in names)
                option.AddItem(name);
            option.Selected = Mathf.Clamp(selected, 0, option.ItemCount - 1);
        }

        /// <summary>
        /// Every chooser's contents come from the type that owns them, so a new
        /// map size, resource set or projection appears here without this file
        /// changing - and the index a chooser reports IS the enum value.
        /// </summary>
        private void PopulateOptions()
        {
            Fill(_mapType, TerrainShapePresets.DisplayNames());
            Fill(_mapSize, TerrainMapSetup.MapSizeNames, (int)TerrainMapSize.Standard);
            Fill(_worldAge, TerrainMapSetup.WorldAgeNames, (int)TerrainWorldAge.Mature);
            Fill(_temperature, TerrainMapSetup.TemperatureNames, (int)TerrainTemperature.Temperate);
            Fill(_rainfall, TerrainMapSetup.RainfallNames, (int)TerrainRainfall.Normal);
            Fill(_seaLevel, TerrainMapSetup.SeaLevelNames, (int)TerrainSeaLevel.Normal);
            Fill(_resourceLevel, TerrainMapSetup.ResourceLevelNames, (int)TerrainResourceLevel.Normal);
            Fill(_resourceSet, TerrainMapSetup.ResourceSetNames);
            Fill(_view, TerrainMapSetup.ProjectionNames);
        }
    }
}
