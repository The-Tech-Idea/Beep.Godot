using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// The DEFAULT transporter: a vehicle that accepts hauls from
    /// GridTransportManagerComponent, drives to the pickup cell, drives to
    /// its depot, and pays the load into the wallet on arrival.
    ///
    /// Default, not doctrine - the manager accepts any Node answering the
    /// IGridTransporter shape, and this one exists so transport works out of
    /// the box. Attach under a vehicle body (Node2D or CharacterBody2D)
    /// beside a GridPathFollowerComponent, point DepotCell at the drop-off,
    /// and register it with the manager (RegisterOnReady finds one
    /// scene-wide). AllowedResourceIds filters what it carries - an oil
    /// tanker is not a log truck; AllowedResourceTags, with Catalog wired,
    /// filters by resource TYPE instead of one id at a time.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridHaulerComponent : GameplayComponent, ITransporter, ISaveable
    {
        public enum HaulerState
        {
            Idle,
            MovingToPickup,
            MovingToDepot
        }

        [Signal] public delegate void HaulAcceptedEventHandler(int x, int y, string resourceId, int amount);
        [Signal] public delegate void HaulDeliveredEventHandler(string resourceId, int amount);
        [Signal] public delegate void HaulFailedEventHandler(string resourceId, int amount, string reason);

        [Export] public bool ParticipatesInSave { get; set; } = true;

        /// <summary>
        /// Cargo, depot and dispatch state. Actor saves restore the sibling
        /// follower before this component; a standalone save without a route
        /// holds undelivered cargo until ResumeHaulToDepot is requested.
        /// </summary>
        [Export] public string SaveKey { get; set; } = "grid_hauler.state";

        [Export] public NodePath TransportManagerPath { get; set; } = new("");
        [Export] public NodePath PathFollowerPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");

        /// <summary>
        /// The clock that decides what a turn of hauling is. Empty finds one
        /// scene-wide; with none anywhere the truck runs off its own frame
        /// delta at one turn per second.
        /// </summary>
        [Export] public NodePath WorkClockPath { get; set; } = new("");
        [Export] public bool RegisterOnReady { get; set; } = true;

        /// <summary>Where deliveries are dropped, in grid cells.</summary>
        private Vector2I _depotCell;
        [Export] public Vector2I DepotCell
        {
            get => _depotCell;
            set
            {
                if (_depotCell == value) return;
                _depotCell = value;
                if (!IsInsideTree()) return;
                if (_cargoAmount > 0) CancelHaul("depot_changed");
                else RefreshChunkPins();
            }
        }

        /// <summary>
        /// Resource ids this hauler carries; empty (together with
        /// AllowedResourceTags) carries anything. Checked before
        /// AllowedResourceTags - an exact id listed here is always accepted,
        /// catalog or not.
        /// </summary>
        [Export] public Godot.Collections.Array<string> AllowedResourceIds { get; set; } = new();

        /// <summary>
        /// The shared resource-type catalog, consulted for AllowedResourceTags
        /// - optional. Without it, only AllowedResourceIds (or carrying
        /// anything, when both lists are empty) decides.
        /// </summary>
        [Export] public ResourceCatalog? Catalog { get; set; }

        /// <summary>
        /// Resource TAGS this hauler carries, checked against Catalog - e.g.
        /// "fluid" carries any catalog resource tagged "fluid" without
        /// listing every fluid id by hand, so a resource the catalog adds
        /// later is haulable here with no scene edit. Applies ON TOP OF
        /// AllowedResourceIds, never instead of it. Only checked when
        /// Catalog is wired; an id the catalog does not recognize can never
        /// match a tag, which is what closes a typo'd resource id out.
        /// </summary>
        [Export] public Godot.Collections.Array<string> AllowedResourceTags { get; set; } = new();

        /// <summary>Units of one resource the hold takes; a haul larger than this is refused.</summary>
        [Export(PropertyHint.Range, "1,99999,1")] public int Capacity { get; set; } = 50;

        /// <summary>
        /// Effective throughput in units per TURN - the dispatch pecking
        /// order. The manager offers hauls to the fastest accepting
        /// transporter first, so a pipeline (high rate) outranks this truck,
        /// and this truck outranks a mule. Same unit as a transport chain's
        /// FlowRatePerTurn, or the ranking would compare two different things.
        /// </summary>
        [Export(PropertyHint.Range, "0.1,999,0.1")] public float TransportRatePerTurn { get; set; } = 5f;

        /// <summary>
        /// Optional: deliver into this storage's LOAD PORT instead of the
        /// wallet. When the depot is FULL the hauler keeps its cargo and
        /// retries every DeliveryRetrySeconds - backpressure, not loss.
        /// </summary>
        [Export] public NodePath DepotStoragePath { get; set; } = new("");

        /// <summary>
        /// How long to wait before knocking at a full depot again. REAL
        /// SECONDS, not turns: this is polling for space somebody else has to
        /// free, not work the hauler is doing, so it must keep retrying on a
        /// turn-based game's clock as much as on a real-time one.
        /// </summary>
        [Export(PropertyHint.Range, "0.1,60,0.1")] public float DeliveryRetrySeconds { get; set; } = 2f;

        public HaulerState State { get; private set; } = HaulerState.Idle;
        public bool IsBusy => State != HaulerState.Idle;
        public string CarryingResourceId => _cargoId;
        public int CarryingAmount => _cargoAmount;
        public int CurrentLoad => _cargoAmount;

        int ILoadPort.Capacity => Mathf.Max(1, Capacity);

        private string _cargoId = "";
        private int _cargoAmount;

        private GridTransportManagerComponent? _manager;
        private GridPathFollowerComponent? _follower;
        private GridResourceWalletComponent? _wallet;
        private Node? _depotStorage;
        private readonly GridWorkClockBinding _workClock = new();
        private bool _wasMoving;
        private bool _registered;
        private float _retryClock;
        private bool _deliverySuspended;

        public override void _Ready()
        {
            base._Ready();
            ResolveReferences();
            RefreshChunkPins();
            if (!Engine.IsEditorHint())
            {
                // A haul is measured in TURNS, so it advances on the work clock
                // - otherwise a turn-based game would have trucks arriving in
                // real time while everything else waited for a turn. _Process
                // is left ON either way, because the blocked-delivery retry
                // below is a real-time backoff rather than work done.
                _workClock.Bind(this, WorkClockPath, AdvanceWork);
                if (RegisterOnReady)
                    TryRegister();
                if (ParticipatesInSave)
                    AddToGroup(SaveableHelper.Group);
            }
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            _pins?.Release();
            _workClock.Unbind();
            if (_registered && _manager != null && GodotObject.IsInstanceValid(_manager))
                _manager.Unregister(this);
            _registered = false;
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
            RequestReady();
            base._ExitTree();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (PathFollowerPath.IsEmpty)
                return new[] { "PathFollowerPath should point to a GridPathFollowerComponent." };
            if (AllowedResourceTags.Count > 0 && Catalog == null)
                return new[] { "AllowedResourceTags is set but Catalog is empty - tag filtering has no effect, and any id not also listed in AllowedResourceIds will be rejected." };
            return Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || Engine.IsEditorHint())
                return;

            AdvanceDeliveryRetry(delta);

            // The haul itself is only self-driven when no work clock was found
            // - a template scene opened on its own, or a headless probe.
            if (!_workClock.FollowsClock)
                AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));
        }

        /// <summary>
        /// Advances the haul by turns of travel, and the real-time delivery
        /// retry by the same span of seconds. Kept public under its old name so
        /// a caller that steps the truck deliberately still can; one turn is
        /// one second on the real-time axis, so the meaning is unchanged there.
        /// </summary>
        public void Tick(double delta)
        {
            AdvanceDeliveryRetry(delta);
            AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));
        }

        /// <summary>Carries the haul forward by turns of travel. Bound to the work clock.</summary>
        public void AdvanceWork(float turns)
        {
            RefreshChunkPins();
            if (!IsActive)
                return;

            if (!_registered && RegisterOnReady)
                TryRegister();

            if (State == HaulerState.Idle || _follower == null)
                return;

            if (!float.IsFinite(turns) || turns <= 0f)
                return;

            if (_awaitingTerrain)
            {
                Vector2I destination = State == HaulerState.MovingToPickup ? _pickupCell : DepotCell;
                if (!EndpointReady(destination)) return;
                if (!BeginHaulLeg(destination)) CancelHaul("no_path_after_terrain_load");
                return;
            }

            if (_wasMoving && !_follower.IsMoving)
                Arrived();
            _wasMoving = _follower?.IsMoving ?? false;
        }

        /// <summary>
        /// Blocked delivery: the depot was full, the cargo stayed in the hold.
        /// Keep knocking until space opens - backpressure holds the material,
        /// it never disappears. Driven by the frame delta on both time axes,
        /// because waiting for somebody else to free space is not work this
        /// hauler is doing.
        /// </summary>
        private void AdvanceDeliveryRetry(double delta)
        {
            if (State != HaulerState.Idle || _cargoAmount <= 0 || _deliverySuspended)
                return;

            float step = double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;
            _retryClock += step;
            if (_retryClock < Mathf.Max(0.1f, DeliveryRetrySeconds))
                return;

            _retryClock = 0f;
            TryDeliverCargo();
        }

        public bool CanAccept(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return false;
            if (AllowedResourceIds.Count == 0 && AllowedResourceTags.Count == 0)
                return true;

            return AcceptsResourceType(GridIds.Normalize(resourceId));
        }

        /// <summary>
        /// HOOK: the acceptance rule once at least one of AllowedResourceIds/
        /// AllowedResourceTags is configured (an unconfigured hauler accepts
        /// everything and never reaches this method - see CanAccept). The
        /// default checks the exact-id list first, then - only when Catalog
        /// is wired - whether the catalog's definition for the id carries any
        /// of AllowedResourceTags. Override to replace the rule entirely
        /// (category-based, a per-project scheme) without touching Load/Unload.
        /// The <paramref name="resourceId"/> arrives already canonicalised through
        /// GridIds.Normalize (DUP-14), matching the storage twin; an override that
        /// compares against author strings must normalise its own side too.
        /// </summary>
        protected virtual bool AcceptsResourceType(string resourceId)
        {
            foreach (string allowed in AllowedResourceIds)
            {
                if (GridIds.Normalize(allowed) == resourceId)
                    return true;
            }

            if (Catalog != null && AllowedResourceTags.Count > 0
                && Catalog.Find(resourceId) is { } definition)
            {
                foreach (string tag in AllowedResourceTags)
                {
                    if (definition.HasTag(tag))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Units of the given resource in the hold.</summary>
        public int Stored(string resourceId)
            => _cargoId.Length > 0 && _cargoId == GridIds.Normalize(resourceId) ? _cargoAmount : 0;

        public Godot.Collections.Array<string> StoredIds()
        {
            var ids = new Godot.Collections.Array<string>();
            if (_cargoId.Length > 0 && _cargoAmount > 0)
                ids.Add(_cargoId);
            return ids;
        }

        public Godot.Collections.Dictionary CaptureState()
            => new Godot.Collections.Dictionary
            {
                ["cargo_id"] = _cargoId,
                ["cargo_amount"] = _cargoAmount,
                ["state"] = (int)State,
                ["depot_x"] = DepotCell.X,
                ["depot_y"] = DepotCell.Y,
                ["suspended"] = _deliverySuspended,
                ["pickup_x"] = _pickupCell.X,
                ["pickup_y"] = _pickupCell.Y,
                ["waiting_terrain"] = _awaitingTerrain,
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            ResolveReferences();
            string id = GridVariantReader.String(state, "cargo_id", "");
            int amount = Mathf.Max(0, GridVariantReader.Int(state, "cargo_amount", 0));
            // Normalise on load so an older save's space-kept cargo id migrates to canonical (DUP-14).
            _cargoId = amount > 0 ? GridIds.Normalize(id) : "";
            _cargoAmount = _cargoId.Length > 0 ? amount : 0;

            // Restoration is state-only. Delivery resumes on a later simulation
            // tick, after the destination's saved inventory has also been loaded.
            State = (HaulerState)GridVariantReader.Int(state, "state", (int)HaulerState.Idle);
            if (!Enum.IsDefined(State)) State = HaulerState.Idle;
            _depotCell = new(GridVariantReader.Int(state, "depot_x", DepotCell.X), GridVariantReader.Int(state, "depot_y", DepotCell.Y));
            _deliverySuspended = state.TryGetValue("suspended", out var suspended) && suspended.AsBool();
            _pickupCell = new(GridVariantReader.Int(state, "pickup_x", 0), GridVariantReader.Int(state, "pickup_y", 0));
            _awaitingTerrain = state.TryGetValue("waiting_terrain", out var waiting) && waiting.AsBool();
            if (State != HaulerState.Idle && ActorComponent.OwningActor(this) is null)
            {
                State = HaulerState.Idle;
                _deliverySuspended = true;
            }
            _awaitingTerrain &= State != HaulerState.Idle;
            _wasMoving = State != HaulerState.Idle && !_awaitingTerrain;
            _retryClock = 0f;
            RefreshChunkPins();
        }

        // Explicit interface implementation: ISaveable.Load(state) would
        // otherwise overload the cargo-hold Load(resourceId, amount) above,
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

        /// <summary>
        /// Takes cargo into the hold, up to Capacity, one resource at a time.
        /// The receiving half of a hand-off - another transporter (or a
        /// pipeline segment) gives via GridTransportManagerComponent.Transfer.
        /// </summary>
        public int Load(string resourceId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(resourceId) || !CanAccept(resourceId))
                return 0;
            // Canonical cargo id (DUP-14): the hold keys its one resource the same way the wallet and
            // storage do, so a "Iron Ore" haul and an "iron_ore" query are the same cargo.
            string id = GridIds.Normalize(resourceId);
            if (_cargoId.Length > 0 && _cargoId != id)
                return 0;

            int space = Mathf.Max(0, Capacity - _cargoAmount);
            int taken = Mathf.Min(space, amount);
            if (taken <= 0)
                return 0;

            _cargoId = id;
            _cargoAmount += taken;
            RefreshChunkPins();
            return taken;
        }

        /// <summary>Releases cargo from the hold - the giving half of a hand-off.</summary>
        public int Unload(string resourceId, int amount)
        {
            if (amount <= 0 || _cargoId.Length == 0 || _cargoId != GridIds.Normalize(resourceId))
                return 0;

            int released = Mathf.Min(amount, _cargoAmount);
            _cargoAmount -= released;
            if (_cargoAmount <= 0)
                _cargoId = "";
            RefreshChunkPins();
            return released;
        }

        public bool RequestHaul(Vector2I fromCell, string resourceId, int amount)
        {
            ResolveReferences();
            var actor = ActorComponent.ForBody(GetParent());
            if (IsBusy || _cargoAmount > 0 || actor is { HasOrders: true } || actor is { IsDead: true }
                || amount <= 0 || !CanAccept(resourceId) || _follower == null)
                return false;

            // All or nothing: a partial load would leave the remainder
            // nowhere. What does not fit stays the caller's to deliver.
            int taken = Load(resourceId, amount);
            if (taken < amount)
            {
                Unload(resourceId, taken);
                return false;
            }

            _pickupCell = fromCell;
            State = HaulerState.MovingToPickup;
            if (!BeginHaulLeg(fromCell))
            {
                State = HaulerState.Idle;
                Unload(resourceId, taken);
                return false;
            }

            _deliverySuspended = false;
            EmitSignal(SignalName.HaulAccepted, fromCell.X, fromCell.Y, resourceId, amount);
            return true;
        }

        private void Arrived()
        {
            if (_follower?.HasReachedDestination != true)
            {
                CancelHaul("route_interrupted");
                return;
            }
            if (State == HaulerState.MovingToPickup)
            {
                if (_follower.DestinationCell != _pickupCell)
                {
                    CancelHaul("pickup_destination_changed");
                    return;
                }
                State = HaulerState.MovingToDepot;
                if (BeginHaulLeg(DepotCell))
                {
                    return;
                }

                CancelHaul("no_path_to_depot");
                return;
            }

            if (State == HaulerState.MovingToDepot)
            {
                if (_follower.DestinationCell != DepotCell) CancelHaul("depot_destination_changed");
                else Deliver("");
            }
        }

        public void CancelHaul(string reason = "cancelled")
        {
            State = HaulerState.Idle;
            _wasMoving = false;
            _deliverySuspended = true;
            _awaitingTerrain = false;
            _follower?.CancelMove();
            RefreshChunkPins();
            if (_cargoAmount > 0) EmitSignal(SignalName.HaulFailed, _cargoId, _cargoAmount, reason);
        }

        public bool ResumeHaulToDepot()
        {
            ResolveReferences();
            var actor = ActorComponent.ForBody(GetParent());
            if (!IsActive || IsBusy || _cargoAmount <= 0 || actor is { HasOrders: true } || actor is { IsDead: true }
                || _follower is null) return false;
            State = HaulerState.MovingToDepot;
            if (!BeginHaulLeg(DepotCell))
            {
                State = HaulerState.Idle;
                return false;
            }
            _deliverySuspended = false;
            return true;
        }

        private void Deliver(string failureReason)
        {
            State = HaulerState.Idle;
            _awaitingTerrain = false;
            _retryClock = 0f;
            RefreshChunkPins();
            if (failureReason.Length > 0 && _cargoAmount > 0)
                EmitSignal(SignalName.HaulFailed, _cargoId, _cargoAmount, failureReason);

            TryDeliverCargo();
        }

        /// <summary>
        /// Empties the hold at the destination: into the depot storage's load
        /// port when one is wired (what does not fit STAYS in the hold and is
        /// retried - a full depot means backpressure, never loss), else into
        /// the wallet. Returns true when the hold is empty afterwards.
        /// </summary>
        public bool TryDeliverCargo()
        {
            RefreshChunkPins();
            if (_cargoAmount <= 0)
                return true;
            if (_deliverySuspended) return false;
            if (!EndpointReady(DepotCell)) return false;

            string id = _cargoId;
            ResolveReferences();

            if (_depotStorage != null && GodotObject.IsInstanceValid(_depotStorage))
            {
                int delivered = GridPorts.Transfer(this, _depotStorage, id, _cargoAmount);
                if (delivered > 0)
                    EmitSignal(SignalName.HaulDelivered, id, delivered);
                return _cargoAmount <= 0;
            }

            // An explicitly configured but unavailable depot must not fall back
            // to another destination. Keep the cargo until its destination exists.
            if (!DepotStoragePath.IsEmpty || _wallet == null)
                return false;

            int amount = Unload(id, _cargoAmount);
            if (amount <= 0)
                return true;

            _wallet?.AddAmount(id, amount);
            EmitSignal(SignalName.HaulDelivered, id, amount);
            return true;
        }

        private void TryRegister()
        {
            ResolveReferences();
            if (_manager == null || _registered)
                return;

            // Registration can be REFUSED, so the flag records what the manager
            // actually did - the same reason GridExtractorComponent reads it.
            // Marking it true regardless would leave a rejected hauler believing
            // it was registered and never retrying.
            _registered = _manager.Register(this);
        }

        private void ResolveReferences()
        {
            Resolve(TransportManagerPath, ref _manager);

            // Not the shared rule: the follower is a sibling on the same body,
            // never found scene-wide.
            if (_follower == null || !GodotObject.IsInstanceValid(_follower))
                _follower = !PathFollowerPath.IsEmpty
                    ? GetNodeOrNull<GridPathFollowerComponent>(PathFollowerPath)
                    : EntityComponent.FindComponent<GridPathFollowerComponent>(GetParent(), recursive: false);

            Resolve(ResourceWalletPath, ref _wallet);

            // Explicit wire only: an unwired depot means straight-to-wallet delivery.
            if (_depotStorage == null || !GodotObject.IsInstanceValid(_depotStorage))
                _depotStorage = !DepotStoragePath.IsEmpty
                    ? GetNodeOrNull<Node>(DepotStoragePath)
                    : null;
        }
    }
}
