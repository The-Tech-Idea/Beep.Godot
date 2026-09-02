using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Lightweight HUD overview for top-down/isometric grid worlds. It draws a
    /// baked terrain background, roads, selected cells, jobs, units, and the
    /// camera viewport from existing Godot nodes without requiring a TileMap or
    /// custom minimap scene.
    ///
    /// Road, job, and selection positions are SNAPSHOTTED and refreshed only
    /// when the owning system signals a change; the per-frame draw reads those
    /// plain lists plus the genuinely live facts (unit positions, the camera
    /// rectangle). Rebuilding marshalled Godot collections for every element on
    /// every frame - which is what this used to do - made an idle minimap one
    /// of the most allocation-heavy nodes in a scene.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridMinimapComponent : Control
    {
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public NodePath RoadPath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath UnitsRootPath { get; set; } = new("");
        [Export] public NodePath CameraPath { get; set; } = new("");

        /// <summary>
        /// The cell model the terrain background is baked from. Left empty, the
        /// scene's GridCellDataComponent is found automatically; point it
        /// explicitly when a scene has more than one.
        /// </summary>
        [Export] public NodePath CellDataPath { get; set; } = new("");

        [Export] public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// How often the minimap repaints while AutoRefresh is on. Units and
        /// the camera rectangle move continuously, so it still repaints on a
        /// clock - but an overview map does not need frame rate.
        /// </summary>
        [Export(PropertyHint.Range, "0.02,2,0.02")] public float RefreshIntervalSeconds { get; set; } = 0.1f;

        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public bool PreferNavigationBounds { get; set; } = true;

        /// <summary>
        /// Bake the map's terrain kinds into a background texture, so the
        /// minimap shows land, water, and relief instead of a flat panel. Baked
        /// once per cell-data change, never per frame.
        /// </summary>
        [Export] public bool ShowTerrain { get; set; } = true;
        [Export] public bool ShowRoads { get; set; } = true;
        [Export] public bool ShowSelection { get; set; } = true;
        [Export] public bool ShowJobs { get; set; } = true;
        [Export] public bool ShowUnits { get; set; } = true;
        [Export] public bool ShowCameraView { get; set; } = true;
        [Export] public Color BackgroundColor { get; set; } = new(0.06f, 0.07f, 0.075f, 0.78f);
        [Export] public Color BorderColor { get; set; } = new(0.75f, 0.65f, 0.48f, 0.9f);
        [Export] public Color RoadColor { get; set; } = new(0.68f, 0.52f, 0.28f, 0.95f);
        [Export] public Color SelectionColor { get; set; } = new(0.35f, 0.75f, 1f, 0.95f);
        [Export] public Color JobColor { get; set; } = new(1f, 0.78f, 0.22f, 0.95f);
        [Export] public Color UnitColor { get; set; } = new(0.3f, 0.9f, 0.48f, 0.95f);
        [Export] public Color CameraColor { get; set; } = new(1f, 1f, 1f, 0.78f);

        private GridProjectionComponent? _grid;
        private GridNavigationComponent? _navigation;
        private GridRoadComponent? _roads;
        private GridSelectionComponent? _selection;
        private GridJobQueueComponent? _jobs;
        private GridCellDataComponent? _cells;
        private Node? _unitsRoot;
        private Camera2D? _camera;

        private GridRoadComponent? _connectedRoads;
        private GridJobQueueComponent? _connectedJobs;
        private GridSelectionComponent? _connectedSelection;
        private GridCellDataComponent? _connectedCells;

        private readonly List<Vector2I> _roadCells = new();
        private readonly List<Vector2I> _jobCells = new();
        private readonly List<Vector2I> _selectedCells = new();
        private bool _roadsDirty = true;
        private bool _jobsDirty = true;
        private bool _selectionDirty = true;
        private bool _terrainDirty = true;
        private ImageTexture? _terrainTexture;
        private float _refreshAccumulator;

        /// <summary>
        /// Muted map colours per terrain kind - an overview reads by hue, not
        /// by art. Unlisted kinds take the panel background.
        /// </summary>
        private static readonly Dictionary<string, Color> TerrainColors = new(StringComparer.Ordinal)
        {
            ["grass"] = new Color(0.32f, 0.5f, 0.3f),
            ["grassland"] = new Color(0.32f, 0.5f, 0.3f),
            ["dry_grass"] = new Color(0.55f, 0.55f, 0.32f),
            ["plains"] = new Color(0.55f, 0.55f, 0.32f),
            ["jungle"] = new Color(0.18f, 0.4f, 0.24f),
            ["desert"] = new Color(0.76f, 0.66f, 0.42f),
            ["sand"] = new Color(0.82f, 0.74f, 0.52f),
            ["beach"] = new Color(0.82f, 0.74f, 0.52f),
            ["tundra"] = new Color(0.55f, 0.58f, 0.52f),
            ["snow"] = new Color(0.88f, 0.9f, 0.92f),
            ["ice"] = new Color(0.78f, 0.86f, 0.92f),
            ["swamp"] = new Color(0.3f, 0.38f, 0.28f),
            ["mud"] = new Color(0.42f, 0.34f, 0.24f),
            ["dirt"] = new Color(0.46f, 0.38f, 0.28f),
            ["gravel"] = new Color(0.52f, 0.5f, 0.46f),
            ["rock"] = new Color(0.44f, 0.42f, 0.4f),
            ["stone"] = new Color(0.44f, 0.42f, 0.4f),
            ["lava"] = new Color(0.55f, 0.2f, 0.1f),
            ["shallow_water"] = new Color(0.25f, 0.5f, 0.62f),
            ["water"] = new Color(0.25f, 0.5f, 0.62f),
            ["deep_water"] = new Color(0.14f, 0.3f, 0.46f),
            ["sea"] = new Color(0.14f, 0.3f, 0.46f),
            ["ocean"] = new Color(0.14f, 0.3f, 0.46f),
        };

        public Vector2I EffectiveBoundsOrigin()
            => PreferNavigationBounds && _navigation != null && _navigation.UseBounds ? _navigation.BoundsOrigin : BoundsOrigin;

        public Vector2I EffectiveBoundsSize()
        {
            Vector2I size = PreferNavigationBounds && _navigation != null && _navigation.UseBounds ? _navigation.BoundsSize : BoundsSize;
            return new Vector2I(Mathf.Max(1, size.X), Mathf.Max(1, size.Y));
        }

        public override void _Ready()
        {
            ResolveReferences();
            SetProcess(AutoRefresh || Engine.IsEditorHint());
            UpdateConfigurationWarnings();
            QueueRedraw();
        }

        public override void _ExitTree()
        {
            DisconnectSignals();
        }

        public override void _Process(double delta)
        {
            if (!AutoRefresh && !Engine.IsEditorHint())
                return;

            _refreshAccumulator += (float)delta;
            if (_refreshAccumulator < Mathf.Max(0.02f, RefreshIntervalSeconds))
                return;

            _refreshAccumulator = 0f;
            QueueRedraw();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (BoundsSize.X <= 0 || BoundsSize.Y <= 0)
                return new[] { "BoundsSize must be greater than zero." };
            return Array.Empty<string>();
        }

        public override void _Draw()
        {
            ResolveReferences();
            RefreshSnapshots();

            Rect2 mapRect = MapRect();
            DrawRect(mapRect, BackgroundColor, filled: true);

            if (ShowTerrain && _terrainTexture != null)
                DrawTextureRect(_terrainTexture, mapRect, false);

            DrawRect(mapRect, BorderColor, filled: false, width: 1.5f);

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();

            if (ShowRoads)
            {
                foreach (Vector2I cell in _roadCells)
                    DrawDot(mapRect, CellToMinimap(cell, mapRect, origin, size), 2.2f, RoadColor);
            }

            if (ShowJobs)
            {
                foreach (Vector2I cell in _jobCells)
                    DrawDot(mapRect, CellToMinimap(cell, mapRect, origin, size), 2.6f, JobColor);
            }

            if (ShowSelection)
            {
                foreach (Vector2I cell in _selectedCells)
                    DrawDot(mapRect, CellToMinimap(cell, mapRect, origin, size), 2.8f, SelectionColor);
            }

            if (ShowUnits)
                DrawUnits(mapRect, origin, size);
            if (ShowCameraView)
                DrawCameraView(mapRect, origin, size);
        }

        public void RebuildMinimap()
        {
            ResolveReferences();
            _roadsDirty = true;
            _jobsDirty = true;
            _selectionDirty = true;
            _terrainDirty = true;
            QueueRedraw();
        }

        public void RefreshMinimap() => RebuildMinimap();

        public Vector2 CellToMinimap(Vector2I cell)
            => CellToMinimap(cell, MapRect(), EffectiveBoundsOrigin(), EffectiveBoundsSize());

        public int VisibleRoadCount()
        {
            ResolveReferences();
            RefreshSnapshots();
            return _roadCells.Count;
        }

        public int VisibleJobCount()
        {
            ResolveReferences();
            RefreshSnapshots();
            return _jobCells.Count;
        }

        public int VisibleUnitCount()
        {
            if (_unitsRoot == null)
                return 0;

            int count = 0;
            foreach (Node child in _unitsRoot.GetChildren())
                if (child is Node2D)
                    count++;
            return count;
        }

        /// <summary>
        /// Rebuilds whichever snapshot its owning system marked dirty. Reading
        /// the marshalled Godot collections happens HERE, on change, never in
        /// the per-frame draw.
        /// </summary>
        private void RefreshSnapshots()
        {
            if (_roadsDirty)
            {
                _roadCells.Clear();
                if (_roads != null)
                {
                    foreach (Vector2I cell in _roads.GetRoadCells())
                        _roadCells.Add(cell);
                }
                _roadsDirty = false;
            }

            if (_jobsDirty)
            {
                _jobCells.Clear();
                if (_jobs != null)
                {
                    foreach (Godot.Collections.Dictionary job in _jobs.GetJobs())
                    {
                        Vector2I cell = GridVariantReader.Vector2I(job, "cell", new Vector2I(int.MinValue, int.MinValue));
                        if (cell.X != int.MinValue && cell.Y != int.MinValue)
                            _jobCells.Add(cell);
                    }
                }
                _jobsDirty = false;
            }

            if (_selectionDirty)
            {
                _selectedCells.Clear();
                if (_selection != null)
                {
                    foreach (Vector2I cell in _selection.GetSelectedCells())
                        _selectedCells.Add(cell);
                }
                _selectionDirty = false;
            }

            if (_terrainDirty)
            {
                BakeTerrain();
                _terrainDirty = false;
            }
        }

        /// <summary>One pixel per cell, scaled up by the draw. Baked on change only.</summary>
        private void BakeTerrain()
        {
            _terrainTexture = null;
            if (!ShowTerrain || _cells == null)
                return;

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();
            if (size.X > 1024 || size.Y > 1024)
                return;

            var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    string kind = NormalizeKind(_cells.GetTerrainKind(new Vector2I(origin.X + x, origin.Y + y)));
                    image.SetPixel(x, y, TerrainColors.TryGetValue(kind, out Color colour)
                        ? colour
                        : Colors.Transparent);
                }
            }

            _terrainTexture = ImageTexture.CreateFromImage(image);
        }

        private void DrawUnits(Rect2 mapRect, Vector2I origin, Vector2I size)
        {
            if (_unitsRoot == null)
                return;

            foreach (Node child in _unitsRoot.GetChildren())
            {
                if (child is not Node2D unit)
                    continue;

                Vector2I cell = _grid?.WorldToCell(unit.GlobalPosition) ?? WorldToApproxCell(unit.GlobalPosition);
                DrawDot(mapRect, CellToMinimap(cell, mapRect, origin, size), 3f, UnitColor);
            }
        }

        private void DrawCameraView(Rect2 mapRect, Vector2I origin, Vector2I size)
        {
            if (_camera == null || GetViewport() == null)
                return;

            Vector2 zoom = EffectiveCameraZoom(_camera.Zoom);
            Vector2 half = GetViewport().GetVisibleRect().Size * 0.5f / zoom;
            Vector2 topLeftWorld = _camera.GlobalPosition - half;
            Vector2 bottomRightWorld = _camera.GlobalPosition + half;
            Vector2I topLeft = _grid?.WorldToCell(topLeftWorld) ?? WorldToApproxCell(topLeftWorld);
            Vector2I bottomRight = _grid?.WorldToCell(bottomRightWorld) ?? WorldToApproxCell(bottomRightWorld);
            Vector2 a = CellToMinimap(topLeft, mapRect, origin, size);
            Vector2 b = CellToMinimap(bottomRight, mapRect, origin, size);
            var rect = new Rect2(a, b - a).Abs();
            DrawRect(rect, CameraColor, filled: false, width: 1.5f);
        }

        private void DrawDot(Rect2 mapRect, Vector2 position, float radius, Color color)
        {
            if (!mapRect.HasPoint(position))
                return;

            DrawCircle(position, radius, color);
        }

        private Vector2 CellToMinimap(Vector2I cell, Rect2 mapRect, Vector2I origin, Vector2I size)
        {
            float x = ((cell.X - origin.X) + 0.5f) / Mathf.Max(1, size.X);
            float y = ((cell.Y - origin.Y) + 0.5f) / Mathf.Max(1, size.Y);
            return mapRect.Position + new Vector2(Mathf.Clamp(x, 0f, 1f) * mapRect.Size.X, Mathf.Clamp(y, 0f, 1f) * mapRect.Size.Y);
        }

        private Vector2I WorldToApproxCell(Vector2 worldPosition)
            => new(
                Mathf.FloorToInt(float.IsFinite(worldPosition.X) ? worldPosition.X : 0f),
                Mathf.FloorToInt(float.IsFinite(worldPosition.Y) ? worldPosition.Y : 0f));

        private Rect2 MapRect()
        {
            Vector2 size = Size;
            if (size.X <= 0f || size.Y <= 0f || !float.IsFinite(size.X) || !float.IsFinite(size.Y))
                size = CustomMinimumSize.X > 0f && CustomMinimumSize.Y > 0f ? CustomMinimumSize : new Vector2(160, 120);
            return new Rect2(Vector2.Zero, size);
        }

        private static Vector2 EffectiveCameraZoom(Vector2 zoom)
            => new(
                Mathf.Max(0.001f, float.IsFinite(zoom.X) ? Mathf.Abs(zoom.X) : 1f),
                Mathf.Max(0.001f, float.IsFinite(zoom.Y) ? Mathf.Abs(zoom.Y) : 1f));

        private static string NormalizeKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty ? GetNodeOrNull<GridProjectionComponent>(GridPath) : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;
            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty ? GetNodeOrNull<GridNavigationComponent>(NavigationPath) : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;
            if (_roads == null || !GodotObject.IsInstanceValid(_roads))
                _roads = !RoadPath.IsEmpty ? GetNodeOrNull<GridRoadComponent>(RoadPath) : IsInsideTree() ? EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene) : null;
            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty ? GetNodeOrNull<GridSelectionComponent>(SelectionPath) : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;
            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs))
                _jobs = !JobQueuePath.IsEmpty ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath) : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;
            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty ? GetNodeOrNull<GridCellDataComponent>(CellDataPath) : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;
            if (_unitsRoot == null || !GodotObject.IsInstanceValid(_unitsRoot))
                _unitsRoot = !UnitsRootPath.IsEmpty ? GetNodeOrNull<Node>(UnitsRootPath) : null;
            if (_camera == null || !GodotObject.IsInstanceValid(_camera))
                _camera = !CameraPath.IsEmpty ? GetNodeOrNull<Camera2D>(CameraPath) : GetViewport()?.GetCamera2D();

            ConnectSignals();
        }

        /// <summary>Idempotent, like the TileMapLayer bridge: re-run whenever a source resolves.</summary>
        private void ConnectSignals()
        {
            if (Engine.IsEditorHint())
                return;

            if (_roads != _connectedRoads)
            {
                if (_connectedRoads != null && GodotObject.IsInstanceValid(_connectedRoads))
                {
                    _connectedRoads.RoadChanged -= OnRoadChanged;
                    _connectedRoads.RoadsChanged -= OnRoadsChanged;
                }
                if (_roads != null)
                {
                    _roads.RoadChanged += OnRoadChanged;
                    _roads.RoadsChanged += OnRoadsChanged;
                }
                _connectedRoads = _roads;
                _roadsDirty = true;
            }

            if (_jobs != _connectedJobs)
            {
                if (_connectedJobs != null && GodotObject.IsInstanceValid(_connectedJobs))
                    _connectedJobs.QueueChanged -= OnQueueChanged;
                if (_jobs != null)
                    _jobs.QueueChanged += OnQueueChanged;
                _connectedJobs = _jobs;
                _jobsDirty = true;
            }

            if (_selection != _connectedSelection)
            {
                if (_connectedSelection != null && GodotObject.IsInstanceValid(_connectedSelection))
                    _connectedSelection.SelectionChanged -= OnSelectionChanged;
                if (_selection != null)
                    _selection.SelectionChanged += OnSelectionChanged;
                _connectedSelection = _selection;
                _selectionDirty = true;
            }

            if (_cells != _connectedCells)
            {
                if (_connectedCells != null && GodotObject.IsInstanceValid(_connectedCells))
                {
                    _connectedCells.CellChanged -= OnCellChanged;
                    _connectedCells.CellsChanged -= OnCellsChanged;
                }
                if (_cells != null)
                {
                    _cells.CellChanged += OnCellChanged;
                    _cells.CellsChanged += OnCellsChanged;
                }
                _connectedCells = _cells;
                _terrainDirty = true;
            }
        }

        private void DisconnectSignals()
        {
            if (_connectedRoads != null && GodotObject.IsInstanceValid(_connectedRoads))
            {
                _connectedRoads.RoadChanged -= OnRoadChanged;
                _connectedRoads.RoadsChanged -= OnRoadsChanged;
            }
            if (_connectedJobs != null && GodotObject.IsInstanceValid(_connectedJobs))
                _connectedJobs.QueueChanged -= OnQueueChanged;
            if (_connectedSelection != null && GodotObject.IsInstanceValid(_connectedSelection))
                _connectedSelection.SelectionChanged -= OnSelectionChanged;
            if (_connectedCells != null && GodotObject.IsInstanceValid(_connectedCells))
            {
                _connectedCells.CellChanged -= OnCellChanged;
                _connectedCells.CellsChanged -= OnCellsChanged;
            }

            _connectedRoads = null;
            _connectedJobs = null;
            _connectedSelection = null;
            _connectedCells = null;
        }

        private void OnRoadChanged(int x, int y, string kind, bool hasRoad) { _roadsDirty = true; QueueRedraw(); }
        private void OnRoadsChanged() { _roadsDirty = true; QueueRedraw(); }
        private void OnQueueChanged(int queued, int claimed, int completed) { _jobsDirty = true; QueueRedraw(); }
        private void OnSelectionChanged(int count) { _selectionDirty = true; QueueRedraw(); }
        private void OnCellChanged(int x, int y) { _terrainDirty = true; QueueRedraw(); }
        private void OnCellsChanged() { _terrainDirty = true; QueueRedraw(); }
    }
}
