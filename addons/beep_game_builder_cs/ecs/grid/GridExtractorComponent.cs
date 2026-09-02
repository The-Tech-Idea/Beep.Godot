using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A placed building that works the deposit UNDER it: the derrick, the
    /// mine shaft, the offshore platform, the colony ice extractor.
    ///
    /// Attach under a building scene alongside its GridObjectComponent. Once
    /// the building is complete it binds to the underground deposit beneath
    /// its footprint, validates that it can work it, then draws it down cycle
    /// by cycle into the wallet - GatherSeconds per AmountPerGather, from the
    /// resource's own definition - until the deposit is worked out.
    ///
    /// What lies below and how much is left are not this component's to own:
    /// the data layers answer the first and GridSubsurfaceStoreComponent the
    /// second. This node is only the pump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridExtractorComponent : GameplayComponent
    {
        [Signal] public delegate void ExtractionStartedEventHandler(string resourceId);
        [Signal] public delegate void ExtractionCycleEventHandler(string resourceId, int amount, int remaining);
        [Signal] public delegate void ExtractionStoppedEventHandler(string reason);

        [Export] public NodePath DataLayersPath { get; set; } = new("");
        [Export] public NodePath SubsurfaceStorePath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");

        /// <summary>
        /// The deepest band this extractor reaches; see ResourceDepth. The
        /// tech ladder in one export: a basic mine reaching Shallow cannot
        /// touch a Deep platinum vein, whatever it is parked over.
        /// </summary>
        [Export] public ResourceDepth ReachDepth { get; set; } = ResourceDepth.Deep;

        /// <summary>
        /// Waits for the sibling GridObjectComponent's Complete flag, so a
        /// build site under construction does not pump while scaffolded.
        /// </summary>
        [Export] public bool RequireCompleteBuild { get; set; } = true;
        [Export] public bool AutoStart { get; set; } = true;

        /// <summary>Overrides the definition's GatherSeconds when above zero.</summary>
        [Export(PropertyHint.Range, "0,600,0.01")] public float CycleSecondsOverride { get; set; } = 0f;

        /// <summary>Overrides the definition's AmountPerGather when above zero.</summary>
        [Export(PropertyHint.Range, "0,9999,1")] public int AmountPerCycleOverride { get; set; } = 0;

        /// <summary>
        /// The shared resource catalog, for the deposit's cycle rules and its
        /// ExtractorBuildId validation.
        /// </summary>
        [Export] public ResourceCatalog? Catalog { get; set; }

        public bool IsExtracting { get; private set; }
        public string ActiveResourceId { get; private set; } = "";

        private TerrainDataLayersComponent? _dataLayers;
        private GridSubsurfaceStoreComponent? _store;
        private GridResourceWalletComponent? _wallet;
        private GridProjectionComponent? _grid;
        private GridObjectComponent? _gridObject;
        private readonly List<Vector2I> _depositCells = new();
        private float _cycleClock;
        private bool _bound;

        public override void _Ready()
        {
            base._Ready();
            ResolveReferences();
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (SubsurfaceStorePath.IsEmpty)
                return new[] { "SubsurfaceStorePath should point to a GridSubsurfaceStoreComponent." };
            return Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || Engine.IsEditorHint())
                return;

            Tick(delta);
        }

        public void Tick(double delta)
        {
            if (!_bound)
            {
                if (!AutoStart)
                    return;
                if (RequireCompleteBuild && ResolveGridObject() is { Complete: false })
                    return;
                if (!TryBind())
                    return;
            }

            if (!IsExtracting)
                return;

            float step = double.IsFinite(delta) && delta > 0.0 ? (float)Mathf.Min(delta, 86400.0) : 0f;
            if (step <= 0f)
                return;

            _cycleClock += step;
            float cycleSeconds = CycleSeconds();
            while (_cycleClock >= cycleSeconds && IsExtracting)
            {
                _cycleClock -= cycleSeconds;
                RunCycle();
            }
        }

        /// <summary>
        /// Finds the deposit under the footprint and starts the pump. Safe to
        /// call again after a stop; returns false with an ExtractionStopped
        /// reason when there is nothing this extractor can work.
        /// </summary>
        public bool TryBind()
        {
            ResolveReferences();
            _bound = true;
            _depositCells.Clear();
            ActiveResourceId = "";
            IsExtracting = false;

            if (_store == null)
                return Stop("missing_subsurface_store");

            string id = "";
            foreach (Vector2I cell in FootprintCells())
            {
                string at = _store.ResourceIdAt(cell);
                if (at.Length == 0)
                    continue;
                if (id.Length == 0)
                    id = at;
                if (at == id)
                    _depositCells.Add(cell);
            }

            if (id.Length == 0)
                return Stop("no_deposit");

            ResourceDefinition? definition = Catalog?.Find(id);
            if (definition != null)
            {
                if (definition.Depth > ReachDepth)
                    return Stop("too_deep");

                string required = definition.ExtractorBuildId?.Trim() ?? "";
                string ownId = ResolveGridObject()?.ObjectId ?? "";
                if (required.Length > 0 && !string.Equals(required, ownId, StringComparison.OrdinalIgnoreCase))
                    return Stop("wrong_extractor");
            }
            else if (_dataLayers != null)
            {
                // No catalog to consult: the published depth band still gates.
                foreach (Vector2I cell in _depositCells)
                {
                    if (_dataLayers.UndergroundDepthAt(cell) > (int)ReachDepth)
                        return Stop("too_deep");
                }
            }

            ActiveResourceId = id;
            IsExtracting = true;
            _cycleClock = 0f;
            EmitSignal(SignalName.ExtractionStarted, id);
            return true;
        }

        public void StopExtraction(string reason = "stopped")
            => Stop(reason);

        private void RunCycle()
        {
            if (_store == null || ActiveResourceId.Length == 0)
            {
                Stop("missing_subsurface_store");
                return;
            }

            int perCycle = AmountPerCycle();
            int drawn = 0;
            int remaining = 0;
            foreach (Vector2I cell in _depositCells)
            {
                if (drawn < perCycle)
                    drawn += _store.Draw(cell, perCycle - drawn);
                remaining += _store.RemainingAt(cell);
            }

            if (drawn > 0)
            {
                _wallet?.AddAmount(ActiveResourceId, drawn);
                EmitSignal(SignalName.ExtractionCycle, ActiveResourceId, drawn, remaining);
            }

            if (remaining <= 0)
                Stop("depleted");
        }

        private bool Stop(string reason)
        {
            bool was = IsExtracting;
            IsExtracting = false;
            if (was || reason != "stopped")
                EmitSignal(SignalName.ExtractionStopped, reason);
            return false;
        }

        private float CycleSeconds()
        {
            if (CycleSecondsOverride > 0f && float.IsFinite(CycleSecondsOverride))
                return CycleSecondsOverride;
            float fromDefinition = Catalog?.Find(ActiveResourceId)?.GatherSeconds ?? 1.5f;
            return Mathf.Max(0.05f, float.IsFinite(fromDefinition) ? fromDefinition : 1.5f);
        }

        private int AmountPerCycle()
        {
            if (AmountPerCycleOverride > 0)
                return AmountPerCycleOverride;
            return Mathf.Max(1, Catalog?.Find(ActiveResourceId)?.AmountPerGather ?? 1);
        }

        /// <summary>
        /// The cells this building stands over: its GridObjectComponent's
        /// footprint when one is beside it, else the single cell under the
        /// parent's position.
        /// </summary>
        private IEnumerable<Vector2I> FootprintCells()
        {
            if (ResolveGridObject() is { } gridObject)
            {
                int width = Mathf.Max(1, gridObject.Footprint.X);
                int height = Mathf.Max(1, gridObject.Footprint.Y);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        yield return new Vector2I(gridObject.Cell.X + x, gridObject.Cell.Y + y);
                yield break;
            }

            if (_grid != null && GetParent() is Node2D body)
                yield return _grid.WorldToCell(body.GlobalPosition);
        }

        private GridObjectComponent? ResolveGridObject()
        {
            if (_gridObject == null || !GodotObject.IsInstanceValid(_gridObject))
                _gridObject = EntityComponent.FindComponent<GridObjectComponent>(GetParent(), recursive: false);
            return _gridObject;
        }

        private void ResolveReferences()
        {
            // Explicit wire only for the data layers, like everywhere else.
            if (_dataLayers == null || !GodotObject.IsInstanceValid(_dataLayers))
                _dataLayers = !DataLayersPath.IsEmpty
                    ? GetNodeOrNull<TerrainDataLayersComponent>(DataLayersPath)
                    : null;

            if (_store == null || !GodotObject.IsInstanceValid(_store))
                _store = !SubsurfaceStorePath.IsEmpty
                    ? GetNodeOrNull<GridSubsurfaceStoreComponent>(SubsurfaceStorePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSubsurfaceStoreComponent>(GetTree()?.CurrentScene) : null;

            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;

            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;
        }
    }
}
