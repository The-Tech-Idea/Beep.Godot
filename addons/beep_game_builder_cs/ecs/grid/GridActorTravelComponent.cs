using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>World-clock travel for dormant identities. Uses shared navigation; never instantiates actor scenes.</summary>
[GlobalClass]
public partial class GridActorTravelComponent : Node, ISaveable
{
    [Export] public NodePath ActorRegistryPath { get; set; } = new("");
    [Export] public NodePath NavigationPath { get; set; } = new("");
    [Export] public NodePath GridPath { get; set; } = new("");
    [Export] public NodePath WorkClockPath { get; set; } = new("");
    [Export] public string SaveKey { get; set; } = "actor_travel";
    [Signal] public delegate void TravelFinishedEventHandler(string actorId, bool arrived, string reason);
    public int TravellerCount => _routes.Count;
    public bool IsTravelling(string actorId) => _routes.ContainsKey(actorId);
    internal bool MatchesRoute(string actorId, Vector2I goal, float speed) =>
        _routes.TryGetValue(actorId, out var route) && route.Goal == goal && route.Speed == speed;
    internal bool MatchesSources(ActorRegistryComponent registry, GridProjectionComponent grid) => _registry == registry && _grid == grid;
    private sealed class Route(string actor, string owner, Vector2I goal, float speed)
    {
        public readonly string Actor = actor, Owner = owner;
        public readonly Vector2I Goal = goal;
        public readonly float Speed = speed;
        public long Request;
        public Vector2I[] Cells = [];
        public int Index = 1;
    }
    private readonly Dictionary<string, Route> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<long, string> _requests = new();
    private ActorRegistryComponent? _registry;
    private GridNavigationComponent? _navigation;
    private GridProjectionComponent? _grid;
    private GridWorkClockComponent? _clock;
    private GridCellDataComponent? _pinCells;

    public override void _Ready()
    {
        _registry = GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath);
        _navigation = GetNodeOrNull<GridNavigationComponent>(NavigationPath);
        _grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
        _clock = GridWorkClockComponent.FindFor(this, WorkClockPath);
        if (_registry is not null) { _registry.ActorWoke += OnActorUnavailable; _registry.ActorDestroyed += OnActorUnavailable; }
        if (_navigation is not null) _navigation.PathRequestCompleted += OnPath;
        if (_clock is not null) _clock.WorkTick += AdvanceTravel;
        AddToGroup(SaveableHelper.Group);
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        foreach (var id in _routes.Keys.ToArray()) RemoveRoute(id);
        if (GodotObject.IsInstanceValid(_registry)) { _registry!.ActorWoke -= OnActorUnavailable; _registry.ActorDestroyed -= OnActorUnavailable; }
        if (GodotObject.IsInstanceValid(_navigation)) _navigation!.PathRequestCompleted -= OnPath;
        if (GodotObject.IsInstanceValid(_clock)) _clock!.WorkTick -= AdvanceTravel;
        RemoveFromGroup(SaveableHelper.Group);
        RequestReady();
    }

    private bool ReadyToTravel => GodotObject.IsInstanceValid(this) && IsInsideTree() && GodotObject.IsInstanceValid(_registry) && _registry!.IsInsideTree()
        && GodotObject.IsInstanceValid(_navigation) && _navigation!.IsInsideTree()
        && GodotObject.IsInstanceValid(_grid) && _grid!.IsInsideTree()
        && GodotObject.IsInstanceValid(_clock) && _clock!.IsInsideTree();

    public bool BeginTravel(string actorId, Vector2I goal, float worldUnitsPerTurn)
    {
        if (!ReadyToTravel || _routes.ContainsKey(actorId) || !float.IsFinite(worldUnitsPerTurn) || worldUnitsPerTurn <= 0
            || !_registry!.IsDormant(actorId) || !_navigation!.IsInBounds(goal)) return false;
        var position = _registry.GetActorPosition(actorId);
        if (!position.IsFinite() || !_registry.ClaimDormantMotion(actorId, this)) return false;
        var route = new Route(actorId, _registry.GetActorOwner(actorId), goal, worldUnitsPerTurn);
        route.Request = _navigation.RequestCellPath(_grid!.WorldToCell(position), goal);
        if (route.Request == 0) { _registry.ReleaseDormantMotion(actorId, this); return false; }
        _routes.Add(actorId, route);
        _requests.Add(route.Request, actorId);
        return true;
    }

    public bool CancelTravel(string actorId)
    {
        if (!_routes.ContainsKey(actorId)) return false;
        Finish(actorId, false, "cancelled");
        return true;
    }

    private void OnActorUnavailable(string id)
    {
        if (_routes.ContainsKey(id)) Finish(id, false, "actor_unavailable");
    }

    private void OnPath(long requestId, Godot.Collections.Array<Vector2I> path, string reason)
    {
        if (!_requests.Remove(requestId, out var id) || !_routes.TryGetValue(id, out var route)) return;
        route.Request = 0;
        if (reason.Length > 0 || path.Count == 0) { Finish(id, false, reason.Length > 0 ? reason : "empty_path"); return; }
        route.Cells = path.ToArray();
        route.Index = path.Count == 1 ? 0 : 1;
        RefreshPins();
    }

    public void AdvanceTravel(float turns)
    {
        if (!float.IsFinite(turns) || turns <= 0) return;
        foreach (var route in _routes.Values.ToArray())
        {
            if (!GodotObject.IsInstanceValid(this)) return;
            if (!_routes.TryGetValue(route.Actor, out var current) || current != route) continue;
            if (!ReadyToTravel || !_registry!.IsDormant(route.Actor) || _registry.GetActorOwner(route.Actor) != route.Owner)
            { Finish(route.Actor, false, "actor_unavailable"); continue; }
            if (route.Request != 0)
            {
                if (!_navigation!.HasPathRequest(route.Request)) Finish(route.Actor, false, "path_request_cancelled");
                continue;
            }
            double distance = (double)turns * route.Speed;
            while (distance > 0 && route.Index < route.Cells.Length)
            {
                var to = route.Cells[route.Index];
                var from = route.Cells[Math.Max(0, route.Index - 1)];
                if ((from == to && _navigation!.IsBlocked(to)) || (from != to && !_navigation!.CanTraverse(from, to)))
                { Finish(route.Actor, false, "route_blocked"); break; }
                var target = _grid!.CellToWorld(to);
                var position = _registry.GetActorPosition(route.Actor);
                if (!target.IsFinite() || !position.IsFinite()) { Finish(route.Actor, false, "invalid_projection"); break; }
                float remaining = position.DistanceTo(target);
                float step = (float)Math.Min(distance, remaining);
                if (!_registry.MoveDormantActor(route.Actor, position.MoveToward(target, step), this))
                { Finish(route.Actor, false, "motion_claim_lost"); break; }
                distance -= step;
                if (step < remaining) break;
                route.Index++;
                if (route.Index == route.Cells.Length) Finish(route.Actor, true, "");
            }
        }
        if (GodotObject.IsInstanceValid(this)) RefreshPins();
    }

    private void Finish(string id, bool arrived, string reason)
    {
        if (!_routes.ContainsKey(id)) return;
        RemoveRoute(id);
        if (GodotObject.IsInstanceValid(this) && IsInsideTree()) EmitSignal(SignalName.TravelFinished, id, arrived, reason);
    }

    private void RemoveRoute(string id)
    {
        if (!_routes.Remove(id, out var route)) return;
        if (route.Request != 0 && GodotObject.IsInstanceValid(_navigation)) _navigation!.CancelPathRequest(route.Request);
        _requests.Remove(route.Request);
        if (GodotObject.IsInstanceValid(_registry)) _registry!.ReleaseDormantMotion(id, this);
        RefreshPins();
    }

    private void RefreshPins()
    {
        var cells = GodotObject.IsInstanceValid(_navigation) ? _navigation!.RouteCellData : null;
        if (_pinCells != cells && GodotObject.IsInstanceValid(_pinCells)) _pinCells!.ReleaseChunkPins(this);
        _pinCells = cells;
        if (!GodotObject.IsInstanceValid(cells)) return;
        var chunks = new HashSet<Vector2I>();
        foreach (var route in _routes.Values)
            for (int i = Math.Max(0, route.Index - 1); i < route.Cells.Length; i++)
            {
                var cell = route.Cells[i];
                var previous = route.Cells[Math.Max(0, i - 1)];
                chunks.Add(new(cell.X >> 5, cell.Y >> 5));
                chunks.Add(new(previous.X >> 5, cell.Y >> 5));
                chunks.Add(new(cell.X >> 5, previous.Y >> 5));
            }
        cells!.ReplaceChunkPins(this, chunks);
    }

    public Godot.Collections.Dictionary CaptureState()
    {
        var state = new Godot.Collections.Dictionary();
        foreach (var (id, route) in _routes)
            state[id] = new Godot.Collections.Dictionary { ["owner"] = route.Owner, ["goal"] = route.Goal, ["speed"] = route.Speed };
        return state;
    }

    public bool RestoreState(Godot.Collections.Dictionary state)
    {
        if (!ReadyToTravel || state.Count > Math.Clamp(_navigation!.MaximumPendingRequests, 1, 4096)
            - _navigation.PendingPathRequestCount + _requests.Count) return false;
        var restored = new List<Route>();
        foreach (var pair in state)
        {
            if (pair.Key.VariantType != Variant.Type.String || !GridVariantReader.TryDictionary(pair.Value, out var saved)) return false;
            string id = pair.Key.AsString();
            var goal = GridVariantReader.Vector2I(saved, "goal", new(int.MinValue, int.MinValue));
            float speed = GridVariantReader.Float(saved, "speed", 0);
            string owner = GridVariantReader.String(saved, "owner", "");
            if (!_registry!.IsDormant(id) || _registry.GetActorOwner(id) != owner || !_navigation.IsInBounds(goal)
                || goal.X == int.MinValue || goal.Y == int.MinValue
                || !_registry.GetActorPosition(id).IsFinite() || !_registry.CanClaimDormantMotion(id, this)
                || !float.IsFinite(speed) || speed <= 0) return false;
            restored.Add(new(id, owner, goal, speed));
        }
        foreach (var id in _routes.Keys.ToArray()) RemoveRoute(id);
        foreach (var route in restored)
            if (!BeginTravel(route.Actor, route.Goal, route.Speed)) return false;
        return true;
    }

    public void Save(GameBuilder.GameStateData state) { if (SaveKey.Length > 0) state.GameData[SaveKey] = CaptureState(); }
    public void Load(GameBuilder.GameStateData state)
    {
        if (state.GameData.TryGetValue(SaveKey, out var saved) && GridVariantReader.TryDictionary(saved, out var data)) RestoreState(data);
    }
}
