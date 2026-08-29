using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Lightweight HUD overview for top-down/isometric grid worlds. It draws
    /// roads, selected cells, jobs, units, and the camera viewport from existing
    /// Godot nodes without requiring a TileMap or custom minimap scene.
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
        [Export] public bool AutoRefresh { get; set; } = true;
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public bool PreferNavigationBounds { get; set; } = true;
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
        private Node? _unitsRoot;
        private Camera2D? _camera;

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

        public override void _Process(double delta)
        {
            if (AutoRefresh || Engine.IsEditorHint())
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

            Rect2 mapRect = MapRect();
            DrawRect(mapRect, BackgroundColor, filled: true);
            DrawRect(mapRect, BorderColor, filled: false, width: 1.5f);

            if (ShowRoads)
                DrawRoads(mapRect);
            if (ShowJobs)
                DrawJobs(mapRect);
            if (ShowSelection)
                DrawSelection(mapRect);
            if (ShowUnits)
                DrawUnits(mapRect);
            if (ShowCameraView)
                DrawCameraView(mapRect);
        }

        public void RebuildMinimap()
        {
            ResolveReferences();
            QueueRedraw();
        }

        public void RefreshMinimap() => RebuildMinimap();

        public Vector2 CellToMinimap(Vector2I cell)
            => CellToMinimap(cell, MapRect(), EffectiveBoundsOrigin(), EffectiveBoundsSize());

        public int VisibleRoadCount()
            => _roads == null ? 0 : _roads.GetRoadCells().Count;

        public int VisibleJobCount()
            => _jobs == null ? 0 : _jobs.GetJobs().Count;

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

        private void DrawRoads(Rect2 mapRect)
        {
            if (_roads == null)
                return;

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();
            foreach (Vector2I cell in _roads.GetRoadCells())
                DrawDot(CellToMinimap(cell, mapRect, origin, size), 2.2f, RoadColor);
        }

        private void DrawSelection(Rect2 mapRect)
        {
            if (_selection == null)
                return;

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();
            foreach (Vector2I cell in _selection.GetSelectedCells())
                DrawDot(CellToMinimap(cell, mapRect, origin, size), 2.8f, SelectionColor);
        }

        private void DrawJobs(Rect2 mapRect)
        {
            if (_jobs == null)
                return;

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();
            foreach (Godot.Collections.Dictionary job in _jobs.GetJobs())
            {
                Vector2I cell = GridVariantReader.Vector2I(job, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                DrawDot(CellToMinimap(cell, mapRect, origin, size), 2.6f, JobColor);
            }
        }

        private void DrawUnits(Rect2 mapRect)
        {
            if (_unitsRoot == null)
                return;

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();
            foreach (Node child in _unitsRoot.GetChildren())
            {
                if (child is not Node2D unit)
                    continue;

                Vector2I cell = _grid?.WorldToCell(unit.GlobalPosition) ?? WorldToApproxCell(unit.GlobalPosition);
                DrawDot(CellToMinimap(cell, mapRect, origin, size), 3f, UnitColor);
            }
        }

        private void DrawCameraView(Rect2 mapRect)
        {
            if (_camera == null || GetViewport() == null)
                return;

            Vector2I origin = EffectiveBoundsOrigin();
            Vector2I size = EffectiveBoundsSize();
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

        private void DrawDot(Vector2 position, float radius, Color color)
        {
            Rect2 mapRect = MapRect();
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
            if (_unitsRoot == null || !GodotObject.IsInstanceValid(_unitsRoot))
                _unitsRoot = !UnitsRootPath.IsEmpty ? GetNodeOrNull<Node>(UnitsRootPath) : null;
            if (_camera == null || !GodotObject.IsInstanceValid(_camera))
                _camera = !CameraPath.IsEmpty ? GetNodeOrNull<Camera2D>(CameraPath) : GetViewport()?.GetCamera2D();
        }
    }
}
