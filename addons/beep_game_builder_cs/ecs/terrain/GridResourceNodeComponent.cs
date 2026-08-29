using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Attach to a Node2D resource prop such as a tree, rock, berry bush, scrap
    /// pile, crate, or oilfield supply cache. Workers can gather it through the
    /// grid job queue, and harvested resources go into GridResourceWalletComponent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridResourceNodeComponent : Node2D
    {
        public const string ResourceNodeGroup = "grid_resource_nodes";

        [Signal] public delegate void GatherQueuedEventHandler(string jobId, int x, int y);
        [Signal] public delegate void GatheredEventHandler(string resourceId, int amount, int remainingAmount);
        [Signal] public delegate void GatherRejectedEventHandler(string reason);
        [Signal] public delegate void DepletedEventHandler();

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public bool UseExplicitCell { get; set; } = false;
        [Export] public Vector2I Cell { get; set; } = Vector2I.Zero;
        [Export] public string ResourceId { get; set; } = "wood";
        [Export(PropertyHint.Range, "0,9999,1")] public int Amount { get; set; } = 5;
        [Export(PropertyHint.Range, "1,9999,1")] public int AmountPerGather { get; set; } = 1;
        [Export] public string GatherJobKind { get; set; } = "gather";
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float GatherSeconds { get; set; } = 1.5f;
        [Export] public int GatherPriority { get; set; } = 0;
        [Export] public bool HideWhenDepleted { get; set; } = true;
        [Export] public bool DisableProcessWhenDepleted { get; set; } = true;
        [Export] public bool QueueFreeWhenDepleted { get; set; } = false;
        [Export] public bool MarkCellOccupiedOnReady { get; set; } = false;
        [Export] public bool ReleaseOccupiedCellWhenDepleted { get; set; } = true;

        private GridProjectionComponent? _grid;
        private GridPlacementComponent? _placement;
        private GridResourceWalletComponent? _wallet;
        private GridJobQueueComponent? _jobs;
        private bool _depleted;
        private Vector2I _reservedCell = new(int.MinValue, int.MinValue);

        public bool IsDepleted => _depleted || Amount <= 0;
        public int RemainingAmount => Mathf.Max(0, Amount);
        public string ActiveGatherJobId { get; private set; } = "";

        public override void _Ready()
        {
            ResolveReferences();
            AddToGroup(ResourceNodeGroup);
            if (MarkCellOccupiedOnReady && !IsDepleted)
                ReserveCurrentCell();
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            ReleaseReservedCell();
            RemoveFromGroup(ResourceNodeGroup);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!UseExplicitCell && GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent unless UseExplicitCell is enabled." };
            if (string.IsNullOrWhiteSpace(ResourceId))
                return new[] { "ResourceId must not be empty." };
            if (AmountPerGather <= 0)
                return new[] { "AmountPerGather must be greater than zero." };
            return Array.Empty<string>();
        }

        public Vector2I CurrentCell()
        {
            if (UseExplicitCell)
                return Cell;

            ResolveReferences();
            return _grid == null ? new Vector2I(int.MinValue, int.MinValue) : _grid.WorldToCell(GlobalPosition);
        }

        public string QueueGatherJob()
        {
            ResolveReferences();
            ClearStaleActiveGatherJob();

            if (!string.IsNullOrEmpty(ActiveGatherJobId))
                return ActiveGatherJobId;

            if (_jobs == null)
            {
                Reject("missing_job_queue");
                return "";
            }

            if (IsDepleted)
            {
                Reject("depleted");
                return "";
            }

            Vector2I cell = CurrentCell();
            if (cell.X == int.MinValue || cell.Y == int.MinValue)
            {
                Reject("invalid_cell");
                return "";
            }

            string jobId = _jobs.AddJob(cell, GatherJobKind, GatherSeconds, GatherPriority);
            ActiveGatherJobId = jobId;
            EmitSignal(SignalName.GatherQueued, jobId, cell.X, cell.Y);
            return jobId;
        }

        public bool Gather()
            => GatherForJob("");

        public bool GatherAllForJob(string jobId)
        {
            bool gatheredAny = false;
            int guard = 0;
            while (!IsDepleted && guard++ < 10000)
            {
                int before = Amount;
                if (!GatherForJob(jobId))
                    return gatheredAny;

                gatheredAny = true;
                if (Amount >= before)
                    break;
            }

            return gatheredAny;
        }

        public bool GatherForJob(string jobId)
        {
            ResolveReferences();
            if (IsDepleted)
                return Reject("depleted");

            if (!ResourceWalletPath.IsEmpty && _wallet == null)
                return Reject("missing_resource_wallet");

            string id = Normalize(ResourceId);
            if (string.IsNullOrEmpty(id))
                return Reject("missing_resource_id");

            int gathered = Mathf.Min(Mathf.Max(1, AmountPerGather), Amount);
            Amount = Mathf.Max(0, Amount - gathered);
            ClearActiveGatherJob(jobId);
            _wallet?.AddAmount(id, gathered);
            EmitSignal(SignalName.Gathered, id, gathered, Amount);

            if (Amount <= 0)
                Deplete();

            return true;
        }

        public Godot.Collections.Dictionary CaptureState()
            => new()
            {
                ["cell"] = CurrentCell(),
                ["resource_id"] = Normalize(ResourceId),
                ["amount"] = Amount,
                ["amount_per_gather"] = AmountPerGather,
                ["depleted"] = IsDepleted
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            if (state.ContainsKey("cell"))
            {
                ReleaseReservedCell();
                Cell = GridVariantReader.Vector2I(state, "cell", Cell);
                UseExplicitCell = true;
            }

            if (state.ContainsKey("resource_id"))
                ResourceId = state["resource_id"].AsString();
            if (state.ContainsKey("amount"))
                Amount = Mathf.Max(0, GridVariantReader.Int(state, "amount", Amount));
            if (state.ContainsKey("amount_per_gather"))
                AmountPerGather = Mathf.Max(1, GridVariantReader.Int(state, "amount_per_gather", AmountPerGather));

            bool depleted = GridVariantReader.Bool(state, "depleted", false);
            if (depleted || Amount <= 0)
                Deplete();
            else
            {
                _depleted = false;
                Visible = true;
                ProcessMode = ProcessModeEnum.Inherit;
                if (MarkCellOccupiedOnReady)
                    ReserveCurrentCell();
            }
        }

        private void Deplete()
        {
            if (_depleted)
                return;

            _depleted = true;
            Amount = 0;
            ClearActiveGatherJob("");
            if (ReleaseOccupiedCellWhenDepleted)
                ReleaseReservedCell();
            EmitSignal(SignalName.Depleted);
            if (HideWhenDepleted)
                Visible = false;
            if (DisableProcessWhenDepleted)
                ProcessMode = ProcessModeEnum.Disabled;
            if (QueueFreeWhenDepleted)
                QueueFree();
        }

        private bool Reject(string reason)
        {
            EmitSignal(SignalName.GatherRejected, reason);
            return false;
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;

            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs))
                _jobs = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;
        }

        private void ClearStaleActiveGatherJob()
        {
            if (string.IsNullOrEmpty(ActiveGatherJobId))
                return;

            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs) || !_jobs.HasJob(ActiveGatherJobId))
            {
                ActiveGatherJobId = "";
                return;
            }

            GridJobQueueComponent.GridJobState state = _jobs.GetJobState(ActiveGatherJobId);
            if (state is GridJobQueueComponent.GridJobState.Completed or GridJobQueueComponent.GridJobState.Cancelled)
                ActiveGatherJobId = "";
        }

        private void ClearActiveGatherJob(string completedJobId)
        {
            if (string.IsNullOrEmpty(ActiveGatherJobId))
                return;

            if (string.IsNullOrEmpty(completedJobId) || ActiveGatherJobId == completedJobId)
            {
                ActiveGatherJobId = "";
                return;
            }

            ClearStaleActiveGatherJob();
        }

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_');

        private void ReserveCurrentCell()
        {
            ResolveReferences();
            if (_placement == null)
                return;

            Vector2I cell = CurrentCell();
            if (cell.X == int.MinValue || cell.Y == int.MinValue)
                return;

            if (_reservedCell != cell)
                ReleaseReservedCell();

            _placement.SetOccupied(cell, true);
            _reservedCell = cell;
        }

        private void ReleaseReservedCell()
        {
            if (_reservedCell.X == int.MinValue || _reservedCell.Y == int.MinValue)
                return;

            ResolveReferences();
            _placement?.SetOccupied(_reservedCell, false);
            _reservedCell = new Vector2I(int.MinValue, int.MinValue);
        }
    }
}
