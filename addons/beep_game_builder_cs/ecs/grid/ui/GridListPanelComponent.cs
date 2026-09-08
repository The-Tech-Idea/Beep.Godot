using Godot;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Base for the HUD panels that render a keyed, sorted list of rows under a
    /// title and a summary line - the job board and the worker status panel.
    ///
    /// Both used to carry their own byte-for-byte copy of all of this: the
    /// Title/Summary/Rows three-tier lookup, the bind-or-generate bootstrap,
    /// the generated PanelContainer/Content/Title/Summary/Rows layout, and the
    /// seen-set row diff. None of it is domain logic - what a row SAYS is the
    /// panel's own business, and that is all a subclass supplies.
    ///
    /// Rows are updated IN PLACE: a Label is created once per id, reused on
    /// every later refresh, moved into sorted position, and freed only when its
    /// id leaves the list. Recreating every row per refresh was node churn on a
    /// panel whose row set is almost always identical to the last pass.
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class GridListPanelComponent : GridPanelComponent
    {
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath SummaryLabelPath { get; set; } = new("");
        [Export] public NodePath RowsContainerPath { get; set; } = new("");
        [Export] public string TitleText { get; set; } = "";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(220, 128);

        /// <summary>Name of the generated root - "GeneratedJobBoard", "GeneratedWorkerStatusPanel".</summary>
        protected abstract string GeneratedRootName { get; }

        /// <summary>Prefix and fallback for a generated row's node name - "Job", "Worker".</summary>
        protected abstract string RowNamePrefix { get; }

        protected Label? TitleLabel { get; private set; }
        protected Label? SummaryLabel { get; private set; }
        protected VBoxContainer? RowsContainer { get; private set; }

        private readonly Dictionary<string, Label> _rowLabels = new();

        /// <summary>Whether the panel has the controls it needs to draw anything.</summary>
        protected bool ControlsReady => SummaryLabel != null && RowsContainer != null;

        protected int RowCount => _rowLabels.Count;

        protected int RowsContainerChildCount => RowsContainer?.GetChildCount() ?? 0;

        protected string RowText(string id)
            => _rowLabels.TryGetValue(id, out Label? label) ? label.Text : "";

        public bool UsesSceneControls()
            => !TitleLabelPath.IsEmpty || !SummaryLabelPath.IsEmpty || !RowsContainerPath.IsEmpty
            || FindTitleLabel() != null || FindSummaryLabel() != null || FindRowsContainer() != null;

        protected bool HasAuthoredControls()
            => FindSummaryLabel() != null && FindRowsContainer() != null;

        protected Label? FindTitleLabel() => FindControl<Label>(TitleLabelPath, "Title");

        protected Label? FindSummaryLabel() => FindControl<Label>(SummaryLabelPath, "Summary");

        protected VBoxContainer? FindRowsContainer() => FindControl<VBoxContainer>(RowsContainerPath, "Rows");

        /// <summary>
        /// Binds the scene's own Title/Summary/Rows controls. A panel with a
        /// Summary and a Rows container is enough; the title is optional.
        /// </summary>
        protected bool BindExistingControls()
        {
            if (!UsesSceneControls())
                return false;

            Label? title = FindTitleLabel();
            Label? summary = FindSummaryLabel();
            VBoxContainer? rows = FindRowsContainer();

            if (summary == null || rows == null)
                return false;

            TitleLabel = title;
            SummaryLabel = summary;
            RowsContainer = rows;
            _rowLabels.Clear();
            return true;
        }

        /// <summary>The default layout, built only when the scene authored none and the panel is allowed to.</summary>
        protected void BuildGeneratedPanel()
        {
            ClearGeneratedControls();
            _rowLabels.Clear();

            var panel = new PanelContainer
            {
                Name = GeneratedRootName,
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

            TitleLabel = new Label
            {
                Name = "Title",
                Text = TitleText,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            KitChrome.SetColorOverrideIfChanged(TitleLabel, "font_color", Colors.White);
            layout.AddChild(TitleLabel);
            SetEditedOwner(TitleLabel);

            SummaryLabel = new Label
            {
                Name = "Summary",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            KitChrome.SetColorOverrideIfChanged(SummaryLabel, "font_color", new Color(0.86f, 0.89f, 0.92f));
            layout.AddChild(SummaryLabel);
            SetEditedOwner(SummaryLabel);

            RowsContainer = new VBoxContainer
            {
                Name = "Rows",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(RowsContainer, "separation", 2);
            layout.AddChild(RowsContainer);
            SetEditedOwner(RowsContainer);
        }

        protected void ClearGeneratedControls()
        {
            foreach (Node child in GetChildren())
                child.QueueFree();
            TitleLabel = null;
            SummaryLabel = null;
            RowsContainer = null;
        }

        protected void ApplyTitleText()
        {
            if (TitleLabel != null)
                TitleLabel.Text = TitleText;
        }

        /// <summary>Drops every row - the source the panel reads is gone.</summary>
        protected void ClearRows()
        {
            if (RowsContainer != null)
                foreach (Node child in RowsContainer.GetChildren())
                    child.QueueFree();
            _rowLabels.Clear();
        }

        /// <summary>
        /// Reconciles the rendered rows with the supplied list, in the caller's
        /// own order, up to maxVisible. Duplicate ids are skipped rather than
        /// fighting over one Label.
        /// </summary>
        protected void UpdateRows(IEnumerable<GridPanelRow> rows, int maxVisible)
        {
            if (RowsContainer == null)
                return;

            var seen = new HashSet<string>();
            int shown = 0;
            foreach (GridPanelRow entry in rows)
            {
                if (shown >= maxVisible)
                    break;

                if (string.IsNullOrEmpty(entry.Id) || !seen.Add(entry.Id))
                    continue;

                if (!_rowLabels.TryGetValue(entry.Id, out Label? row) || !GodotObject.IsInstanceValid(row))
                {
                    row = new Label
                    {
                        Name = $"{RowNamePrefix}_{SafeName(entry.Id, RowNamePrefix)}",
                        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                        CustomMinimumSize = new Vector2(0, 22)
                    };
                    RowsContainer.AddChild(row);
                    SetEditedOwner(row);
                    _rowLabels[entry.Id] = row;
                }

                row.Text = entry.Text;
                row.TooltipText = entry.Tooltip;
                KitChrome.SetColorOverrideIfChanged(row, "font_color", entry.Color);
                // Reused rows still follow the caller's sorted order instead of
                // keeping the position they were first created at.
                RowsContainer.MoveChild(row, shown);
                shown++;
            }

            var stale = new List<string>();
            foreach ((string id, Label row) in _rowLabels)
            {
                if (seen.Contains(id))
                    continue;

                if (GodotObject.IsInstanceValid(row))
                {
                    RowsContainer.RemoveChild(row);
                    row.QueueFree();
                }
                stale.Add(id);
            }
            foreach (string id in stale)
                _rowLabels.Remove(id);
        }
    }
}
