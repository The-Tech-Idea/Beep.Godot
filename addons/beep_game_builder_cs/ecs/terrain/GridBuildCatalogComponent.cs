using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Catalog of placeable grid builds. It keeps build menu data together and can
    /// start GridPlacementComponent with footprint, scene, preview, id, and costs.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildCatalogComponent : Node
    {
        [Signal] public delegate void BuildSelectedEventHandler(string buildId);
        [Signal] public delegate void BuildRejectedEventHandler(string buildId, string reason);

        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public bool RequireAffordableToBegin { get; set; } = true;
        [Export] public Godot.Collections.Array Builds { get; set; } = new();

        private GridPlacementComponent? _placement;
        private GridResourceWalletComponent? _wallet;

        public override void _Ready()
        {
            ResolveReferences();
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (PlacementPath.IsEmpty)
                return new[] { "PlacementPath should point to a GridPlacementComponent." };
            return System.Array.Empty<string>();
        }

        public GridBuildDefinition? FindBuild(string buildId)
        {
            if (string.IsNullOrWhiteSpace(buildId))
                return null;

            string id = Normalize(buildId);
            foreach (GridBuildDefinition build in GridBuildDefinition.Enumerate(Builds))
            {
                if (build != null && Normalize(build.BuildId) == id)
                    return build;
            }

            return null;
        }

        public Godot.Collections.Array<string> BuildIdsForCategory(string category)
        {
            string normalized = Normalize(category);
            var ids = new Godot.Collections.Array<string>();
            foreach (GridBuildDefinition build in GridBuildDefinition.Enumerate(Builds))
            {
                if (build == null)
                    continue;

                if (string.IsNullOrEmpty(normalized) || Normalize(build.Category) == normalized)
                    ids.Add(build.BuildId);
            }

            return ids;
        }

        public bool CanAfford(string buildId)
        {
            GridBuildDefinition? build = FindBuild(buildId);
            if (build == null)
                return false;

            ResolveReferences();
            return _wallet == null || _wallet.CanAfford(build.Costs);
        }

        public bool BeginPlacement(string buildId)
        {
            GridBuildDefinition? build = FindBuild(buildId);
            if (build == null)
                return Reject(buildId, "missing_build");

            if (!build.HasPlayableSurface())
                return Reject(buildId, "missing_scene_or_preview");

            ResolveReferences();
            if (_placement == null)
                return Reject(buildId, "missing_placement");

            if (RequireAffordableToBegin && _wallet != null && !_wallet.CanAfford(build.Costs))
                return Reject(buildId, "missing_resources");

            _placement.BeginPlacement(build, chargeCostOnConfirm: _wallet != null);
            EmitSignal(SignalName.BuildSelected, build.BuildId);
            return true;
        }

        public Godot.Collections.Dictionary CostSummary(string buildId)
        {
            var summary = new Godot.Collections.Dictionary();
            GridBuildDefinition? build = FindBuild(buildId);
            if (build == null)
                return summary;

            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(build.Costs))
            {
                string id = Normalize(resourceId);
                if (string.IsNullOrEmpty(id))
                    continue;

                summary[id] = summary.ContainsKey(id)
                    ? GridVariantReader.Int(summary[id], 0) + Mathf.Max(0, amount)
                    : Mathf.Max(0, amount);
            }

            return summary;
        }

        private bool Reject(string buildId, string reason)
        {
            EmitSignal(SignalName.BuildRejected, buildId, reason);
            return false;
        }

        private void ResolveReferences()
        {
            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;
        }

        private static string Normalize(string value)
            => value.Trim().ToLowerInvariant();
    }
}
