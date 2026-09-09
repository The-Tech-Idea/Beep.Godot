using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Lightweight visual overlay for GridCellDataComponent. It draws cell-state
    /// fills/outline cues for farming and builder workflows before a project has
    /// custom TileMap art for every state.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCellOverlayComponent : Node2D
    {
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public bool DrawCells { get; set; } = true;
        [Export] public bool DrawOutlines { get; set; } = true;
        /// <summary>Draw every stored cell instead of only those inside the camera window. Off by
        /// default so a large map draws the viewport, not the million records behind it; enable it
        /// for a tiny debug map, or when this overlay has no camera to cull against.</summary>
        [Export] public bool DrawAll { get; set; } = false;
        [Export] public Color ClearedColor { get; set; } = new(0.46f, 0.34f, 0.2f, 0.18f);
        [Export] public Color TilledColor { get; set; } = new(0.42f, 0.25f, 0.12f, 0.36f);
        [Export] public Color WateredColor { get; set; } = new(0.24f, 0.48f, 0.95f, 0.28f);
        [Export] public Color PlantedColor { get; set; } = new(0.22f, 0.66f, 0.28f, 0.32f);
        [Export] public Color HarvestReadyColor { get; set; } = new(1f, 0.82f, 0.24f, 0.42f);
        [Export] public Color BlockedColor { get; set; } = new(0.85f, 0.18f, 0.16f, 0.32f);
        [Export] public Color OutlineColor { get; set; } = new(0.08f, 0.08f, 0.08f, 0.35f);
        [Export(PropertyHint.Range, "0.5,6,0.1")] public float OutlineWidth { get; set; } = 1.5f;

        private GridProjectionComponent? _grid;
        private GridCellDataComponent? _cells;

        public float EffectiveOutlineWidth => Mathf.Max(0f, float.IsFinite(OutlineWidth) ? OutlineWidth : 1.5f);

        public override void _Ready()
        {
            ResolveReferences();
            SetProcess(Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectCells();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
                QueueRedraw();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            return System.Array.Empty<string>();
        }

        /// <summary>How many cells the last _Draw actually painted. Test hook for the culling probe.</summary>
        internal int LastDrawnCellCount { get; private set; }

        public override void _Draw()
        {
            if (!DrawCells)
                return;

            int drawn = 0;
            foreach ((Vector2I cell, GridCellDataComponent.CellFlags flags) in VisibleCells())
            {
                DrawCell(cell, ColorForFlags((int)flags));
                drawn++;
            }
            LastDrawnCellCount = drawn;
        }

        /// <summary>The cells this overlay would paint this frame: those with a visible fill or an
        /// outline, culled to the camera window unless <see cref="DrawAll"/> is set (or there is no
        /// camera to cull against). One owner of "what draws", shared by _Draw and the culling guard.</summary>
        internal IEnumerable<(Vector2I Cell, GridCellDataComponent.CellFlags Flags)> VisibleCells()
        {
            ResolveReferences();
            if (_grid == null || _cells == null)
                yield break;

            Rect2I visible = default;
            bool cull = !DrawAll && _grid.TryGetVisibleCellRect(out visible);
            // The typed view: GetCells marshals one Godot Dictionary per cell; EnumerateFlags is the
            // per-draw shape, and the window filter keeps the paint work proportional to the view.
            foreach ((Vector2I cell, GridCellDataComponent.CellFlags flags) in _cells.EnumerateFlags())
            {
                if (cull && !visible.HasPoint(cell))
                    continue;
                if (ColorForFlags((int)flags).A <= 0f && !DrawOutlines)
                    continue;
                yield return (cell, flags);
            }
        }

        public int VisibleCellCount()
        {
            ResolveReferences();
            if (_cells == null)
                return 0;

            int count = 0;
            foreach ((Vector2I _, GridCellDataComponent.CellFlags flags) in _cells.EnumerateFlags())
            {
                if (ColorForFlags((int)flags).A > 0f)
                    count++;
            }
            return count;
        }

        public Color ColorForCell(Vector2I cell)
        {
            ResolveReferences();
            return _cells == null ? Colors.Transparent : ColorForFlags(_cells.GetFlags(cell));
        }

        public Color ColorForFlags(int flags)
        {
            var cellFlags = (GridCellDataComponent.CellFlags)flags;
            if ((cellFlags & GridCellDataComponent.CellFlags.Blocked) != 0) return BlockedColor;
            if ((cellFlags & GridCellDataComponent.CellFlags.HarvestReady) != 0) return HarvestReadyColor;
            if ((cellFlags & GridCellDataComponent.CellFlags.Planted) != 0) return PlantedColor;
            if ((cellFlags & GridCellDataComponent.CellFlags.Watered) != 0) return WateredColor;
            if ((cellFlags & GridCellDataComponent.CellFlags.Tilled) != 0) return TilledColor;
            if ((cellFlags & GridCellDataComponent.CellFlags.Cleared) != 0) return ClearedColor;
            return Colors.Transparent;
        }

        private void DrawCell(Vector2I cell, Color fill)
        {
            if (_grid == null)
                return;

            System.Span<Vector2> gridCorners = stackalloc Vector2[4];
            int n = _grid.CellCorners(cell, gridCorners);
            if (n < 3)
                return;
            var points = new Vector2[n];
            for (int i = 0; i < n; i++)
                points[i] = ToLocal(_grid.ToGlobal(gridCorners[i]));

            if (fill.A > 0f)
                DrawColoredPolygon(points, fill);

            if (DrawOutlines)
                DrawPolyline(points, OutlineColor, EffectiveOutlineWidth, true);
        }

        private void ResolveReferences()
        {
            EntityComponent.Resolve(this, GridPath, ref _grid);
            EntityComponent.Resolve(this, CellDataPath, ref _cells);

            ConnectCells();
        }

        private GridCellDataComponent? _connectedCells;

        /// <summary>
        /// At runtime the overlay redraws when the CELLS say so. It used to
        /// repaint only from the editor's per-frame loop, so tilled and watered
        /// state drawn at startup silently went stale during play.
        /// </summary>
        private void ConnectCells()
        {
            if (Engine.IsEditorHint() || _cells == _connectedCells)
                return;

            DisconnectCells();
            if (_cells != null)
            {
                _cells.CellChanged += OnCellChanged;
                _cells.CellsChanged += OnCellsChanged;
            }
            _connectedCells = _cells;
            QueueRedraw();
        }

        private void DisconnectCells()
        {
            if (_connectedCells != null && GodotObject.IsInstanceValid(_connectedCells))
            {
                _connectedCells.CellChanged -= OnCellChanged;
                _connectedCells.CellsChanged -= OnCellsChanged;
            }
            _connectedCells = null;
        }

        private void OnCellChanged(int x, int y, int kind) => QueueRedraw();
        private void OnCellsChanged(int kind, Godot.Collections.Array<Vector2I> chunks)
        {
            if (((TerrainChangeKind)kind & TerrainChangeKind.Content) != 0) QueueRedraw();
        }
    }
}
