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
    /// tanker is not a log truck.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridHaulerComponent : GameplayComponent, ITransporter
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

        [Export] public NodePath TransportManagerPath { get; set; } = new("");
        [Export] public NodePath PathFollowerPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public bool RegisterOnReady { get; set; } = true;

        /// <summary>Where deliveries are dropped, in grid cells.</summary>
        [Export] public Vector2I DepotCell { get; set; } = Vector2I.Zero;

        /// <summary>Resource ids this hauler carries; empty carries anything.</summary>
        [Export] public Godot.Collections.Array<string> AllowedResourceIds { get; set; } = new();

        /// <summary>Units of one resource the hold takes; a haul larger than this is refused.</summary>
        [Export(PropertyHint.Range, "1,99999,1")] public int Capacity { get; set; } = 50;

        /// <summary>
        /// Effective throughput in units per second - the dispatch pecking
        /// order. The manager offers hauls to the fastest accepting
        /// transporter first, so a pipeline (high rate) outranks this truck,
        /// and this truck outranks a mule.
        /// </summary>
        [Export(PropertyHint.Range, "0.1,999,0.1")] public float TransportRate { get; set; } = 5f;

        /// <summary>
        /// Optional: deliver into this storage's LOAD PORT instead of the
        /// wallet. When the depot is FULL the hauler keeps its cargo and
        /// retries every DeliveryRetrySeconds - backpressure, not loss.
        /// </summary>
        [Export] public NodePath DepotStoragePath { get; set; } = new("");
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
        private bool _wasMoving;
        private bool _registered;
        private float _retryClock;

        public override void _Ready()
        {
            base._Ready();
            ResolveReferences();
            if (!Engine.IsEditorHint() && RegisterOnReady)
                TryRegister();
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_registered && _manager != null && GodotObject.IsInstanceValid(_manager))
                _manager.Unregister(this);
            _registered = false;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (PathFollowerPath.IsEmpty)
                return new[] { "PathFollowerPath should point to a GridPathFollowerComponent." };
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
            if (!_registered && RegisterOnReady)
                TryRegister();

            if (State == HaulerState.Idle)
            {
                // Blocked delivery: the depot was full, the cargo stayed in
                // the hold. Keep knocking until space opens - backpressure
                // holds the material, it never disappears.
                if (_cargoAmount > 0 && !DepotStoragePath.IsEmpty)
                {
                    float step = double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;
                    _retryClock += step;
                    if (_retryClock >= Mathf.Max(0.1f, DeliveryRetrySeconds))
                    {
                        _retryClock = 0f;
                        TryDeliverCargo();
                    }
                }
                return;
            }

            if (_follower == null)
                return;

            if (_wasMoving && !_follower.IsMoving)
                Arrived();
            _wasMoving = _follower?.IsMoving ?? false;
        }

        public bool CanAccept(string resourceId)
        {
            if (AllowedResourceIds.Count == 0)
                return true;

            foreach (string allowed in AllowedResourceIds)
            {
                if (string.Equals(allowed?.Trim(), resourceId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Units of the given resource in the hold.</summary>
        public int Stored(string resourceId)
            => _cargoId.Length > 0 && _cargoId == resourceId ? _cargoAmount : 0;

        public Godot.Collections.Array<string> StoredIds()
        {
            var ids = new Godot.Collections.Array<string>();
            if (_cargoId.Length > 0 && _cargoAmount > 0)
                ids.Add(_cargoId);
            return ids;
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
            if (_cargoId.Length > 0 && _cargoId != resourceId)
                return 0;

            int space = Mathf.Max(0, Capacity - _cargoAmount);
            int taken = Mathf.Min(space, amount);
            if (taken <= 0)
                return 0;

            _cargoId = resourceId;
            _cargoAmount += taken;
            return taken;
        }

        /// <summary>Releases cargo from the hold - the giving half of a hand-off.</summary>
        public int Unload(string resourceId, int amount)
        {
            if (amount <= 0 || _cargoId.Length == 0 || _cargoId != resourceId)
                return 0;

            int released = Mathf.Min(amount, _cargoAmount);
            _cargoAmount -= released;
            if (_cargoAmount <= 0)
                _cargoId = "";
            return released;
        }

        public bool RequestHaul(Vector2I fromCell, string resourceId, int amount)
        {
            ResolveReferences();
            if (IsBusy || amount <= 0 || !CanAccept(resourceId) || _follower == null)
                return false;

            // All or nothing: a partial load would leave the remainder
            // nowhere. What does not fit stays the caller's to deliver.
            int taken = Load(resourceId, amount);
            if (taken < amount)
            {
                Unload(resourceId, taken);
                return false;
            }

            if (!_follower.MoveToCell(fromCell))
            {
                Unload(resourceId, taken);
                return false;
            }

            _wasMoving = true;
            State = HaulerState.MovingToPickup;
            EmitSignal(SignalName.HaulAccepted, fromCell.X, fromCell.Y, resourceId, amount);
            return true;
        }

        private void Arrived()
        {
            if (State == HaulerState.MovingToPickup)
            {
                if (_follower != null && _follower.MoveToCell(DepotCell))
                {
                    _wasMoving = true;
                    State = HaulerState.MovingToDepot;
                    return;
                }

                // No way home: deliver where it stands rather than lose the
                // load, and say so.
                Deliver("no_path_to_depot");
                return;
            }

            if (State == HaulerState.MovingToDepot)
                Deliver("");
        }

        private void Deliver(string failureReason)
        {
            State = HaulerState.Idle;
            _retryClock = 0f;
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
            if (_cargoAmount <= 0)
                return true;

            string id = _cargoId;
            ResolveReferences();

            if (_depotStorage != null && GodotObject.IsInstanceValid(_depotStorage))
            {
                int delivered = GridPorts.Transfer(this, _depotStorage, id, _cargoAmount);
                if (delivered > 0)
                    EmitSignal(SignalName.HaulDelivered, id, delivered);
                return _cargoAmount <= 0;
            }

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

            _manager.Register(this);
            _registered = true;
        }

        private void ResolveReferences()
        {
            if (_manager == null || !GodotObject.IsInstanceValid(_manager))
                _manager = !TransportManagerPath.IsEmpty
                    ? GetNodeOrNull<GridTransportManagerComponent>(TransportManagerPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridTransportManagerComponent>(GetTree()?.CurrentScene) : null;

            if (_follower == null || !GodotObject.IsInstanceValid(_follower))
                _follower = !PathFollowerPath.IsEmpty
                    ? GetNodeOrNull<GridPathFollowerComponent>(PathFollowerPath)
                    : EntityComponent.FindComponent<GridPathFollowerComponent>(GetParent(), recursive: false);

            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;

            if (_depotStorage == null || !GodotObject.IsInstanceValid(_depotStorage))
                _depotStorage = !DepotStoragePath.IsEmpty
                    ? GetNodeOrNull<Node>(DepotStoragePath)
                    : null;
        }
    }
}
