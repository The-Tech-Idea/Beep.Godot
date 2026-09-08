using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

public partial class GridJobQueueComponent
{
    private readonly Dictionary<string, string> _workerClaims = new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, string> _workCells = new();

    /// <summary>Checks local claims and other queues bound to the same live map.</summary>
    public bool CanClaimJob(string id, string workerId)
        => EnsureReservationScope() && CanWorkerClaim(workerId) && !_workerClaims.ContainsKey(workerId)
            && _jobs.TryGetValue(id, out var job) && job.State == GridJobState.Queued
            && job.ApproachCell.X != int.MinValue && job.ApproachCell.Y != int.MinValue
            && !_workCells.ContainsKey(job.ApproachCell) && SharedClaimAvailable(workerId, job.ApproachCell);

    public string GetWorkCellReservation(Vector2I cell) => _workCells.GetValueOrDefault(cell, "");

    public Vector2I GetReservedWorkCell(string id)
        => _jobs.TryGetValue(id, out var job) && job.State == GridJobState.Claimed
            ? job.ReservedCell : new(int.MinValue, int.MinValue);

    /// <summary>Atomically transfers a claim's standing cell, for example when choosing a path fallback.</summary>
    public bool TryReserveWorkCell(string id, string workerId, Vector2I cell)
    {
        if (!EnsureReservationScope()) return false;
        if (!_jobs.TryGetValue(id, out var job) || job.State != GridJobState.Claimed
            || job.ClaimedBy != workerId || cell.X == int.MinValue || cell.Y == int.MinValue) return false;
        if (_workCells.TryGetValue(cell, out var other) && other != id) return false;
        if (!SharedCellAvailable(cell, id)) return false;
        ReleaseSharedReservation(job);
        _workCells.Remove(job.ReservedCell);
        job.ReservedCell = cell;
        _workCells[cell] = id;
        AddSharedReservation(job);
        RefreshChunkPins();
        return true;
    }

    private void ReserveClaim(GridJob job, string workerId)
    {
        job.State = GridJobState.Claimed;
        job.ClaimedBy = workerId;
        job.ReservedCell = job.ApproachCell;
        _workerClaims.Add(workerId, job.Id);
        _workCells.Add(job.ReservedCell, job.Id);
        AddSharedReservation(job);
    }

    private void ReleaseReservation(GridJob job)
    {
        if (job.State != GridJobState.Claimed) return;
        _executionOwners.Remove(job.Id);
        _dispatchOwners.Remove(job.Id);
        ReleaseSharedReservation(job);
        _workerClaims.Remove(job.ClaimedBy);
        _workCells.Remove(job.ReservedCell);
    }

    private void RebuildReservations()
    {
        _executionOwners.Clear();
        _dispatchOwners.Clear();
        ReleaseSharedReservations();
        _workerClaims.Clear();
        _workCells.Clear();
        // Saved claims are facts, not trusted indexes. Conflicting claims return to the queue.
        foreach (var job in _jobs.Values.OrderBy(job => job.Id, StringComparer.Ordinal))
        {
            if (job.State != GridJobState.Claimed) continue;
            if (string.IsNullOrWhiteSpace(job.ClaimedBy) || _workerClaims.ContainsKey(job.ClaimedBy)
                || _workCells.ContainsKey(job.ReservedCell) || !SharedClaimAvailable(job.ClaimedBy, job.ReservedCell)
                || job.ReservedCell.X == int.MinValue || job.ReservedCell.Y == int.MinValue)
            {
                job.State = GridJobState.Queued;
                job.ClaimedBy = "";
                continue;
            }
            _workerClaims.Add(job.ClaimedBy, job.Id);
            _workCells.Add(job.ReservedCell, job.Id);
            AddSharedReservation(job);
        }
    }
}
