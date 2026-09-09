using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for worker/truck status. It scans a units root for
    /// GridWorkerComponent instances and shows whether each worker is idle,
    /// moving, or working.
    ///
    /// The panel surface itself - authored-control binding, the generated
    /// fallback layout, and the row diff - is GridListPanelComponent's; this
    /// file owns only the worker roster and what a worker row says.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkerStatusPanelComponent : GridListPanelComponent
    {
        [Signal] public delegate void WorkerCancelRequestedEventHandler(string workerId);

        [Export] public NodePath UnitsRootPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");

        /// <summary>
        /// Optional. Wired (or found scene-wide when empty), new units the
        /// spawner reports via UnitSpawned are appended to the cached roster
        /// instead of triggering a full re-walk of UnitsRootPath. A worker
        /// added some OTHER way - a second spawner, or one hand-placed in the
        /// scene after this panel's first refresh - is missed by the
        /// incremental path; call InvalidateWorkerCache() after adding one
        /// that way.
        /// </summary>
        [Export] public NodePath SpawnerPath { get; set; } = new("");
        [Export] public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// How often AutoRefresh repaints. A readout of worker states does not
        /// need frame rate; refreshing every frame used to rebuild every row
        /// Label and rescan the units tree 60 times a second.
        /// </summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.25f;
        [Export(PropertyHint.Range, "1,24,1")] public int MaxVisibleWorkers { get; set; } = 8;

        public GridWorkerStatusPanelComponent()
        {
            TitleText = "Workers";
            PanelMinimumSize = new Vector2(220, 126);
        }

        protected override string GeneratedRootName => "GeneratedWorkerStatusPanel";
        protected override string RowNamePrefix => "Worker";

        private Node? _unitsRoot;
        private GridJobQueueComponent? _jobs;
        private GridWorkerSpawnerComponent? _spawner;
        private bool _spawnerConnected;
        private readonly GridRosterCache<GridWorkerComponent> _roster = new();
        private float _refreshAccumulator;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildPanel));

            SetProcess(AutoRefresh || Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectSpawner();
        }

        /// <summary>Forces the next Workers() call to re-walk UnitsRootPath from
        /// scratch, for a roster change this panel's incremental cache cannot
        /// see on its own (see the SpawnerPath doc comment).</summary>
        public void InvalidateWorkerCache() => _roster.Invalidate();

        public override void _Process(double delta)
        {
            if (!AutoRefresh && !Engine.IsEditorHint())
                return;

            _refreshAccumulator += (float)delta;
            if (_refreshAccumulator < Mathf.Max(0.05f, RefreshIntervalSeconds))
                return;

            _refreshAccumulator = 0f;
            RefreshPanel();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (UnitsRootPath.IsEmpty)
                return new[] { "UnitsRootPath should point to the Node that contains worker/truck scenes." };
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set SummaryLabelPath and RowsContainerPath, add scene-authored Summary/Rows children, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildPanel()
        {
            ResolveReferences();
            if (BindExistingControls())
            {
                RefreshPanel();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedPanel();
            RefreshPanel();
        }

        public void RefreshPanel()
        {
            ResolveReferences();
            if (!ControlsReady)
                return;

            ApplyTitleText();

            var workers = Workers();
            // One pass over the queue for the whole refresh, not one scan per
            // idle worker: EffectiveState/TextForWorker used to each re-derive
            // "what job is this worker claiming" independently via a fresh
            // GetJobs() marshal-and-scan, up to three times per idle worker.
            Dictionary<string, string> claimedByWorker = _jobs?.GetClaimedJobIdsByWorker()
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int idle = 0;
            int active = 0;
            foreach (GridWorkerComponent worker in workers)
            {
                if (EffectiveState(worker, claimedByWorker) == GridWorkerComponent.WorkerState.Idle)
                    idle++;
                else
                    active++;
            }

            SummaryLabel!.Text = $"Total {workers.Count} | Idle {idle} | Active {active}";

            UpdateRows(WorkerRows(workers, claimedByWorker), MaxVisibleWorkers);
        }

        private IEnumerable<GridPanelRow> WorkerRows(
            List<GridWorkerComponent> workers,
            Dictionary<string, string> claimedByWorker)
        {
            foreach (GridWorkerComponent worker in workers)
                yield return new GridPanelRow(
                    worker.WorkerId,
                    TextForWorker(worker, claimedByWorker),
                    ColorForState(worker.State));
        }

        public string SummaryText()
        {
            RefreshPanel();
            return SummaryLabel?.Text ?? "";
        }

        public string TextForWorker(string workerId)
        {
            RefreshPanel();
            return RowText(workerId);
        }

        public string TextForWorker(GridWorkerComponent worker)
            => TextForWorker(worker, null);

        private string TextForWorker(GridWorkerComponent worker, Dictionary<string, string>? claimedByWorker)
        {
            string id = string.IsNullOrWhiteSpace(worker.WorkerId) ? worker.Name : worker.WorkerId;
            GridWorkerComponent.WorkerState stateValue = EffectiveState(worker, claimedByWorker);
            string state = stateValue.ToString();
            string jobId = string.IsNullOrWhiteSpace(worker.CurrentJobId)
                ? ClaimedJobIdFor(worker.WorkerId, claimedByWorker)
                : worker.CurrentJobId;
            if (string.IsNullOrWhiteSpace(jobId))
                return $"{id}: {state}";

            string kind = _jobs?.GetJobKind(jobId) ?? "";
            Vector2I cell = _jobs?.GetJobCell(jobId) ?? new Vector2I(int.MinValue, int.MinValue);
            string job = string.IsNullOrWhiteSpace(kind) ? jobId : kind;
            string target = cell.X == int.MinValue ? "" : $" ({cell.X},{cell.Y})";
            // Turns, the grid's one unit - shown as such, not as seconds it never was.
            string remaining = worker.State == GridWorkerComponent.WorkerState.Working
                ? $" {Mathf.Max(0f, worker.WorkRemainingTurns):0.0}t"
                : "";
            return $"{id}: {state} {job}{target}{remaining}";
        }

        public int VisibleWorkerRowCount() => RowCount;

        public bool CancelWorkerJob(string workerId, string reason = "cancelled_from_worker_panel")
        {
            foreach (GridWorkerComponent worker in Workers())
            {
                if (!string.Equals(worker.WorkerId, workerId, StringComparison.OrdinalIgnoreCase))
                    continue;

                string jobId = string.IsNullOrEmpty(worker.CurrentJobId)
                    ? ClaimedJobIdFor(worker.WorkerId, null)
                    : worker.CurrentJobId;
                if (string.IsNullOrEmpty(jobId))
                    return false;

                EmitSignal(SignalName.WorkerCancelRequested, worker.WorkerId);
                if (!string.IsNullOrEmpty(worker.CurrentJobId))
                    worker.CancelCurrentJob(reason);
                else
                    _jobs?.ReleaseJob(jobId, worker.WorkerId);
                RefreshPanel();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cached and only rebuilt on a genuine roster change - new units
        /// arrive via GridWorkerSpawnerComponent.UnitSpawned (appended, no
        /// re-walk), and freed ones drop out on the cheap IsInstanceValid
        /// prune below - the same registry-pruning shape
        /// GridTransportManagerComponent/GridExtractionManagerComponent use for
        /// their own registries, rather than re-walking UnitsRootPath's whole
        /// subtree every refresh tick.
        /// </summary>
        private List<GridWorkerComponent> Workers()
        {
            ResolveReferences();
            return _roster.Members(BuildWorkers, CompareWorkers);
        }

        private List<GridWorkerComponent> BuildWorkers()
        {
            var workers = new List<GridWorkerComponent>();
            if (_unitsRoot != null)
                CollectWorkers(_unitsRoot, workers);
            return workers;
        }

        private static int CompareWorkers(GridWorkerComponent a, GridWorkerComponent b)
            => string.Compare(DisplayId(a), DisplayId(b), StringComparison.OrdinalIgnoreCase);

        private void OnUnitSpawned(Node unit, string workerId, int x, int y)
        {
            if (!_roster.HasCache)
                return;

            GridWorkerComponent? worker = EntityComponent.FindComponent<GridWorkerComponent>(unit, recursive: true);
            if (worker != null)
                _roster.Append(worker, CompareWorkers);
        }

        private void ConnectSpawner()
        {
            if (_spawner == null || _spawnerConnected)
                return;

            _spawner.UnitSpawned += OnUnitSpawned;
            _spawnerConnected = true;
        }

        private void DisconnectSpawner()
        {
            if (_spawner != null && GodotObject.IsInstanceValid(_spawner) && _spawnerConnected)
                _spawner.UnitSpawned -= OnUnitSpawned;
            _spawnerConnected = false;
        }

        private void ResolveReferences()
        {
            // Explicit wire only: with no root there is nothing to walk.
            if (_unitsRoot == null || !GodotObject.IsInstanceValid(_unitsRoot))
                _unitsRoot = !UnitsRootPath.IsEmpty ? GetNodeOrNull<Node>(UnitsRootPath) : null;

            EntityComponent.Resolve(this, JobQueuePath, ref _jobs);

            // A freed spawner took its signal connection with it; the flag must
            // drop before Resolve picks a replacement up. Never resolved in the
            // editor - the spawner is a runtime roster source only.
            if (_spawner != null && !GodotObject.IsInstanceValid(_spawner))
            {
                _spawner = null;
                _spawnerConnected = false;
            }
            if (_spawner == null && !Engine.IsEditorHint())
            {
                EntityComponent.Resolve(this, SpawnerPath, ref _spawner);
                ConnectSpawner();
            }
        }

        private static void CollectWorkers(Node node, List<GridWorkerComponent> workers)
        {
            if (node is GridWorkerComponent worker)
                workers.Add(worker);

            foreach (Node child in node.GetChildren())
                CollectWorkers(child, workers);
        }

        private static string DisplayId(GridWorkerComponent worker)
            => string.IsNullOrWhiteSpace(worker.WorkerId) ? worker.Name : worker.WorkerId;

        private GridWorkerComponent.WorkerState EffectiveState(GridWorkerComponent worker, Dictionary<string, string>? claimedByWorker = null)
        {
            if (worker.State != GridWorkerComponent.WorkerState.Idle)
                return worker.State;
            return string.IsNullOrEmpty(ClaimedJobIdFor(worker.WorkerId, claimedByWorker))
                ? GridWorkerComponent.WorkerState.Idle
                : GridWorkerComponent.WorkerState.Working;
        }

        /// <summary>
        /// The job a worker currently holds Claimed. Consults the
        /// once-per-refresh map when the caller has one (RefreshPanel's own
        /// loop over every worker); falls back to a single direct queue scan
        /// for an on-demand, single-worker lookup (CancelWorkerJob, the
        /// public single-worker TextForWorker overload) - either way, no
        /// per-job Dictionary marshalling.
        /// </summary>
        private string ClaimedJobIdFor(string workerId, Dictionary<string, string>? claimedByWorker)
        {
            if (claimedByWorker != null)
                return claimedByWorker.TryGetValue(workerId, out string? jobId) ? jobId : "";

            return _jobs?.FindClaimedJobId(workerId) ?? "";
        }

        private static Color ColorForState(GridWorkerComponent.WorkerState state)
            => state switch
            {
                GridWorkerComponent.WorkerState.Idle => new Color(0.82f, 0.86f, 0.9f),
                GridWorkerComponent.WorkerState.MovingToJob => new Color(0.45f, 0.78f, 1f),
                GridWorkerComponent.WorkerState.Working => new Color(0.48f, 0.92f, 0.58f),
                _ => Colors.White
            };
    }
}
