using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Turns placed build definitions with BuildSeconds into worker-completed
    /// build sites. This gives builder games a blueprint -> job -> finished
    /// building loop without project-specific glue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildSiteComponent : Node
    {
        [Signal] public delegate void BuildSiteCreatedEventHandler(string buildId, string jobId, Node2D placed, int x, int y);
        [Signal] public delegate void BuildSiteCompletedEventHandler(string buildId, string jobId, Node2D placed, int x, int y);
        [Signal] public delegate void BuildSiteCancelledEventHandler(string buildId, string jobId, int x, int y);
        [Signal] public delegate void BuildSiteRejectedEventHandler(string buildId, int x, int y, string reason);

        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath BuildCatalogPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public bool AutoConnect { get; set; } = true;
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
        [Export] public bool HidePlacedUntilBuilt { get; set; } = false;
        [Export] public Color UnderConstructionModulate { get; set; } = new(1f, 0.88f, 0.46f, 0.72f);
        [Export] public Color CompletedModulate { get; set; } = Colors.White;

        private readonly Dictionary<string, BuildSite> _sitesByJobId = new();
        private GridPlacementComponent? _placement;
        private GridBuildCatalogComponent? _catalog;
        private GridJobQueueComponent? _jobs;
        private GridResourceWalletComponent? _wallet;
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

            GridBuildDefinition? build = _catalog.FindBuild(buildId);
            if (build == null)
                return Reject(buildId, cell, "missing_build_definition");

            if (build.EffectiveBuildSeconds <= 0f)
                return false;

            string kind = string.IsNullOrWhiteSpace(build.JobKind) ? "build" : build.JobKind.Trim();
            string jobId = _jobs.AddJob(cell, kind, build.EffectiveBuildSeconds);
            _sitesByJobId[jobId] = new BuildSite(build.BuildId, placed, cell);

            placed.SetMeta("grid_build_site_job_id", jobId);
            placed.SetMeta("grid_build_site_state", "under_construction");
            if (EntityComponent.FindComponent<GridObjectComponent>(placed, recursive: false) is { } gridObject)
            {
                gridObject.Complete = false;
                gridObject.SetMetadataValue("build_site_job_id", jobId);
                gridObject.SetMetadataValue("build_site_state", "under_construction");
            }
            if (HidePlacedUntilBuilt)
                placed.Visible = false;
            else
                placed.Modulate = UnderConstructionModulate;

            EmitSignal(SignalName.BuildSiteCreated, build.BuildId, jobId, placed, cell.X, cell.Y);
            return true;
        }

        public bool CompleteBuildSite(string jobId)
        {
            if (!_sitesByJobId.TryGetValue(jobId, out BuildSite? site))
                return false;

            _sitesByJobId.Remove(jobId);
            if (!GodotObject.IsInstanceValid(site.Placed))
                return false;

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
                return _sitesByJobId.Count;
            }
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
        }

        private void ResolveReferences()
        {
            if (_placement != null && !GodotObject.IsInstanceValid(_placement))
            {
                _placement = null;
                _placementConnected = false;
            }
            if (_placement == null)
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_catalog == null || !GodotObject.IsInstanceValid(_catalog))
                _catalog = !BuildCatalogPath.IsEmpty
                    ? GetNodeOrNull<GridBuildCatalogComponent>(BuildCatalogPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridBuildCatalogComponent>(GetTree()?.CurrentScene) : null;

            if (_jobs != null && !GodotObject.IsInstanceValid(_jobs))
            {
                _jobs = null;
                _jobsConnected = false;
            }
            if (_jobs == null)
                _jobs = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;
        }

        private sealed record BuildSite(string BuildId, Node2D Placed, Vector2I Cell);
    }
}
