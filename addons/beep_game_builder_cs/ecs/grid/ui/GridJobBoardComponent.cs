using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridJobQueueComponent. It shows queued/claimed/done
    /// counts plus a short job list so builder and settlement games can expose
    /// what workers are doing without custom queue UI.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridJobBoardComponent : Control
    {
        [Signal] public delegate void JobCancelRequestedEventHandler(string jobId);

        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath SummaryLabelPath { get; set; } = new("");
        [Export] public NodePath RowsContainerPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool HideWhenEmpty { get; set; } = false;
        [Export] public bool ShowCompletedJobs { get; set; } = false;
        [Export(PropertyHint.Range, "1,20,1")] public int MaxVisibleJobs { get; set; } = 6;
        [Export] public string TitleText { get; set; } = "Jobs";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(220, 128);

        private GridJobQueueComponent? _queue;
        private Label? _title;
        private Label? _summary;
        private VBoxContainer? _jobRows;
        private readonly Dictionary<string, Label> _rowLabels = new();

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

            ClearChildren();
            _rowLabels.Clear();

            var panel = new PanelContainer
            {
                Name = "GeneratedJobBoard",
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

            _jobRows = new VBoxContainer
            {
                Name = "Rows",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_jobRows, "separation", 2);
            layout.AddChild(_jobRows);
            SetEditedOwner(_jobRows);

            RefreshBoard();
        }

        public void RefreshBoard()
        {
            ResolveReferences();
            if (_summary == null || _jobRows == null)
                return;

            if (_title != null)
                _title.Text = TitleText;

            if (_queue == null)
            {
                foreach (Node child in _jobRows.GetChildren())
                    child.QueueFree();
                _rowLabels.Clear();
                _summary.Text = "Job queue missing";
                Visible = !HideWhenEmpty;
                return;
            }

            _summary.Text = SummaryText();
            Visible = !HideWhenEmpty || _queue.QueuedCount + _queue.ClaimedCount + _queue.CompletedCount > 0;

            // Rows updated IN PLACE; added or removed only when the job set
            // changes. QueueChanged fires on every claim and completion, so
            // recreating every Label per refresh was constant node churn.
            var seen = new HashSet<string>();
            int shown = 0;
            foreach (Godot.Collections.Dictionary job in VisibleJobs())
            {
                if (shown >= MaxVisibleJobs)
                    break;

                string id = DictString(job, "id", "");
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    continue;

                if (!_rowLabels.TryGetValue(id, out Label? row) || !GodotObject.IsInstanceValid(row))
                {
                    row = new Label
                    {
                        Name = $"Job_{SafeName(id)}",
                        TooltipText = id,
                        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                        CustomMinimumSize = new Vector2(0, 22)
                    };
                    _jobRows.AddChild(row);
                    SetEditedOwner(row);
                    _rowLabels[id] = row;
                }

                row.Text = TextForJob(job);
                KitChrome.SetColorOverrideIfChanged(row, "font_color", ColorForState(DictString(job, "state", "")));
                // Reused rows still follow the sorted order (claimed first,
                // then priority) instead of keeping their old position.
                _jobRows.MoveChild(row, shown);
                shown++;
            }

            var stale = new List<string>();
            foreach ((string id, Label row) in _rowLabels)
            {
                if (seen.Contains(id))
                    continue;

                if (GodotObject.IsInstanceValid(row))
                {
                    _jobRows.RemoveChild(row);
                    row.QueueFree();
                }
                stale.Add(id);
            }
            foreach (string id in stale)
                _rowLabels.Remove(id);
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
            return _rowLabels.TryGetValue(jobId, out Label? label) ? label.Text : "";
        }

        public string TextForJob(Godot.Collections.Dictionary job)
        {
            string id = DictString(job, "id", "job");
            string kind = DictString(job, "kind", "work");
            string state = DictString(job, "state", "Queued");
            Vector2I cell = DictVector2I(job, "cell", Vector2I.Zero);
            string worker = DictString(job, "claimed_by", "");
            string suffix = string.IsNullOrWhiteSpace(worker) ? "" : $" by {worker}";
            return $"{kind} ({cell.X},{cell.Y}) {state}{suffix} [{id}]";
        }

        public int VisibleJobRowCount()
            => _jobRows?.GetChildCount() ?? 0;

        public bool CancelJob(string jobId, string reason = "cancelled_from_job_board")
        {
            ResolveReferences();
            EmitSignal(SignalName.JobCancelRequested, jobId);
            bool cancelled = _queue?.CancelJob(jobId, reason) == true;
            RefreshBoard();
            return cancelled;
        }

        private void OnQueueChanged(int queued, int claimed, int completed) => RefreshBoard();

        private List<Godot.Collections.Dictionary> VisibleJobs()
        {
            var jobs = new List<Godot.Collections.Dictionary>();
            if (_queue == null)
                return jobs;

            foreach (Godot.Collections.Dictionary job in _queue.GetJobs())
            {
                string state = DictString(job, "state", "");
                if (!ShowCompletedJobs && string.Equals(state, nameof(GridJobQueueComponent.GridJobState.Completed), StringComparison.OrdinalIgnoreCase))
                    continue;
                jobs.Add(job);
            }

            jobs.Sort((a, b) =>
            {
                int stateCompare = StateRank(DictString(a, "state", "")).CompareTo(StateRank(DictString(b, "state", "")));
                if (stateCompare != 0)
                    return stateCompare;

                int priorityCompare = DictInt(b, "priority", 0).CompareTo(DictInt(a, "priority", 0));
                if (priorityCompare != 0)
                    return priorityCompare;

                return string.CompareOrdinal(DictString(a, "id", ""), DictString(b, "id", ""));
            });

            return jobs;
        }

        private void ResolveReferences()
        {
            if (_queue != null && GodotObject.IsInstanceValid(_queue))
                return;

            if (!JobQueuePath.IsEmpty)
                _queue = GetNodeOrNull<GridJobQueueComponent>(JobQueuePath);
            else if (IsInsideTree())
                _queue = EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene);
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
            _jobRows = rows;
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
            _jobRows = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

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

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;

        private static int DictInt(Godot.Collections.Dictionary dict, string key, int fallback)
            => GridVariantReader.Int(dict, key, fallback);

        private static Vector2I DictVector2I(Godot.Collections.Dictionary dict, string key, Vector2I fallback)
            => GridVariantReader.Vector2I(dict, key, fallback);

        private static string SafeName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Job" : value.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return result.Replace(' ', '_');
        }
    }
}
