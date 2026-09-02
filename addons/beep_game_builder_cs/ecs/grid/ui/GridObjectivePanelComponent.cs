using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridObjectiveTrackerComponent. It lists active
    /// settlement/tutorial goals and their progress.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridObjectivePanelComponent : Control
    {
        [Export] public NodePath ObjectiveTrackerPath { get; set; } = new("");
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath SummaryLabelPath { get; set; } = new("");
        [Export] public NodePath RowsContainerPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// How often AutoRefresh repaints. Objective changes arrive through the
        /// tracker's signals anyway; this timer is only the safety net, so it
        /// runs slow rather than every frame.
        /// </summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.5f;
        [Export] public bool HideCompleted { get; set; } = false;
        [Export(PropertyHint.Range, "1,24,1")] public int MaxVisibleObjectives { get; set; } = 6;
        [Export] public string TitleText { get; set; } = "Objectives";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(236, 128);

        private GridObjectiveTrackerComponent? _tracker;
        private Label? _title;
        private Label? _summary;
        private VBoxContainer? _rows;
        private readonly Dictionary<string, Label> _rowLabels = new();

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

        private float _refreshAccumulator;

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

            ClearChildren();
            _rowLabels.Clear();

            var panel = new PanelContainer
            {
                Name = "GeneratedObjectivePanel",
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

            List<GridObjectiveDefinition> objectives = VisibleObjectives();
            int completed = 0;
            foreach (GridObjectiveDefinition objective in objectives)
                if (_tracker != null && _tracker.IsComplete(objective.ObjectiveId))
                    completed++;

            _summary.Text = $"Goals {objectives.Count} | Done {completed}";

            // Rows updated IN PLACE; added or removed only when the goal set
            // changes. Recreating every Label per refresh was pure node churn.
            var seen = new HashSet<string>();
            int shown = 0;
            foreach (GridObjectiveDefinition objective in objectives)
            {
                if (shown >= MaxVisibleObjectives)
                    break;

                string id = objective.NormalizedId();
                if (!seen.Add(id))
                    continue;

                if (!_rowLabels.TryGetValue(id, out Label? row) || !GodotObject.IsInstanceValid(row))
                {
                    row = new Label
                    {
                        Name = $"Objective_{SafeName(id)}",
                        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                        CustomMinimumSize = new Vector2(0, 22)
                    };
                    _rows.AddChild(row);
                    SetEditedOwner(row);
                    _rowLabels[id] = row;
                }

                row.Text = TextForObjective(objective);
                row.TooltipText = string.IsNullOrWhiteSpace(objective.Description) ? objective.DisplayName : objective.Description;
                KitChrome.SetColorOverrideIfChanged(row, "font_color", ColorForObjective(objective));
                // Reused rows still follow the tracker's listed order.
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

        public string TextForObjective(string objectiveId)
        {
            RefreshPanel();
            string id = GridObjectiveDefinition.Normalize(objectiveId);
            return _rowLabels.TryGetValue(id, out Label? label) ? label.Text : "";
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

        public int VisibleObjectiveRowCount()
            => _rowLabels.Count;

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
            if (_tracker == null || !GodotObject.IsInstanceValid(_tracker))
            {
                _tracker = !ObjectiveTrackerPath.IsEmpty
                    ? GetNodeOrNull<GridObjectiveTrackerComponent>(ObjectiveTrackerPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridObjectiveTrackerComponent>(GetTree()?.CurrentScene) : null;
                ConnectTrackerSignals();
            }
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

        private Color ColorForObjective(GridObjectiveDefinition objective)
            => _tracker != null && _tracker.IsComplete(objective.ObjectiveId)
                ? new Color(0.56f, 0.9f, 0.62f)
                : new Color(0.95f, 0.9f, 0.72f);

        private static string SafeName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "Objective" : value;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return safe.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
