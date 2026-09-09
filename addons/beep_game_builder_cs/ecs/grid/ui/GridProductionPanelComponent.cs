using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridProductionComponent buildings. It scans a
    /// production root, shows machine/recipe state, and exposes start, pause,
    /// resume, and cancel commands without custom game UI glue.
    ///
    /// The panel surface itself - authored-control binding, the generated
    /// fallback layout, and the row diff - is GridListPanelComponent's; this
    /// file owns only the machine roster and the production commands.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridProductionPanelComponent : GridListPanelComponent
    {
        [Signal] public delegate void ProductionCommandRequestedEventHandler(string machinePath, string command, string recipeId);

        [Export] public NodePath ProductionRootPath { get; set; } = new("");

        /// <summary>
        /// Optional. Wired (or found scene-wide when empty), a placement this
        /// reports via PlacementPlaced is checked for a GridProductionComponent
        /// and appended to the cached roster instead of triggering a full
        /// re-walk of ProductionRootPath. A machine added some OTHER way - one
        /// hand-placed in the scene without going through GridPlacementComponent,
        /// or added after this panel's first refresh with no placement signal
        /// at all - is missed by the incremental path; call
        /// InvalidateMachineCache() after adding one that way.
        /// </summary>
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// How often AutoRefresh repaints. Progress percentages read fine at a
        /// few updates per second; refreshing every frame used to rebuild every
        /// row Label and rescan the production root 60 times a second.
        /// </summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.25f;
        [Export(PropertyHint.Range, "1,24,1")] public int MaxVisibleMachines { get; set; } = 6;

        public GridProductionPanelComponent()
        {
            TitleText = "Production";
            PanelMinimumSize = new Vector2(246, 142);
        }

        protected override string GeneratedRootName => "GeneratedProductionPanel";
        protected override string RowNamePrefix => "Production";

        private Node? _productionRoot;
        private GridPlacementComponent? _placement;
        private bool _placementConnected;
        private readonly GridRosterCache<GridProductionComponent> _roster = new();
        private readonly Dictionary<GridProductionComponent, string> _machineKeys = new();
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
            DisconnectPlacement();
        }

        /// <summary>Forces the next Machines() call to re-walk ProductionRootPath
        /// from scratch, for a roster change this panel's incremental cache
        /// cannot see on its own (see the PlacementPath doc comment).</summary>
        public void InvalidateMachineCache() => _roster.Invalidate();

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

            BuildGeneratedPanel();
            RefreshPanel();
        }

        public void RefreshPanel()
        {
            ResolveReferences();
            if (!ControlsReady)
                return;

            ApplyTitleText();

            var machines = Machines();
            int active = 0;
            foreach (GridProductionComponent machine in machines)
                if (machine.State == GridProductionComponent.ProductionState.Producing)
                    active++;

            SummaryLabel!.Text = $"Machines {machines.Count} | Active {active}";

            UpdateRows(MachineRows(machines), MaxVisibleMachines);
        }

        private IEnumerable<GridPanelRow> MachineRows(List<GridProductionComponent> machines)
        {
            foreach (GridProductionComponent machine in machines)
                yield return new GridPanelRow(
                    CachedKey(machine),
                    TextForMachine(machine),
                    ColorForState(machine.State));
        }

        public string SummaryText()
        {
            RefreshPanel();
            return SummaryLabel?.Text ?? "";
        }

        public string TextForMachine(string machinePath)
        {
            RefreshPanel();
            return RowText(machinePath);
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

        public int VisibleMachineRowCount() => RowCount;

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
                if (string.Equals(CachedKey(machine), machinePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(MachineName(machine), machinePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(machine.Name, machinePath, StringComparison.OrdinalIgnoreCase))
                    return machine;
            return null;
        }

        /// <summary>
        /// Cached and only rebuilt on a genuine roster change - a new machine
        /// arrives via GridPlacementComponent.PlacementPlaced (appended, no
        /// re-walk), and a freed one drops out on the cheap IsInstanceValid
        /// prune below - rather than re-walking ProductionRootPath's whole
        /// subtree every refresh tick.
        /// </summary>
        private List<GridProductionComponent> Machines()
        {
            ResolveReferences();
            return _roster.Members(BuildMachines, CompareMachines, machine => _machineKeys.Remove(machine));
        }

        private List<GridProductionComponent> BuildMachines()
        {
            var machines = new List<GridProductionComponent>();
            if (_productionRoot != null)
                CollectMachines(_productionRoot, machines);
            _machineKeys.Clear();
            foreach (GridProductionComponent machine in machines)
                _machineKeys[machine] = MachineKey(machine);
            return machines;
        }

        private int CompareMachines(GridProductionComponent a, GridProductionComponent b)
            => string.Compare(MachineKey(a), MachineKey(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>Each machine's sort/row key, computed once and cached -
        /// MachineKey walks the node to the scene root and allocates a fresh
        /// string on every call, which RefreshPanel used to do 2-3 times per
        /// machine every tick via the sort comparator and the seen.Add loop.</summary>
        private string CachedKey(GridProductionComponent machine)
        {
            if (!_machineKeys.TryGetValue(machine, out string? key))
            {
                key = MachineKey(machine);
                _machineKeys[machine] = key;
            }
            return key;
        }

        private void OnPlacementPlaced(string buildId, Node2D placed, int x, int y)
        {
            if (!_roster.HasCache)
                return;

            GridProductionComponent? machine = EntityComponent.FindComponent<GridProductionComponent>(placed, recursive: true);
            if (machine != null && _roster.Append(machine, CompareMachines))
                _machineKeys[machine] = MachineKey(machine);
        }

        private void ConnectPlacement()
        {
            if (_placement == null || _placementConnected)
                return;

            _placement.PlacementPlaced += OnPlacementPlaced;
            _placementConnected = true;
        }

        private void DisconnectPlacement()
        {
            if (_placement != null && GodotObject.IsInstanceValid(_placement) && _placementConnected)
                _placement.PlacementPlaced -= OnPlacementPlaced;
            _placementConnected = false;
        }

        private void ResolveReferences()
        {
            if (_productionRoot == null || !GodotObject.IsInstanceValid(_productionRoot))
                _productionRoot = !ProductionRootPath.IsEmpty ? GetNodeOrNull<Node>(ProductionRootPath) : null;

            if (_placement != null && !GodotObject.IsInstanceValid(_placement))
            {
                _placement = null;
                _placementConnected = false;
            }
            // Never resolved in the editor - placement is a runtime roster
            // source only.
            if (_placement == null && !Engine.IsEditorHint())
            {
                EntityComponent.Resolve(this, PlacementPath, ref _placement);
                ConnectPlacement();
            }
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
    }
}
