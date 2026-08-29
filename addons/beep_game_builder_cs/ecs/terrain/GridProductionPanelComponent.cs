using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridProductionComponent buildings. It scans a
    /// production root, shows machine/recipe state, and exposes start, pause,
    /// resume, and cancel commands without custom game UI glue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridProductionPanelComponent : Control
    {
        [Signal] public delegate void ProductionCommandRequestedEventHandler(string machinePath, string command, string recipeId);

        [Export] public NodePath ProductionRootPath { get; set; } = new("");
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath SummaryLabelPath { get; set; } = new("");
        [Export] public NodePath RowsContainerPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool AutoRefresh { get; set; } = true;
        [Export(PropertyHint.Range, "1,24,1")] public int MaxVisibleMachines { get; set; } = 6;
        [Export] public string TitleText { get; set; } = "Production";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(246, 142);

        private Node? _productionRoot;
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
            if (AutoRefresh || Engine.IsEditorHint())
                RefreshPanel();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (ProductionRootPath.IsEmpty)
                return new[] { "ProductionRootPath should point to the Node that contains production buildings." };
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
                Name = "GeneratedProductionPanel",
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

            foreach (Node child in _rows.GetChildren())
                child.QueueFree();
            _rowLabels.Clear();

            var machines = Machines();
            int active = 0;
            foreach (GridProductionComponent machine in machines)
                if (machine.State == GridProductionComponent.ProductionState.Producing)
                    active++;

            _summary.Text = $"Machines {machines.Count} | Active {active}";

            int shown = 0;
            foreach (GridProductionComponent machine in machines)
            {
                string key = MachineKey(machine);
                var row = new Label
                {
                    Name = $"Production_{SafeName(key)}",
                    Text = TextForMachine(machine),
                    TooltipText = key,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    CustomMinimumSize = new Vector2(0, 22)
                };
                KitChrome.SetColorOverrideIfChanged(row, "font_color", ColorForState(machine.State));
                _rows.AddChild(row);
                SetEditedOwner(row);
                _rowLabels[key] = row;

                shown++;
                if (shown >= MaxVisibleMachines)
                    break;
            }
        }

        public string SummaryText()
        {
            RefreshPanel();
            return _summary?.Text ?? "";
        }

        public string TextForMachine(string machinePath)
        {
            RefreshPanel();
            return _rowLabels.TryGetValue(machinePath, out Label? label) ? label.Text : "";
        }

        public string TextForMachine(GridProductionComponent machine)
        {
            string name = MachineName(machine);
            string state = machine.State.ToString();
            string recipe = ActiveRecipeName(machine);
            string progress = machine.State == GridProductionComponent.ProductionState.Producing
                ? $" {Mathf.RoundToInt(machine.Progress01 * 100f)}%"
                : "";
            return string.IsNullOrEmpty(recipe)
                ? $"{name}: {state}"
                : $"{name}: {state} {recipe}{progress}";
        }

        public int VisibleMachineRowCount()
            => _rowLabels.Count;

        public bool StartMachine(string machinePath, string recipeId = "")
        {
            GridProductionComponent? machine = FindMachine(machinePath);
            if (machine == null)
                return false;

            string id = string.IsNullOrWhiteSpace(recipeId) ? machine.ActiveRecipeId : recipeId;
            EmitSignal(SignalName.ProductionCommandRequested, machinePath, "start", id);
            bool started = machine.StartProduction(id);
            RefreshPanel();
            return started;
        }

        public bool PauseMachine(string machinePath)
        {
            GridProductionComponent? machine = FindMachine(machinePath);
            if (machine == null || machine.State != GridProductionComponent.ProductionState.Producing)
                return false;

            EmitSignal(SignalName.ProductionCommandRequested, machinePath, "pause", machine.CurrentRecipeId);
            machine.PauseProduction();
            RefreshPanel();
            return true;
        }

        public bool ResumeMachine(string machinePath)
        {
            GridProductionComponent? machine = FindMachine(machinePath);
            if (machine == null || machine.State != GridProductionComponent.ProductionState.Paused)
                return false;

            EmitSignal(SignalName.ProductionCommandRequested, machinePath, "resume", machine.CurrentRecipeId);
            machine.ResumeProduction();
            RefreshPanel();
            return true;
        }

        public bool CancelMachine(string machinePath, bool refundInputs = false)
        {
            GridProductionComponent? machine = FindMachine(machinePath);
            if (machine == null || machine.State == GridProductionComponent.ProductionState.Idle)
                return false;

            EmitSignal(SignalName.ProductionCommandRequested, machinePath, "cancel", machine.CurrentRecipeId);
            machine.CancelProduction(refundInputs);
            RefreshPanel();
            return true;
        }

        private GridProductionComponent? FindMachine(string machinePath)
        {
            foreach (GridProductionComponent machine in Machines())
                if (string.Equals(MachineKey(machine), machinePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(MachineName(machine), machinePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(machine.Name, machinePath, StringComparison.OrdinalIgnoreCase))
                    return machine;
            return null;
        }

        private List<GridProductionComponent> Machines()
        {
            ResolveReferences();
            var machines = new List<GridProductionComponent>();
            if (_productionRoot != null)
                CollectMachines(_productionRoot, machines);
            machines.Sort((a, b) => string.Compare(MachineKey(a), MachineKey(b), StringComparison.OrdinalIgnoreCase));
            return machines;
        }

        private void ResolveReferences()
        {
            if (_productionRoot == null || !GodotObject.IsInstanceValid(_productionRoot))
                _productionRoot = !ProductionRootPath.IsEmpty ? GetNodeOrNull<Node>(ProductionRootPath) : null;
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

        private static void CollectMachines(Node node, List<GridProductionComponent> machines)
        {
            if (node is GridProductionComponent machine)
                machines.Add(machine);

            foreach (Node child in node.GetChildren())
                CollectMachines(child, machines);
        }

        private static string MachineKey(GridProductionComponent machine)
            => machine.GetPath().ToString();

        private static string MachineName(GridProductionComponent machine)
            => machine.GetParent()?.Name ?? machine.Name;

        private static string ActiveRecipeName(GridProductionComponent machine)
        {
            string recipeId = string.IsNullOrWhiteSpace(machine.CurrentRecipeId) ? machine.ActiveRecipeId : machine.CurrentRecipeId;
            GridProductionRecipe? recipe = machine.FindRecipe(recipeId);
            if (recipe == null)
                return recipeId;
            return string.IsNullOrWhiteSpace(recipe.DisplayName) ? recipe.RecipeId : recipe.DisplayName;
        }

        private static Color ColorForState(GridProductionComponent.ProductionState state)
            => state switch
            {
                GridProductionComponent.ProductionState.Producing => new Color(0.48f, 0.92f, 0.58f),
                GridProductionComponent.ProductionState.Paused => new Color(1f, 0.78f, 0.22f),
                _ => new Color(0.82f, 0.86f, 0.9f)
            };

        private static string SafeName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Machine" : value.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return result.Replace(' ', '_').Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}
