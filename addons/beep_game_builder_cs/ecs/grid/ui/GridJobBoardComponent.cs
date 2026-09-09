using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridJobQueueComponent. It shows queued/claimed/done
    /// counts plus a short job list so builder and settlement games can expose
    /// what workers are doing without custom queue UI.
    ///
    /// The panel surface itself - authored-control binding, the generated
    /// fallback layout, and the row diff - is GridListPanelComponent's; this
    /// file owns only what a job row says and how jobs are ordered.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridJobBoardComponent : GridListPanelComponent
    {
        [Signal] public delegate void JobCancelRequestedEventHandler(string jobId);

        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public bool HideWhenEmpty { get; set; } = false;
        [Export] public bool ShowCompletedJobs { get; set; } = false;
        [Export(PropertyHint.Range, "1,20,1")] public int MaxVisibleJobs { get; set; } = 6;

        public GridJobBoardComponent()
        {
            TitleText = "Jobs";
            PanelMinimumSize = new Vector2(220, 128);
        }

        protected override string GeneratedRootName => "GeneratedJobBoard";
        protected override string RowNamePrefix => "Job";

        private GridJobQueueComponent? _queue;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildBoard));

            if (!Engine.IsEditorHint() && _queue != null)
                _queue.QueueChanged += OnQueueChanged;

            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_queue != null && GodotObject.IsInstanceValid(_queue))
                _queue.QueueChanged -= OnQueueChanged;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (JobQueuePath.IsEmpty)
                return new[] { "JobQueuePath should point to a GridJobQueueComponent." };
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set SummaryLabelPath and RowsContainerPath, add scene-authored Summary/Rows children, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildBoard()
        {
            ResolveReferences();
            if (BindExistingControls())
            {
                RefreshBoard();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedPanel();
            RefreshBoard();
        }

        public void RefreshBoard()
        {
            ResolveReferences();
            if (!ControlsReady)
                return;

            ApplyTitleText();

            if (_queue == null)
            {
                ClearRows();
                SummaryLabel!.Text = "Job queue missing";
                Visible = !HideWhenEmpty;
                return;
            }

            SummaryLabel!.Text = SummaryText();
            Visible = !HideWhenEmpty || _queue.QueuedCount + _queue.ClaimedCount + _queue.CompletedCount > 0;

            UpdateRows(JobRows(), MaxVisibleJobs);
        }

        public string SummaryText()
        {
            ResolveReferences();
            return _queue == null
                ? "Queued 0 | Active 0 | Done 0"
                : $"Queued {_queue.QueuedCount} | Active {_queue.ClaimedCount} | Done {_queue.CompletedCount}";
        }

        public string TextForJob(string jobId)
        {
            RefreshBoard();
            return RowText(jobId);
        }

        public string TextForJob(Godot.Collections.Dictionary job)
        {
            string id = GridVariantReader.String(job, "id", "job");
            string kind = GridVariantReader.String(job, "kind", "work");
            string state = GridVariantReader.String(job, "state", "Queued");
            Vector2I cell = GridVariantReader.Vector2I(job, "cell", Vector2I.Zero);
            string worker = GridVariantReader.String(job, "claimed_by", "");
            string suffix = string.IsNullOrWhiteSpace(worker) ? "" : $" by {worker}";
            return $"{kind} ({cell.X},{cell.Y}) {state}{suffix} [{id}]";
        }

        public int VisibleJobRowCount() => RowsContainerChildCount;

        public bool CancelJob(string jobId, string reason = "cancelled_from_job_board")
        {
            ResolveReferences();
            EmitSignal(SignalName.JobCancelRequested, jobId);
            bool cancelled = _queue?.CancelJob(jobId, reason) == true;
            RefreshBoard();
            return cancelled;
        }

        private void OnQueueChanged(int queued, int claimed, int completed) => RefreshBoard();

        private IEnumerable<GridPanelRow> JobRows()
        {
            foreach (Godot.Collections.Dictionary job in VisibleJobs())
                yield return new GridPanelRow(
                    GridVariantReader.String(job, "id", ""),
                    TextForJob(job),
                    ColorForState(GridVariantReader.String(job, "state", "")));
        }

        private List<Godot.Collections.Dictionary> VisibleJobs()
        {
            var jobs = new List<Godot.Collections.Dictionary>();
            if (_queue == null)
                return jobs;

            foreach (Godot.Collections.Dictionary job in _queue.GetJobs())
            {
                string state = GridVariantReader.String(job, "state", "");
                if (!ShowCompletedJobs && string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Completed), StringComparison.OrdinalIgnoreCase))
                    continue;
                jobs.Add(job);
            }

            jobs.Sort((a, b) =>
            {
                int stateCompare = StateRank(GridVariantReader.String(a, "state", "")).CompareTo(StateRank(GridVariantReader.String(b, "state", "")));
                if (stateCompare != 0)
                    return stateCompare;

                int priorityCompare = GridVariantReader.Int(b, "priority", 0).CompareTo(GridVariantReader.Int(a, "priority", 0));
                if (priorityCompare != 0)
                    return priorityCompare;

                return string.CompareOrdinal(GridVariantReader.String(a, "id", ""), GridVariantReader.String(b, "id", ""));
            });

            return jobs;
        }

        private void ResolveReferences()
            => EntityComponent.Resolve(this, JobQueuePath, ref _queue);

        private static int StateRank(string state)
        {
            if (string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Claimed), StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Queued), StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Completed), StringComparison.OrdinalIgnoreCase))
                return 2;
            return 3;
        }

        private static Color ColorForState(string state)
        {
            if (string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Claimed), StringComparison.OrdinalIgnoreCase))
                return new Color(0.42f, 0.78f, 1f);
            if (string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Completed), StringComparison.OrdinalIgnoreCase))
                return new Color(0.45f, 0.9f, 0.55f);
            return new Color(0.95f, 0.86f, 0.48f);
        }



    }
}
