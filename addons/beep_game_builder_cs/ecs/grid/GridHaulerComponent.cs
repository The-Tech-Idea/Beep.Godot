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
        private bool _wasMoving;
        private bool _registered;

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

            if (State == HaulerState.Idle || _follower == null)
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
            string id = _cargoId;
            int amount = Unload(id, _cargoAmount);
            State = HaulerState.Idle;

            if (amount <= 0)
                return;

            _wallet?.AddAmount(id, amount);
            if (failureReason.Length > 0)
                EmitSignal(SignalName.HaulFailed, id, amount, failureReason);
            EmitSignal(SignalName.HaulDelivered, id, amount);
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
        }
    }
}
