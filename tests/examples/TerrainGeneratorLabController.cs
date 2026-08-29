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
    [Export] public NodePath TerrainPath { get; set; } = new("");
    [Export] public NodePath GeneratorPath { get; set; } = new("");
    [Export] public NodePath BridgePath { get; set; } = new("");
    [Export] public NodePath ScatterPath { get; set; } = new("");
    [Export] public NodePath MapOverlayPath { get; set; } = new("");
    [Export] public NodePath TileRendererPath { get; set; } = new("");
    [Export] public NodePath TileRenderTogglePath { get; set; } = new("");
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
    [Export] public NodePath SeaCoveragePath { get; set; } = new("");
    [Export] public NodePath BeachWidthPath { get; set; } = new("");
    [Export] public NodePath LakeCoveragePath { get; set; } = new("");
    [Export] public NodePath LakeSizePath { get; set; } = new("");
    [Export] public NodePath DetailCoveragePath { get; set; } = new("");
    [Export] public NodePath SwampCoveragePath { get; set; } = new("");
    [Export] public NodePath SnowCoveragePath { get; set; } = new("");
    [Export] public NodePath IceCoveragePath { get; set; } = new("");
    [Export] public NodePath GrassPropCoveragePath { get; set; } = new("");
    [Export] public NodePath DesertPropCoveragePath { get; set; } = new("");
    [Export] public NodePath RockPropCoveragePath { get; set; } = new("");
    [Export] public NodePath PropSpacingPath { get; set; } = new("");
    [Export] public NodePath FoamPath { get; set; } = new("");
    [Export] public NodePath PlantsPath { get; set; } = new("");
    [Export] public NodePath GenerateButtonPath { get; set; } = new("");
    [Export] public NodePath RandomSeedButtonPath { get; set; } = new("");
    [Export] public NodePath ResetViewButtonPath { get; set; } = new("");
    [Export] public NodePath StatusPath { get; set; } = new("");
    [Export] public GridTerrainGeneratorComponent.LandformMode InitialLandform { get; set; } = GridTerrainGeneratorComponent.LandformMode.Mainland;

    [ExportGroup("Preview Navigation")]
    [Export] public float MinimumZoom { get; set; } = 0.15f;
    [Export] public float MaximumZoom { get; set; } = 3.0f;
    [Export] public float ZoomStep { get; set; } = 1.15f;

    private PainterlyTerrainComponent? _terrain;
    private Node2D? _terrainNode;
    private GridTerrainGeneratorComponent? _generator;
    private GridPainterlyTerrainBridgeComponent? _bridge;
    private SeededTerrainPropScatterComponent? _scatter;
    private GridTerrainMapOverlayComponent? _mapOverlay;
    private GridBiomeTileMapRendererComponent? _tileRenderer;
    private CheckButton? _tileRenderToggle;
    private CheckButton? _climateBiomes;
    private CheckButton? _relief;
    private CheckButton? _hillshade;
    private CheckButton? _rivers;
    private CheckButton? _resources;
    private CheckButton? _startMarkers;
    private Node2D? _preview;
    private OptionButton? _preset;
    private OptionButton? _landform;
    private SpinBox? _landmassScale;
    private SpinBox? _islandCount;
    private SpinBox? _seed;
    private SpinBox? _width;
    private SpinBox? _height;
    private SpinBox? _frequency;
    private SpinBox? _octaves;
    private SpinBox? _seaCoverage;
    private SpinBox? _beachWidth;
    private SpinBox? _lakeCoverage;
    private SpinBox? _lakeSize;
    private SpinBox? _detailCoverage;
    private SpinBox? _swampCoverage;
    private SpinBox? _snowCoverage;
    private SpinBox? _iceCoverage;
    private SpinBox? _grassPropCoverage;
    private SpinBox? _desertPropCoverage;
    private SpinBox? _rockPropCoverage;
    private SpinBox? _propSpacing;
    private CheckButton? _foam;
    private CheckButton? _plants;
    private Label? _status;
    private bool _isPanning;

    public override void _Ready()
    {
        ResolveNodes();
        PopulateOptions();

        GetNodeOrNull<Button>(GenerateButtonPath)?.Pressed += Generate;
        foreach (CheckButton? toggle in new[]
                 { _tileRenderToggle, _climateBiomes, _relief, _hillshade, _rivers, _resources, _startMarkers })
        {
            if (toggle is not null)
                toggle.Toggled += _ => Generate();
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
            if (mouseButton.ButtonIndex == MouseButton.Middle)
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

    public void Generate()
    {
        if (_terrain is null || _generator is null || _bridge is null)
            return;

        // A Civilization-scale map is 80-120 tiles across. Capping this at
        // 64x40 made every biome region a handful of tiles wide, which reads as
        // boxes no matter how good the generation or the tile transitions are.
        Vector2I size = new(
            Mathf.Clamp((int)(_width?.Value ?? _terrain.WidthTiles), 8, 200),
            Mathf.Clamp((int)(_height?.Value ?? _terrain.HeightTiles), 6, 200));
        int seed = Mathf.Clamp((int)(_seed?.Value ?? _terrain.Seed), 0, int.MaxValue);

		_terrain.Preset = BasePresetFor(_preset?.Selected ?? 0);
        _terrain.WidthTiles = size.X;
        _terrain.HeightTiles = size.Y;
        _terrain.Seed = seed;
        _terrain.Frequency = Mathf.Clamp((float)(_frequency?.Value ?? _terrain.Frequency), 0.002f, 0.25f);
        _terrain.Octaves = Mathf.Clamp((int)(_octaves?.Value ?? _terrain.Octaves), 1, 10);
        _terrain.BeachWidth = Mathf.Clamp((float)(_beachWidth?.Value ?? _terrain.BeachWidth), 0.0f, 0.2f);
        _terrain.BiomeDetailCoverage = Mathf.Clamp((float)(_detailCoverage?.Value ?? _terrain.BiomeDetailCoverage), 0.0f, 1.0f);
        _terrain.UseAnimatedFoamEdges = _foam?.ButtonPressed ?? false;

        _generator.BoundsSize = size;
        _generator.Seed = seed;
		_generator.Preset = _terrain.Preset;
		_generator.Landform = LandformFor(_landform?.Selected ?? 0);
		_generator.LandmassScale = Percent(_landmassScale);
        _generator.ArchipelagoIslandCount = Mathf.Clamp((int)(_islandCount?.Value ?? _generator.ArchipelagoIslandCount), 2, 12);
        _generator.SeaCoverage = Percent(_seaCoverage);
        _generator.LakeCoverage = Percent(_lakeCoverage);
        _generator.LakeFrequencyMultiplier = Mathf.Clamp((float)(_lakeSize?.Value ?? _generator.LakeFrequencyMultiplier), 0.02f, 1.0f);
		_generator.SwampCoverage = Percent(_swampCoverage);
		_generator.SnowCoverage = Percent(_snowCoverage);
		_generator.IceCoverage = Percent(_iceCoverage);
        // Optional layers. Each is a dial the generator already owns, so turning
        // one off here is the same switch a game would set in the inspector.
        _generator.UseClimateBiomeMaps = _climateBiomes?.ButtonPressed ?? true;
        bool relief = _relief?.ButtonPressed ?? true;
        _generator.HillsFraction = relief ? 0.16f : 0.0f;
        _generator.MountainsFraction = relief ? 0.07f : 0.0f;
        _generator.HillshadeStrength = (_hillshade?.ButtonPressed ?? true) ? 1.0f : 0.0f;
        _generator.RiverDensity = (_rivers?.ButtonPressed ?? true) ? 1.0f : 0.0f;
        _generator.ResourceDensity = (_resources?.ButtonPressed ?? true) ? 1.0f : 0.0f;
        _generator.StartPositionCount = (_startMarkers?.ButtonPressed ?? true) ? 6 : 0;

        _generator.GenerateTerrain();
        _bridge.RebuildTerrain();

        if (_scatter is not null)
        {
            _scatter.SizeInTiles = size;
            _scatter.Seed = seed;
            _scatter.GrassCoverage = Percent(_grassPropCoverage);
            _scatter.DesertCoverage = Percent(_desertPropCoverage);
            _scatter.MudCoverage = Percent(_grassPropCoverage) * 0.70f;
            _scatter.RockCoverage = Percent(_rockPropCoverage);
            _scatter.MinimumDistanceTiles = Mathf.Clamp((float)(_propSpacing?.Value ?? _scatter.MinimumDistanceTiles), 0.0f, 3.0f);
            _scatter.Visible = _plants?.ButtonPressed ?? false;
            if (_scatter.Visible)
                _scatter.Rebuild();
        }

        // Painted and tile renderers read the same generated tiles, so switching
        // between them changes only how the world is drawn, never what it is.
        bool tileRender = _tileRenderToggle?.ButtonPressed ?? false;
        if (_tileRenderer is not null)
        {
            _tileRenderer.Visible = tileRender;
            if (tileRender)
            {
                _tileRenderer.BoundsSize = size;
                _tileRenderer.Rebuild();
            }
        }
        // The painter's C# type derives from Node, while the scene node it is
        // attached to is a Node2D, so visibility is toggled through the node
        // resolved as Node2D rather than through the component type.
        if (_terrainNode is not null)
            _terrainNode.Visible = !tileRender;

        if (_mapOverlay is not null)
        {
            _mapOverlay.BoundsSize = size;
            _mapOverlay.TileSize = _terrain.TileSize;
            _mapOverlay.Refresh();
        }

		Godot.Collections.Dictionary diagnostics = _generator.GetGenerationDiagnostics();
        float footprint = diagnostics["land_footprint_coverage"].AsSingle();
        float ocean = diagnostics["ocean_coverage"].AsSingle();
        float lakes = diagnostics["lake_coverage"].AsSingle();
        int components = diagnostics["land_component_count"].AsInt32();
        long elapsed = diagnostics["generation_milliseconds"].AsInt64();
		int resources = diagnostics["resource_count"].AsInt32();
		int starts = diagnostics["start_position_count"].AsInt32();
		float rivers = diagnostics["river_coverage"].AsSingle();
		_status?.SetText($"{size.X} x {size.Y}  |  {LandformName(_generator.Landform)}  |  land {footprint:P0}  ocean {ocean:P0}  lakes {lakes:P0}  rivers {rivers:P1}  |  {components} landmasses  |  {resources} resources  {starts} starts  |  {elapsed} ms");
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
        if (_preview is null || _terrain is null)
            return;

		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 terrainSize = new(
			Mathf.Max(1, _terrain.WidthTiles * _terrain.TileSize),
			Mathf.Max(1, _terrain.HeightTiles * _terrain.TileSize));
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
			previewTop + ((availableSize.Y - (terrainSize.Y * zoom)) * 0.5f));
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
        _terrain = GetNodeOrNull<PainterlyTerrainComponent>(TerrainPath);
        _terrainNode = GetNodeOrNull<Node2D>(TerrainPath);
        _generator = GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);
        _bridge = GetNodeOrNull<GridPainterlyTerrainBridgeComponent>(BridgePath);
        _scatter = GetNodeOrNull<SeededTerrainPropScatterComponent>(ScatterPath);
        _mapOverlay = GetNodeOrNull<GridTerrainMapOverlayComponent>(MapOverlayPath);
        _tileRenderer = GetNodeOrNull<GridBiomeTileMapRendererComponent>(TileRendererPath);
        _tileRenderToggle = GetNodeOrNull<CheckButton>(TileRenderTogglePath);
        _climateBiomes = GetNodeOrNull<CheckButton>(ClimateBiomesPath);
        _relief = GetNodeOrNull<CheckButton>(ReliefPath);
        _hillshade = GetNodeOrNull<CheckButton>(HillshadePath);
        _rivers = GetNodeOrNull<CheckButton>(RiversPath);
        _resources = GetNodeOrNull<CheckButton>(ResourcesPath);
        _startMarkers = GetNodeOrNull<CheckButton>(StartMarkersPath);
        _preview = GetNodeOrNull<Node2D>(PreviewPath);
        _preset = GetNodeOrNull<OptionButton>(PresetPath);
        _landform = GetNodeOrNull<OptionButton>(LandformPath);
        _landmassScale = GetNodeOrNull<SpinBox>(LandmassScalePath);
        _islandCount = GetNodeOrNull<SpinBox>(IslandCountPath);
        _seed = GetNodeOrNull<SpinBox>(SeedPath);
        _width = GetNodeOrNull<SpinBox>(WidthPath);
        _height = GetNodeOrNull<SpinBox>(HeightPath);
        _frequency = GetNodeOrNull<SpinBox>(FrequencyPath);
        _octaves = GetNodeOrNull<SpinBox>(OctavesPath);
        _seaCoverage = GetNodeOrNull<SpinBox>(SeaCoveragePath);
        _beachWidth = GetNodeOrNull<SpinBox>(BeachWidthPath);
        _lakeCoverage = GetNodeOrNull<SpinBox>(LakeCoveragePath);
        _lakeSize = GetNodeOrNull<SpinBox>(LakeSizePath);
        _detailCoverage = GetNodeOrNull<SpinBox>(DetailCoveragePath);
        _swampCoverage = GetNodeOrNull<SpinBox>(SwampCoveragePath);
        _snowCoverage = GetNodeOrNull<SpinBox>(SnowCoveragePath);
        _iceCoverage = GetNodeOrNull<SpinBox>(IceCoveragePath);
        _grassPropCoverage = GetNodeOrNull<SpinBox>(GrassPropCoveragePath);
        _desertPropCoverage = GetNodeOrNull<SpinBox>(DesertPropCoveragePath);
        _rockPropCoverage = GetNodeOrNull<SpinBox>(RockPropCoveragePath);
        _propSpacing = GetNodeOrNull<SpinBox>(PropSpacingPath);
        _foam = GetNodeOrNull<CheckButton>(FoamPath);
        _plants = GetNodeOrNull<CheckButton>(PlantsPath);
        _status = GetNodeOrNull<Label>(StatusPath);
    }

    private void PopulateOptions()
    {
        if (_preset is not null && _preset.ItemCount == 0)
        {
            foreach (string preset in new[] { "Grassland", "Desert", "Sand", "Rock", "Lava" })
                _preset.AddItem(preset);
            _preset.Selected = 0;
        }

        if (_landform is not null && _landform.ItemCount == 0)
        {
            foreach (string landform in new[] { "Mainland", "Island", "Archipelago" })
                _landform.AddItem(landform);
            _landform.Selected = Mathf.Clamp((int)InitialLandform, 0, _landform.ItemCount - 1);
        }
    }

	private static PainterlyTerrainComponent.TerrainPreset BasePresetFor(int selected) => selected switch
	{
		1 => PainterlyTerrainComponent.TerrainPreset.Desert,
		2 => PainterlyTerrainComponent.TerrainPreset.Sand,
		3 => PainterlyTerrainComponent.TerrainPreset.Rock,
		4 => PainterlyTerrainComponent.TerrainPreset.Lava,
		_ => PainterlyTerrainComponent.TerrainPreset.Grassland,
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

	private static float Percent(SpinBox? control)
		=> Mathf.Clamp((float)(control?.Value ?? 0.0) / 100.0f, 0.0f, 1.0f);
}
