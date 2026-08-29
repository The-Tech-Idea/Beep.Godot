using Godot;
using Beep.ECS;

namespace Beep.Tests.Examples;

/// <summary>
/// Generates a world and renders it as real Godot tiles, to show the same
/// generator output driving a tile renderer rather than a painted image.
/// </summary>
[GlobalClass]
public partial class TerrainTileMapDemoController : Node
{
    [Export] public NodePath GeneratorPath { get; set; } = new("");
    [Export] public NodePath RendererPath { get; set; } = new("");
    [Export] public NodePath WorldPath { get; set; } = new("");
    [Export] public NodePath CameraPath { get; set; } = new("");
    [Export] public NodePath CameraControllerPath { get; set; } = new("");
    [Export] public NodePath StatusPath { get; set; } = new("HUD/Status");

    [ExportGroup("View")]
    [Export] public int TileSize { get; set; } = 64;

    private GridTerrainGeneratorComponent? _generator;
    private GridBiomeTileMapRendererComponent? _renderer;
    private Node2D? _world;
    private Camera2D? _camera;
    private GridCameraControllerComponent? _cameraController;
    private Label? _status;

    public override void _Ready()
    {
        _generator = GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);
        _renderer = GetNodeOrNull<GridBiomeTileMapRendererComponent>(RendererPath);
        _world = GetNodeOrNull<Node2D>(WorldPath);
        _camera = GetNodeOrNull<Camera2D>(CameraPath);
        _cameraController = GetNodeOrNull<GridCameraControllerComponent>(CameraControllerPath);
        _status = GetNodeOrNull<Label>(StatusPath);

        // Be explicit about which camera renders, rather than relying on it
        // happening to be the only one in the scene.
        _camera?.MakeCurrent();

        CallDeferred(nameof(Generate));
    }

    public void Generate()
    {
        if (_generator is null || _renderer is null)
            return;

        _generator.GenerateTerrain();
        _renderer.Rebuild();
        FitToViewport();

        Godot.Collections.Dictionary diagnostics = _generator.GetGenerationDiagnostics();
        _status?.SetText(
            $"tiles {_generator.BoundsSize.X} x {_generator.BoundsSize.Y}  |  " +
            $"land {diagnostics["land_footprint_coverage"].AsSingle():P0}  " +
            $"rivers {diagnostics["river_coverage"].AsSingle():P1}  |  " +
            $"{diagnostics["continent_count"].AsInt32()} continents  " +
            $"{diagnostics["resource_count"].AsInt32()} resources  " +
            $"{diagnostics["start_position_count"].AsInt32()} starts  |  " +
            $"{diagnostics["generation_milliseconds"].AsInt64()} ms");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The camera controller owns pan and zoom; this only offers a way back
        // to the whole map once you have zoomed somewhere.
        if (@event is InputEventKey { Pressed: true, Keycode: Key.R })
            FitToViewport();
    }

    /// <summary>
    /// Frames the whole map through the CAMERA rather than by scaling the world.
    /// Scaling the world would fight the camera controller and make zoom
    /// compound with it; a game views a map by moving a camera.
    /// </summary>
    private void FitToViewport()
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

        // Keep the camera inside the map it is actually looking at.
        _cameraController.BoundsPosition = Vector2.Zero;
        _cameraController.BoundsSize = map;

        float fit = Mathf.Min((viewport.X - 32.0f) / map.X, (viewport.Y - 96.0f) / map.Y);
        _cameraController.SetZoomLevel(Mathf.Max(0.02f, fit), immediate: true);
        _cameraController.FocusWorld(map * 0.5f, immediate: true);
    }
}
