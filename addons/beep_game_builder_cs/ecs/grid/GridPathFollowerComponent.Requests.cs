using Godot;

namespace Beep.ECS;

public partial class GridPathFollowerComponent
{
    public bool IsPathPending => _pathRequestId != 0 || _resumeSearch;
    public string LastMoveFailure { get; private set; } = "";
    private long _pathRequestId;
    private GridNavigationComponent? _requestNavigation;
    private GridProjectionComponent? _requestGrid;
    private Vector2I _requestStart;
    private int _searchRetries;
    private bool _resumeSearch;

    private bool BeginPathRequest()
    {
        _resumeSearch = false;
        _requestStart = _grid!.WorldToCell(_body!.GlobalPosition);
        _requestNavigation = _navigation;
        _requestGrid = _grid;
        _requestNavigation!.PathRequestCompleted += OnPathRequestCompleted;
        _pathRequestId = _requestNavigation.RequestCellPath(_requestStart, DestinationCell);
        if (_pathRequestId == 0) return FailActiveRoute("navigation_request_rejected");
        IsMoving = true;
        return true;
    }

    private void CancelPathRequest()
    {
        var navigation = _requestNavigation;
        long id = _pathRequestId;
        _pathRequestId = 0;
        _requestNavigation = null;
        _requestGrid = null;
        if (!GodotObject.IsInstanceValid(navigation)) return;
        navigation!.PathRequestCompleted -= OnPathRequestCompleted;
        if (id != 0) navigation.CancelPathRequest(id);
    }

    private bool CheckPendingRequest()
    {
        if (!GodotObject.IsInstanceValid(_requestNavigation) || _navigation != _requestNavigation
            || _grid != _requestGrid || !GodotObject.IsInstanceValid(_requestGrid)
            || !_requestNavigation!.HasPathRequest(_pathRequestId))
            return FailActiveRoute("navigation_request_lost");
        return true;
    }

    private void OnPathRequestCompleted(long id, Godot.Collections.Array<Vector2I> path, string reason)
    {
        if (id != _pathRequestId) return;
        ResolveReferences();
        bool sameSources = _navigation == _requestNavigation && _grid == _requestGrid && _body is not null && _grid is not null;
        Vector2I start = _requestStart;
        CancelPathRequest();
        if (!sameSources) { FailActiveRoute("navigation_source_changed"); return; }
        bool moved = _grid!.WorldToCell(_body!.GlobalPosition) != start;
        if ((reason == "navigation_changed" || moved) && _searchRetries++ < 3)
        {
            BeginPathRequest();
            return;
        }
        if (reason.Length > 0 || moved)
        {
            FailActiveRoute(reason.Length > 0 ? reason : "navigation_start_changed");
            return;
        }
        if (!SetCellPath(path)) FailActiveRoute("navigation_route_invalid");
    }

    public override void _ExitTree()
    {
        _runtimeReady = false;
        SetPhysicsProcess(false);
        ReleaseRoutePins();
        _resumeSearch = IsPathPending || IsMoving && _cellPath.Count > 0 && _navigation?.LoadMissingTerrain == true;
        CancelPathRequest();
        _body = null; _characterBody = null; _grid = null; _navigation = null;
        RequestReady();
        base._ExitTree();
    }
}
