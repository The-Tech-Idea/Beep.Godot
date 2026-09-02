using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Coordinates map interaction modes so selection, tools, and placement do
    /// not all consume the same click. Attach one scene-level node and point it
    /// at the existing grid components.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridInteractionModeComponent : Node
    {
        public enum InteractionMode
        {
            Select,
            Tool,
            Build,
            Inspect,
            Disabled
        }

        [Signal] public delegate void ModeChangedEventHandler(int mode);
        [Signal] public delegate void InteractionAppliedEventHandler(string mode, int x, int y);
        [Signal] public delegate void InteractionRejectedEventHandler(string mode, int x, int y, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath ToolActionPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public InteractionMode CurrentMode { get; set; } = InteractionMode.Select;
        [Export] public bool UseMouseInput { get; set; } = true;
        [Export] public bool ManageChildMouseInput { get; set; } = true;
        [Export] public bool AdditiveSelectionWithShift { get; set; } = true;
        [Export] public bool ClearSelectionWhenLeavingSelect { get; set; } = false;

        private static readonly Vector2I InvalidCell = new(int.MinValue, int.MinValue);
        private GridProjectionComponent? _grid;
        private GridSelectionComponent? _selection;
        private GridToolActionComponent? _tools;
        private GridPlacementComponent? _placement;

        public override void _Ready()
        {
            ResolveReferences();
            ApplyChildInputOwnership();
            SetProcess(!Engine.IsEditorHint());
            SetProcessUnhandledInput(!Engine.IsEditorHint() && UseMouseInput);
            UpdateConfigurationWarnings();
        }

        public override void _Process(double delta)
        {
            if (!UseMouseInput || Engine.IsEditorHint())
                return;

            ResolveReferences();
            Vector2I cell = MouseCell();
            if (CurrentMode is InteractionMode.Select or InteractionMode.Tool or InteractionMode.Inspect)
                _selection?.UpdateHoverFromWorld(MouseWorldPosition());
            if (CurrentMode == InteractionMode.Build && _placement != null && _placement.State == GridPlacementComponent.PlacementState.Placing)
                _placement.MovePreviewToCell(cell);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!UseMouseInput || CurrentMode == InteractionMode.Disabled)
                return;

            if (@event is InputEventMouseButton { Pressed: true } mouse)
            {
                Vector2I cell = MouseCell();
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    bool additive = AdditiveSelectionWithShift && (Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.Ctrl));
                    if (HandlePrimaryCell(cell, additive))
                        GetViewport()?.SetInputAsHandled();
                }
                else if (mouse.ButtonIndex == MouseButton.Right)
                {
                    if (HandleSecondaryCell(cell))
                        GetViewport()?.SetInputAsHandled();
                }
            }
            else if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                if (CancelCurrentInteraction())
                    GetViewport()?.SetInputAsHandled();
            }
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };
            return Array.Empty<string>();
        }

        public void SetMode(InteractionMode mode)
        {
            if (CurrentMode == mode)
                return;

            if (ClearSelectionWhenLeavingSelect && CurrentMode == InteractionMode.Select && mode != InteractionMode.Select)
                _selection?.ClearSelection();

            CurrentMode = mode;
            ApplyChildInputOwnership();
            EmitSignal(SignalName.ModeChanged, (int)mode);
        }

        public void SelectMode() => SetMode(InteractionMode.Select);
        public void ToolMode() => SetMode(InteractionMode.Tool);
        public void BuildMode() => SetMode(InteractionMode.Build);
        public void InspectMode() => SetMode(InteractionMode.Inspect);
        public void DisableInteractions() => SetMode(InteractionMode.Disabled);

        public bool HandlePrimaryCell(Vector2I cell, bool additive = false)
        {
            ResolveReferences();
            if (cell == InvalidCell)
                return Reject("invalid_cell", cell);

            return CurrentMode switch
            {
                InteractionMode.Select => SelectCell(cell, additive),
                InteractionMode.Inspect => SelectCell(cell, additive: false),
                InteractionMode.Tool => ApplyToolAtCell(cell),
                InteractionMode.Build => ConfirmBuildAtCell(cell),
                _ => Reject("disabled", cell)
            };
        }

        public bool HandleSecondaryCell(Vector2I cell)
        {
            ResolveReferences();
            if (CurrentMode == InteractionMode.Build && _placement?.State == GridPlacementComponent.PlacementState.Placing)
            {
                _placement.CancelPlacement();
                EmitSignal(SignalName.InteractionApplied, CurrentMode.ToString(), cell.X, cell.Y);
                return true;
            }

            if (CurrentMode is InteractionMode.Select or InteractionMode.Inspect && _selection != null)
            {
                _selection.ClearSelection();
                EmitSignal(SignalName.InteractionApplied, CurrentMode.ToString(), cell.X, cell.Y);
                return true;
            }

            return false;
        }

        public bool CancelCurrentInteraction()
        {
            ResolveReferences();
            if (_placement?.State == GridPlacementComponent.PlacementState.Placing)
            {
                _placement.CancelPlacement();
                return true;
            }

            if (_selection?.IsDragging == true)
            {
                _selection.CancelDrag();
                return true;
            }

            return false;
        }

        public bool BeginDragAtCell(Vector2I cell, bool additive = false)
        {
            ResolveReferences();
            if (_selection == null)
                return Reject("missing_selection", cell);

            _selection.BeginDrag(cell, additive);
            EmitSignal(SignalName.InteractionApplied, InteractionMode.Select.ToString(), cell.X, cell.Y);
            return true;
        }

        public bool FinishDragAtCell(Vector2I cell)
        {
            ResolveReferences();
            if (_selection == null || !_selection.IsDragging)
                return Reject("missing_drag", cell);

            _selection.FinishDrag(cell);
            EmitSignal(SignalName.InteractionApplied, InteractionMode.Select.ToString(), cell.X, cell.Y);
            return true;
        }

        public bool ApplyToolAtCell(Vector2I cell)
        {
            ResolveReferences();
            if (_tools == null)
                return Reject("missing_tool_action", cell);

            bool applied = _tools.ApplyToCell(cell, _tools.CurrentAction);
            if (applied)
                EmitSignal(SignalName.InteractionApplied, InteractionMode.Tool.ToString(), cell.X, cell.Y);
            else
                Reject("tool_rejected", cell);
            return applied;
        }

        public bool ConfirmBuildAtCell(Vector2I cell)
        {
            ResolveReferences();
            if (_placement == null)
                return Reject("missing_placement", cell);

            if (_placement.State != GridPlacementComponent.PlacementState.Placing)
                return Reject("not_placing", cell);

            _placement.MovePreviewToCell(cell);
            Node2D? placed = _placement.ConfirmPlacement();
            if (placed == null)
                return Reject("placement_rejected", cell);

            EmitSignal(SignalName.InteractionApplied, InteractionMode.Build.ToString(), cell.X, cell.Y);
            return true;
        }

        private bool SelectCell(Vector2I cell, bool additive)
        {
            if (_selection == null)
                return Reject("missing_selection", cell);

            _selection.SelectCell(cell, additive);
            EmitSignal(SignalName.InteractionApplied, CurrentMode.ToString(), cell.X, cell.Y);
            return true;
        }

        private Vector2I MouseCell()
        {
            ResolveReferences();
            return _grid?.WorldToCell(MouseWorldPosition()) ?? InvalidCell;
        }

        private Vector2 MouseWorldPosition()
        {
            Viewport? viewport = GetViewport();
            if (viewport == null)
                return Vector2.Zero;

            return viewport.GetCanvasTransform().AffineInverse() * viewport.GetMousePosition();
        }

        private bool Reject(string reason, Vector2I cell)
        {
            EmitSignal(SignalName.InteractionRejected, CurrentMode.ToString(), cell.X, cell.Y, reason);
            return false;
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty
                    ? GetNodeOrNull<GridSelectionComponent>(SelectionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;

            if (_tools == null || !GodotObject.IsInstanceValid(_tools))
                _tools = !ToolActionPath.IsEmpty
                    ? GetNodeOrNull<GridToolActionComponent>(ToolActionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridToolActionComponent>(GetTree()?.CurrentScene) : null;

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;
        }

        private void ApplyChildInputOwnership()
        {
            ResolveReferences();
            if (!ManageChildMouseInput)
                return;

            if (_selection != null)
                _selection.UseMouseInput = false;
            if (_tools != null)
                _tools.UseMouseInput = false;
            if (_placement != null)
                _placement.UseMouseInput = false;
        }
    }
}
