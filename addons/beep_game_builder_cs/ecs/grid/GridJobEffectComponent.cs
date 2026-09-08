using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Applies cell effects when GridJobQueueComponent jobs complete. This turns
    /// settler/worker jobs into land state changes without custom project glue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridJobEffectComponent : Node
    {
        [Signal] public delegate void JobEffectAppliedEventHandler(string jobId, string kind, int x, int y, string effect);
        [Signal] public delegate void JobEffectRejectedEventHandler(string jobId, string kind, int x, int y, string reason);

        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath ToolActionPath { get; set; } = new("");
        [Export] public NodePath ResourceNodesRootPath { get; set; } = new("");
        [Export] public bool AutoConnect { get; set; } = true;
        [Export] public bool UseToolActionForHarvest { get; set; } = true;
        /// <summary>
        /// Route a completed clear/till/water job through
        /// GridToolActionComponent.ApplyToCell instead of mutating
        /// GridCellDataComponent directly, so a job-completed effect is
        /// gated by the same terrain-workability rule (CanWorkTerrain,
        /// BlockedTerrainKinds, the Blocked/Tilled flag checks) the
        /// click-driven tool path already enforces for the identical action.
        /// A completed job could otherwise clear/till/water a cell the tool
        /// path would have refused outright. Falls back to the direct
        /// mutation, ungated, when no GridToolActionComponent is wired.
        /// </summary>
        [Export] public bool UseToolActionForClear { get; set; } = true;
        [Export] public bool UseToolActionForTill { get; set; } = true;
        [Export] public bool UseToolActionForWater { get; set; } = true;
        [Export] public bool ClearLandGathersResourceNode { get; set; } = true;

        private GridJobQueueComponent? _queue;
        private GridJobQueueComponent? _connectedQueue;
        private GridCellDataComponent? _cells;
        private GridToolActionComponent? _tools;
        private bool _connected;

        public override void _Ready()
        {
            ResolveReferences();
            if (AutoConnect && !Engine.IsEditorHint())
                ConnectQueue();
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectQueue();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (JobQueuePath.IsEmpty)
                return new[] { "JobQueuePath should point to a GridJobQueueComponent." };
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            return Array.Empty<string>();
        }

        public void ConnectQueue()
        {
            ResolveReferences();
            if (_queue == null)
                return;

            if (_connected && _connectedQueue == _queue)
                return;

            DisconnectQueue();
            _queue.JobCompleted += OnJobCompleted;
            _connectedQueue = _queue;
            _connected = true;
        }

        public void DisconnectQueue()
        {
            if (_connectedQueue != null && GodotObject.IsInstanceValid(_connectedQueue) && _connected)
                _connectedQueue.JobCompleted -= OnJobCompleted;
            _connectedQueue = null;
            _connected = false;
        }

        public bool ApplyJobEffect(string jobId)
        {
            ResolveReferences();
            if (_queue == null)
                return Reject(jobId, "", new Vector2I(int.MinValue, int.MinValue), "missing_job_queue");

            string kind = _queue.GetJobKind(jobId);
            Vector2I cell = _queue.GetJobCell(jobId);
            return ApplyJobEffect(jobId, kind, cell);
        }

        public bool ApplyJobEffect(string jobId, string kind, Vector2I cell)
        {
            ResolveReferences();
            if (cell.X == int.MinValue || cell.Y == int.MinValue)
                return Reject(jobId, kind, cell, "invalid_cell");

            string normalized = NormalizeKind(kind);
            if (normalized is "gather" or "collect" or "forage" or "chop" or "mine" or "fish")
                return ApplyGather(jobId, kind, cell);

            if (_cells == null)
                return Reject(jobId, kind, cell, "missing_cell_data");

            bool requiresTools = normalized switch
            {
                "clear_land" or "clear" => UseToolActionForClear,
                "till" or "hoe" or "prepare_soil" => UseToolActionForTill,
                "water" => UseToolActionForWater,
                "harvest" => UseToolActionForHarvest,
                _ => false
            };
            if (requiresTools && !ToolActionPath.IsEmpty && _tools is null)
                return Reject(jobId, kind, cell, "missing_tool_action");

            return normalized switch
            {
                "clear_land" or "clear" => ApplyClear(jobId, kind, cell),
                "till" or "hoe" or "prepare_soil" => ApplyTill(jobId, kind, cell),
                "water" => ApplyWater(jobId, kind, cell),
                "harvest" => ApplyHarvest(jobId, kind, cell),
                _ => Reject(jobId, kind, cell, "unknown_job_kind")
            };
        }

        private void OnJobCompleted(string jobId, string workerId)
        {
            var source = _connectedQueue;
            ResolveReferences();
            if (source is null || source != _queue)
                return;
            ApplyJobEffect(jobId);
        }

        private bool ApplyClear(string jobId, string kind, Vector2I cell)
        {
            if (UseToolActionForClear && _tools is not null && !_tools.CanClearCell(cell))
                return Reject(jobId, kind, cell, "clear_rejected");

            if (ClearLandGathersResourceNode && FindResourceNodeAt(cell) is { } resource)
            {
                if (!resource.GatherAllForJob(jobId))
                    return Reject(jobId, kind, cell, "clear_resource_rejected");
            }

            if (UseToolActionForClear && _tools != null)
            {
                return _tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Clear)
                    ? Applied(jobId, kind, cell, "clear_land")
                    : Reject(jobId, kind, cell, "clear_rejected");
            }

            _cells!.ClearLand(cell);
            return Applied(jobId, kind, cell, "clear_land");
        }

        private bool ApplyTill(string jobId, string kind, Vector2I cell)
        {
            if (UseToolActionForTill && _tools != null)
            {
                return _tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Hoe)
                    ? Applied(jobId, kind, cell, "till")
                    : Reject(jobId, kind, cell, "till_rejected");
            }

            _cells!.Till(cell);
            return Applied(jobId, kind, cell, "till");
        }

        private bool ApplyWater(string jobId, string kind, Vector2I cell)
        {
            if (UseToolActionForWater && _tools != null)
            {
                return _tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Water)
                    ? Applied(jobId, kind, cell, "water")
                    : Reject(jobId, kind, cell, "water_rejected");
            }

            _cells!.Water(cell);
            return Applied(jobId, kind, cell, "water");
        }

        private bool ApplyHarvest(string jobId, string kind, Vector2I cell)
        {
            if (UseToolActionForHarvest && _tools != null)
            {
                bool ok = _tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Harvest);
                return ok
                    ? Applied(jobId, kind, cell, "harvest")
                    : Reject(jobId, kind, cell, "harvest_rejected");
            }

            if (!_cells!.HarvestCrop(cell))
                return Reject(jobId, kind, cell, "missing_crop");

            return Applied(jobId, kind, cell, "harvest");
        }

        private bool ApplyGather(string jobId, string kind, Vector2I cell)
        {
            GridResourceNodeComponent? resource = FindResourceNodeAt(cell);
            if (resource == null)
                return Reject(jobId, kind, cell, "missing_resource_node");

            bool ok = resource.GatherForJob(jobId);
            return ok
                ? Applied(jobId, kind, cell, "gather")
                : Reject(jobId, kind, cell, "gather_rejected");
        }

        private bool Applied(string jobId, string kind, Vector2I cell, string effect)
        {
            EmitSignal(SignalName.JobEffectApplied, jobId, kind, cell.X, cell.Y, effect);
            return true;
        }

        private bool Reject(string jobId, string kind, Vector2I cell, string reason)
        {
            EmitSignal(SignalName.JobEffectRejected, jobId, kind, cell.X, cell.Y, reason);
            return false;
        }

        private void ResolveReferences()
        {
            if (!JobQueuePath.IsEmpty) _queue = GetNodeOrNull<Node>(JobQueuePath) as GridJobQueueComponent;
            else EntityComponent.Resolve(this, JobQueuePath, ref _queue);
            if (_connected && _connectedQueue != _queue)
                DisconnectQueue();

            if (!CellDataPath.IsEmpty) _cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            else EntityComponent.Resolve(this, CellDataPath, ref _cells);
            if (!ToolActionPath.IsEmpty) _tools = GetNodeOrNull<GridToolActionComponent>(ToolActionPath);
            else EntityComponent.Resolve(this, ToolActionPath, ref _tools);
        }

        private GridResourceNodeComponent? FindResourceNodeAt(Vector2I cell)
        {
            Node? root = !ResourceNodesRootPath.IsEmpty
                ? GetNodeOrNull<Node>(ResourceNodesRootPath)
                : IsInsideTree() ? GetTree()?.CurrentScene : null;
            if (root == null)
                return null;

            foreach (Node node in GetTree()?.GetNodesInGroup(GridResourceNodeComponent.ResourceNodeGroup) ?? new Godot.Collections.Array<Node>())
            {
                if (node is GridResourceNodeComponent resource
                    && GodotObject.IsInstanceValid(resource)
                    && IsDescendantOrSelf(root, resource)
                    && !resource.IsDepleted
                    && resource.CurrentCell() == cell)
                {
                    return resource;
                }
            }

            return FindResourceNodeAtRecursive(root, cell);
        }

        private static GridResourceNodeComponent? FindResourceNodeAtRecursive(Node root, Vector2I cell)
        {
            if (root is GridResourceNodeComponent resource && !resource.IsDepleted && resource.CurrentCell() == cell)
                return resource;

            foreach (Node child in root.GetChildren())
                if (FindResourceNodeAtRecursive(child, cell) is { } found)
                    return found;

            return null;
        }

        private static bool IsDescendantOrSelf(Node ancestor, Node node)
        {
            Node? current = node;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.GetParent();
            }
            return false;
        }

        private static string NormalizeKind(string kind)
            => string.IsNullOrWhiteSpace(kind) ? "" : kind.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
