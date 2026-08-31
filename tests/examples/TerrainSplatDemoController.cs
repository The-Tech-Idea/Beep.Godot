using Godot;
using Beep.ECS;

namespace Beep.Tests.Examples;

/// <summary>
/// Proof of the shader-blended terrain surface: the same generated map, drawn
/// as one continuous material rather than one sprite per tile.
/// </summary>
[GlobalClass]
public partial class TerrainSplatDemoController : Node
{
    [Export] public NodePath GeneratorPath { get; set; } = new("");
    [Export] public NodePath SplatPath { get; set; } = new("");
    [Export] public NodePath FeaturesPath { get; set; } = new("");
    [Export] public NodePath ReliefPath { get; set; } = new("");
    [Export] public NodePath ResourcesPath { get; set; } = new("");
    [Export] public NodePath CameraPath { get; set; } = new("");
    [Export] public NodePath CameraControllerPath { get; set; } = new("");
    [Export] public NodePath WorldPath { get; set; } = new("");
    [Export] public NodePath StatusPath { get; set; } = new("HUD/Status");
    [Export] public int TileSize { get; set; } = 64;

    private GridTerrainGeneratorComponent? _generator;
    private GridSplatTerrainRendererComponent? _splat;
    private GridTerrainFeatureRendererComponent? _features;
    private GridTerrainReliefRendererComponent? _relief;
    private GridTerrainResourceRendererComponent? _resources;
    private Node2D? _world;
    private Camera2D? _camera;
    private GridCameraControllerComponent? _cameraController;
    private Label? _status;

    public override void _Ready()
    {
        _generator = GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);
        _splat = GetNodeOrNull<GridSplatTerrainRendererComponent>(SplatPath);
        _features = GetNodeOrNull<GridTerrainFeatureRendererComponent>(FeaturesPath);
        _relief = GetNodeOrNull<GridTerrainReliefRendererComponent>(ReliefPath);
        _resources = GetNodeOrNull<GridTerrainResourceRendererComponent>(ResourcesPath);
        _world = GetNodeOrNull<Node2D>(WorldPath);
        _camera = GetNodeOrNull<Camera2D>(CameraPath);
        _cameraController = GetNodeOrNull<GridCameraControllerComponent>(CameraControllerPath);
        _camera?.MakeCurrent();
        _status = GetNodeOrNull<Label>(StatusPath);
        CallDeferred(nameof(Generate));
    }

    public void Generate()
    {
        if (_generator is null || _splat is null)
            return;

        _generator.GenerateTerrain();
        _splat.Rebuild();
        _relief?.Rebuild();
        _features?.Rebuild();
        _resources?.Rebuild();
        Fit();

        Godot.Collections.Dictionary d = _generator.GetGenerationDiagnostics();
        _status?.SetText(
            $"{_generator.BoundsSize.X} x {_generator.BoundsSize.Y} tiles  |  " +
            $"land {d["land_footprint_coverage"].AsSingle():P0}  " +
            $"rivers {d["river_coverage"].AsSingle():P1}  |  " +
            $"{d["feature_count"].AsInt32()} features  |  {d["generation_milliseconds"].AsInt64()} ms");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The camera controller owns pan and zoom; this only offers a way back
        // to the whole map.
        if (@event is InputEventKey { Pressed: true, Keycode: Key.R })
            Fit();
    }

    /// <summary>
    /// Frames the map through the CAMERA rather than by scaling the world.
    /// Scaling the world as well would compound with camera zoom and make
    /// panning drift.
    /// </summary>
    private void Fit()
    {
        if (_generator is null || _cameraController is null)
            return;

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 map = new(
            Mathf.Max(1, _generator.BoundsSize.X * TileSize),
            Mathf.Max(1, _generator.BoundsSize.Y * TileSize));

        if (_world is not null)
        {
            _world.Scale = Vector2.One;
            _world.Position = Vector2.Zero;
        }

        _cameraController.BoundsPosition = Vector2.Zero;
        _cameraController.BoundsSize = map;

        float fit = Mathf.Min((viewport.X - 24.0f) / map.X, (viewport.Y - 96.0f) / map.Y);
        _cameraController.SetZoomLevel(Mathf.Max(0.02f, fit), immediate: true);
        _cameraController.FocusWorld(map * 0.5f, immediate: true);
    }
}
