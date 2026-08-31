using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Synchronizes GridCellDataComponent and GridRoadComponent state into a real
    /// Godot TileMapLayer. Use this when a game has authored tiles and should
    /// show map state through Godot's tile renderer instead of lightweight debug
    /// overlays.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTileMapLayerBridgeComponent : Node
    {
        [Export] public NodePath TileMapLayerPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath RoadPath { get; set; } = new("");

        [Export] public bool PaintCells { get; set; } = true;
        [Export] public bool PaintRoads { get; set; } = true;
        [Export] public bool RoadsOverrideCells { get; set; } = true;
        [Export] public bool ClearBeforeRebuild { get; set; } = false;
        [Export] public bool FillDefaultTerrainInBounds { get; set; } = false;
        [Export] public Vector2I BoundsMin { get; set; } = new(0, 0);
        [Export] public Vector2I BoundsMax { get; set; } = new(15, 15);

        [ExportGroup("Tile Source")]
        [Export] public int SourceId { get; set; } = 0;
        [Export] public int AlternativeTile { get; set; } = 0;

        [ExportGroup("Atlas Coordinates")]
        [Export] public Vector2I DefaultTerrainAtlas { get; set; } = new(0, 0);
        [Export] public Vector2I ClearedAtlas { get; set; } = new(1, 0);
        [Export] public Vector2I TilledAtlas { get; set; } = new(2, 0);
        [Export] public Vector2I WateredAtlas { get; set; } = new(3, 0);
        [Export] public Vector2I PlantedAtlas { get; set; } = new(4, 0);
        [Export] public Vector2I HarvestReadyAtlas { get; set; } = new(5, 0);
        [Export] public Vector2I BlockedAtlas { get; set; } = new(6, 0);
        [Export] public Vector2I RoadAtlas { get; set; } = new(7, 0);

        private TileMapLayer? _tileMapLayer;
        private GridCellDataComponent? _cells;
        private GridRoadComponent? _roads;
        private GridCellDataComponent? _connectedCells;
        private GridRoadComponent? _connectedRoads;

        public override void _Ready()
        {
            ResolveReferences();
            ConnectSignals();
            UpdateConfigurationWarnings();
            if (!Engine.IsEditorHint())
                Rebuild();
        }

        public override void _ExitTree()
        {
            DisconnectSignals();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (TileMapLayerPath.IsEmpty)
                return new[] { "TileMapLayerPath should point to a Godot TileMapLayer." };
            if (PaintCells && CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent when PaintCells is enabled." };
            if (PaintRoads && RoadPath.IsEmpty)
                return new[] { "RoadPath should point to a GridRoadComponent when PaintRoads is enabled." };
            if (BoundsMax.X < BoundsMin.X || BoundsMax.Y < BoundsMin.Y)
                return new[] { "BoundsMax must be greater than or equal to BoundsMin." };
            return Array.Empty<string>();
        }

        public void Rebuild()
        {
            ResolveReferences();
            if (_tileMapLayer == null)
                return;

            if (_tileMapLayer.TileSet == null)
                return;

            if (ClearBeforeRebuild)
                _tileMapLayer.Clear();

            if (FillDefaultTerrainInBounds)
                FillDefaultTerrain();

            // PaintCell, not RefreshCell: RefreshCell calls UpdateInternals for
            // the one cell it wrote, which is right for a single edit and wrong
            // inside a loop. Rebuilding a map ran a full internal update once per
            // painted cell and then once more at the end, so the cost grew with
            // the square of the map rather than with it.
            if (PaintCells && _cells != null)
            {
                foreach (Godot.Collections.Dictionary cellData in _cells.GetCells())
                {
                    Vector2I cell = GridVariantReader.Vector2I(cellData, "cell", new Vector2I(int.MinValue, int.MinValue));
                    if (cell.X == int.MinValue || cell.Y == int.MinValue)
                        continue;

                    PaintCell(cell);
                }
            }

            if (PaintRoads && _roads != null)
            {
                foreach (Vector2I roadCell in _roads.GetRoadCells())
                    PaintCell(roadCell);
            }

            // Once, for the whole batch.
            _tileMapLayer.UpdateInternals();
        }

        /// <summary>Repaints one cell and publishes it immediately.</summary>
        public void RefreshCell(Vector2I cell)
        {
            if (!PaintCell(cell))
                return;

            _tileMapLayer!.UpdateInternals();
        }

        /// <summary>
        /// Writes one cell's tile WITHOUT publishing it, so a batch can update
        /// the layer's internals once at the end. Reports whether it wrote.
        /// </summary>
        private bool PaintCell(Vector2I cell)
        {
            ResolveReferences();
            if (_tileMapLayer == null || _tileMapLayer.TileSet == null)
                return false;

            _tileMapLayer.SetCell(cell, SourceId, AtlasForCell(cell), AlternativeTile);
            return true;
        }

        public void EraseCell(Vector2I cell)
        {
            ResolveReferences();
            _tileMapLayer?.EraseCell(cell);
        }

        public int PaintedCellCount()
        {
            ResolveReferences();
            return _tileMapLayer?.GetUsedCells().Count ?? 0;
        }

        public Vector2I AtlasForCell(Vector2I cell)
        {
            ResolveReferences();
            if (PaintRoads && RoadsOverrideCells && _roads != null && _roads.HasRoad(cell))
                return RoadAtlas;

            int flags = PaintCells && _cells != null ? _cells.GetFlags(cell) : 0;
            var cellFlags = (GridCellDataComponent.CellFlags)flags;
            if ((cellFlags & GridCellDataComponent.CellFlags.Blocked) != 0) return BlockedAtlas;
            if ((cellFlags & GridCellDataComponent.CellFlags.HarvestReady) != 0) return HarvestReadyAtlas;
            if ((cellFlags & GridCellDataComponent.CellFlags.Planted) != 0) return PlantedAtlas;
            if ((cellFlags & GridCellDataComponent.CellFlags.Watered) != 0) return WateredAtlas;
            if ((cellFlags & GridCellDataComponent.CellFlags.Tilled) != 0) return TilledAtlas;
            if ((cellFlags & GridCellDataComponent.CellFlags.Cleared) != 0) return ClearedAtlas;

            if (PaintRoads && !RoadsOverrideCells && _roads != null && _roads.HasRoad(cell))
                return RoadAtlas;

            return DefaultTerrainAtlas;
        }

        private void FillDefaultTerrain()
        {
            if (_tileMapLayer == null)
                return;

            for (int y = BoundsMin.Y; y <= BoundsMax.Y; y++)
            {
                for (int x = BoundsMin.X; x <= BoundsMax.X; x++)
                    _tileMapLayer.SetCell(new Vector2I(x, y), SourceId, DefaultTerrainAtlas, AlternativeTile);
            }
        }

        private void ResolveReferences()
        {
            if (_tileMapLayer == null || !GodotObject.IsInstanceValid(_tileMapLayer))
                _tileMapLayer = !TileMapLayerPath.IsEmpty
                    ? GetNodeOrNull<TileMapLayer>(TileMapLayerPath)
                    : null;

            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_roads == null || !GodotObject.IsInstanceValid(_roads))
                _roads = !RoadPath.IsEmpty
                    ? GetNodeOrNull<GridRoadComponent>(RoadPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene) : null;

            // Subscribe to whatever was just resolved. Signals were connected
            // only in _Ready, so a source that resolved later - a path assigned
            // at runtime, or a component added after this one - was found and
            // painted once and then never listened to again. The bridge went on
            // reporting a painted cell count while silently no longer tracking
            // the state it exists to mirror. ConnectSignals is idempotent.
            ConnectSignals();
        }

        private void ConnectSignals()
        {
            if (_connectedCells == _cells && _connectedRoads == _roads)
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

            _connectedCells = null;
            _connectedRoads = null;
        }

        private void OnCellChanged(int x, int y)
        {
            if (PaintCells)
                RefreshCell(new Vector2I(x, y));
        }

        private void OnRoadChanged(int x, int y, string kind, bool hasRoad)
        {
            if (PaintRoads)
                RefreshCell(new Vector2I(x, y));
        }

        private void OnCellsChanged()
        {
            if (PaintCells)
                Rebuild();
        }

        private void OnRoadsChanged()
        {
            if (PaintRoads)
                Rebuild();
        }
    }
}
