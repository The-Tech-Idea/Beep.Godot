using Godot;

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

        public override void _Draw()
        {
            if (!DrawCells)
                return;

            ResolveReferences();
            if (_grid == null || _cells == null)
                return;

            foreach (Godot.Collections.Dictionary cellData in _cells.GetCells())
            {
                if (!cellData.ContainsKey("cell"))
                    continue;

                Vector2I cell = GridVariantReader.Vector2I(cellData, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                int flags = GridVariantReader.Int(cellData, "flags", 0);
                Color fill = ColorForFlags(flags);
                if (fill.A <= 0f && !DrawOutlines)
                    continue;

                DrawCell(cell, fill);
            }
        }

        public int VisibleCellCount()
        {
            ResolveReferences();
            if (_cells == null)
                return 0;

            int count = 0;
            foreach (Godot.Collections.Dictionary cellData in _cells.GetCells())
            {
                int flags = GridVariantReader.Int(cellData, "flags", 0);
                if (ColorForFlags(flags).A > 0f)
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

            Vector2[] gridCorners = _grid.CellCorners(cell);
            var points = new Vector2[gridCorners.Length];
            for (int i = 0; i < gridCorners.Length; i++)
                points[i] = ToLocal(_grid.ToGlobal(gridCorners[i]));

            if (fill.A > 0f)
                DrawColoredPolygon(points, fill);

            if (DrawOutlines)
                DrawPolyline(points, OutlineColor, EffectiveOutlineWidth, true);
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;
        }
    }
}
