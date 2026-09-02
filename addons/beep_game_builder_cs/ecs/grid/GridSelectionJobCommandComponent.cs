using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Converts selected grid cells into jobs. Pair with GridSelectionComponent
    /// and GridJobQueueComponent for settler-style commands such as clear land,
    /// prepare pad, harvest, repair, build, deliver, or inspect.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridSelectionJobCommandComponent : Node
    {
        [Signal] public delegate void JobsQueuedEventHandler(string kind, int count);
        [Signal] public delegate void QueueFailedEventHandler(string reason);

        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public string JobKind { get; set; } = "clear_land";
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float WorkSeconds { get; set; } = 1.5f;
        [Export] public int Priority { get; set; } = 0;
        [Export] public bool ClearSelectionAfterQueue { get; set; } = true;
        [Export] public bool UseKeyboardShortcut { get; set; } = false;
        [Export] public Key QueueShortcutKey { get; set; } = Key.Enter;
        [Export] public bool UseNavigationBounds { get; set; } = true;
        [Export] public bool RejectNavigationBlockedCells { get; set; } = false;
        [Export] public bool TreatCellDataBlockedAsUnqueueable { get; set; } = false;
        [Export] public bool TreatBlockedTerrainKindsAsUnqueueable { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; }
            = GridTerrainRules.DefaultBlockedTerrainKinds();
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();

        private GridSelectionComponent? _selection;
        private GridJobQueueComponent? _queue;
        private GridCellDataComponent? _cellData;
        private GridNavigationComponent? _navigation;

        public float EffectiveWorkSeconds => Mathf.Max(0.01f, float.IsFinite(WorkSeconds) ? WorkSeconds : 1.5f);

        public override void _Ready()
        {
            ResolveReferences();
            SetProcessUnhandledInput(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (SelectionPath.IsEmpty)
                return new[] { "SelectionPath should point to a GridSelectionComponent." };
            if (JobQueuePath.IsEmpty)
                return new[] { "JobQueuePath should point to a GridJobQueueComponent." };
            if (UseNavigationBounds && NavigationPath.IsEmpty)
                return new[] { "NavigationPath should point to a GridNavigationComponent when UseNavigationBounds is enabled." };
            if (WorkSeconds <= 0f)
                return new[] { "WorkSeconds must be greater than zero." };
            return System.Array.Empty<string>();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!UseKeyboardShortcut || @event is not InputEventKey { Pressed: true } key)
                return;

            if (key.Keycode != QueueShortcutKey)
                return;

            QueueSelectedCells();
            GetViewport()?.SetInputAsHandled();
        }

        public int QueueSelectedCells(string kind = "", float workSeconds = -1f, int? priority = null)
        {
            ResolveReferences();
            if (_selection == null)
                return Fail("missing_selection");
            if (_queue == null)
                return Fail("missing_job_queue");

            string resolvedKind = string.IsNullOrWhiteSpace(kind) ? JobKind : kind;
            float resolvedWorkSeconds = workSeconds > 0f && float.IsFinite(workSeconds) ? workSeconds : EffectiveWorkSeconds;
            int resolvedPriority = priority ?? Priority;
            int count = QueueCells(_selection.GetSelectedCells(), resolvedKind, resolvedWorkSeconds, resolvedPriority);

            if (count > 0 && ClearSelectionAfterQueue)
                _selection.ClearSelection();

            return count;
        }

        public int QueueCells(Godot.Collections.Array cells, string kind = "", float workSeconds = -1f, int? priority = null)
        {
            ResolveReferences();
            if (_queue == null)
                return Fail("missing_job_queue");

            string resolvedKind = string.IsNullOrWhiteSpace(kind) ? JobKind : kind;
            float resolvedWorkSeconds = workSeconds > 0f && float.IsFinite(workSeconds) ? workSeconds : EffectiveWorkSeconds;
            int resolvedPriority = priority ?? Priority;
            int count = 0;
            bool sawCandidate = false;

            foreach (Variant value in cells)
            {
                if (!GridVariantReader.TryReadCell(value, out Vector2I cell))
                    continue;

                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                sawCandidate = true;
                if (QueueBlockReason(cell) != null)
                    continue;

                _queue.AddJob(cell, resolvedKind, resolvedWorkSeconds, resolvedPriority);
                count++;
            }

            if (count == 0)
                EmitSignal(SignalName.QueueFailed, sawCandidate ? "no_valid_cells" : "no_cells");
            else
                EmitSignal(SignalName.JobsQueued, resolvedKind, count);

            return count;
        }

        public int QueueCells(Godot.Collections.Array<Vector2I> cells, string kind = "", float workSeconds = -1f, int? priority = null)
        {
            var looseCells = new Godot.Collections.Array();
            foreach (Vector2I cell in cells)
                looseCells.Add(cell);

            return QueueCells(looseCells, kind, workSeconds, priority);
        }

        public int QueueRectangle(Vector2I a, Vector2I b, string kind = "", float workSeconds = -1f, int? priority = null)
            => QueueCells(GridSelectionComponent.CellsInRect(a, b), kind, workSeconds, priority);

        public bool CanQueueJobAt(Vector2I cell, string kind = "")
        {
            ResolveReferences();
            return _queue != null
                && cell.X != int.MinValue
                && cell.Y != int.MinValue
                && QueueBlockReason(cell) == null;
        }

        private int Fail(string reason)
        {
            EmitSignal(SignalName.QueueFailed, reason);
            return 0;
        }

        private void ResolveReferences()
        {
            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty
                    ? GetNodeOrNull<GridSelectionComponent>(SelectionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;

            if (_queue == null || !GodotObject.IsInstanceValid(_queue))
                _queue = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_cellData == null || !GodotObject.IsInstanceValid(_cellData))
                _cellData = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;
        }

        private string? QueueBlockReason(Vector2I cell)
        {
            if (_navigation != null)
            {
                if (UseNavigationBounds && !_navigation.IsInBounds(cell))
                    return "cell_out_of_bounds";

                if (RejectNavigationBlockedCells && _navigation.IsBlocked(cell))
                    return "blocked_cell";
            }

            if (_cellData == null)
                return null;

            if (TreatCellDataBlockedAsUnqueueable
                && _cellData.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return "blocked_cell";

            string terrainKind = GridTerrainRules.Normalize(_cellData.GetTerrainKind(cell));
            if (!GridTerrainRules.IsAllowed(terrainKind, AllowedTerrainKinds))
                return "unqueueable_terrain";

            if (TreatBlockedTerrainKindsAsUnqueueable
                && GridTerrainRules.MatchesAny(terrainKind, BlockedTerrainKinds))
                return "unqueueable_terrain";

            return null;
        }
    }
}
