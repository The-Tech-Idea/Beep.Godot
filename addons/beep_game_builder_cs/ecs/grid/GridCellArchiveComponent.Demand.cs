using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    private bool _autoLoadPinnedChunks;
    [Export] public bool AutoLoadPinnedChunks
    {
        get => _autoLoadPinnedChunks;
        set
        {
            _autoLoadPinnedChunks = value;
            if (!value && _automaticReadId != 0) CancelLoad();
            _nextDemandScan = 0;
            if (IsInsideTree()) SetProcess(NeedsProcessing);
        }
    }
    [Export(PropertyHint.Range, "0.25,60,0.25")] public double DemandRetrySeconds { get; set; } = 2;
    private readonly Dictionary<Vector2I, ulong> _demandRetryAt = new();
    private ulong _nextDemandScan, _demandServiceId;
    private string _demandDirectory = "";
    private long _automaticReadId;
    private Vector2I _automaticCoordinate;
    private Vector2I? _lastDemandCandidate;

    private bool AutomaticReadStillWanted()
    {
        if (_automaticReadId == 0 || PendingLoadId != _automaticReadId) return true;
        return AutoLoadPinnedChunks && IsInsideTree() && GodotObject.IsInstanceValid(_readCells)
            && GetNodeOrNull<GridCellDataComponent>(CellDataPath) == _readCells
            && ArchiveDirectory == _readDirectory && _readCells!.IsInsideTree()
            && _readCells.IsChunkPinned(_readCoordinate) && !_readCells.IsChunkAvailable(_readCoordinate);
    }

    public void ProcessChunkDemand()
    {
        if (!IsInsideTree() || Engine.IsEditorHint()) return;
        if (!AutomaticReadStillWanted()) CancelLoad();
        if (!AutoLoadPinnedChunks || IsBusy) return;
        ulong now = Time.GetTicksMsec();
        if (now < _nextDemandScan) return;
        _nextDemandScan = now + 100;
        var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        if (!GodotObject.IsInstanceValid(cells) || !cells!.IsInsideTree() || cells.IsQueuedForDeletion()) return;
        if (_demandServiceId != cells.GetInstanceId() || _demandDirectory != ArchiveDirectory)
        {
            _demandRetryAt.Clear();
            _lastDemandCandidate = null;
            _demandServiceId = cells.GetInstanceId();
            _demandDirectory = ArchiveDirectory;
        }
        var stale = new List<Vector2I>();
        foreach (var pair in _demandRetryAt)
            if (!cells.IsChunkPinned(pair.Key) || cells.IsChunkAvailable(pair.Key)) stale.Add(pair.Key);
        foreach (var coordinate in stale) _demandRetryAt.Remove(coordinate);
        Vector2I? candidate = null, first = null;
        foreach (var coordinate in cells.GetPinnedChunks())
        {
            if (cells.IsChunkAvailable(coordinate)
                || (_demandRetryAt.TryGetValue(coordinate, out ulong at) && now < at)) continue;
            if (first is null || Before(coordinate, first.Value)) first = coordinate;
            if ((_lastDemandCandidate is null || Before(_lastDemandCandidate.Value, coordinate))
                && (candidate is null || Before(coordinate, candidate.Value))) candidate = coordinate;
        }
        candidate ??= first;
        if (candidate is not { } next) return;
        _lastDemandCandidate = next;
        _automaticCoordinate = next;
        _automaticReadId = RequestLoadChunk(next);
        if (_automaticReadId == 0) DelayDemandRetry(next);
    }

    private static bool Before(Vector2I a, Vector2I b) => a.X < b.X || (a.X == b.X && a.Y < b.Y);

    private void DelayDemandRetry(Vector2I coordinate)
    {
        double seconds = double.IsFinite(DemandRetrySeconds) ? Math.Clamp(DemandRetrySeconds, 0.25, 60) : 2;
        _demandRetryAt[coordinate] = Time.GetTicksMsec() + (ulong)(seconds * 1000);
    }

    private void FinishAutomaticRead(long id, bool success)
    {
        if (id != _automaticReadId) return;
        _automaticReadId = 0;
        if (success) _demandRetryAt.Remove(_automaticCoordinate);
        else DelayDemandRetry(_automaticCoordinate);
    }
}
