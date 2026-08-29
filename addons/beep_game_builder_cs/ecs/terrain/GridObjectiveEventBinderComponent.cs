using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Connects grid gameplay signals to GridObjectiveTrackerComponent progress.
    /// Use it to advance goals from completed jobs, finished builds, gathered
    /// resources, and completed production cycles without project-specific glue.
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

        [Export] public bool AutoConnect { get; set; } = true;
        [Export] public bool TrackCompletedJobs { get; set; } = true;
        [Export] public bool TrackCompletedBuilds { get; set; } = true;
        [Export] public bool TrackGatheredResources { get; set; } = true;
        [Export] public bool TrackCompletedProduction { get; set; } = true;
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
        private bool _jobsConnected;
        private bool _buildSitesConnected;
        private readonly HashSet<GridResourceNodeComponent> _resourceNodes = new();
        private readonly HashSet<GridProductionComponent> _productionNodes = new();

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
        }

        public void DisconnectSystems()
        {
            if (_jobs != null && GodotObject.IsInstanceValid(_jobs) && _jobsConnected)
                _jobs.JobCompleted -= OnJobCompleted;
            if (_buildSites != null && GodotObject.IsInstanceValid(_buildSites) && _buildSitesConnected)
                _buildSites.BuildSiteCompleted -= OnBuildSiteCompleted;

            foreach (GridResourceNodeComponent node in _resourceNodes)
                if (GodotObject.IsInstanceValid(node))
                    node.Gathered -= OnResourceGathered;
            foreach (GridProductionComponent production in _productionNodes)
                if (GodotObject.IsInstanceValid(production))
                    production.ProductionCompleted -= OnProductionCompleted;

            _jobsConnected = false;
            _buildSitesConnected = false;
            _resourceNodes.Clear();
            _productionNodes.Clear();
        }

        public bool ApplyObjectiveEvent(string objectiveId, int amount = 1, string source = "manual")
        {
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

        private void ResolveReferences()
        {
            if (_tracker == null || !GodotObject.IsInstanceValid(_tracker))
                _tracker = !ObjectiveTrackerPath.IsEmpty
                    ? GetNodeOrNull<GridObjectiveTrackerComponent>(ObjectiveTrackerPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridObjectiveTrackerComponent>(GetTree()?.CurrentScene) : null;

            if (_jobs != null && !GodotObject.IsInstanceValid(_jobs))
            {
                _jobs = null;
                _jobsConnected = false;
            }
            if (_jobs == null)
                _jobs = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_buildSites != null && !GodotObject.IsInstanceValid(_buildSites))
            {
                _buildSites = null;
                _buildSitesConnected = false;
            }
            if (_buildSites == null)
                _buildSites = !BuildSitePath.IsEmpty
                    ? GetNodeOrNull<GridBuildSiteComponent>(BuildSitePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridBuildSiteComponent>(GetTree()?.CurrentScene) : null;

            if (_resourceNodesRoot == null || !GodotObject.IsInstanceValid(_resourceNodesRoot))
                _resourceNodesRoot = !ResourceNodesRootPath.IsEmpty ? GetNodeOrNull<Node>(ResourceNodesRootPath) : null;

            if (_productionRoot == null || !GodotObject.IsInstanceValid(_productionRoot))
                _productionRoot = !ProductionRootPath.IsEmpty ? GetNodeOrNull<Node>(ProductionRootPath) : null;
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
        }
    }
}
