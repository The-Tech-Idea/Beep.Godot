using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for worker/truck status. It scans a units root for
    /// GridWorkerComponent instances and shows whether each worker is idle,
    /// moving, or working.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkerStatusPanelComponent : Control
    {
        [Signal] public delegate void WorkerCancelRequestedEventHandler(string workerId);

        [Export] public NodePath UnitsRootPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath SummaryLabelPath { get; set; } = new("");
        [Export] public NodePath RowsContainerPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// How often AutoRefresh repaints. A readout of worker states does not
        /// need frame rate; refreshing every frame used to rebuild every row
        /// Label and rescan the units tree 60 times a second.
        /// </summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.25f;
        [Export(PropertyHint.Range, "1,24,1")] public int MaxVisibleWorkers { get; set; } = 8;
        [Export] public string TitleText { get; set; } = "Workers";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(220, 126);

        private Node? _unitsRoot;
        private GridJobQueueComponent? _jobs;
        private Label? _title;
        private Label? _summary;
        private VBoxContainer? _rows;
        private readonly Dictionary<string, Label> _rowLabels = new();

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildPanel));

            SetProcess(AutoRefresh || Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

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

        private float _refreshAccumulator;

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

            ClearChildren();
            _rowLabels.Clear();

            var panel = new PanelContainer
            {
                Name = "GeneratedWorkerStatusPanel",
                CustomMinimumSize = PanelMinimumSize,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(panel);
            SetEditedOwner(panel);

            var layout = new VBoxContainer
            {
                Name = "Content",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(layout, "separation", 4);
            panel.AddChild(layout);
            SetEditedOwner(layout);

            _title = new Label
            {
                Name = "Title",
                Text = TitleText,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            KitChrome.SetColorOverrideIfChanged(_title, "font_color", Colors.White);
            layout.AddChild(_title);
            SetEditedOwner(_title);

            _summary = new Label
            {
                Name = "Summary",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            KitChrome.SetColorOverrideIfChanged(_summary, "font_color", new Color(0.86f, 0.89f, 0.92f));
            layout.AddChild(_summary);
            SetEditedOwner(_summary);

            _rows = new VBoxContainer
            {
                Name = "Rows",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_rows, "separation", 2);
            layout.AddChild(_rows);
            SetEditedOwner(_rows);

            RefreshPanel();
        }

        public void RefreshPanel()
        {
            ResolveReferences();
            if (_summary == null || _rows == null)
                return;

            if (_title != null)
                _title.Text = TitleText;

            var workers = Workers();
            int idle = 0;
            int active = 0;
            foreach (GridWorkerComponent worker in workers)
            {
                if (EffectiveState(worker) == GridWorkerComponent.WorkerState.Idle)
                    idle++;
                else
                    active++;
            }

            _summary.Text = $"Total {workers.Count} | Idle {idle} | Active {active}";

            // Rows are updated IN PLACE and only added or removed when the
            // worker set actually changes. Freeing and recreating every Label
            // per refresh was UI node churn for a panel whose set of rows is
            // almost always identical to the last refresh.
            var seen = new HashSet<string>();
            int shown = 0;
            foreach (GridWorkerComponent worker in workers)
            {
                if (shown >= MaxVisibleWorkers)
                    break;

                string id = worker.WorkerId;
                if (!seen.Add(id))
                    continue;

                if (!_rowLabels.TryGetValue(id, out Label? row) || !GodotObject.IsInstanceValid(row))
                {
                    row = new Label
                    {
                        Name = $"Worker_{SafeName(id)}",
                        TooltipText = id,
                        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                        CustomMinimumSize = new Vector2(0, 22)
                    };
                    _rows.AddChild(row);
                    SetEditedOwner(row);
                    _rowLabels[id] = row;
                }

                row.Text = TextForWorker(worker);
                KitChrome.SetColorOverrideIfChanged(row, "font_color", ColorForState(worker.State));
                // Reused rows still follow the sorted id order.
                _rows.MoveChild(row, shown);
                shown++;
            }

            var stale = new List<string>();
            foreach ((string id, Label row) in _rowLabels)
            {
                if (seen.Contains(id))
                    continue;

                if (GodotObject.IsInstanceValid(row))
                {
                    _rows.RemoveChild(row);
                    row.QueueFree();
                }
                stale.Add(id);
            }
            foreach (string id in stale)
                _rowLabels.Remove(id);
        }

        public string SummaryText()
        {
            RefreshPanel();
            return _summary?.Text ?? "";
        }

        public string TextForWorker(string workerId)
        {
            RefreshPanel();
            return _rowLabels.TryGetValue(workerId, out Label? label) ? label.Text : "";
        }

        public string TextForWorker(GridWorkerComponent worker)
        {
            string id = string.IsNullOrWhiteSpace(worker.WorkerId) ? worker.Name : worker.WorkerId;
            GridWorkerComponent.WorkerState stateValue = EffectiveState(worker);
            string state = stateValue.ToString();
            string jobId = string.IsNullOrWhiteSpace(worker.CurrentJobId)
                ? FindClaimedJobId(worker.WorkerId)
                : worker.CurrentJobId;
            if (string.IsNullOrWhiteSpace(jobId))
                return $"{id}: {state}";

            string kind = _jobs?.GetJobKind(jobId) ?? "";
            Vector2I cell = _jobs?.GetJobCell(jobId) ?? new Vector2I(int.MinValue, int.MinValue);
            string job = string.IsNullOrWhiteSpace(kind) ? jobId : kind;
            string target = cell.X == int.MinValue ? "" : $" ({cell.X},{cell.Y})";
            string remaining = worker.State == GridWorkerComponent.WorkerState.Working
                ? $" {Mathf.Max(0f, worker.WorkRemainingSeconds):0.0}s"
                : "";
            return $"{id}: {state} {job}{target}{remaining}";
        }

        public int VisibleWorkerRowCount()
            => _rowLabels.Count;

        public bool CancelWorkerJob(string workerId, string reason = "cancelled_from_worker_panel")
        {
            foreach (GridWorkerComponent worker in Workers())
            {
                if (!string.Equals(worker.WorkerId, workerId, StringComparison.OrdinalIgnoreCase))
                    continue;

                string jobId = string.IsNullOrEmpty(worker.CurrentJobId)
                    ? FindClaimedJobId(worker.WorkerId)
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

        private List<GridWorkerComponent> Workers()
        {
            ResolveReferences();
            var workers = new List<GridWorkerComponent>();
            if (_unitsRoot != null)
                CollectWorkers(_unitsRoot, workers);
            workers.Sort((a, b) => string.Compare(DisplayId(a), DisplayId(b), StringComparison.OrdinalIgnoreCase));
            return workers;
        }

        private void ResolveReferences()
        {
            if (_unitsRoot == null || !GodotObject.IsInstanceValid(_unitsRoot))
                _unitsRoot = !UnitsRootPath.IsEmpty ? GetNodeOrNull<Node>(UnitsRootPath) : null;
            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs))
                _jobs = !JobQueuePath.IsEmpty ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath) : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;
        }

        public bool UsesSceneControls()
            => !TitleLabelPath.IsEmpty || !SummaryLabelPath.IsEmpty || !RowsContainerPath.IsEmpty
            || FindTitleLabel() != null || FindSummaryLabel() != null || FindRowsContainer() != null;

        private bool BindExistingControls()
        {
            if (!UsesSceneControls())
                return false;

            Label? title = FindTitleLabel();
            Label? summary = FindSummaryLabel();
            VBoxContainer? rows = FindRowsContainer();

            if (summary == null || rows == null)
                return false;

            _title = title;
            _summary = summary;
            _rows = rows;
            _rowLabels.Clear();
            return true;
        }

        private bool HasAuthoredControls()
            => FindSummaryLabel() != null && FindRowsContainer() != null;

        private Label? FindTitleLabel()
        {
            if (!TitleLabelPath.IsEmpty && GetNodeOrNull<Label>(TitleLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("Title", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Title", recursive: true, owned: false) as Label;
        }

        private Label? FindSummaryLabel()
        {
            if (!SummaryLabelPath.IsEmpty && GetNodeOrNull<Label>(SummaryLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("Summary", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Summary", recursive: true, owned: false) as Label;
        }

        private VBoxContainer? FindRowsContainer()
        {
            if (!RowsContainerPath.IsEmpty && GetNodeOrNull<VBoxContainer>(RowsContainerPath) is { } pathRows)
                return pathRows;

            if (FindChild("Rows", recursive: true, owned: false) is VBoxContainer childRows)
                return childRows;

            return GetParent()?.FindChild("Rows", recursive: true, owned: false) as VBoxContainer;
        }

        private void ClearChildren()
        {
            foreach (Node child in GetChildren())
                child.QueueFree();
            _title = null;
            _summary = null;
            _rows = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
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

        private GridWorkerComponent.WorkerState EffectiveState(GridWorkerComponent worker)
        {
            if (worker.State != GridWorkerComponent.WorkerState.Idle)
                return worker.State;
            return string.IsNullOrEmpty(FindClaimedJobId(worker.WorkerId))
                ? GridWorkerComponent.WorkerState.Idle
                : GridWorkerComponent.WorkerState.Working;
        }

        private string FindClaimedJobId(string workerId)
        {
            if (_jobs == null || string.IsNullOrWhiteSpace(workerId))
                return "";

            foreach (Godot.Collections.Dictionary job in _jobs.GetJobs())
            {
                string claimedBy = DictString(job, "claimed_by", "");
                string state = DictString(job, "state", "");
                if (string.Equals(claimedBy, workerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Claimed), StringComparison.OrdinalIgnoreCase))
                    return DictString(job, "id", "");
            }

            return "";
        }

        private static Color ColorForState(GridWorkerComponent.WorkerState state)
            => state switch
            {
                GridWorkerComponent.WorkerState.Idle => new Color(0.82f, 0.86f, 0.9f),
                GridWorkerComponent.WorkerState.MovingToJob => new Color(0.45f, 0.78f, 1f),
                GridWorkerComponent.WorkerState.Working => new Color(0.48f, 0.92f, 0.58f),
                _ => Colors.White
            };

        private static string SafeName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Worker" : value.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return result.Replace(' ', '_');
        }

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;
    }
}
