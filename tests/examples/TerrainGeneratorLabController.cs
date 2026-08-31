using Godot;
using Beep.ECS;

namespace Beep.Tests.Examples;

/// <summary>
/// Binds the design-time controls in terrain_generator_lab.tscn to the terrain
/// components. It creates no controls and keeps each generation deterministic.
/// </summary>
[GlobalClass]
public partial class TerrainGeneratorLabController : Node
{
    [Export] public NodePath MapTypePath { get; set; } = new("");
    [Export] public NodePath MapSizePath { get; set; } = new("");
    [Export] public NodePath WorldAgePath { get; set; } = new("");
    [Export] public NodePath TemperaturePath { get; set; } = new("");
    [Export] public NodePath RainfallPath { get; set; } = new("");
    [Export] public NodePath SeaLevelPath { get; set; } = new("");
    [Export] public NodePath ResourceLevelPath { get; set; } = new("");
    [Export] public NodePath ResourceSetPath { get; set; } = new("");
    [Export] public NodePath WavesPath { get; set; } = new("");
    [Export] public NodePath SplatRendererPath { get; set; } = new("");
    [Export] public NodePath GeneratorPath { get; set; } = new("");
    [Export] public NodePath FeaturesPath { get; set; } = new("");
    [Export] public NodePath MapOverlayPath { get; set; } = new("");
    [Export] public NodePath TileRendererPath { get; set; } = new("");
    [Export] public NodePath IsoRendererPath { get; set; } = new("");
    [Export] public NodePath IsoFeaturesPath { get; set; } = new("");
    [Export] public NodePath ViewPath { get; set; } = new("");
    [Export] public NodePath ClimateBiomesPath { get; set; } = new("");
    [Export] public NodePath ReliefPath { get; set; } = new("");
    [Export] public NodePath HillshadePath { get; set; } = new("");
    [Export] public NodePath RiversPath { get; set; } = new("");
    [Export] public NodePath ResourcesPath { get; set; } = new("");
    [Export] public NodePath StartMarkersPath { get; set; } = new("");
    [Export] public NodePath PreviewPath { get; set; } = new("");
    [Export] public NodePath PresetPath { get; set; } = new("");
    [Export] public NodePath LandformPath { get; set; } = new("");
    [Export] public NodePath LandmassScalePath { get; set; } = new("");
    [Export] public NodePath IslandCountPath { get; set; } = new("");
    [Export] public NodePath SeedPath { get; set; } = new("");
    [Export] public NodePath WidthPath { get; set; } = new("");
    [Export] public NodePath HeightPath { get; set; } = new("");
    [Export] public NodePath FrequencyPath { get; set; } = new("");
    [Export] public NodePath OctavesPath { get; set; } = new("");
    [Export] public NodePath BeachWidthPath { get; set; } = new("");
    [Export] public NodePath LakeCoveragePath { get; set; } = new("");
    [Export] public NodePath LakeSizePath { get; set; } = new("");
    [Export] public NodePath PlantsPath { get; set; } = new("");
    [Export] public NodePath GenerateButtonPath { get; set; } = new("");
    [Export] public NodePath RandomSeedButtonPath { get; set; } = new("");
    [Export] public NodePath ResetViewButtonPath { get; set; } = new("");
    [Export] public NodePath StatusPath { get; set; } = new("");
    [Export] public GridTerrainGeneratorComponent.LandformMode InitialLandform { get; set; } = GridTerrainGeneratorComponent.LandformMode.Mainland;

    [ExportGroup("Preview Navigation")]
        /// <summary>
    /// Low enough for the ISOMETRIC view. Its cells are 111x64 against the flat
    /// view's 64px tile, so a map that fits the panel flat needs roughly a third
    /// of the zoom in isometric - a 96x60 map fits at 0.106, and a floor of 0.15
    /// meant it simply could not be zoomed out far enough to see.
    /// </summary>
    [Export] public float MinimumZoom { get; set; } = 0.04f;
    [Export] public float MaximumZoom { get; set; } = 3.0f;
    [Export] public float ZoomStep { get; set; } = 1.15f;

    /// <summary>
    /// The three ways the lab can draw the same generated world. One generator,
    /// one renderer per projection - switching changes how the world is drawn,
    /// never what it is.
    /// </summary>
    private enum LabView
    {
        Painted = 0,
        Tiles = 1,
        Isometric = 2,
    }

    private GridSplatTerrainRendererComponent? _splat;
    private GridIsoTileMapRendererComponent? _iso;
    private GridIsoFeatureRendererComponent? _isoFeatures;
    private OptionButton? _view;
    private Node2D? _terrainNode;
    private GridTerrainGeneratorComponent? _generator;
    private GridTerrainFeatureRendererComponent? _features;
    private GridTerrainMapOverlayComponent? _mapOverlay;
    private GridBiomeTileMapRendererComponent? _tileRenderer;
    private Node2D? _mapOverlayNode;
    private CheckButton? _climateBiomes;
    private SpinBox? _relief;
    private CheckButton? _hillshade;
    private SpinBox? _rivers;
    private SpinBox? _resources;
    private CheckButton? _startMarkers;
    private Node2D? _preview;
    /// <summary>
    /// The world type currently applied. The toggles below gate ITS values
    /// rather than hardcoded ones: a checkbox says whether a world has rivers,
    /// not how many, so turning rivers off and on again must give back the
    /// Highlands' 1.6 rather than a generic 1.0.
    /// </summary>

    private OptionButton? _mapType;
    private OptionButton? _mapSize;
    private OptionButton? _worldAge;
    private OptionButton? _temperature;
    private OptionButton? _rainfall;
    private OptionButton? _seaLevel;
    private OptionButton? _resourceLevel;
    private OptionButton? _preset;
    private OptionButton? _landform;
    private SpinBox? _landmassScale;
    private SpinBox? _islandCount;
    private SpinBox? _seed;
    private SpinBox? _width;
    private SpinBox? _height;
    private SpinBox? _frequency;
    private SpinBox? _octaves;
    private SpinBox? _beachWidth;
    private SpinBox? _lakeCoverage;
    private SpinBox? _lakeSize;
    private SpinBox? _waves;
    private OptionButton? _resourceSet;
    private SpinBox? _plants;
    private Label? _status;
    private bool _isPanning;

    public override void _Ready()
    {
        ResolveNodes();
        PopulateOptions();

        // Reframe when the window changes size. The preview's zoom and position
        // are computed FROM the viewport, so after a resize they describe a
        // viewport that no longer exists - which is why enlarging the window to
        // see more of the map left it sitting somewhere off to one side.
        GetViewport().SizeChanged += ResetPreviewView;

        GetNodeOrNull<Button>(GenerateButtonPath)?.Pressed += Generate;
        foreach (CheckButton? toggle in new[]
                 { _climateBiomes, _hillshade, _startMarkers })
        {
            if (toggle is not null)
                toggle.Toggled += _ => Generate();
        }
        foreach (OptionButton? axis in new[]
                 { _mapType, _mapSize, _worldAge, _temperature, _rainfall, _seaLevel, _resourceLevel })
        {
            if (axis is not null)
                axis.ItemSelected += _ => Generate();
        }
        if (_resourceSet is not null)
            _resourceSet.ItemSelected += _ => Generate();
        if (_view is not null)
        {
            _view.ItemSelected += _ =>
            {
                Generate();
                ResetPreviewView();
            };
        }

        GetNodeOrNull<Button>(RandomSeedButtonPath)?.Pressed += RandomizeSeed;
        GetNodeOrNull<Button>(ResetViewButtonPath)?.Pressed += ResetPreviewView;
        CallDeferred(nameof(Generate));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_preview is null)
            return;

        if (@event is InputEventMouseButton mouseButton)
        {
            // LEFT as well as middle. Middle-drag alone is what a strategy
            // game does, but the panel is the only thing here a left click can
            // hit, and it is a Control that takes its own clicks - so the map
            // has nothing to lose by panning on left-drag, and plenty of people
            // have no middle button to find.
            if (mouseButton.ButtonIndex is MouseButton.Middle or MouseButton.Left)
            {
                _isPanning = mouseButton.Pressed;
                return;
            }

            if (!mouseButton.Pressed)
                return;

            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                ZoomAt(mouseButton.Position, ZoomStep);
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                ZoomAt(mouseButton.Position, 1.0f / Mathf.Max(1.01f, ZoomStep));
            return;
        }

        if (_isPanning && @event is InputEventMouseMotion motion)
        {
            _preview.Position += motion.Relative;
            return;
        }

        if (@event is InputEventPanGesture pan)
            _preview.Position += pan.Delta;
    }

    /// <summary>
    /// Applies a whole world type, then pushes the resulting values back into
    /// the controls.
    ///
    /// The write-back is the point. Without it the sliders keep showing the
    /// previous world's numbers while the map shows the new one, and the next
    /// Generate quietly reads those stale controls and undoes the preset - so
    /// the dropdown would appear to work once and then stop.
    /// </summary>
    private static void SetValue(SpinBox? box, double value)
    {
        if (box is not null)
            box.Value = value;
    }

    public void Generate()
    {
        if (_splat is null || _generator is null)
            return;

        // A Civilization-scale map is 80-120 tiles across. Capping this at
        // 64x40 made every biome region a handful of tiles wide, which reads as
        // boxes no matter how good the generation or the tile transitions are.
        Vector2I size = TerrainMapSetup.BoundsFor(
            (TerrainMapSize)Mathf.Clamp(_mapSize?.Selected ?? 2, 0, 4));
        int seed = Mathf.Clamp((int)(_seed?.Value ?? _generator.Seed), 0, int.MaxValue);

        _generator.BoundsSize = size;
        _generator.Seed = seed;
        // The generator owns its own generation settings now, so the UI drives it
        // directly rather than setting them on the painter and relying on the
        // generator to read them back off a renderer.
        // The mapping from a chosen world to generator settings lives on the
        // generator, so this reads the dropdowns and nothing else.
        _generator.ApplyMapSetup(
            _mapType?.Selected ?? 0,
            _worldAge?.Selected ?? 1,
            _temperature?.Selected ?? 1,
            _rainfall?.Selected ?? 1,
            _seaLevel?.Selected ?? 1,
            _resourceLevel?.Selected ?? 1);

        _generator.ResourceSet = (ResourceSet)Mathf.Clamp(_resourceSet?.Selected ?? 0, 0, 2);

        // The size axis owns the bounds now, so there are no raw width and
        // height boxes to disagree with it.
        _generator.UseClimateBiomeMaps = true;
        _generator.UseScaleRules = true;
        _generator.UseBiomeQuotas = true;
        _generator.HillshadeStrength = 1.0f;
        _splat.WaveIntensity = 1.0f;
        _splat.BoundsSize = size;

        _generator.GenerateTerrain();
        _splat.Rebuild();

        // Vegetation is whatever the GENERATOR decided, drawn - not a second
        // scatter inventing its own placement from terrain kind. One owner.
        if (_features is not null)
        {
            _features.BoundsSize = size;
            _features.Seed = seed;
            _features.Rebuild();
        }

        // All three renderers read the same generated tiles, so switching
        // between them changes only how the world is drawn, never what it is.
        var view = (LabView)Mathf.Clamp(_view?.Selected ?? 0, 0, 2);

        if (_tileRenderer is not null)
        {
            _tileRenderer.Visible = view == LabView.Tiles;
            if (view == LabView.Tiles)
            {
                _tileRenderer.BoundsSize = size;
                _tileRenderer.Rebuild();
            }
        }

        if (_iso is not null)
        {
            _iso.Visible = view == LabView.Isometric;
            if (view == LabView.Isometric)
            {
                _iso.BoundsSize = size;
                _iso.Rebuild();
            }
        }

        if (_isoFeatures is not null)
        {
            _isoFeatures.Visible = view == LabView.Isometric;
            if (view == LabView.Isometric)
            {
                _isoFeatures.BoundsSize = size;
                _isoFeatures.Rebuild();
            }
        }

        // The painter's C# type derives from Node, while the scene node it is
        // attached to is a Node2D, so visibility is toggled through the node
        // resolved as Node2D rather than through the component type.
        if (_terrainNode is not null)
            _terrainNode.Visible = view == LabView.Painted;

        // The flat overlay is drawn on the square tile grid, so it lines up with
        // the flat views only. Left on in the isometric view it would sit over
        // the map in the wrong projection.
        if (_mapOverlayNode is not null)
            _mapOverlayNode.Visible = view != LabView.Isometric;

        if (_mapOverlay is not null)
        {
            _mapOverlay.BoundsSize = size;
            _mapOverlay.TileSize = _splat.TileSize;
            _mapOverlay.Refresh();
        }

		Godot.Collections.Dictionary diagnostics = _generator.GetGenerationDiagnostics();
        float footprint = diagnostics["land_footprint_coverage"].AsSingle();
        float ocean = diagnostics["ocean_coverage"].AsSingle();
        float lakes = diagnostics["lake_coverage"].AsSingle();
        int components = diagnostics["land_component_count"].AsInt32();
        int wanted = diagnostics["requested_landmass_count"].AsInt32();
        long elapsed = diagnostics["generation_milliseconds"].AsInt64();
		int resources = diagnostics["resource_count"].AsInt32();
		int starts = diagnostics["start_position_count"].AsInt32();
		float rivers = diagnostics["river_coverage"].AsSingle();
		_status?.SetText($"{size.X} x {size.Y}  |  {LandformName(_generator.Landform)}  |  land {footprint:P0}  ocean {ocean:P0}  lakes {lakes:P0}  rivers {rivers:P1}  |  {components} of {wanted} landmasses  |  {resources} resources  {starts} starts  |  {elapsed} ms");
		if (_preview?.Scale == Vector2.One)
			ResetPreviewView();
    }

    private void RandomizeSeed()
    {
        if (_seed is null)
            return;

        _seed.Value = GD.Randi() % int.MaxValue;
        Generate();
    }

    private void ResetPreviewView()
    {
        if (_preview is null || _splat is null)
            return;

		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

		// The isometric view has a different footprint AND a different origin:
		// its map is a diamond whose top vertex sits at the local origin, so it
		// extends to the LEFT of it. Framing it on the flat map's rectangle puts
		// half the world off the edge of the preview.
		var view = (LabView)Mathf.Clamp(_view?.Selected ?? 0, 0, 2);
		Vector2 terrainSize;
		Vector2 origin = Vector2.Zero;
		if (view == LabView.Isometric && _iso is not null)
		{
			Vector2I bounds = _iso.BoundsSize;
			float halfWide = Mathf.Max(1, _iso.CellSize.X) * 0.5f;
			float halfHigh = Mathf.Max(1, _iso.CellSize.Y) * 0.5f;
			terrainSize = new Vector2(
				Mathf.Max(1.0f, (bounds.X + bounds.Y) * halfWide),
				Mathf.Max(1.0f, ((bounds.X + bounds.Y) * halfHigh) + (_iso.LevelHeight * 2)));
			origin = new Vector2(-bounds.Y * halfWide, -_iso.LevelHeight * 2);
		}
		else
		{
			terrainSize = new Vector2(
				Mathf.Max(1, _splat.BoundsSize.X * _splat.TileSize),
				Mathf.Max(1, _splat.BoundsSize.Y * _splat.TileSize));
		}
		const float previewLeft = 340.0f;
		const float previewTop = 40.0f;
		const float previewBottomMargin = 70.0f;
		const float previewRightMargin = 24.0f;
		Vector2 availableSize = new(
			Mathf.Max(1, viewportSize.X - previewLeft - previewRightMargin),
			Mathf.Max(1, viewportSize.Y - previewTop - previewBottomMargin));
		float zoom = Mathf.Clamp(
			Mathf.Min(availableSize.X / terrainSize.X, availableSize.Y / terrainSize.Y),
			MinimumZoom,
			MaximumZoom);

		_preview.Scale = Vector2.One * zoom;
		_preview.Position = new Vector2(
			previewLeft + ((availableSize.X - (terrainSize.X * zoom)) * 0.5f),
			previewTop + ((availableSize.Y - (terrainSize.Y * zoom)) * 0.5f))
			- (origin * zoom);
    }

    private void ZoomAt(Vector2 screenPosition, float factor)
    {
        if (_preview is null)
            return;

        Vector2 previousLocalPosition = _preview.ToLocal(screenPosition);
        float currentZoom = _preview.Scale.X;
        float targetZoom = Mathf.Clamp(currentZoom * factor, MinimumZoom, MaximumZoom);
        if (Mathf.IsEqualApprox(currentZoom, targetZoom))
            return;

        _preview.Scale = Vector2.One * targetZoom;
        Vector2 newLocalPosition = _preview.ToLocal(screenPosition);
        _preview.Position += (newLocalPosition - previousLocalPosition) * targetZoom;
    }

    private void ResolveNodes()
    {
        _splat = GetNodeOrNull<GridSplatTerrainRendererComponent>(SplatRendererPath);
        _terrainNode = GetNodeOrNull<Node2D>(SplatRendererPath);
        _generator = GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);
        _features = GetNodeOrNull<GridTerrainFeatureRendererComponent>(FeaturesPath);
        _mapOverlay = GetNodeOrNull<GridTerrainMapOverlayComponent>(MapOverlayPath);
        _tileRenderer = GetNodeOrNull<GridBiomeTileMapRendererComponent>(TileRendererPath);
        _iso = GetNodeOrNull<GridIsoTileMapRendererComponent>(IsoRendererPath);
        _isoFeatures = GetNodeOrNull<GridIsoFeatureRendererComponent>(IsoFeaturesPath);
        _view = GetNodeOrNull<OptionButton>(ViewPath);
        _mapOverlayNode = GetNodeOrNull<Node2D>(MapOverlayPath);
        _climateBiomes = GetNodeOrNull<CheckButton>(ClimateBiomesPath);
        _relief = GetNodeOrNull<SpinBox>(ReliefPath);
        _hillshade = GetNodeOrNull<CheckButton>(HillshadePath);
        _rivers = GetNodeOrNull<SpinBox>(RiversPath);
        _resources = GetNodeOrNull<SpinBox>(ResourcesPath);
        _startMarkers = GetNodeOrNull<CheckButton>(StartMarkersPath);
        _preview = GetNodeOrNull<Node2D>(PreviewPath);
        _mapType = GetNodeOrNull<OptionButton>(MapTypePath);
        _mapSize = GetNodeOrNull<OptionButton>(MapSizePath);
        _worldAge = GetNodeOrNull<OptionButton>(WorldAgePath);
        _temperature = GetNodeOrNull<OptionButton>(TemperaturePath);
        _rainfall = GetNodeOrNull<OptionButton>(RainfallPath);
        _seaLevel = GetNodeOrNull<OptionButton>(SeaLevelPath);
        _resourceLevel = GetNodeOrNull<OptionButton>(ResourceLevelPath);
        _preset = GetNodeOrNull<OptionButton>(PresetPath);
        _landform = GetNodeOrNull<OptionButton>(LandformPath);
        _landmassScale = GetNodeOrNull<SpinBox>(LandmassScalePath);
        _islandCount = GetNodeOrNull<SpinBox>(IslandCountPath);
        _seed = GetNodeOrNull<SpinBox>(SeedPath);
        _width = GetNodeOrNull<SpinBox>(WidthPath);
        _height = GetNodeOrNull<SpinBox>(HeightPath);
        _frequency = GetNodeOrNull<SpinBox>(FrequencyPath);
        _octaves = GetNodeOrNull<SpinBox>(OctavesPath);
        _beachWidth = GetNodeOrNull<SpinBox>(BeachWidthPath);
        _lakeCoverage = GetNodeOrNull<SpinBox>(LakeCoveragePath);
        _lakeSize = GetNodeOrNull<SpinBox>(LakeSizePath);
        _waves = GetNodeOrNull<SpinBox>(WavesPath);
        _resourceSet = GetNodeOrNull<OptionButton>(ResourceSetPath);
        _plants = GetNodeOrNull<SpinBox>(PlantsPath);
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

    private void PopulateOptions()
    {
        if (_resourceSet is not null && _resourceSet.ItemCount == 0)
        {
            foreach (string name in new[] { "Historical", "Oil and gas", "Space exploration" })
                _resourceSet.AddItem(name);
            _resourceSet.Selected = 0;
        }

        if (_view is not null && _view.ItemCount == 0)
        {
            // In the order the enum declares them, so the index IS the view.
            _view.AddItem("Painted");
            _view.AddItem("Game tiles");
            _view.AddItem("Isometric");
            _view.Selected = 0;
        }

        Fill(_mapType, TerrainShapePresets.DisplayNames());
        Fill(_mapSize, TerrainMapSetup.MapSizeNames, (int)TerrainMapSize.Standard);
        Fill(_worldAge, TerrainMapSetup.WorldAgeNames, (int)TerrainWorldAge.Mature);
        Fill(_temperature, TerrainMapSetup.TemperatureNames, (int)TerrainTemperature.Temperate);
        Fill(_rainfall, TerrainMapSetup.RainfallNames, (int)TerrainRainfall.Normal);
        Fill(_seaLevel, TerrainMapSetup.SeaLevelNames, (int)TerrainSeaLevel.Normal);
        Fill(_resourceLevel, TerrainMapSetup.ResourceLevelNames, (int)TerrainResourceLevel.Normal);
    }

	private static TerrainPreset BasePresetFor(int selected) => selected switch
	{
		1 => TerrainPreset.Desert,
		2 => TerrainPreset.Sand,
		3 => TerrainPreset.Rock,
		4 => TerrainPreset.Lava,
		_ => TerrainPreset.Grassland,
	};

	/// <summary>Inverse of BasePresetFor, so a preset can select the matching row.</summary>
	private static int PresetIndexFor(TerrainPreset preset) => preset switch
	{
		TerrainPreset.Desert => 1,
		TerrainPreset.Sand => 2,
		TerrainPreset.Rock => 3,
		TerrainPreset.Lava => 4,
		_ => 0,
	};

	private static GridTerrainGeneratorComponent.LandformMode LandformFor(int selected) => selected switch
	{
		1 => GridTerrainGeneratorComponent.LandformMode.Island,
		2 => GridTerrainGeneratorComponent.LandformMode.Archipelago,
		_ => GridTerrainGeneratorComponent.LandformMode.Mainland,
	};

	private static string LandformName(GridTerrainGeneratorComponent.LandformMode landform)
		=> landform switch
		{
			GridTerrainGeneratorComponent.LandformMode.Island => "island",
			GridTerrainGeneratorComponent.LandformMode.Archipelago => "archipelago",
			_ => "mainland"
		};

	/// <summary>
	/// A percentage control's value, or the world preset's own value when the
	/// control is not on the panel. Defaulting to zero instead would mean
	/// removing a row silently sets the value to nothing, which is the same
	/// accepted-then-ignored failure wearing a different hat.
	/// </summary>
	private static float Percent(SpinBox? control, float fallback)
		=> control is null
			? Mathf.Clamp(fallback, 0.0f, 1.0f)
			: Mathf.Clamp((float)control.Value / 100.0f, 0.0f, 1.0f);
}
