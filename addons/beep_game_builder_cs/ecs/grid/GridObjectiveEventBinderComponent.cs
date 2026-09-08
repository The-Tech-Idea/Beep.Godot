using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Connects grid gameplay signals to GridObjectiveTrackerComponent progress.
    /// Use it to advance goals from completed jobs, finished builds, gathered
    /// resources (hand-gathered AND extracted - both feed the same resource-id
    /// objective, see TrackExtractionCycles), and completed production cycles
    /// without project-specific glue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridObjectiveEventBinderComponent : Node
    {
        [Signal] public delegate void ObjectiveEventAppliedEventHandler(string objectiveId, int amount, string source);

        [Export] public NodePath ObjectiveTrackerPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath BuildSitePath { get; set; } = new("");
        [Export] public NodePath ResourceNodesRootPath { get; set; } = new("");
        [Export] public NodePath ProductionRootPath { get; set; } = new("");
        [Export] public NodePath ExtractionManagerPath { get; set; } = new("");

        [Export] public bool AutoConnect { get; set; } = true;
        [Export] public bool TrackCompletedJobs { get; set; } = true;
        [Export] public bool TrackCompletedBuilds { get; set; } = true;
        [Export] public bool TrackGatheredResources { get; set; } = true;
        [Export] public bool TrackCompletedProduction { get; set; } = true;
        /// <summary>
        /// Whether a registered GridExtractorComponent's ExtractionCycle also
        /// advances the SAME objective id as TrackGatheredResources - a
        /// "collect N wood" goal counts wood drawn by an extractor exactly
        /// like wood hand-gathered from a GridResourceNodeComponent. Only the
        /// extraction cycle counts, not a hauler's later HaulDelivered: an
        /// extractor delivering DeliverVia TransportManager already fires one
        /// ExtractionCycle for that resource, and the hauler's delivery is
        /// the same units moving, not new units acquired - wiring both would
        /// double-count.
        /// </summary>
        [Export] public bool TrackExtractionCycles { get; set; } = true;
        [Export] public bool UseGatherAmountAsProgress { get; set; } = true;

        [Export] public string CompletedJobPrefix { get; set; } = "";
        [Export] public string CompletedBuildPrefix { get; set; } = "build_";
        [Export] public string GatheredResourcePrefix { get; set; } = "gather_";
        [Export] public string CompletedProductionPrefix { get; set; } = "produce_";

        private GridObjectiveTrackerComponent? _tracker;
        private GridJobQueueComponent? _jobs;
        private GridBuildSiteComponent? _buildSites;
        private Node? _resourceNodesRoot;
        private Node? _productionRoot;
        private GridExtractionManagerComponent? _extractionManager;
        private bool _jobsConnected;
        private bool _buildSitesConnected;
        private bool _extractionManagerConnected;
        private readonly HashSet<GridResourceNodeComponent> _resourceNodes = new();
        private readonly HashSet<GridProductionComponent> _productionNodes = new();
        private readonly HashSet<GridExtractorComponent> _extractors = new();

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
            if (ObjectiveTrackerPath.IsEmpty)
                return new[] { "ObjectiveTrackerPath should point to a GridObjectiveTrackerComponent." };
            return Array.Empty<string>();
        }

        public void ConnectSystems()
        {
            ResolveReferences();
            PruneInvalidTrackedNodes();

            if (TrackCompletedJobs && _jobs != null && !_jobsConnected)
            {
                _jobs.JobCompleted += OnJobCompleted;
                _jobsConnected = true;
            }

            if (TrackCompletedBuilds && _buildSites != null && !_buildSitesConnected)
            {
                _buildSites.BuildSiteCompleted += OnBuildSiteCompleted;
                _buildSitesConnected = true;
            }

            if (TrackGatheredResources && _resourceNodesRoot != null)
                ConnectResourceNodes(_resourceNodesRoot);

            if (TrackCompletedProduction && _productionRoot != null)
                ConnectProductionNodes(_productionRoot);

            if (TrackExtractionCycles && _extractionManager != null && !_extractionManagerConnected)
            {
                _extractionManager.ExtractorRegistered += OnExtractorRegistered;
                _extractionManager.ExtractorUnregistered += OnExtractorUnregistered;
                _extractionManagerConnected = true;

                // Extractors that registered with the manager before this
                // binder connected (ordering between the two isn't
                // guaranteed) still get picked up here.
                foreach (Node node in _extractionManager.Extractors())
                    if (node is GridExtractorComponent extractor)
                        ConnectExtractor(extractor);
            }
        }

        public void DisconnectSystems()
        {
            if (_jobs != null && GodotObject.IsInstanceValid(_jobs) && _jobsConnected)
                _jobs.JobCompleted -= OnJobCompleted;
            if (_buildSites != null && GodotObject.IsInstanceValid(_buildSites) && _buildSitesConnected)
                _buildSites.BuildSiteCompleted -= OnBuildSiteCompleted;
            if (_extractionManager != null && GodotObject.IsInstanceValid(_extractionManager) && _extractionManagerConnected)
            {
                _extractionManager.ExtractorRegistered -= OnExtractorRegistered;
                _extractionManager.ExtractorUnregistered -= OnExtractorUnregistered;
            }

            foreach (GridResourceNodeComponent node in _resourceNodes)
                if (GodotObject.IsInstanceValid(node))
                    node.Gathered -= OnResourceGathered;
            foreach (GridProductionComponent production in _productionNodes)
                if (GodotObject.IsInstanceValid(production))
                    production.ProductionCompleted -= OnProductionCompleted;
            foreach (GridExtractorComponent extractor in _extractors)
                if (GodotObject.IsInstanceValid(extractor))
                    extractor.ExtractionCycle -= OnExtractionCycle;

            _jobsConnected = false;
            _buildSitesConnected = false;
            _extractionManagerConnected = false;
            _resourceNodes.Clear();
            _productionNodes.Clear();
            _extractors.Clear();
        }

        public bool ApplyObjectiveEvent(string objectiveId, int amount = 1, string source = "manual")
        {
            if (amount <= 0)
                return false;
            ResolveReferences();
            if (_tracker == null)
                return false;

            string id = GridObjectiveDefinition.Normalize(objectiveId);
            if (string.IsNullOrEmpty(id))
                return false;

            bool applied = _tracker.AddProgress(id, Mathf.Max(1, amount));
            if (applied)
                EmitSignal(SignalName.ObjectiveEventApplied, id, Mathf.Max(1, amount), source);
            return applied;
        }

        public string ObjectiveIdForJob(string jobKind)
            => $"{CompletedJobPrefix}{GridObjectiveDefinition.Normalize(jobKind)}";

        public string ObjectiveIdForBuild(string buildId)
            => $"{CompletedBuildPrefix}{GridObjectiveDefinition.Normalize(buildId)}";

        /// <summary>Shared by hand-gathering AND extraction - the same
        /// resource id advances the same objective whichever path acquired
        /// it. See TrackExtractionCycles.</summary>
        public string ObjectiveIdForResource(string resourceId)
            => $"{GatheredResourcePrefix}{GridObjectiveDefinition.Normalize(resourceId)}";

        public string ObjectiveIdForProduction(string recipeId)
            => $"{CompletedProductionPrefix}{GridObjectiveDefinition.Normalize(recipeId)}";

        private void OnJobCompleted(string jobId, string workerId)
        {
            if (_jobs == null)
                return;

            string kind = _jobs.GetJobKind(jobId);
            ApplyObjectiveEvent(ObjectiveIdForJob(kind), 1, "job_completed");
        }

        private void OnBuildSiteCompleted(string buildId, string jobId, Node2D placed, int x, int y)
            => ApplyObjectiveEvent(ObjectiveIdForBuild(buildId), 1, "build_completed");

        private void OnResourceGathered(string resourceId, int amount, int remainingAmount)
            => ApplyObjectiveEvent(ObjectiveIdForResource(resourceId), UseGatherAmountAsProgress ? amount : 1, "resource_gathered");

        private void OnProductionCompleted(string recipeId)
            => ApplyObjectiveEvent(ObjectiveIdForProduction(recipeId), 1, "production_completed");

        private void OnExtractionCycle(string resourceId, int amount, int remaining)
            => ApplyObjectiveEvent(ObjectiveIdForResource(resourceId), UseGatherAmountAsProgress ? amount : 1, "extraction_cycle");

        private void OnExtractorRegistered(Node extractor)
        {
            if (extractor is GridExtractorComponent typed)
                ConnectExtractor(typed);
        }

        private void OnExtractorUnregistered(Node extractor)
        {
            if (extractor is GridExtractorComponent typed && _extractors.Remove(typed) && GodotObject.IsInstanceValid(typed))
                typed.ExtractionCycle -= OnExtractionCycle;
        }

        private void ConnectExtractor(GridExtractorComponent extractor)
        {
            if (!_extractors.Add(extractor))
                return;
            extractor.ExtractionCycle += OnExtractionCycle;
        }

        private void ResolveReferences()
        {
            EntityComponent.Resolve(this, ObjectiveTrackerPath, ref _tracker);

            // A freed source took its signal connection with it; the flag must
            // drop before Resolve picks a replacement up, or ConnectSystems
            // would believe the new instance is already wired.
            if (_jobs != null && !GodotObject.IsInstanceValid(_jobs))
            {
                _jobs = null;
                _jobsConnected = false;
            }
            EntityComponent.Resolve(this, JobQueuePath, ref _jobs);

            if (_buildSites != null && !GodotObject.IsInstanceValid(_buildSites))
            {
                _buildSites = null;
                _buildSitesConnected = false;
            }
            EntityComponent.Resolve(this, BuildSitePath, ref _buildSites);

            // Explicit wires only: with no root there is nothing to walk.
            if (_resourceNodesRoot == null || !GodotObject.IsInstanceValid(_resourceNodesRoot))
                _resourceNodesRoot = !ResourceNodesRootPath.IsEmpty ? GetNodeOrNull<Node>(ResourceNodesRootPath) : null;

            if (_productionRoot == null || !GodotObject.IsInstanceValid(_productionRoot))
                _productionRoot = !ProductionRootPath.IsEmpty ? GetNodeOrNull<Node>(ProductionRootPath) : null;

            if (_extractionManager != null && !GodotObject.IsInstanceValid(_extractionManager))
            {
                _extractionManager = null;
                _extractionManagerConnected = false;
            }
            EntityComponent.Resolve(this, ExtractionManagerPath, ref _extractionManager);
        }

        private void ConnectResourceNodes(Node node)
        {
            if (node is GridResourceNodeComponent resourceNode && !_resourceNodes.Contains(resourceNode))
            {
                resourceNode.Gathered += OnResourceGathered;
                _resourceNodes.Add(resourceNode);
            }

            foreach (Node child in node.GetChildren())
                ConnectResourceNodes(child);
        }

        private void ConnectProductionNodes(Node node)
        {
            if (node is GridProductionComponent production && !_productionNodes.Contains(production))
            {
                production.ProductionCompleted += OnProductionCompleted;
                _productionNodes.Add(production);
            }

            foreach (Node child in node.GetChildren())
                ConnectProductionNodes(child);
        }

        private void PruneInvalidTrackedNodes()
        {
            _resourceNodes.RemoveWhere(node => !GodotObject.IsInstanceValid(node));
            _productionNodes.RemoveWhere(node => !GodotObject.IsInstanceValid(node));
            _extractors.RemoveWhere(node => !GodotObject.IsInstanceValid(node));
        }
    }
}
