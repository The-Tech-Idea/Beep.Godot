using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

public enum PlayerControlMode { Direct, Orders, Colony, Interface }

/// <summary>A command issuer, with optional possession. RTS and colony players have no avatar.</summary>
[Tool, GlobalClass]
public partial class PlayerContextComponent : Node
{
    [Export] public string PlayerId { get; set; } = "player_1";
    [Export] public string FactionId { get; set; } = "faction_1";
    [Export] public PlayerControlMode ControlMode { get; set; } = PlayerControlMode.Orders;
    [Export] public bool ReadLocalInput { get; set; } = true;
    [Export] public NodePath RegistryPath { get; set; } = new("");
    [Export] public string InitialActorId { get; set; } = "";
    [Export] public string NextActorAction { get; set; } = "";
    [Export] public string PreviousActorAction { get; set; } = "";
    /// <summary>-1 cycles the owned roster; 0..9 uses an existing saved control group.</summary>
    [Export(PropertyHint.Range, "-1,9,1")] public int PossessionGroup { get; set; } = -1;
    [Signal] public delegate void SelectionChangedEventHandler();
    [Signal] public delegate void PossessionChangedEventHandler(string actorId);
    public string PossessedActorId { get; private set; } = "";
    public ActorRegistryComponent? Registry { get; private set; }
    private readonly HashSet<string> _selection = new();
    private readonly Dictionary<int, Godot.Collections.Array<string>> _groups = new();
    private readonly HashSet<string> _held = new();
    private bool _initialPossessionScheduled;
    private readonly HashSet<ActorOrdersControllerComponent> _orderControllers = new();
    internal void RegisterOrders(ActorOrdersControllerComponent controller) => _orderControllers.Add(controller);
    internal void UnregisterOrders(ActorOrdersControllerComponent controller) => _orderControllers.Remove(controller);
    internal bool HasPlannedOrder(string actorId)
    {
        foreach (var controller in _orderControllers)
            if (GodotObject.IsInstanceValid(controller) && controller.IsInsideTree() && controller.ReferencesActor(actorId)) return true;
        return false;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        Registry = GetNodeOrNull<ActorRegistryComponent>(RegistryPath);
        if (Registry?.RegisterPlayer(this) != true)
        {
            Registry = null;
            GD.PushError($"[{Name}] Player needs a unique PlayerId and explicit ActorRegistry path.");
            return;
        }
        ProcessPhysicsPriority = -30;
        if (PossessedActorId.Length > 0 && FollowPartyOnPossession)
            Callable.From(() => { if (IsInsideTree()) ApplyPartyFollowing(); }).CallDeferred();
        if (!_initialPossessionScheduled && InitialActorId.Length > 0)
        {
            _initialPossessionScheduled = true;
            Callable.From(() => { if (IsInsideTree()) Possess(InitialActorId); }).CallDeferred();
        }
    }

    public override void _ExitTree()
    {
        StopPartyFollowing();
        if (GodotObject.IsInstanceValid(Registry))
        {
            Registry!.FindActor(PossessedActorId)?.ClearIntent();
            Registry.UnregisterPlayer(this);
        }
        Registry = null;
        _held.Clear();
        RequestReady();
    }

    public bool Possess(string actorId)
    {
        if (ControlMode != PlayerControlMode.Direct || Registry is null) return false;
        if (Registry.GetActorOwner(actorId) != PlayerId) return false;
        var actor = Registry.WakeActor(actorId);
        if (actor is null || actor.OwnerId != PlayerId || actor.IsDead || !actor.IsActive) return false;
        Registry.FindActor(PossessedActorId)?.ClearIntent();
        actor.CancelOrders();
        _held.Clear();
        PossessedActorId = actorId;
        if (FollowPartyOnPossession) ApplyPartyFollowing();
        EmitSignal(SignalName.PossessionChanged, actorId);
        return true;
    }

    public bool SelectActor(string actorId, bool additive = false)
    {
        if (Registry is null || Registry.GetActorOwner(actorId) != PlayerId
            || Registry.WakeActor(actorId) is not { IsDead: false, IsActive: true }) return false;
        if (!additive) _selection.Clear();
        _selection.Add(actorId);
        EmitSignal(SignalName.SelectionChanged);
        return true;
    }

    /// <summary>Switch direct control without rebuilding selection or waking the entire party.</summary>
    public bool CyclePossession(int direction = 1, int group = -1)
    {
        if (ControlMode != PlayerControlMode.Direct || Registry is null || direction == 0 || group < -1 || group > 9) return false;
        var members = group == -1 ? GetOwnedActors() : GetGroupActors(group);
        if (members.Count == 0) return false;
        int current = members.IndexOf(PossessedActorId);
        int step = direction > 0 ? 1 : -1;
        int index = current < 0 ? (step > 0 ? members.Count - 1 : 0) : current;
        for (int attempt = 0; attempt < members.Count; attempt++)
        {
            index = (index + step + members.Count) % members.Count;
            string id = members[index];
            if (id == PossessedActorId || Registry.GetActorOwner(id) != PlayerId) continue;
            if (Possess(id)) return true;
        }
        return false;
    }

    public Godot.Collections.Array<string> GetGroupActors(int number)
        => _groups.TryGetValue(number, out var members)
            ? new(members.Where(id => Registry?.GetActorOwner(id) == PlayerId).Distinct().Order(System.StringComparer.Ordinal))
            : new();

    public int SelectRectangle(Rect2 rectangle, bool additive = false)
    {
        if (Registry is null) return 0;
        if (!additive) _selection.Clear();
        foreach (string id in Registry.QueryActors(rectangle, PlayerId, true))
            if (Registry.WakeActor(id) is not null && CanControl(id)) _selection.Add(id);
        EmitSignal(SignalName.SelectionChanged);
        return _selection.Count;
    }

    public void ClearSelection() { _selection.Clear(); EmitSignal(SignalName.SelectionChanged); }
    public Godot.Collections.Array<string> GetSelectedActors() => new(_selection);
    public Godot.Collections.Array<string> GetOwnedActors() => Registry?.GetOwnedActors(PlayerId) ?? new();
    public void StoreGroup(int number) { if (number >= 0 && number <= 9) _groups[number] = GetSelectedActors(); }
    public void RecallGroup(int number)
    {
        if (!_groups.TryGetValue(number, out var members)) return;
        _selection.Clear();
        foreach (string id in members)
            if (Registry?.GetActorOwner(id) == PlayerId && Registry.WakeActor(id) is not null && CanControl(id)) _selection.Add(id);
        EmitSignal(SignalName.SelectionChanged);
    }

    public int IssueOrder(ActorAction action, Vector2I targetCell, string targetActorId = "", bool append = false, string jobId = "")
    {
        using var command = new ActorCommand { IssuerId = PlayerId, Action = action, TargetCell = targetCell,
            TargetActorId = targetActorId, Append = append, JobId = jobId, Recipients = GetSelectedActors() };
        return Registry?.Submit(command) ?? 0;
    }

    private bool CanControl(string actorId) => Registry?.FindActor(actorId) is { IsDead: false, IsActive: true } actor
        && actor.OwnerId == PlayerId;

    internal void ForgetActor(string id)
    {
        _partyFollowing.Remove(id);
        _partyHeld.Remove(id);
        if (_selection.Remove(id)) EmitSignal(SignalName.SelectionChanged);
        if (PossessedActorId != id) return;
        Registry?.FindActor(id)?.ClearIntent();
        PossessedActorId = "";
        StopPartyFollowing();
        _held.Clear();
        EmitSignal(SignalName.PossessionChanged, "");
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!ReadLocalInput || ControlMode != PlayerControlMode.Direct || GetViewport().GuiGetFocusOwner() is not null) return;
        if (!input.IsEcho())
        {
            int cycle = NextActorAction.Length > 0 && InputMap.HasAction(NextActorAction) && input.IsActionPressed(NextActorAction) ? 1
                : PreviousActorAction.Length > 0 && InputMap.HasAction(PreviousActorAction) && input.IsActionPressed(PreviousActorAction) ? -1 : 0;
            if (cycle != 0)
            {
                CyclePossession(cycle, PossessionGroup);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if (PossessedActorId.Length == 0) return;
        var actions = new[] { "move_left", "move_right", "move_up", "move_down", "attack", "jump", "dash" }
            .Concat(Registry?.FindActor(PossessedActorId)?.HeldAbilityActions() ?? System.Array.Empty<string>()).Distinct();
        foreach (string action in actions)
        {
            if (!InputMap.HasAction(action) || !input.IsAction(action)) continue;
            if (input.IsActionPressed(action)) _held.Add(action); else if (input.IsActionReleased(action)) _held.Remove(action);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Engine.IsEditorHint() && ControlMode != PlayerControlMode.Direct && _partyFollowing.Count > 0)
            StopPartyFollowing();
        if (Engine.IsEditorHint() || Registry?.FindActor(PossessedActorId) is not { Body: { } body } actor) return;
        if (!ReadLocalInput || ControlMode != PlayerControlMode.Direct || actor.IsDead || GetViewport().GuiGetFocusOwner() is not null)
        {
            _held.Clear(); actor.ClearIntent(); return;
        }
        // Releases may be consumed by a UI control; never leave a movement key latched.
        _held.RemoveWhere(action => !InputMap.HasAction(action) || !Input.IsActionPressed(action));
        Vector2 move = new((_held.Contains("move_right") ? 1 : 0) - (_held.Contains("move_left") ? 1 : 0),
            (_held.Contains("move_down") ? 1 : 0) - (_held.Contains("move_up") ? 1 : 0));
        actor.SetIntent(move, body.GetGlobalMousePosition() - body.GlobalPosition, _held.Contains("attack"), _held.Contains("jump"));
        actor.SetDashIntent(_held.Contains("dash"));
        actor.ReplaceHeldAbilities(_held);
    }

    internal Godot.Collections.Dictionary CaptureState()
    {
        var groups = new Godot.Collections.Dictionary();
        foreach (var (key, value) in _groups) groups[key.ToString()] = value;
        var formations = new Godot.Collections.Dictionary();
        foreach (var controller in _orderControllers)
            if (GodotObject.IsInstanceValid(controller) && controller.IsInsideTree())
                formations[GetPathTo(controller).ToString()] = controller.CaptureFormations();
        return new() { ["faction"] = FactionId, ["possessed"] = PossessedActorId, ["selection"] = GetSelectedActors(), ["groups"] = groups, ["formations"] = formations, ["party_follow"] = CapturePartyFollowing() };
    }

    internal void RestoreState(Godot.Collections.Dictionary state)
    {
        _held.Clear(); _selection.Clear(); _groups.Clear();
        Registry?.FindActor(PossessedActorId)?.ClearIntent();
        PossessedActorId = "";
        FactionId = state["faction"].AsString();
        foreach (var id in state["selection"].AsGodotArray())
            if (CanControl(id.AsString())) _selection.Add(id.AsString());
        foreach (var (key, value) in state["groups"].AsGodotDictionary())
            if (int.TryParse(key.AsString(), out int number)) _groups[number] = new(value.AsGodotArray());
        string possessed = state["possessed"].AsString();
        if (ControlMode == PlayerControlMode.Direct && CanControl(possessed))
            PossessedActorId = possessed;
        var formations = state["formations"].AsGodotDictionary();
        foreach (var controller in _orderControllers)
        {
            if (!GodotObject.IsInstanceValid(controller) || !controller.IsInsideTree()) continue;
            if (formations.TryGetValue(GetPathTo(controller).ToString(), out var saved))
            {
                if (saved.VariantType != Variant.Type.Dictionary || !controller.RestoreFormations(saved.AsGodotDictionary()))
                    throw new System.FormatException("Invalid saved group movement plan.");
            }
            else controller.CancelFormation();
        }
        RestorePartyFollowing(state.GetValueOrDefault("party_follow").AsGodotDictionary());
        EmitSignal(SignalName.SelectionChanged);
        EmitSignal(SignalName.PossessionChanged, PossessedActorId);
    }
}
