using Godot;
using Beep.ECS;

namespace Beep.Tests.Examples;

/// <summary>
/// Generates a world and renders it isometrically.
///
/// The same generator and the same cell data as the flat demo - only the
/// projection differs. Nothing here decides terrain; if the two demos disagree
/// about a map, the renderer is wrong, not the world.
/// </summary>
[GlobalClass]
public partial class TerrainIsoDemoController : Node
{
    [Export] public NodePath GeneratorPath { get; set; } = new("");
    [Export] public NodePath IsoRendererPath { get; set; } = new("");
    [Export] public NodePath IsoFeaturesPath { get; set; } = new("");
    [Export] public NodePath CameraPath { get; set; } = new("");
    [Export] public NodePath CameraControllerPath { get; set; } = new("");
    [Export] public NodePath StatusPath { get; set; } = new("HUD/Status");

    /// <summary>Zoom the scene opens at. One is the art's own scale.</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float SceneZoom { get; set; } = 1.0f;

    private GridTerrainGeneratorComponent? _generator;
    private GridIsoTileMapRendererComponent? _iso;
    private GridIsoFeatureRendererComponent? _isoFeatures;
    private Camera2D? _camera;
    private GridCameraControllerComponent? _cameraController;
    private Label? _status;

    public override void _Ready()
    {
        _generator = GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);
        _iso = GetNodeOrNull<GridIsoTileMapRendererComponent>(IsoRendererPath);
        _isoFeatures = GetNodeOrNull<GridIsoFeatureRendererComponent>(IsoFeaturesPath);
        _camera = GetNodeOrNull<Camera2D>(CameraPath);
        _cameraController = GetNodeOrNull<GridCameraControllerComponent>(CameraControllerPath);
        _status = GetNodeOrNull<Label>(StatusPath);

        _camera?.MakeCurrent();
        CallDeferred(nameof(Generate));
    }

    public void Generate()
    {
        if (_generator is null || _iso is null)
            return;

        _generator.GenerateTerrain();
        _iso.Rebuild();
        _isoFeatures?.Rebuild();
        FrameScene();

        Godot.Collections.Dictionary diagnostics = _generator.GetGenerationDiagnostics();
        _status?.SetText(
            $"isometric  |  {_generator.BoundsSize.X} x {_generator.BoundsSize.Y} tiles  |  " +
            $"land {diagnostics["land_footprint_coverage"].AsSingle():P0}  " +
            $"{diagnostics["continent_count"].AsInt32()} continents  |  " +
            $"{diagnostics["generation_milliseconds"].AsInt64()} ms");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.R })
            FrameMap();
    }

    /// <summary>
    /// Puts the camera in the WORLD, not above the map.
    ///
    /// Fitting the whole island in frame is a map view: every tile becomes a few
    /// pixels and the result reads as a diagram. An isometric game is played
    /// close enough to recognise a single tree or building, so the camera starts
    /// on a start position at roughly one-to-one. R still frames the whole map
    /// for when the overview is what is wanted.
    /// </summary>
    private void FrameScene()
    {
        if (_generator is null || _cameraController is null || _iso is null)
            return;

        Vector2I cell = FirstStartPosition();
        Vector2I diamond = _iso.CellSize;
        var focus = new Vector2(
            (cell.X - cell.Y) * diamond.X * 0.5f,
            (cell.X + cell.Y) * diamond.Y * 0.5f);

        SetCameraBounds();
        _cameraController.SetZoomLevel(SceneZoom, immediate: true);
        _cameraController.FocusWorld(focus, immediate: true);
    }

    /// <summary>
    /// Where a player would actually begin. Falls back to the middle of the map
    /// only when the generator produced no start at all.
    /// </summary>
    private Vector2I FirstStartPosition()
    {
        Godot.Collections.Array<Vector2I> starts = _generator!.GetStartPositions();
        return starts.Count > 0 ? starts[0] : _generator.BoundsSize / 2;
    }

    private void SetCameraBounds()
    {
        Vector2I diamond = _iso!.CellSize;
        Vector2I size = _generator!.BoundsSize;
        var extent = new Vector2(
            Mathf.Max(1, (size.X + size.Y) * diamond.X * 0.5f),
            Mathf.Max(1, (size.X + size.Y) * diamond.Y * 0.5f));
        var centre = new Vector2(0.0f, extent.Y * 0.5f);
        _cameraController!.BoundsPosition = centre - (extent * 0.5f);
        _cameraController.BoundsSize = extent;
    }

    /// <summary>
    /// Frames the whole diamond. An isometric map is twice as wide as it is
    /// tall and its origin is the TOP corner, not a corner of a rectangle, so
    /// fitting it like a grid leaves the map half off-screen.
    /// </summary>
    private void FrameMap()
    {
        if (_generator is null || _cameraController is null || _iso is null)
            return;

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2I diamond = _iso.CellSize;
        Vector2I size = _generator.BoundsSize;
        var extent = new Vector2(
            Mathf.Max(1.0f, (size.X + size.Y) * diamond.X * 0.5f),
            Mathf.Max(1.0f, (size.X + size.Y) * diamond.Y * 0.5f));

        SetCameraBounds();
        float fit = Mathf.Min((viewport.X - 48.0f) / extent.X, (viewport.Y - 96.0f) / extent.Y);
        _cameraController.SetZoomLevel(Mathf.Max(0.02f, fit), immediate: true);
        _cameraController.FocusWorld(new Vector2(0.0f, extent.Y * 0.5f), immediate: true);
    }
}
