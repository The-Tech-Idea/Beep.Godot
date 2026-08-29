using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Draws a lightweight cell cursor for the active grid interaction. It uses
    /// GridProjectionComponent.CellCorners, so the same node works for top-down
    /// square cells and isometric diamond cells.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridInteractionCursorComponent : Node2D
    {
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public bool DrawCursor { get; set; } = true;
        [Export] public bool HideWhenDisabled { get; set; } = true;
        [Export] public Color SelectColor { get; set; } = new(0.56f, 0.9f, 1f, 0.82f);
        [Export] public Color ToolColor { get; set; } = new(1f, 0.86f, 0.32f, 0.88f);
        [Export] public Color BuildValidColor { get; set; } = new(0.36f, 1f, 0.52f, 0.88f);
        [Export] public Color BuildInvalidColor { get; set; } = new(1f, 0.24f, 0.18f, 0.9f);
        [Export] public Color InspectColor { get; set; } = new(0.85f, 0.72f, 1f, 0.86f);
        [Export] public Color FillColor { get; set; } = new(1f, 1f, 1f, 0.08f);
        [Export(PropertyHint.Range, "0.5,8,0.1")] public float OutlineWidth { get; set; } = 2.5f;

        private static readonly Vector2I InvalidCell = new(int.MinValue, int.MinValue);
        private GridProjectionComponent? _grid;
        private GridInteractionModeComponent? _interaction;
        private GridSelectionComponent? _selection;
        private GridPlacementComponent? _placement;

        public override void _Ready()
        {
            ResolveReferences();
            SetProcess(true);
            UpdateConfigurationWarnings();
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };
            return Array.Empty<string>();
        }

        public override void _Draw()
        {
            if (!DrawCursor)
                return;

            ResolveReferences();
            Vector2I cell = CurrentCell();
            if (_grid == null || cell == InvalidCell || !ShouldDrawForMode())
                return;

            Vector2[] gridCorners = _grid.CellCorners(cell);
            var points = new Vector2[gridCorners.Length];
            for (int i = 0; i < gridCorners.Length; i++)
                points[i] = ToLocal(_grid.ToGlobal(gridCorners[i]));

            if (FillColor.A > 0f)
                DrawColoredPolygon(points, FillColor);
            DrawPolyline(points, CurrentOutlineColor(), OutlineWidth, true);
        }

        public Vector2I CurrentCell()
        {
            ResolveReferences();
            if (_placement != null && _placement.State == GridPlacementComponent.PlacementState.Placing)
                return _placement.CurrentCell;

            return _selection?.HoverCell ?? InvalidCell;
        }

        public Color CurrentOutlineColor()
        {
            ResolveReferences();
            if (_interaction == null)
                return SelectColor;

            return _interaction.CurrentMode switch
            {
                GridInteractionModeComponent.InteractionMode.Build => _placement != null && _placement.CurrentCellValid ? BuildValidColor : BuildInvalidColor,
                GridInteractionModeComponent.InteractionMode.Tool => ToolColor,
                GridInteractionModeComponent.InteractionMode.Inspect => InspectColor,
                GridInteractionModeComponent.InteractionMode.Disabled => new Color(1f, 1f, 1f, 0.16f),
                _ => SelectColor
            };
        }

        public bool ShouldDrawForMode()
        {
            ResolveReferences();
            if (_interaction == null)
                return true;

            return _interaction.CurrentMode != GridInteractionModeComponent.InteractionMode.Disabled || !HideWhenDisabled;
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_interaction == null || !GodotObject.IsInstanceValid(_interaction))
                _interaction = !InteractionModePath.IsEmpty
                    ? GetNodeOrNull<GridInteractionModeComponent>(InteractionModePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridInteractionModeComponent>(GetTree()?.CurrentScene) : null;

            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty
                    ? GetNodeOrNull<GridSelectionComponent>(SelectionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;
        }
    }
}
