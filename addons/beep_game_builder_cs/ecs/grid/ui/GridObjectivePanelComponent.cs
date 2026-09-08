using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridObjectiveTrackerComponent. It lists active
    /// settlement/tutorial goals and their progress.
    ///
    /// The panel surface itself - authored-control binding, the generated
    /// fallback layout, and the row diff - is GridListPanelComponent's; this
    /// file owns only which goals are visible and what a goal row says.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridObjectivePanelComponent : GridListPanelComponent
    {
        [Export] public NodePath ObjectiveTrackerPath { get; set; } = new("");
        [Export] public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// How often AutoRefresh repaints. Objective changes arrive through the
        /// tracker's signals anyway; this timer is only the safety net, so it
        /// runs slow rather than every frame.
        /// </summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.5f;
        [Export] public bool HideCompleted { get; set; } = false;
        [Export(PropertyHint.Range, "1,24,1")] public int MaxVisibleObjectives { get; set; } = 6;

        public GridObjectivePanelComponent()
        {
            TitleText = "Objectives";
            PanelMinimumSize = new Vector2(236, 128);
        }

        protected override string GeneratedRootName => "GeneratedObjectivePanel";
        protected override string RowNamePrefix => "Objective";

        private GridObjectiveTrackerComponent? _tracker;
        private float _refreshAccumulator;

        public override void _Ready()
        {
            ResolveReferences();
            ConnectTrackerSignals();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildPanel));

            SetProcess(AutoRefresh || Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectTrackerSignals();
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

        public override string[] _GetConfigurationWarnings()
        {
            if (ObjectiveTrackerPath.IsEmpty)
                return new[] { "ObjectiveTrackerPath should point to a GridObjectiveTrackerComponent." };
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

            List<GridObjectiveDefinition> objectives = VisibleObjectives();
            int completed = 0;
            foreach (GridObjectiveDefinition objective in objectives)
                if (_tracker != null && _tracker.IsComplete(objective.ObjectiveId))
                    completed++;

            SummaryLabel!.Text = $"Goals {objectives.Count} | Done {completed}";

            UpdateRows(ObjectiveRows(objectives), MaxVisibleObjectives);
        }

        private IEnumerable<GridPanelRow> ObjectiveRows(List<GridObjectiveDefinition> objectives)
        {
            foreach (GridObjectiveDefinition objective in objectives)
                yield return new GridPanelRow(
                    objective.NormalizedId(),
                    TextForObjective(objective),
                    ColorForObjective(objective),
                    string.IsNullOrWhiteSpace(objective.Description) ? objective.DisplayName : objective.Description);
        }

        public string SummaryText()
        {
            RefreshPanel();
            return SummaryLabel?.Text ?? "";
        }

        public string TextForObjective(string objectiveId)
        {
            RefreshPanel();
            return RowText(GridObjectiveDefinition.Normalize(objectiveId));
        }

        public string TextForObjective(GridObjectiveDefinition objective)
        {
            if (_tracker == null)
                return $"{objective.DisplayName}: unavailable";

            string status = _tracker.IsComplete(objective.ObjectiveId) ? "Done" : "Active";
            int progress = _tracker.GetProgress(objective.ObjectiveId);
            int target = _tracker.GetTarget(objective.ObjectiveId);
            return $"{objective.DisplayName}: {progress}/{target} {status}";
        }

        public int VisibleObjectiveRowCount() => RowCount;

        private List<GridObjectiveDefinition> VisibleObjectives()
        {
            ResolveReferences();
            var objectives = new List<GridObjectiveDefinition>();
            if (_tracker == null)
                return objectives;

            foreach (GridObjectiveDefinition objective in GridObjectiveDefinition.Enumerate(_tracker.Objectives))
            {
                if (objective == null)
                    continue;

                bool active = _tracker.IsActive(objective.ObjectiveId);
                bool complete = _tracker.IsComplete(objective.ObjectiveId);
                if (objective.HiddenUntilActive && !active)
                    continue;
                if (!active && !complete)
                    continue;
                if (HideCompleted && complete)
                    continue;

                objectives.Add(objective);
                if (objectives.Count >= MaxVisibleObjectives)
                    break;
            }

            return objectives;
        }

        private void ResolveReferences()
        {
            // The guard stays so signals are (re)connected only when Resolve
            // actually picks a new instance up, not on every call.
            if (_tracker == null || !GodotObject.IsInstanceValid(_tracker))
            {
                EntityComponent.Resolve(this, ObjectiveTrackerPath, ref _tracker);
                ConnectTrackerSignals();
            }
        }

        private void ConnectTrackerSignals()
        {
            if (_tracker == null || Engine.IsEditorHint())
                return;

            _tracker.ObjectiveActivated -= OnObjectiveActivated;
            _tracker.ObjectiveProgressChanged -= OnObjectiveProgressChanged;
            _tracker.ObjectiveCompleted -= OnObjectiveCompleted;
            _tracker.ObjectiveActivated += OnObjectiveActivated;
            _tracker.ObjectiveProgressChanged += OnObjectiveProgressChanged;
            _tracker.ObjectiveCompleted += OnObjectiveCompleted;
        }

        private void DisconnectTrackerSignals()
        {
            if (_tracker == null || !GodotObject.IsInstanceValid(_tracker))
                return;

            _tracker.ObjectiveActivated -= OnObjectiveActivated;
            _tracker.ObjectiveProgressChanged -= OnObjectiveProgressChanged;
            _tracker.ObjectiveCompleted -= OnObjectiveCompleted;
        }

        private void OnObjectiveActivated(string objectiveId, bool active) => RefreshPanel();
        private void OnObjectiveProgressChanged(string objectiveId, int progress, int target) => RefreshPanel();
        private void OnObjectiveCompleted(string objectiveId) => RefreshPanel();

        private Color ColorForObjective(GridObjectiveDefinition objective)
            => _tracker != null && _tracker.IsComplete(objective.ObjectiveId)
                ? new Color(0.56f, 0.9f, 0.62f)
                : new Color(0.95f, 0.9f, 0.72f);
    }
}
