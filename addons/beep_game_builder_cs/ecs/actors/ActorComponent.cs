using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>Stable identity, command execution and local save boundary for one authored body.</summary>
[Tool, GlobalClass]
public partial class ActorComponent : GameplayComponent
{
    [Export] public string ActorId { get; set; } = "";
    [Export] public string OwnerId { get; set; } = "";
    [Export] public ActorDefinition? Definition { get; set; }
    [Export] public NodePath RegistryPath { get; set; } = new("");
    [Export] public NodePath CommandHandlerPath { get; set; } = new("");
    [Signal] public delegate void CommandFinishedEventHandler(int action, bool success, string reason);
    [Signal] public delegate void OwnershipChangedEventHandler(string previousOwner, string owner);

    public ActorRegistryComponent? Registry { get; private set; }
    private Node2D? _body;
    private bool _bodyResolved;
    public Node2D? Body
    {
        get
        {
            if (!_bodyResolved) RefreshBody();
            return _body;
        }
    }

    private void RefreshBody()
    {
        _body = GetParent() as Node2D;
        _bodyResolved = true;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationParented) RefreshBody();
        else if (what == NotificationUnparented)
        {
            _body = null;
            _bodyResolved = true;
        }
    }
    public Vector2 MoveIntent { get; private set; }
    public Vector2 AimIntent { get; private set; } = Vector2.Right;
    public bool FireHeld { get; private set; }
    public bool JumpHeld { get; private set; }
    private bool _jumpRequested;
    private readonly HashSet<string> _heldAbilities = new(StringComparer.Ordinal);
    public void SetAbilityHeld(string action, bool held)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        if (held) _heldAbilities.Add(action); else _heldAbilities.Remove(action);
    }
    public bool IsAbilityHeld(string action) => (action == "jump" && JumpHeld) || _heldAbilities.Contains(action);
    internal void ReplaceHeldAbilities(IEnumerable<string> actions) { _heldAbilities.Clear(); _heldAbilities.UnionWith(actions); }
    internal IEnumerable<string> HeldAbilityActions()
    {
        if (Body is null) yield break;
        foreach (Node child in Body.GetChildren())
            if (child is GlideComponent glide) yield return glide.GlideAction;
            else if (child is HoverComponent hover) yield return hover.HoverAction;
            else if (child is SlideComponent slide) yield return slide.SlideAction;
    }
    private bool _dashHeld, _dashRequested;
    public void SetDashIntent(bool held) { _dashRequested |= held && !_dashHeld; _dashHeld = held; }
    public bool ConsumeDash() { bool requested = _dashRequested; _dashRequested = false; return requested; }
    private readonly Queue<Godot.Collections.Dictionary> _orders = new();
    private Godot.Collections.Dictionary? _current;
    private bool _started;
    private double _repathDelay;
    public bool HasOrders => _current is not null || _orders.Count > 0;
    internal ulong CommandRevision { get; private set; }
    internal bool? RestoredFloorContact { get; set; }

    public override void _Ready()
    {
        base._Ready();
        if (Engine.IsEditorHint()) { SetPhysicsProcess(false); return; }
        Registry = GetNodeOrNull<ActorRegistryComponent>(RegistryPath);
        if (Body is null || Registry is null || !Registry.RegisterActor(this))
        {
            Registry = null;
            GD.PushError($"[{Name}] Actor needs a Node2D body, an explicit registry, and a unique actor ID.");
            SetPhysicsProcess(false);
            return;
        }
        ProcessPhysicsPriority = -20;
        SetPhysicsProcess(true);
        if (GetSiblingComponent<HealthComponent>() is { } health) health.Died += OnDied;
        if (GetSiblingComponent<GridWorkerComponent>() is { } worker)
        {
            worker.WorkerId = ActorId;
            worker.WorkerCompletedJob += OnJobCompleted;
            worker.WorkerFailedJob += OnJobFailed;
        }
    }

    public override void _ExitTree()
    {
        ResetFollowState();
        if (GetSiblingComponent<HealthComponent>() is { } health) health.Died -= OnDied;
        if (GetSiblingComponent<GridWorkerComponent>() is { } worker)
        {
            worker.WorkerCompletedJob -= OnJobCompleted;
            worker.WorkerFailedJob -= OnJobFailed;
        }
        if (GodotObject.IsInstanceValid(Registry)) Registry!.UnregisterActor(this);
        Registry = null;
        base._ExitTree();
        // Godot does not repeat _Ready when a live body is reparented or reattached.
        RequestReady();
    }

    public static ActorComponent? ForBody(Node? body)
        => FindDirectComponent<ActorComponent>(body);

    public static ActorComponent? OwningActor(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
            if (ForBody(current) is { Registry: not null } actor) return actor;
        return null;
    }

    private IEnumerable<Node> SaveParticipants(Node node)
    {
        if (node != Body && ForBody(node) is not null) yield break;
        if (node is ISaveable) yield return node;
        foreach (Node child in node.GetChildren())
            foreach (Node participant in SaveParticipants(child)) yield return participant;
    }

    public bool HasCapability(ActorCapabilities capability)
        => Definition is not null && (Definition.Capabilities & capability) == capability;

    public void SetIntent(Vector2 movement, Vector2 aim, bool fire, bool jump)
    {
        MoveIntent = movement.IsFinite() ? movement.LimitLength() : Vector2.Zero;
        if (aim.IsFinite() && aim.LengthSquared() > 0.0001f) AimIntent = aim.Normalized();
        FireHeld = fire;
        _jumpRequested |= jump && !JumpHeld;
        JumpHeld = jump;
    }

    public bool ConsumeJump()
    {
        bool value = _jumpRequested;
        _jumpRequested = false;
        return value;
    }

    public void ClearIntent()
    {
        MoveIntent = Vector2.Zero;
        FireHeld = JumpHeld = _jumpRequested = false;
        _dashHeld = _dashRequested = false;
        _heldAbilities.Clear();
    }

    /// <summary>Only one body mover may run, including while a direct-control actor follows an order.</summary>
    public bool CanDrive(Node candidate)
    {
        if (!IsActive || Body is null || IsDead) return false;
        var follower = GetSiblingComponent<GridPathFollowerComponent>();
        if (follower is { IsMoving: true }) return candidate == follower;
        if (candidate == follower) return true;
        if (FollowTargetActorId.Length > 0) return false;
        foreach (Node child in Body.GetChildren())
            if (child is MovementComponent or TopDownController or PlatformerController or ShooterController or AIController or AnimalBehaviorComponent or FlyComponent or FlockingComponent)
                if (child is EntityComponent { IsActive: true }) return candidate == child;
        return false;
    }

    public bool IsDead => GetSiblingComponent<HealthComponent>() is { IsDead: true };

    /// <summary>Publish a custom mover/teleport's current position before spatial queries or callbacks.</summary>
    public void SynchronizePosition()
    {
        if (GodotObject.IsInstanceValid(Registry)) Registry!.UpdatePosition(this);
    }

    public bool CanBecomeDormant()
    {
        if (!IsActive || IsDead || HasOrders || MoveIntent != Vector2.Zero || FireHeld || JumpHeld) return false;
        if (Body is CharacterBody2D body && body.Velocity.LengthSquared() > 0.01f) return false;
        if (_dashHeld || _dashRequested || GetSiblingComponent<DashComponent>() is { IsDashing: true }
            || GetSiblingComponent<KnockbackComponent>() is { IsKnockedBack: true }
            || GetSiblingComponent<FlyComponent>() is { IsBoosting: true }) return false;
        if (GetSiblingComponent<WallJumpComponent>() is { IsWallSliding: true } or { IsWallJumpLocked: true }) return false;
        if (_heldAbilities.Count > 0 || GetSiblingComponent<GlideComponent>() is { IsGliding: true }
            || GetSiblingComponent<HoverComponent>() is { IsHovering: true }) return false;
        if (GetSiblingComponent<SlideComponent>() is { IsSliding: true } or { IsCollisionReduced: true }) return false;
        if (GetSiblingComponent<GridPathFollowerComponent>() is { IsMoving: true }
            || (GetSiblingComponent<GridWorkerComponent>() is { CurrentJobId.Length: > 0 } worker && !worker.CanSuspendActor())
            || GetSiblingComponent<GridHaulerComponent>() is { IsBusy: true }
            || GetSiblingComponent<StatusEffectComponent>() is { ActiveEffects.Count: > 0 }) return false;
        if (GetSiblingComponent<GridHaulerComponent>() is { } hauler && hauler.StoredIds().Count > 0) return false;
        if (GetSiblingComponent<AIController>() is { } ai && ai.Mode != AIController.AIMode.Idle) return false;
        if (GetSiblingComponent<HealthComponent>() is { } health && health.CurrentHealth < health.EffectiveMaxHealth) return false;
        if (Body is not null)
            foreach (Node node in ActorDescendants(Body))
            {
                if (node is ActorComponent nested && nested != this) return false;
                if (node is IActorResidencyGuard guard && !guard.CanSuspendActor()) return false;
                if (node is not IActorResidencyGuard && node.HasMethod("CanSuspendActor") && !node.Call("CanSuspendActor").AsBool()) return false;
            }
        return true;
    }

    private static IEnumerable<Node> ActorDescendants(Node parent)
    {
        foreach (Node node in parent.GetChildren())
        {
            yield return node;
            foreach (Node child in ActorDescendants(node)) yield return child;
        }
    }

    internal bool ReferencesActor(string actorId)
        => (_current is not null && _current["target"].AsString() == actorId)
            || _orders.Any(order => order["target"].AsString() == actorId);

    private void OnDied()
    {
        CancelOrders();
        Registry?.FindPlayer(OwnerId)?.ForgetActor(ActorId);
    }

    internal string Validate(ActorCommand command)
    {
        if (!IsActive || IsDead) return "actor_unavailable";
        if (Body is null) return "missing_body";
        if (command.Action == ActorAction.Stop) return "";
        if (!CommandHandlerPath.IsEmpty)
        {
            var handler = GetNodeOrNull(CommandHandlerPath);
            return handler is not null && ActorCommandPorts.Supports(handler) && ActorCommandPorts.CanExecute(handler, command.Snapshot())
                ? "" : "custom_handler_rejected";
        }
        ActorCapabilities needed = command.Action switch
        {
            ActorAction.Move or ActorAction.Follow => ActorCapabilities.Move,
            ActorAction.Attack => ActorCapabilities.Attack,
            ActorAction.Work => ActorCapabilities.Work,
            _ => ActorCapabilities.Interact
        };
        if (!HasCapability(needed)) return "missing_capability";
        if (command.Action is ActorAction.Move or ActorAction.Follow && GetSiblingComponent<GridPathFollowerComponent>() is null)
            return "missing_path_follower";
        if (command.Action == ActorAction.Follow)
        {
            if (!float.IsFinite(command.FollowDistance) || command.FollowDistance < 1 || command.FollowDistance > 65536)
                return "invalid_follow_distance";
            if (command.TargetActorId == ActorId || Registry?.GetActorOwner(command.TargetActorId) != OwnerId)
                return "invalid_follow_target";
            if (WouldCreateFollowCycle(command.TargetActorId)) return "follow_cycle";
            if (Registry.WakeActor(command.TargetActorId) is not { IsActive: true, IsDead: false, Body: not null })
                return "invalid_follow_target";
        }
        if (command.Action == ActorAction.Work && GetSiblingComponent<GridWorkerComponent>()?.CanAcceptJob(command.JobId) != true)
            return "missing_worker_or_job";
        if (command.Action == ActorAction.Attack)
        {
            if (GetSiblingComponent<AttackComponent>() is null) return "missing_attack";
            var target = Registry?.WakeActor(command.TargetActorId);
            if (target is null || target.IsDead) return "invalid_target";
            if (!Registry!.AreHostile(OwnerId, target.OwnerId)) return "target_not_hostile";
        }
        if (command.Action == ActorAction.Interact)
        {
            var target = Registry?.WakeActor(command.TargetActorId);
            if (target?.Body is null || FindComponent<InteractableComponent>(target.Body) is null)
                return "target_not_interactable";
        }
        return "";
    }

    internal void Enqueue(ActorCommand command)
    {
        CommandRevision++;
        if (!command.Append || command.Action == ActorAction.Stop) CancelOrders();
        if (command.Action != ActorAction.Stop) _orders.Enqueue(command.Snapshot());
    }

    public void CancelOrders()
        => CancelOrdersCore(false);

    private void CancelOrdersCore(bool preserveWorldWork)
    {
        CommandRevision++;
        ResetFollowState();
        GetSiblingComponent<DashComponent>()?.CancelDash();
        GetSiblingComponent<WallJumpComponent>()?.CancelWallMotion();
        GetSiblingComponent<GlideComponent>()?.CancelGlide();
        GetSiblingComponent<HoverComponent>()?.CancelHover();
        GetSiblingComponent<SlideComponent>()?.CancelSlide();
        if (_started && !CommandHandlerPath.IsEmpty && GetNodeOrNull(CommandHandlerPath) is { } handler && ActorCommandPorts.Supports(handler))
            ActorCommandPorts.Cancel(handler);
        _current = null;
        _orders.Clear();
        _started = false;
        if (GetSiblingComponent<GridHaulerComponent>() is { } hauler && (hauler.IsBusy || hauler.StoredIds().Count > 0))
            hauler.CancelHaul("command_replaced");
        GetSiblingComponent<GridPathFollowerComponent>()?.CancelMove();
        if (GetSiblingComponent<GridWorkerComponent>() is { } worker && worker.CurrentJobId.Length > 0
            && (!preserveWorldWork || !worker.HasWorldExecution))
            worker.CancelCurrentJob("command_replaced");
        ClearIntent();
        if (Body is CharacterBody2D body) body.Velocity = Vector2.Zero;
        if (GetSiblingComponent<MovementComponent>() is { } movement)
            movement.Velocity = movement.DesiredDirection = Vector2.Zero;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Registry is assigned only by runtime _Ready and cleared on tree exit.
        if (Registry is null || !IsActive || Body is null) return;
        Registry.UpdatePosition(this);
        if (IsDead) { if (HasOrders) CancelOrders(); return; }
        if (_current is null && _orders.TryDequeue(out var order))
        {
            _current = order;
            ResetFollowState();
            _started = false;
            _repathDelay = 0;
        }
        if (_current is null) return;
        var action = (ActorAction)_current["action"].AsInt32();
        if (!CommandHandlerPath.IsEmpty)
        {
            var handler = GetNodeOrNull(CommandHandlerPath);
            if (handler is null || !ActorCommandPorts.Supports(handler)) { Finish(false, "handler_missing"); return; }
            if (!_started)
            {
                _started = ActorCommandPorts.Start(handler, _current.Duplicate(true));
                if (!_started) Finish(false, "handler_rejected");
            }
            else if (ActorCommandPorts.Complete(handler)) Finish(true, "");
            return;
        }
        var follower = GetSiblingComponent<GridPathFollowerComponent>();
        if (action == ActorAction.Follow)
        {
            AdvanceFollow(delta, follower);
            return;
        }
        if (action == ActorAction.Interact)
        {
            var target = Registry.FindActor(_current["target"].AsString());
            var interaction = target?.Body is { } targetBody ? FindComponent<InteractableComponent>(targetBody) : null;
            if (interaction is null) { Finish(false, "target_gone"); return; }
            if (interaction.TryInteract(Body)) { follower?.CancelMove(); Finish(true, ""); return; }
            _repathDelay -= delta;
            if (_repathDelay <= 0)
            {
                _repathDelay = 0.5;
                if (follower?.MoveToWorld(target!.Body!.GlobalPosition) != true) Finish(false, "interaction_unreachable");
            }
            return;
        }
        if (action == ActorAction.Work)
        {
            var worker = GetSiblingComponent<GridWorkerComponent>();
            if (!_started)
            {
                _started = worker?.AssignJob(_current["job"].AsString()) == true;
                if (!_started && _current is not null) Finish(false, "job_rejected");
            }
            else if (worker is null || worker.State == GridWorkerComponent.WorkerState.Idle)
                Finish(false, "job_interrupted");
            return;
        }
        if (action == ActorAction.Attack)
        {
            var target = Registry.FindActor(_current["target"].AsString());
            if (target?.Body is null || target.IsDead) { Finish(true, "target_gone"); return; }
            if (!Registry.AreHostile(OwnerId, target.OwnerId)) { Finish(false, "target_not_hostile"); return; }
            var attack = GetSiblingComponent<AttackComponent>()!;
            if (Body.GlobalPosition.DistanceTo(target.Body.GlobalPosition) <= attack.EffectiveRange)
            {
                follower?.CancelMove();
                attack.Attack(target.Body.GlobalPosition);
                return;
            }
            _repathDelay -= delta;
            if (_repathDelay <= 0)
            {
                _repathDelay = 0.5;
                if (follower?.MoveToWorld(target.Body.GlobalPosition) != true) Finish(false, "no_path");
            }
            return;
        }
        if (!_started)
        {
            _started = follower?.MoveToCell(new(_current["x"].AsInt32(), _current["y"].AsInt32())) == true;
            if (!_started) Finish(false, "no_path");
        }
        else if (follower is { IsMoving: false })
            Finish(follower.HasReachedDestination, follower.HasReachedDestination ? "" : "route_interrupted");
    }

    private void Finish(bool success, string reason)
    {
        int action = _current?["action"].AsInt32() ?? 0;
        ResetFollowState();
        _current = null;
        _started = false;
        EmitSignal(SignalName.CommandFinished, action, success, reason);
    }

    internal bool IsCurrentJob(string jobId) => _current is not null
        && (ActorAction)_current["action"].AsInt32() == ActorAction.Work
        && _current["job"].AsString() == jobId;

    private void OnJobCompleted(string workerId, string jobId)
    {
        if (IsCurrentJob(jobId)) Finish(true, "");
    }

    private void OnJobFailed(string workerId, string jobId, string reason)
    {
        if (IsCurrentJob(jobId)) Finish(false, reason);
    }

    internal void ChangeOwner(string owner)
    {
        string previous = OwnerId;
        CancelOrders();
        OwnerId = owner;
        EmitSignal(SignalName.OwnershipChanged, previous, owner);
    }

    public Godot.Collections.Dictionary CaptureActor()
    {
        var components = new Godot.Collections.Dictionary();
        if (Body is not null)
            foreach (Node node in SaveParticipants(Body))
                if (node is ISaveable saveable)
                {
                    using var state = new GameBuilder.GameStateData();
                    saveable.Save(state);
                    components[Body.GetPathTo(node).ToString()] = state.ToJson();
                }
        var orders = new Godot.Collections.Array();
        foreach (var order in _orders) orders.Add(order.Duplicate(true));
        return new()
        {
            ["id"] = ActorId, ["owner"] = OwnerId, ["definition"] = Definition?.Id ?? "",
            ["x"] = Body?.GlobalPosition.X ?? 0, ["y"] = Body?.GlobalPosition.Y ?? 0,
            ["velocity_x"] = Body is CharacterBody2D movingX ? movingX.Velocity.X : 0,
            ["velocity_y"] = Body is CharacterBody2D movingY ? movingY.Velocity.Y : 0,
            ["on_floor"] = Body is CharacterBody2D contactBody && CharacterMotion.IsOnFloor(contactBody),
            ["rotation"] = Body?.Rotation ?? 0, ["scale_x"] = Body?.Scale.X ?? 1, ["scale_y"] = Body?.Scale.Y ?? 1,
            ["components"] = components, ["orders"] = orders,
            ["current"] = _current?.Duplicate(true) ?? new Godot.Collections.Dictionary(), ["started"] = _started
        };
    }

    public void RestoreActor(Godot.Collections.Dictionary snapshot)
    {
        var velocity = new Vector2(MovementAbilityState.Number(snapshot, "velocity_x"), MovementAbilityState.Number(snapshot, "velocity_y"));
        bool onFloor = MovementAbilityState.Flag(snapshot, "on_floor");
        CancelOrdersCore(true);
        OwnerId = snapshot["owner"].AsString();
        if (Body is null) return;
        Body.GlobalPosition = new(snapshot["x"].AsSingle(), snapshot["y"].AsSingle());
        Body.Rotation = snapshot["rotation"].AsSingle();
        Body.Scale = new(snapshot["scale_x"].AsSingle(), snapshot["scale_y"].AsSingle());
        var components = snapshot["components"].AsGodotDictionary();
        // Progression precedes derived RPG pools; routes precede workers, regardless of child order.
        foreach (Node node in SaveParticipants(Body).OrderBy(n => n is LevelingComponent ? -1 : n is GridPathFollowerComponent ? 0
            : n is GridWorkerComponent or GridHaulerComponent ? 2 : 1))
            if (node is ISaveable saveable && components.TryGetValue(Body.GetPathTo(node).ToString(), out var saved))
            {
                using var state = new GameBuilder.GameStateData();
                if (!state.FromJsonString(saved.AsString())) throw new InvalidOperationException("Invalid actor component state.");
                saveable.Load(state);
            }
        if (Body is CharacterBody2D moving)
        {
            moving.Velocity = velocity;
            RestoredFloorContact = onFloor;
        }
        foreach (var item in snapshot["orders"].AsGodotArray()) _orders.Enqueue(item.AsGodotDictionary().Duplicate(true));
        var current = snapshot["current"].AsGodotDictionary();
        _current = current.Count > 0 ? current.Duplicate(true) : null;
        _started = _current is not null && snapshot["started"].AsBool();
        Registry?.UpdatePosition(this);
    }
}
