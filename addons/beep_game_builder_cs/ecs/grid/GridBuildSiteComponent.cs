using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Turns placed build definitions with BuildTurns into worker-completed
    /// build sites. This gives builder games a blueprint -> job -> finished
    /// building loop without project-specific glue. The site's duration is
    /// turns - a turn is a day - so one authored number means the same amount
    /// of world time whether the game runs on turns or in real time.
    ///
    /// A build with RequiredMaterials adds one more step first: delivery ->
    /// blueprint -> job -> finished. The job is not created - so no worker
    /// can claim and complete it - until a GridStorageComponent found on the
    /// placed node reports every required resource in stock, which an
    /// ITransporter fills exactly like any other cargo hold. A build with no
    /// RequiredMaterials skips straight to the job, unchanged from before.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildSiteComponent : Node
    {
        [Signal] public delegate void BuildSiteCreatedEventHandler(string buildId, string jobId, Node2D placed, int x, int y);
        [Signal] public delegate void BuildSiteAwaitingMaterialsEventHandler(string buildId, Node2D placed, int x, int y);
        [Signal] public delegate void BuildSiteCompletedEventHandler(string buildId, string jobId, Node2D placed, int x, int y);
        [Signal] public delegate void BuildSiteCancelledEventHandler(string buildId, string jobId, int x, int y);
        [Signal] public delegate void BuildSiteRejectedEventHandler(string buildId, int x, int y, string reason);

        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath BuildCatalogPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        /// <summary>World binding for directly registered sites; placement already binds its GridObject.</summary>
        [Export] public NodePath ChunkCellDataPath { get; set; } = new("");

        [Export] public bool AutoConnect { get; set; } = true;
        [Export(PropertyHint.Range, "1,64,1")] public int PendingChecksPerFrame { get; set; } = 8;
        public int PendingChecksLastFrame { get; private set; }
        /// <summary>
        /// Whether cancelling a build job also removes the placed node. On by
        /// default: before it, a cancelled job just forgot the site and left a
        /// tinted, incomplete, footprint-blocking building standing forever.
        /// </summary>
        [Export] public bool RemovePlacedOnJobCancelled { get; set; } = true;
        /// <summary>
        /// Whether cancelling refunds the build's costs - only when placement
        /// recorded that the wallet was actually charged for this node.
        /// </summary>
        [Export] public bool RefundOnJobCancelled { get; set; } = true;
        /// <summary>
        /// Hide the placed node until its build completes. Always in effect
        /// for a build with ConstructionStages: those scenes ARE its
        /// under-construction look (GridBuildStageVisualComponent draws them
        /// beside the placed node, outside its modulate), and every real
        /// game hard-swaps to the finished art at the end - a finished
        /// building showing through its own construction site at 72% alpha
        /// is the "fading in" look, not a site. For a build with no stages
        /// this export decides, and UnderConstructionModulate tints it.
        /// </summary>
        [Export] public bool HidePlacedUntilBuilt { get; set; } = false;
        [Export] public Color UnderConstructionModulate { get; set; } = new(1f, 0.88f, 0.46f, 0.72f);
        [Export] public Color CompletedModulate { get; set; } = Colors.White;

        private readonly Dictionary<string, BuildSite> _sitesByJobId = new();

        /// <summary>
        /// Sites whose job has not been created yet because their build has
        /// RequiredMaterials that have not all arrived. Separate from
        /// _sitesByJobId because there IS no job id until the last delivery
        /// lands - adding one early would let a generic worker claim and
        /// complete it before the site actually has what it needs.
        /// </summary>
        private readonly Dictionary<Node2D, PendingBuild> _pendingByPlaced = new();
        private readonly LinkedList<Node2D> _pendingChecks = new();
        private readonly HashSet<Node2D> _committingMaterials = new();
        private GridPlacementComponent? _placement;
        private GridBuildCatalogComponent? _catalog;
        private GridJobQueueComponent? _jobs;
        private GridResourceWalletComponent? _wallet;
        private GridNavigationComponent? _navigation;
        private bool _placementConnected;
        private bool _jobsConnected;

        public override void _Ready()
        {
            ResolveReferences();
            if (AutoConnect && !Engine.IsEditorHint())
                ConnectSystems();
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectSystems();
            DisconnectPendingBuilds();
        }

        public override void _Process(double delta)
        {
            PendingChecksLastFrame = 0;
            if (Engine.IsEditorHint()) return;
            int count = Math.Min(_pendingChecks.Count, Mathf.Clamp(PendingChecksPerFrame, 1, 64));
            // Rotate before checking: callbacks can remove sites or register new ones.
            while (PendingChecksLastFrame < count && _pendingChecks.First is { } next)
            {
                _pendingChecks.RemoveFirst();
                _pendingChecks.AddLast(next);
                PendingChecksLastFrame++;
                CheckPendingMaterials(next.Value);
            }
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (PlacementPath.IsEmpty)
                return new[] { "PlacementPath should point to a GridPlacementComponent." };
            if (BuildCatalogPath.IsEmpty)
                return new[] { "BuildCatalogPath should point to a GridBuildCatalogComponent." };
            if (JobQueuePath.IsEmpty)
                return new[] { "JobQueuePath should point to a GridJobQueueComponent." };
            return Array.Empty<string>();
        }

        public void ConnectSystems()
        {
            ResolveReferences();
            if (_placement != null && !_placementConnected)
            {
                _placement.PlacementPlaced += OnPlacementPlaced;
                _placementConnected = true;
            }

            if (_jobs != null && !_jobsConnected)
            {
                _jobs.JobCompleted += OnJobCompleted;
                _jobs.JobCancelled += OnJobCancelled;
                _jobsConnected = true;
            }
        }

        public void DisconnectSystems()
        {
            if (_placement != null && GodotObject.IsInstanceValid(_placement) && _placementConnected)
                _placement.PlacementPlaced -= OnPlacementPlaced;
            if (_jobs != null && GodotObject.IsInstanceValid(_jobs) && _jobsConnected)
            {
                _jobs.JobCompleted -= OnJobCompleted;
                _jobs.JobCancelled -= OnJobCancelled;
            }

            _placementConnected = false;
            _jobsConnected = false;
        }

        public bool RegisterPlacedBuild(string buildId, Node2D placed, Vector2I cell)
        {
            ResolveReferences();
            if (_catalog == null)
                return Reject(buildId, cell, "missing_build_catalog");
            if (_jobs == null)
                return Reject(buildId, cell, "missing_job_queue");
            if (placed == null || !GodotObject.IsInstanceValid(placed))
                return Reject(buildId, cell, "missing_placed_node");
            if (_committingMaterials.Contains(placed) || _pendingByPlaced.ContainsKey(placed))
                return false;
            foreach (BuildSite site in _sitesByJobId.Values)
                if (site.Placed == placed) return false;

            GridBuildDefinition? build = _catalog.FindBuild(buildId);
            if (build == null)
                return Reject(buildId, cell, "missing_build_definition");

            // Zero turns is not a site at all: the build has no construction
            // phase and is simply placed. This is the default, so a definition
            // opts INTO construction by declaring the turns it takes. Reported
            // through the return value, not swallowed.
            if (build.EffectiveBuildTurns <= 0)
                return false;

            if (!TryChooseApproach(cell, build.EffectiveFootprint, out Vector2I approach))
                return Reject(buildId, cell, "no_build_approach");

            GridStorageComponent? storage = null;
            if (build.RequiredMaterials.Count > 0)
            {
                storage = EntityComponent.FindComponent<GridStorageComponent>(placed, recursive: true);
                if (storage is null)
                    return Reject(buildId, cell, "missing_material_storage");
            }

            if (!ChunkCellDataPath.IsEmpty)
            {
                var cells = GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath);
                if (cells is null || !placed.IsInsideTree())
                    return Reject(buildId, cell, "missing_chunk_world");
                var gridObject = EntityComponent.FindComponent<GridObjectComponent>(placed, recursive: false);
                if (gridObject is null)
                {
                    gridObject = new GridObjectComponent { Name = "GridObject", Cell = cell, Footprint = build.EffectiveFootprint };
                    placed.AddChild(gridObject);
                }
                // Demand belongs to the placed object and survives job completion and coordinator teardown.
                gridObject.ChunkCellDataPath = gridObject.GetPathTo(cells);
            }

            // Under-construction visuals apply the moment the site exists,
            // whether or not its job has started yet - a footprint awaiting
            // delivery reads as unbuilt exactly as much as one mid-job.
            placed.SetMeta("grid_build_site_state", "under_construction");
            if (EntityComponent.FindComponent<GridObjectComponent>(placed, recursive: false) is { } earlyGridObject)
            {
                earlyGridObject.Complete = false;
                earlyGridObject.SetMetadataValue("build_site_state", "under_construction");
            }
            if (HidePlacedUntilBuilt || build.ConstructionStages.Count > 0)
                placed.Visible = false;
            else
                placed.Modulate = UnderConstructionModulate;

            if (storage is not null)
                return AwaitMaterials(build, placed, cell, storage);

            StartBuildJob(build, placed, cell, approach);
            return true;
        }

        /// <summary>
        /// Holds a site's job back until every RequiredMaterials entry has
        /// been physically delivered - via ITransporter, into the
        /// GridStorageComponent this looks for on the placed node - so a
        /// generic worker can never claim and complete a build the site does
        /// not actually have the material for.
        /// </summary>
        private bool AwaitMaterials(GridBuildDefinition build, Node2D placed, Vector2I cell, GridStorageComponent storage)
        {
            void OnStorageChanged(string resourceId, int stored, int currentLoad) => CheckPendingMaterials(placed);
            storage.StorageChanged += OnStorageChanged;
            _pendingByPlaced[placed] = new PendingBuild(build.BuildId, cell, storage, OnStorageChanged, _pendingChecks.AddLast(placed));

            EmitSignal(SignalName.BuildSiteAwaitingMaterials, build.BuildId, placed, cell.X, cell.Y);

            // Placement may have delivered nothing yet, or the storage may
            // already hold enough from a prior scene load - check once
            // rather than only reacting to a change that might never come.
            CheckPendingMaterials(placed);
            return true;
        }

        private void CheckPendingMaterials(Node2D placed)
        {
            if (!_pendingByPlaced.TryGetValue(placed, out PendingBuild? pending))
                return;
            if (!GodotObject.IsInstanceValid(placed) || !placed.IsInsideTree())
            {
                RemovePending(placed, pending, releaseMaterials: true);
                return;
            }

            ResolveReferences();
            GridBuildDefinition? build = _catalog?.FindBuild(pending.BuildId);
            if (build == null || !GodotObject.IsInstanceValid(_jobs))
                return;

            if (!GridResourceAmount.TryTotals(build.RequiredMaterials, out var required)) return;
            if (!GodotObject.IsInstanceValid(pending.Storage)
                || !pending.Storage.TryReserveMaterials(placed, build.RequiredMaterials)) return;
            if (!TryChooseApproach(pending.Cell, build.EffectiveFootprint, out Vector2I approach)) return;

            // End the pending transition before storage publishes its committed debit.
            RemovePending(placed, pending, releaseMaterials: false);
            _committingMaterials.Add(placed);
            try
            {
                if (required.Count == 0 || pending.Storage.TryConsumeReserved(placed))
                    StartBuildJob(build, placed, pending.Cell, approach);
            }
            finally { _committingMaterials.Remove(placed); }
        }

        /// <summary>Cancels a site still waiting on delivery - there is no job yet to cancel.</summary>
        public bool CancelPendingBuild(Node2D placed)
        {
            if (!_pendingByPlaced.TryGetValue(placed, out PendingBuild? pending))
                return false;

            RemovePending(placed, pending, releaseMaterials: true);

            bool placedValid = GodotObject.IsInstanceValid(placed);
            if (RefundOnJobCancelled
                && placedValid
                && placed.HasMeta("grid_build_cost_charged")
                && placed.GetMeta("grid_build_cost_charged").AsBool()
                && _catalog?.FindBuild(pending.BuildId) is { } build)
            {
                _wallet?.Refund(build.Costs);
            }

            if (RemovePlacedOnJobCancelled && placedValid)
                placed.QueueFree();

            EmitSignal(SignalName.BuildSiteCancelled, pending.BuildId, "", pending.Cell.X, pending.Cell.Y);
            return true;
        }

        private void StartBuildJob(GridBuildDefinition build, Node2D placed, Vector2I cell, Vector2I approach)
        {
            string kind = string.IsNullOrWhiteSpace(build.JobKind) ? "build" : build.JobKind.Trim();
            // The job's work amount is the site's TURNS. The queue is
            // unit-agnostic - it holds a number and a remainder and never
            // divides by a frame delta - so what the number means is decided by
            // whoever advances it, and that is GridWorkClockComponent, which
            // measures in turns on both axes.
            string jobId = _jobs!.AddJob(cell, kind, build.EffectiveBuildTurns);
            _jobs.SetJobApproachCell(jobId, approach);
            _sitesByJobId[jobId] = new BuildSite(build.BuildId, placed, cell, approach);

            placed.SetMeta("grid_build_site_job_id", jobId);
            if (EntityComponent.FindComponent<GridObjectComponent>(placed, recursive: false) is { } gridObject)
                gridObject.SetMetadataValue("build_site_job_id", jobId);

            // Queued like any other job kind - a GridWorkerComponent with
            // "build" (or this definition's own JobKind) in AllowedJobKinds
            // polls and claims it the same way it claims gather/clear/till
            // jobs. No separate dispatch path for construction.
            EmitSignal(SignalName.BuildSiteCreated, build.BuildId, jobId, placed, cell.X, cell.Y);
        }

        /// <summary>
        /// Where a worker stands to build: the cell just in front of the
        /// footprint's front edge, under its middle column - outside the
        /// blocked footprint, on the face the oblique projection shows, the
        /// way Age of Empires villagers and Settlers builders work a site
        /// from beside it. The footprint extends right/down from the anchor
        /// (GridPlacementComponent.FootprintCells).
        /// </summary>
        public static Vector2I ApproachCellFor(Vector2I anchorCell, Vector2I footprint)
            => new(anchorCell.X + Mathf.Max(0, footprint.X - 1) / 2, anchorCell.Y + Mathf.Max(1, footprint.Y));

        private bool TryChooseApproach(Vector2I anchor, Vector2I footprint, out Vector2I approach)
        {
            approach = ApproachCellFor(anchor, footprint);
            if (_navigation is null) return NavigationPath.IsEmpty;
            if (CanStandAt(approach)) return true;
            int width = Mathf.Max(1, footprint.X), height = Mathf.Max(1, footprint.Y);
            // Edge neighbours only: a diagonal corner is not a usable work face.
            for (int x = 0; x < width; x++)
            {
                approach = anchor + new Vector2I(x, height);
                if (CanStandAt(approach)) return true;
                approach = anchor + new Vector2I(x, -1);
                if (CanStandAt(approach)) return true;
            }
            for (int y = 0; y < height; y++)
            {
                approach = anchor + new Vector2I(-1, y);
                if (CanStandAt(approach)) return true;
                approach = anchor + new Vector2I(width, y);
                if (CanStandAt(approach)) return true;
            }
            return false;
        }

        private bool CanStandAt(Vector2I cell)
            => _navigation is not null && _navigation.IsInBounds(cell) && !_navigation.IsBlocked(cell);

        public bool CompleteBuildSite(string jobId)
        {
            if (!_sitesByJobId.TryGetValue(jobId, out BuildSite? site))
                return false;

            if (!GodotObject.IsInstanceValid(site.Placed))
            {
                _sitesByJobId.Remove(jobId);
                return false;
            }

            ResolveReferences();
            if ((_navigation is not null && !CanStandAt(site.Approach))
                || (_navigation is null && !NavigationPath.IsEmpty))
            {
                CancelBuildSite(jobId);
                return Reject(site.BuildId, site.Cell, "build_approach_blocked");
            }

            _sitesByJobId.Remove(jobId);

            site.Placed.Visible = true;
            site.Placed.Modulate = CompletedModulate;
            site.Placed.SetMeta("grid_build_site_state", "complete");
            if (EntityComponent.FindComponent<GridObjectComponent>(site.Placed, recursive: false) is { } gridObject)
            {
                gridObject.Complete = true;
                gridObject.SetMetadataValue("build_site_state", "complete");
            }
            EmitSignal(SignalName.BuildSiteCompleted, site.BuildId, jobId, site.Placed, site.Cell.X, site.Cell.Y);
            return true;
        }

        public int ActiveBuildSiteCount
        {
            get
            {
                PruneInvalidSites();
                return _sitesByJobId.Count + _pendingByPlaced.Count;
            }
        }

        /// <summary>Sites still waiting for RequiredMaterials to finish arriving.</summary>
        public int PendingMaterialsCount => _pendingByPlaced.Count;

        /// <summary>Immediately rechecks all waiting sites; normal automatic retries are frame-budgeted.</summary>
        public void RefreshPendingBuilds()
        {
            foreach (Node2D placed in new List<Node2D>(_pendingByPlaced.Keys))
                CheckPendingMaterials(placed);
        }

        private void OnPlacementPlaced(string buildId, Node2D placed, int x, int y)
        {
            RegisterPlacedBuild(buildId, placed, new Vector2I(x, y));
        }

        private void OnJobCompleted(string jobId, string workerId)
        {
            CompleteBuildSite(jobId);
        }

        private void OnJobCancelled(string jobId, string reason)
        {
            CancelBuildSite(jobId);
        }

        /// <summary>
        /// Tears a site down after its build job is cancelled. Just forgetting
        /// the site - the old behaviour - left a paid, tinted, incomplete
        /// building standing on blocked cells with no job that could ever
        /// finish it.
        /// </summary>
        public bool CancelBuildSite(string jobId)
        {
            if (!_sitesByJobId.TryGetValue(jobId, out BuildSite? site))
                return false;

            _sitesByJobId.Remove(jobId);
            ResolveReferences();

            bool placedValid = GodotObject.IsInstanceValid(site.Placed);

            // Refund only what placement says was actually charged - a build
            // begun with chargeCostOnConfirm false owes nothing back.
            if (RefundOnJobCancelled
                && placedValid
                && site.Placed.HasMeta("grid_build_cost_charged")
                && site.Placed.GetMeta("grid_build_cost_charged").AsBool()
                && _catalog?.FindBuild(site.BuildId) is { } build)
            {
                _wallet?.Refund(build.Costs);
            }

            if (RemovePlacedOnJobCancelled && placedValid)
            {
                // The stamped GridObjectComponent releases the reserved
                // footprint (occupancy and navigation blocks) on exit.
                site.Placed.QueueFree();
            }

            EmitSignal(SignalName.BuildSiteCancelled, site.BuildId, jobId, site.Cell.X, site.Cell.Y);
            return true;
        }

        private bool Reject(string buildId, Vector2I cell, string reason)
        {
            EmitSignal(SignalName.BuildSiteRejected, buildId, cell.X, cell.Y, reason);
            return false;
        }

        private void PruneInvalidSites()
        {
            var invalid = new List<string>();
            foreach (var pair in _sitesByJobId)
                if (!GodotObject.IsInstanceValid(pair.Value.Placed))
                    invalid.Add(pair.Key);

            foreach (string jobId in invalid)
                _sitesByJobId.Remove(jobId);

            var invalidPending = new List<Node2D>();
            foreach (var pair in _pendingByPlaced)
                if (!GodotObject.IsInstanceValid(pair.Key))
                    invalidPending.Add(pair.Key);

            foreach (Node2D placed in invalidPending)
                RemovePending(placed, _pendingByPlaced[placed], releaseMaterials: true);
        }

        private void ResolveReferences()
        {
            // A freed placement or queue took its signal connections with it;
            // the flag must drop before Resolve picks a replacement up, or
            // ConnectSystems would believe the new instance is already wired.
            if (_placement != null && !GodotObject.IsInstanceValid(_placement))
            {
                _placement = null;
                _placementConnected = false;
            }
            EntityComponent.Resolve(this, PlacementPath, ref _placement);

            EntityComponent.Resolve(this, BuildCatalogPath, ref _catalog);

            if (_jobs != null && !GodotObject.IsInstanceValid(_jobs))
            {
                _jobs = null;
                _jobsConnected = false;
            }
            EntityComponent.Resolve(this, JobQueuePath, ref _jobs);

            EntityComponent.Resolve(this, ResourceWalletPath, ref _wallet);
            if (!NavigationPath.IsEmpty) _navigation = GetNodeOrNull<Node>(NavigationPath) as GridNavigationComponent;
            else EntityComponent.Resolve(this, NavigationPath, ref _navigation);
        }

        private void DisconnectPendingBuilds()
        {
            foreach (var (placed, pending) in _pendingByPlaced)
            {
                if (GodotObject.IsInstanceValid(pending.Storage))
                {
                    pending.Storage.StorageChanged -= pending.Handler;
                    pending.Storage.ReleaseMaterials(placed);
                }
            }
            _pendingByPlaced.Clear();
            _pendingChecks.Clear();
        }

        private void RemovePending(Node2D placed, PendingBuild pending, bool releaseMaterials)
        {
            _pendingByPlaced.Remove(placed);
            _pendingChecks.Remove(pending.Check);
            if (!GodotObject.IsInstanceValid(pending.Storage)) return;
            pending.Storage.StorageChanged -= pending.Handler;
            if (releaseMaterials) pending.Storage.ReleaseMaterials(placed);
        }

        private sealed record BuildSite(string BuildId, Node2D Placed, Vector2I Cell, Vector2I Approach);

        private sealed record PendingBuild(
            string BuildId,
            Vector2I Cell,
            GridStorageComponent Storage,
            GridStorageComponent.StorageChangedEventHandler Handler,
            LinkedListNode<Node2D> Check);
    }
}
