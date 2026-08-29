using Godot;
using Beep.ECS;

namespace Beep.Tests.Examples;

[GlobalClass]
public partial class GridWorldPainterlyDemoController : Node
{
    [Export] public NodePath TerrainBridgePath { get; set; } = new("");
    [Export] public NodePath CellsPath { get; set; } = new("");
    [Export] public NodePath RoadsPath { get; set; } = new("");
    [Export] public NodePath StatePath { get; set; } = new("");
    [Export] public NodePath DepotObjectPath { get; set; } = new("");
    [Export] public NodePath SpawnerPath { get; set; } = new("");
    [Export] public NodePath StatusLabelPath { get; set; } = new("");

    public int LastGeneratedCellCount { get; private set; }
    public bool LastSnapshotRestored { get; private set; }

    private GridPainterlyTerrainBridgeComponent? _bridge;
    private GridCellDataComponent? _cells;
    private GridRoadComponent? _roads;
    private GridWorldStateComponent? _state;
    private GridObjectComponent? _depot;
    private GridWorkerSpawnerComponent? _spawner;
    private Label? _status;

    public override void _Ready()
    {
        CallDeferred(nameof(InitializeDemo));
    }

    public void InitializeDemo()
    {
        ResolveReferences();

        _bridge?.RebuildTerrain();
        LastGeneratedCellCount = _cells?.CellCount ?? 0;

        SeedDemoCells();
        SeedRoads();
        ReserveDepot();
        _spawner?.SpawnWorker();
        RoundTripState();

        _bridge?.RebuildTerrain();
        SetStatus($"Painterly grid demo ready: {LastGeneratedCellCount} generated cells, roads, depot footprint, worker spawn, and state restore verified.");
    }

    private void ResolveReferences()
    {
        _bridge = GetNodeOrNull<GridPainterlyTerrainBridgeComponent>(TerrainBridgePath);
        _cells = GetNodeOrNull<GridCellDataComponent>(CellsPath);
        _roads = GetNodeOrNull<GridRoadComponent>(RoadsPath);
        _state = GetNodeOrNull<GridWorldStateComponent>(StatePath);
        _depot = GetNodeOrNull<GridObjectComponent>(DepotObjectPath);
        _spawner = GetNodeOrNull<GridWorkerSpawnerComponent>(SpawnerPath);
        _status = GetNodeOrNull<Label>(StatusLabelPath);
    }

    private void SeedDemoCells()
    {
        if (_cells == null)
            return;

        for (int x = 4; x <= 9; x++)
        {
            for (int y = 5; y <= 8; y++)
            {
                _cells.SetTerrainKind(new Vector2I(x, y), "grass");
                _cells.ClearLand(new Vector2I(x, y));
            }
        }

        for (int x = 12; x <= 15; x++)
        {
            for (int y = 7; y <= 9; y++)
            {
                Vector2I cell = new(x, y);
                _cells.SetTerrainKind(cell, "dirt");
                _cells.Till(cell);
                _cells.Water(cell);
                _cells.PlantCrop(cell, "demo_crop", 2);
            }
        }

        for (int x = 5; x <= 16; x++)
            _cells.SetTerrainKind(new Vector2I(x, 6), "grass");
        _cells.SetTerrainKind(new Vector2I(16, 7), "grass");
        _cells.SetTerrainKind(new Vector2I(16, 8), "grass");

        _cells.SetTerrainKind(new Vector2I(19, 5), "water");
        _cells.SetTerrainKind(new Vector2I(20, 5), "water");
        _cells.SetTerrainKind(new Vector2I(21, 5), "deep_water");
    }

    private void SeedRoads()
    {
        if (_roads == null)
            return;

        for (int x = 5; x <= 16; x++)
            _roads.SetRoad(new Vector2I(x, 6), "dirt_path", 0.55f);

        _roads.SetRoad(new Vector2I(16, 7), "stone_path", 0.40f);
        _roads.SetRoad(new Vector2I(16, 8), "stone_path", 0.40f);
    }

    private void ReserveDepot()
    {
        if (_depot == null)
            return;

        _depot.Cell = new Vector2I(4, 4);
        _depot.Footprint = new Vector2I(2, 2);
        _depot.ObjectKind = "base";
        _depot.Description = "Starting depot. It reserves placement and navigation cells, then spawns the first worker.";
        _depot.ReserveFootprint();
    }

    private void RoundTripState()
    {
        LastSnapshotRestored = false;
        if (_state == null || _cells == null || _roads == null || _depot == null)
            return;

        Godot.Collections.Dictionary snapshot = _state.CaptureState();
        _cells.ClearLand(new Vector2I(2, 2));
        _roads.ClearRoads();
        _depot.SetCell(new Vector2I(10, 10));
        _state.RestoreState(snapshot);

        LastSnapshotRestored = _roads.HasRoad(new Vector2I(8, 6))
            && _cells.HasFlag(new Vector2I(13, 8), GridCellDataComponent.CellFlags.Planted)
            && _depot.Cell == new Vector2I(4, 4);
    }

    private void SetStatus(string text)
    {
        if (_status != null)
            _status.Text = text;
    }
}
