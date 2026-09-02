using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Hover, click, and rectangular cell selection for top-down/isometric grids.
    /// Pair with GridProjectionComponent for map editors, tactics games, RTS units,
    /// farming plots, builder tools, and tile/cell inspectors.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridSelectionComponent : Node2D
    {
        public enum SelectionMode
        {
            Single,
            Rectangle
        }

        [Signal] public delegate void HoverCellChangedEventHandler(int x, int y);
        [Signal] public delegate void CellSelectedEventHandler(int x, int y);
        [Signal] public delegate void SelectionChangedEventHandler(int count);
        [Signal] public delegate void DragSelectionStartedEventHandler(int x, int y);
        [Signal] public delegate void DragSelectionFinishedEventHandler(int count);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public bool UseMouseInput { get; set; } = true;
        [Export] public SelectionMode Mode { get; set; } = SelectionMode.Rectangle;
        [Export] public bool AdditiveWithShift { get; set; } = true;
        [Export] public bool ClearOnSingleSelect { get; set; } = true;
        [Export] public bool DrawSelection { get; set; } = true;
        [Export] public bool DrawHover { get; set; } = true;
        [Export] public Color HoverColor { get; set; } = new(1f, 1f, 1f, 0.65f);
        [Export] public Color SelectedFillColor { get; set; } = new(0.25f, 0.72f, 1f, 0.22f);
        [Export] public Color SelectedOutlineColor { get; set; } = new(0.55f, 0.9f, 1f, 0.78f);
        [Export] public Color DragFillColor { get; set; } = new(1f, 0.85f, 0.25f, 0.18f);
        [Export] public Color DragOutlineColor { get; set; } = new(1f, 0.85f, 0.25f, 0.75f);

        public Vector2I HoverCell { get; private set; } = InvalidCell;
        public bool IsDragging { get; private set; }
        public Vector2I DragStartCell { get; private set; } = InvalidCell;
        public Vector2I DragEndCell { get; private set; } = InvalidCell;

        private static readonly Vector2I InvalidCell = new(int.MinValue, int.MinValue);
        private readonly HashSet<Vector2I> _selected = new();
        private GridProjectionComponent? _grid;

        public override void _Ready()
        {
            ResolveGrid();
            SetProcess(!Engine.IsEditorHint());
            SetProcessUnhandledInput(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            if (!UseMouseInput) return;
            UpdateHoverFromWorld(GetGlobalMousePosition());
            if (IsDragging)
                UpdateDrag(HoverCell);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!UseMouseInput) return;

            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            {
                Vector2I cell = WorldToCell(mouse.GlobalPosition);
                if (mouse.Pressed)
                {
                    bool additive = AdditiveWithShift && (Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.Ctrl));
                    if (Mode == SelectionMode.Rectangle)
                        BeginDrag(cell, additive);
                    else
                        SelectCell(cell, additive || !ClearOnSingleSelect);
                    GetViewport().SetInputAsHandled();
                }
                else if (IsDragging)
                {
                    FinishDrag(cell);
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                ClearSelection();
                CancelDrag();
                GetViewport().SetInputAsHandled();
            }
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };

            return System.Array.Empty<string>();
        }

        public void UpdateHoverFromWorld(Vector2 worldPosition)
        {
            Vector2I cell = WorldToCell(worldPosition);
            if (cell == HoverCell)
                return;

            HoverCell = cell;
            EmitSignal(SignalName.HoverCellChanged, cell.X, cell.Y);
            QueueRedraw();
        }

        public void SelectCell(Vector2I cell, bool additive = false)
        {
            if (!additive)
                _selected.Clear();

            _selected.Add(cell);
            EmitSignal(SignalName.CellSelected, cell.X, cell.Y);
            EmitSignal(SignalName.SelectionChanged, _selected.Count);
            QueueRedraw();
        }

        public void ToggleCell(Vector2I cell)
        {
            if (!_selected.Remove(cell))
                _selected.Add(cell);

            EmitSignal(SignalName.SelectionChanged, _selected.Count);
            QueueRedraw();
        }

        public void BeginDrag(Vector2I startCell, bool additive = false)
        {
            if (!additive)
                _selected.Clear();

            IsDragging = true;
            DragStartCell = startCell;
            DragEndCell = startCell;
            EmitSignal(SignalName.DragSelectionStarted, startCell.X, startCell.Y);
            QueueRedraw();
        }

        public void UpdateDrag(Vector2I endCell)
        {
            if (!IsDragging)
                return;

            DragEndCell = endCell;
            QueueRedraw();
        }

        public void FinishDrag(Vector2I endCell)
        {
            if (!IsDragging)
                return;

            DragEndCell = endCell;
            foreach (Vector2I cell in CellsInRectangle(DragStartCell, DragEndCell))
                _selected.Add(cell);

            IsDragging = false;
            EmitSignal(SignalName.SelectionChanged, _selected.Count);
            EmitSignal(SignalName.DragSelectionFinished, _selected.Count);
            QueueRedraw();
        }

        public void CancelDrag()
        {
            IsDragging = false;
            DragStartCell = InvalidCell;
            DragEndCell = InvalidCell;
            QueueRedraw();
        }

        public void ClearSelection()
        {
            if (_selected.Count == 0)
                return;

            _selected.Clear();
            EmitSignal(SignalName.SelectionChanged, 0);
            QueueRedraw();
        }

        public bool IsSelected(Vector2I cell) => _selected.Contains(cell);

        public Godot.Collections.Array<Vector2I> GetSelectedCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in _selected)
                cells.Add(cell);
            return cells;
        }

        public Godot.Collections.Array<Vector2I> GetDragCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            if (!IsDragging)
                return cells;

            foreach (Vector2I cell in CellsInRectangle(DragStartCell, DragEndCell))
                cells.Add(cell);
            return cells;
        }

        public static Godot.Collections.Array<Vector2I> CellsInRect(Vector2I a, Vector2I b)
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in CellsInRectangle(a, b))
                cells.Add(cell);
            return cells;
        }

        public override void _Draw()
        {
            if (!DrawSelection)
                return;

            ResolveGrid();
            if (_grid == null)
                return;

            foreach (Vector2I cell in _selected)
                DrawCell(cell, SelectedFillColor, SelectedOutlineColor, 2f);

            if (IsDragging)
            {
                foreach (Vector2I cell in CellsInRectangle(DragStartCell, DragEndCell))
                    DrawCell(cell, DragFillColor, DragOutlineColor, 1.5f);
            }

            if (DrawHover && HoverCell != InvalidCell)
                DrawCell(HoverCell, Colors.Transparent, HoverColor, 2f);
        }

        private Vector2I WorldToCell(Vector2 worldPosition)
        {
            ResolveGrid();
            return _grid?.WorldToCell(worldPosition) ?? InvalidCell;
        }

        private void ResolveGrid()
        {
            if (_grid != null && GodotObject.IsInstanceValid(_grid))
                return;

            if (!GridPath.IsEmpty)
                _grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
            else if (IsInsideTree())
                _grid = EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene);
        }

        private void DrawCell(Vector2I cell, Color fill, Color outline, float width)
        {
            if (_grid == null)
                return;

            Vector2[] localToGrid = _grid.CellCorners(cell);
            var points = new Vector2[localToGrid.Length];
            for (int i = 0; i < localToGrid.Length; i++)
                points[i] = ToLocal(_grid.ToGlobal(localToGrid[i]));

            if (fill.A > 0f)
                DrawColoredPolygon(points, fill);

            DrawPolyline(points, outline, width, true);
        }

        private static IEnumerable<Vector2I> CellsInRectangle(Vector2I a, Vector2I b)
        {
            int minX = Mathf.Min(a.X, b.X);
            int maxX = Mathf.Max(a.X, b.X);
            int minY = Mathf.Min(a.Y, b.Y);
            int maxY = Mathf.Max(a.Y, b.Y);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    yield return new Vector2I(x, y);
        }
    }
}
