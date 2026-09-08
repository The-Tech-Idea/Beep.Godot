using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Beep.ECS;

public partial class GridNavigationComponent
{
    [Signal] public delegate void PathRequestCompletedEventHandler(long requestId, Godot.Collections.Array<Vector2I> path, string reason);
    [ExportGroup("Path Scheduling")]
    [Export(PropertyHint.Range, "16,16384,16")] public int PathExpansionsPerFrame { get; set; } = 1024;
    [Export(PropertyHint.Range, "0,16,0.1")] public double PathMillisecondsPerFrame { get; set; } = 2;
    [Export(PropertyHint.Range, "1,32,1")] public int MaximumActiveSearches { get; set; } = 8;
    [Export(PropertyHint.Range, "1,4096,1")] public int MaximumPendingRequests { get; set; } = 1024;
    public int PendingPathRequestCount => _requests.Count;
    public int ActivePathSearchCount => _activeRequests.Count;
    public int PathExpansionsLastFrame { get; private set; }
    public double PathMillisecondsLastFrame { get; private set; }

    private readonly Dictionary<long, PathRequest> _requests = new();
    private readonly LinkedList<PathRequest> _waitingRequests = new(), _activeRequests = new();
    private long _nextRequestId;
    private ulong _navigationRevision;
    private bool _processingRequests;
    private GridRoadComponent? _requestRoads;

    private readonly record struct Configuration(bool UseBounds, Vector2I Origin, Vector2I Size,
        DiagonalPolicy Diagonals, bool Start, bool Goal, int Limit, ulong OccupancyRevision,
        ulong CellRevision, string DefaultTerrain, bool StreamTerrain);

    private Configuration RequestConfiguration() => new(UseBounds, BoundsOrigin, BoundsSize, Diagonals,
        AllowBlockedStart, AllowBlockedGoal, MaxVisitedCells, _placement?.OccupancyRevision ?? 0,
        (LoadMissingTerrain ? _cellData?.PinnedNavigationRevision : _cellData?.NavigationRevision) ?? 0,
        GridTerrainRules.Normalize(_cellData?.DefaultTerrainKind ?? ""), LoadMissingTerrain);

    private sealed class PathRequest
    {
        public long Id;
        public Vector2I Start, Goal;
            public PathSearch? Search;
            public TerrainDemand? Demand;
        public Configuration Configuration;
        public ulong Revision;
        public LinkedListNode<PathRequest>? Node;
    }

    /// <summary>Queues a main-thread, time-sliced search. Results use PathRequestCompleted.
    /// Returns zero if rejected. Cancelled requests never emit completion.</summary>
    public long RequestCellPath(Vector2I start, Vector2I goal)
    {
        if (!IsInsideTree() || Engine.IsEditorHint()
            || _requests.Count >= Mathf.Clamp(MaximumPendingRequests, 1, 4096)) return 0;
        var request = new PathRequest { Id = ++_nextRequestId, Start = start, Goal = goal };
        request.Node = _waitingRequests.AddLast(request);
        _requests.Add(request.Id, request);
        SetProcess(true);
        return request.Id;
    }

    public bool CancelPathRequest(long id)
    {
        if (!_requests.Remove(id, out var request)) return false;
        request.Node?.List?.Remove(request.Node);
            request.Search = null;
            request.Demand?.Dispose();
            request.Demand = null;
        if (_requests.Count == 0) SetProcess(false);
        return true;
    }

    public bool HasPathRequest(long id) => _requests.ContainsKey(id);

        public void ClearPathRequests()
        {
            foreach (var request in _requests.Values) request.Demand?.Dispose();
            _terrainWaiting.Clear();
        _requests.Clear(); _waitingRequests.Clear(); _activeRequests.Clear();
        SetProcess(false);
    }

    public override void _Process(double delta) => ProcessPathRequests();

    public void ProcessPathRequests()
    {
        if (_processingRequests || Engine.IsEditorHint() || !IsInsideTree()) return;
        _processingRequests = true;
        PathExpansionsLastFrame = 0;
        long started = Stopwatch.GetTimestamp();
        double milliseconds = double.IsFinite(PathMillisecondsPerFrame) ? Math.Clamp(PathMillisecondsPerFrame, 0, 16) : 2;
        try
        {
            int budget = Mathf.Clamp(PathExpansionsPerFrame, 16, 16384), completions = 0;
            ResolveReferences();
            TrackRequestSources();
            PollTerrainRequests();
            Search currentRules = BuildSearch();
            Configuration configuration = RequestConfiguration();
            while (_requests.Count > 0 && budget > 0 && completions < 64 && IsInsideTree())
            {
                if ((completions > 0 || PathExpansionsLastFrame > 0) && milliseconds > 0
                    && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= milliseconds) break;
                while (_waitingRequests.First is { } waiting
                    && _activeRequests.Count < Mathf.Clamp(MaximumActiveSearches, 1, 32))
                {
                    _waitingRequests.RemoveFirst();
                    PathRequest next = waiting.Value;
                    var rules = BuildSearch();
                    if (next.Demand is { } old && (!LoadMissingTerrain || old.Cells != rules.Cells))
                    {
                        old.Dispose(); next.Demand = null;
                    }
                    if (LoadMissingTerrain && rules.Cells is { } cells)
                    {
                        next.Demand ??= new TerrainDemand(this, cells);
                        rules.Availability = next.Demand.Observe;
                    }
                    next.Search = new PathSearch(this, rules, next.Start, next.Goal);
                    next.Configuration = configuration;
                    next.Revision = _navigationRevision;
                    next.Node = _activeRequests.AddLast(next);
                }
                if (_activeRequests.First is not { } node) break;
                PathRequest request = node.Value;
                PathSearch search = request.Search!;
                string changed = request.Revision != _navigationRevision || request.Configuration != configuration
                    || !SameRules(search.Rules, currentRules) ? "navigation_changed" : "";
                if (request.Demand is { } demand)
                {
                    if (demand.LimitExceeded) changed = "terrain_demand_limit";
                    else if (demand.Expired(TerrainTimeout)) changed = "terrain_request_timeout";
                    else if (changed == "navigation_changed")
                    {
                        RestartTerrainRequest(request, false);
                        completions++;
                        continue;
                    }
                }
                if (changed.Length == 0)
                {
                    int used = search.Step(Math.Min(budget, 64));
                    budget -= used;
                    PathExpansionsLastFrame += used;
                }
                if (changed.Length > 0 || search.Complete)
                {
                    if (changed.Length == 0 && search.Path.Count == 0 && request.Demand is { } missing
                        && !missing.LimitExceeded && missing.HasMissing)
                    {
                        RestartTerrainRequest(request, true);
                        completions++;
                        continue;
                    }
                    if (request.Demand?.LimitExceeded == true) changed = "terrain_demand_limit";
                    var completedDemand = request.Demand;
                    request.Demand = null;
                    CancelPathRequest(request.Id);
                    completions++;
                    try
                    {
                        // A follower acquires its route pins during completion. Keep search pins until that handoff finishes.
                        EmitSignal(SignalName.PathRequestCompleted, request.Id,
                            changed.Length == 0 ? search.Path : new Godot.Collections.Array<Vector2I>(),
                            changed.Length == 0 ? search.Reason : changed);
                    }
                    finally { completedDemand?.Dispose(); }
                    // User callbacks may reconfigure, remove or clear this service and its sources.
                    if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) break;
                    ResolveReferences(); TrackRequestSources();
                    currentRules = BuildSearch(); configuration = RequestConfiguration();
                }
                else
                {
                    _activeRequests.RemoveFirst();
                    request.Node = _activeRequests.AddLast(request);
                }
                if (milliseconds > 0 && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= milliseconds) break;
            }
        }
        finally
        {
            _processingRequests = false;
            PathMillisecondsLastFrame = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (_requests.Count == 0 && GodotObject.IsInstanceValid(this)) SetProcess(false);
        }
    }

    private static bool SameRules(Search a, Search b)
    {
        return a.Cells == b.Cells && a.Placement == b.Placement && a.Roads == b.Roads
            && a.UseCellBlockedFlag == b.UseCellBlockedFlag && a.RespectHeight == b.RespectHeight
            && a.AllowRamps == b.AllowRamps && a.MaximumStep == b.MaximumStep
            && a.MinimumStepCost == b.MinimumStepCost
            && (a.BlockedKinds is null ? b.BlockedKinds is null : b.BlockedKinds is not null && a.BlockedKinds.SetEquals(b.BlockedKinds))
            && (a.Costs is null ? b.Costs is null : b.Costs is not null && a.Costs.Count == b.Costs.Count
                && a.Costs.All(pair => b.Costs.TryGetValue(pair.Key, out float cost) && cost == pair.Value));
    }

    private void TrackRequestSources()
    {
        if (_requestRoads == _roads) return;
        DisconnectRequestSources();
        _navigationRevision++;
        _requestRoads = _roads;
        if (_requestRoads is not null)
        {
            _requestRoads.RoadChanged += RequestRoadChanged;
            _requestRoads.RoadsChanged += RequestSourceChanged;
        }
    }

    private void RequestRoadChanged(int x, int y, string kind, bool hasRoad) => _navigationRevision++;
    private void RequestSourceChanged() => _navigationRevision++;

    private void DisconnectRequestSources()
    {
        if (GodotObject.IsInstanceValid(_requestRoads))
        {
            _requestRoads!.RoadChanged -= RequestRoadChanged;
            _requestRoads.RoadsChanged -= RequestSourceChanged;
        }
        _requestRoads = null;
    }
}
