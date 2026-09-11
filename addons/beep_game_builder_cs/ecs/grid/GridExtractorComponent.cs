using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The DEFAULT extractor: a placed building that works the deposit UNDER
    /// it - a derrick, a mine shaft, an offshore platform, a colony ice
    /// extractor. Attach under a building scene alongside its
    /// GridObjectComponent; once the building is complete it binds to the
    /// deposit beneath its footprint, validates that it can work it, and
    /// draws it down cycle by cycle into the wallet until it is worked out.
    ///
    /// DEFAULT, not doctrine. How extraction FEELS is the game's decision,
    /// and the system's real contract is GridSubsurfaceStoreComponent's three
    /// methods - ResourceIdAt, RemainingAt, Draw - which any script can call.
    /// A game replaces this component entirely, writes its own in GDScript,
    /// or subclasses it and overrides the hooks: DeliverYield to change where
    /// the yield goes (a local buffer, a truck to be collected, a silo), and
    /// DepositBlockReason to change what may be worked (tech trees, permits,
    /// licence blocks). Several extractor TYPES are just several build
    /// definitions, each with its own scene carrying its own configured (or
    /// subclassed) extractor - ReachDepth is the shipped tech ladder, and a
    /// resource's ExtractorBuildId optionally binds it to one of them.
    ///
    /// What lies below and how much is left are never this component's to
    /// own: the data layers answer the first and the subsurface store the
    /// second. This node is only a pump.
    /// </summary>
    /// <summary>Where the shipped extractor's yield goes each cycle.</summary>
    public enum ExtractorDelivery
    {
        /// <summary>Straight into the wallet - extraction with no logistics.</summary>
        Wallet = 0,

        /// <summary>
        /// Offered to GridTransportManagerComponent: a registered transporter
        /// hauls it, and only an unassignable load falls back to the wallet so
        /// yield is never lost.
        /// </summary>
        TransportManager = 1,

        /// <summary>
        /// Into the extractor's OWN buffer - its unload port - for a pipeline
        /// or hauler to draw from (pull logistics). A full buffer overflows
        /// to the wallet so yield is never lost.
        /// </summary>
        Buffer = 2,
    }

    [Tool]
    [GlobalClass]
    public partial class GridExtractorComponent : GameplayComponent, IExtractor, ISaveable
    {
        [Signal] public delegate void ExtractionStartedEventHandler(string resourceId);
        [Signal] public delegate void ExtractionCycleEventHandler(string resourceId, int amount, int remaining);
        [Signal] public delegate void ExtractionStoppedEventHandler(string reason);
        [Signal] public delegate void ExtractionStalledEventHandler(string resourceId);
        [Signal] public delegate void ExtractionResumedEventHandler(string resourceId);

        [Export] public bool ParticipatesInSave { get; set; } = true;

        /// <summary>
        /// Only the output buffer round-trips through a save - what a cycle
        /// has already drawn up and not yet handed off, real economic value
        /// that would otherwise vanish on reload. Binding state (which
        /// deposit, IsExtracting, the cycle clock) is not saved because it is
        /// not lost: _Ready re-derives it by calling TryBind again against
        /// the also-persisted GridSubsurfaceStoreComponent.
        /// </summary>
        [Export] public string SaveKey { get; set; } = "grid_extractor.state";

        [Export] public NodePath DataLayersPath { get; set; } = new("");
        [Export] public NodePath SubsurfaceStorePath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath TransportManagerPath { get; set; } = new("");
        [Export] public NodePath ExtractionManagerPath { get; set; } = new("");

        /// <summary>
        /// The clock that decides what a turn of extraction is. Empty finds one
        /// scene-wide; with none anywhere the pump runs off its own frame delta
        /// at one turn per second.
        /// </summary>
        [Export] public NodePath WorkClockPath { get; set; } = new("");
        [Export] public ExtractorDelivery DeliverVia { get; set; } = ExtractorDelivery.Wallet;

        /// <summary>Units the output buffer holds when DeliverVia is Buffer.</summary>
        [Export(PropertyHint.Range, "1,99999,1")] public int BufferCapacity { get; set; } = 100;

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

        /// <summary>
        /// Whether this extractor announces itself to the extraction manager on
        /// its own. Left false, an orchestrator controls registration timing and
        /// order itself - the same lever GridHaulerComponent carries for the
        /// transport manager.
        /// </summary>
        [Export] public bool RegisterOnReady { get; set; } = true;

        /// <summary>
        /// Turns of work one cycle takes, overriding the definition's when
        /// above zero. A turn is a day, so the authored number means the same
        /// amount of world time on the turn axis and the real-time one.
        /// </summary>
        [Export(PropertyHint.Range, "0,600,0.01")] public float CycleTurnsOverride { get; set; } = 0f;

        /// <summary>Overrides the definition's AmountPerGather when above zero.</summary>
        [Export(PropertyHint.Range, "0,9999,1")] public int AmountPerCycleOverride { get; set; } = 0;

        /// <summary>
        /// The shared resource catalog, for the deposit's cycle rules and its
        /// ExtractorBuildId validation.
        /// </summary>
        [Export] public ResourceCatalog? Catalog { get; set; }

        public bool IsExtracting { get; private set; }
        public string ActiveResourceId { get; private set; } = "";

        /// <summary>
        /// True while Buffer delivery is shut in by a full buffer - source
        /// side backpressure. The deposit stays intact; extraction resumes by
        /// itself when the chain drains the buffer.
        /// </summary>
        public bool IsStalled { get; private set; }

        // ---- The extractor's own ports ---------------------------------------
        //
        // The output buffer is what the rest of a chain CONNECTS to: a
        // pipeline segment or hauler Unloads from it (pull), and Load exists
        // for the rare thing pushed back down a well. Both ports, like every
        // IStorage and ITransporter, so anything clips to anything.

        private string _bufferId = "";
        private int _bufferAmount;

        public int CurrentLoad => _bufferAmount;

        int ILoadPort.Capacity => Mathf.Max(1, BufferCapacity);

        public bool CanAccept(string resourceId)
            => !string.IsNullOrWhiteSpace(resourceId)
                && (_bufferId.Length == 0 || _bufferId == GridIds.Normalize(resourceId));

        public int Load(string resourceId, int amount)
        {
            if (amount <= 0 || !CanAccept(resourceId))
                return 0;

            // Canonical buffer id (DUP-14): the single-slot hold keys its resource the way the wallet,
            // storage and hauler do, so a spaced/dashed extract id and a canonical query are one cargo.
            string id = GridIds.Normalize(resourceId);
            int space = Mathf.Max(0, Mathf.Max(1, BufferCapacity) - _bufferAmount);
            int taken = Mathf.Min(space, amount);
            if (taken <= 0)
                return 0;

            _bufferId = id;
            _bufferAmount += taken;
            return taken;
        }

        public int Unload(string resourceId, int amount)
        {
            if (amount <= 0 || _bufferId.Length == 0 || _bufferId != GridIds.Normalize(resourceId))
                return 0;

            int released = Mathf.Min(amount, _bufferAmount);
            _bufferAmount -= released;
            if (_bufferAmount <= 0)
                _bufferId = "";
            return released;
        }

        public int Stored(string resourceId)
            => _bufferId.Length > 0 && _bufferId == GridIds.Normalize(resourceId) ? _bufferAmount : 0;

        public Godot.Collections.Array<string> StoredIds()
        {
            var ids = new Godot.Collections.Array<string>();
            if (_bufferId.Length > 0 && _bufferAmount > 0)
                ids.Add(_bufferId);
            return ids;
        }

        public Godot.Collections.Dictionary CaptureState()
            => new Godot.Collections.Dictionary
            {
                ["buffer_id"] = _bufferId,
                ["buffer_amount"] = _bufferAmount,
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            string id = GridVariantReader.String(state, "buffer_id", "");
            int amount = Mathf.Max(0, GridVariantReader.Int(state, "buffer_amount", 0));
            // Normalise on load so an older save's space-kept buffer id migrates to canonical (DUP-14).
            _bufferId = amount > 0 ? GridIds.Normalize(id) : "";
            _bufferAmount = _bufferId.Length > 0 ? amount : 0;
        }

        // Explicit interface implementation: ISaveable.Load(state) would
        // otherwise overload the ILoadPort Load(resourceId, amount) above,
        // making every duck hand-off ambiguous under Godot's name-based Call.
        void ISaveable.Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        void ISaveable.Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }

        private TerrainDataLayersComponent? _dataLayers;
        private GridSubsurfaceStoreComponent? _store;
        private GridResourceWalletComponent? _wallet;
        private GridProjectionComponent? _grid;
        private GridObjectComponent? _gridObject;
        private GridTransportManagerComponent? _transport;
        private GridExtractionManagerComponent? _extractionManager;
        private readonly List<Vector2I> _depositCells = new();
        private readonly GridWorkClockBinding _workClock = new();
        private float _cycleClock;
        private bool _bound;
        private bool _registered;

        public override void _Ready()
        {
            base._Ready();
            ResolveReferences();
            if (!Engine.IsEditorHint())
            {
                // An extraction cycle is measured in TURNS, so it advances on
                // the work clock - otherwise a turn-based game would have
                // derricks pumping in real time while everything else waited
                // for a turn.
                bool bound = _workClock.Bind(this, WorkClockPath, AdvanceWork);
                SetProcess(!bound);

                if (RegisterOnReady)
                    TryRegisterWithManager();
                if (ParticipatesInSave)
                    AddToGroup(SaveableHelper.Group);
            }
            else
            {
                SetProcess(false);
            }
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            _workClock.Unbind();
            if (_registered && _extractionManager != null && GodotObject.IsInstanceValid(_extractionManager))
                _extractionManager.Unregister(this);
            _registered = false;
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (SubsurfaceStorePath.IsEmpty)
                return new[] { "SubsurfaceStorePath should point to a GridSubsurfaceStoreComponent." };
            return Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            // Reached only when no work clock was found - a template scene
            // opened on its own, or a headless probe. SetProcess is off otherwise.
            if (Engine.IsEditorHint())
                return;

            AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));
        }

        /// <summary>
        /// Advances the pump by turns of work. Kept public under its old name
        /// so a caller that steps extraction deliberately still can; one turn
        /// is one second on the real-time axis, so the meaning is unchanged
        /// there.
        /// </summary>
        public void Tick(double delta) => AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));

        /// <summary>Runs whole cycles out of the turns elapsed. Bound to the work clock.</summary>
        public void AdvanceWork(float turns)
        {
            if (!IsActive)
                return;

            if (!_registered && RegisterOnReady)
                TryRegisterWithManager();

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

            if (!float.IsFinite(turns) || turns <= 0f)
                return;

            _cycleClock += turns;
            float cycleTurns = CycleTurns();
            while (_cycleClock >= cycleTurns && IsExtracting)
            {
                _cycleClock -= cycleTurns;
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
            foreach (Vector2I cell in ResolveGridObject()?.FootprintCells() ?? GridFootprint.SingleUnder(GetParent(), _grid))
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
            string blocked = DepositBlockReason(definition);
            if (blocked.Length > 0)
                return Stop(blocked);

            // A Fluid or Gas deposit is one connected RESERVOIR: the pump on
            // this footprint drains the whole contiguous field, the way an
            // oil well actually behaves. Solid stays cell-local - a mine only
            // gets what is under it.
            if (definition != null && definition.Form != ResourceForm.Solid)
                ExpandToReservoir(id);

            ActiveResourceId = id;
            IsExtracting = true;
            _cycleClock = 0f;
            EmitSignal(SignalName.ExtractionStarted, id);
            return true;
        }

        public void StopExtraction(string reason = "stopped")
            => Stop(reason);

        /// <summary>The cells this extractor is currently working, for subclasses.</summary>
        protected IReadOnlyList<Vector2I> DepositCells => _depositCells;

        /// <summary>The bound store, for subclasses running their own cycles.</summary>
        protected GridSubsurfaceStoreComponent? BoundStore => _store;

        /// <summary>
        /// HOOK: whether this extractor may work the deposit; empty string
        /// means yes, anything else is the ExtractionStopped reason. The
        /// default enforces the depth ladder and the resource's authored
        /// ExtractorBuildId. Override for tech trees, permits, licence blocks.
        /// </summary>
        protected virtual string DepositBlockReason(ResourceDefinition? definition)
        {
            if (definition != null)
            {
                if (definition.Depth > ReachDepth)
                    return "too_deep";

                string required = definition.ExtractorBuildId?.Trim() ?? "";
                string ownId = ResolveGridObject()?.ObjectId ?? "";
                if (required.Length > 0 && !string.Equals(required, ownId, StringComparison.OrdinalIgnoreCase))
                    return "wrong_extractor";
                return "";
            }

            if (_dataLayers != null)
            {
                // No catalog to consult: the published depth band still gates.
                foreach (Vector2I cell in _depositCells)
                {
                    if (_dataLayers.UndergroundDepthAt(cell) > (int)ReachDepth)
                        return "too_deep";
                }
            }
            return "";
        }

        /// <summary>
        /// HOOK: where a cycle's yield goes. The default pays the wallet, or
        /// offers the load to the transport manager when DeliverVia says so -
        /// falling back to the wallet if no transporter takes it, so yield is
        /// never lost. Override to fill a local buffer, spawn a pallet, feed
        /// a silo, or anything else the game means by "extracted".
        /// </summary>
        protected virtual void DeliverYield(string resourceId, int amount)
        {
            if (DeliverVia == ExtractorDelivery.Buffer)
            {
                // Pull logistics: the yield sits in this extractor's own
                // unload port until a pipeline or hauler draws it. Overflow
                // goes to the wallet so yield is never lost.
                int buffered = Load(resourceId, amount);
                if (buffered < amount)
                    _wallet?.AddAmount(resourceId, amount - buffered);
                return;
            }

            if (DeliverVia == ExtractorDelivery.TransportManager
                && _transport != null
                && GodotObject.IsInstanceValid(_transport))
            {
                Vector2I from = ResolveGridObject()?.Cell
                    ?? (_depositCells.Count > 0 ? _depositCells[0] : Vector2I.Zero);
                if (_transport.RequestHaul(from, resourceId, amount))
                    return;
            }

            _wallet?.AddAmount(resourceId, amount);
        }

        /// <summary>Turns per extraction cycle right now, for rate displays.</summary>
        public float CurrentCycleTurns() => CycleTurns();

        /// <summary>Units per extraction cycle right now, for rate displays.</summary>
        public int CurrentAmountPerCycle() => AmountPerCycle();

        private void TryRegisterWithManager()
        {
            ResolveReferences();
            if (_extractionManager == null || _registered)
                return;

            // Registration can be REFUSED, so the flag records what the manager
            // actually did - marking it true regardless would leave a rejected
            // extractor believing it was registered and never retrying.
            _registered = _extractionManager.Register(this);
        }

        /// <summary>
        /// Grows the working set from the footprint through every 4-connected
        /// cell holding the same resource. Cells outside the deposit read as
        /// empty from the store, so the field's own rim is the boundary; the
        /// cap is a runaway guard far above any real basin.
        /// </summary>
        private void ExpandToReservoir(string id)
        {
            const int MaxReservoirCells = 4096;
            if (_store == null || _depositCells.Count == 0)
                return;

            var seen = new HashSet<Vector2I>(_depositCells);
            var frontier = new Queue<Vector2I>(_depositCells);
            while (frontier.Count > 0 && seen.Count < MaxReservoirCells)
            {
                Vector2I cell = frontier.Dequeue();
                foreach (Vector2I next in new[]
                {
                    new Vector2I(cell.X + 1, cell.Y),
                    new Vector2I(cell.X - 1, cell.Y),
                    new Vector2I(cell.X, cell.Y + 1),
                    new Vector2I(cell.X, cell.Y - 1),
                })
                {
                    if (seen.Contains(next) || _store.ResourceIdAt(next) != id)
                        continue;
                    seen.Add(next);
                    frontier.Enqueue(next);
                    _depositCells.Add(next);
                }
            }
        }

        private void RunCycle()
        {
            if (_store == null || ActiveResourceId.Length == 0)
            {
                Stop("missing_subsurface_store");
                return;
            }

            int perCycle = AmountPerCycle();

            // Source-side backpressure: in Buffer delivery, never draw more
            // than the buffer can hold. A full buffer shuts the pump in with
            // the DEPOSIT INTACT - like the whole chain, stopped, not lost -
            // and it resumes by itself the moment the chain drains it.
            if (DeliverVia == ExtractorDelivery.Buffer)
            {
                int space = Mathf.Max(0, Mathf.Max(1, BufferCapacity) - _bufferAmount);
                perCycle = Mathf.Min(perCycle, space);
                if (perCycle <= 0)
                {
                    if (!IsStalled)
                    {
                        IsStalled = true;
                        EmitSignal(SignalName.ExtractionStalled, ActiveResourceId);
                    }
                    return;
                }
            }

            if (IsStalled)
            {
                IsStalled = false;
                EmitSignal(SignalName.ExtractionResumed, ActiveResourceId);
            }

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
                DeliverYield(ActiveResourceId, drawn);
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

        private float CycleTurns()
        {
            if (CycleTurnsOverride > 0f && float.IsFinite(CycleTurnsOverride))
                return CycleTurnsOverride;
            // ResourceDefinition.GatherTurns is the catalog's per-cycle turn count,
            // the unit every timed grid subsystem measures in - read here directly.
            float fromDefinition = Catalog?.Find(ActiveResourceId)?.GatherTurns ?? 1.5f;
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

            Resolve(SubsurfaceStorePath, ref _store);
            Resolve(ResourceWalletPath, ref _wallet);
            Resolve(GridPath, ref _grid);
            Resolve(TransportManagerPath, ref _transport);
            Resolve(ExtractionManagerPath, ref _extractionManager);
        }
    }
}
