using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Converts selected grid cells into jobs. Pair with GridSelectionComponent
    /// and GridJobQueueComponent for settler-style commands such as clear land,
    /// prepare pad, harvest, build, deliver, or inspect.
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
        /// <summary>Work each queued job carries, in turns - the grid's one unit.</summary>
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float WorkTurns { get; set; } = 1.5f;
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

        public float EffectiveWorkTurns => Mathf.Max(0.01f, float.IsFinite(WorkTurns) ? WorkTurns : 1.5f);

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
            if (WorkTurns <= 0f)
                return new[] { "WorkTurns must be greater than zero." };
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

        public int QueueSelectedCells(string kind = "", float workTurns = -1f, int? priority = null)
        {
            ResolveReferences();
            if (_selection == null)
                return Fail("missing_selection");
            if (_queue == null)
                return Fail("missing_job_queue");

            string resolvedKind = string.IsNullOrWhiteSpace(kind) ? JobKind : kind;
            float resolvedWorkTurns = workTurns > 0f && float.IsFinite(workTurns) ? workTurns : EffectiveWorkTurns;
            int resolvedPriority = priority ?? Priority;
            int count = QueueCells(_selection.GetSelectedCells(), resolvedKind, resolvedWorkTurns, resolvedPriority);

            if (count > 0 && ClearSelectionAfterQueue)
                _selection.ClearSelection();

            return count;
        }

        public int QueueCells(Godot.Collections.Array cells, string kind = "", float workTurns = -1f, int? priority = null)
        {
            ResolveReferences();
            if (_queue == null)
                return Fail("missing_job_queue");

            string resolvedKind = string.IsNullOrWhiteSpace(kind) ? JobKind : kind;
            float resolvedWorkTurns = workTurns > 0f && float.IsFinite(workTurns) ? workTurns : EffectiveWorkTurns;
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

                _queue.AddJob(cell, resolvedKind, resolvedWorkTurns, resolvedPriority);
                count++;
            }

            if (count == 0)
                EmitSignal(SignalName.QueueFailed, sawCandidate ? "no_valid_cells" : "no_cells");
            else
                EmitSignal(SignalName.JobsQueued, resolvedKind, count);

            return count;
        }

        public int QueueCells(Godot.Collections.Array<Vector2I> cells, string kind = "", float workTurns = -1f, int? priority = null)
        {
            var looseCells = new Godot.Collections.Array();
            foreach (Vector2I cell in cells)
                looseCells.Add(cell);

            return QueueCells(looseCells, kind, workTurns, priority);
        }

        public int QueueRectangle(Vector2I a, Vector2I b, string kind = "", float workTurns = -1f, int? priority = null)
            => QueueCells(GridSelectionComponent.CellsInRect(a, b), kind, workTurns, priority);

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
            EntityComponent.Resolve(this, SelectionPath, ref _selection);
            EntityComponent.Resolve(this, JobQueuePath, ref _queue);
            EntityComponent.Resolve(this, CellDataPath, ref _cellData);
            EntityComponent.Resolve(this, NavigationPath, ref _navigation);
        }

        /// <summary>
        /// This component's own exports, as the shared cell rule. Built per
        /// call so a NodePath re-resolved by ResolveReferences is picked up.
        /// </summary>
        private GridCellRules Rules() => new()
        {
            Navigation = _navigation,
            Cells = _cellData,
            UseNavigationBounds = UseNavigationBounds,
            RejectNavigationBlockedCells = RejectNavigationBlockedCells,
            RejectCellDataBlockedCells = TreatCellDataBlockedAsUnqueueable,
            TreatBlockedTerrainKindsAsBlocking = TreatBlockedTerrainKindsAsUnqueueable,
            BlockedTerrainKinds = BlockedTerrainKinds,
            AllowedTerrainKinds = AllowedTerrainKinds
        };

        /// <summary>
        /// The shared queueability answer in this component's own words - the
        /// vocabulary its QueueFailed signal has always reported.
        /// </summary>
        private string? QueueBlockReason(Vector2I cell)
            => Rules().QueueBlock(cell) switch
            {
                GridJobBlock.OutOfBounds => "cell_out_of_bounds",
                GridJobBlock.Blocked => "blocked_cell",
                GridJobBlock.UnworkableTerrain => "unqueueable_terrain",
                _ => null
            };
    }
}
