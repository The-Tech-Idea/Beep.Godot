using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Beep.ECS;

public partial class GridJobQueueComponent
{
    private sealed class ReservationScope
    {
        internal readonly Dictionary<string, (GridJobQueueComponent Queue, string Job)> Workers = new(System.StringComparer.Ordinal);
        internal readonly Dictionary<Vector2I, (GridJobQueueComponent Queue, string Job)> Cells = new();
    }

    // A live map scopes claims; no global worker identity or duplicate world service.
    private static readonly ConditionalWeakTable<GridCellDataComponent, ReservationScope> ReservationScopes = new();
    private GridCellDataComponent? _reservationCells;
    private ReservationScope? _reservationScope;

    private bool EnsureReservationScope()
    {
        var cells = IsInsideTree() && !ChunkCellDataPath.IsEmpty
            ? GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath) : null;
        BindReservationScope(cells);
        return ChunkCellDataPath.IsEmpty || cells is not null;
    }

    private void BindReservationScope(GridCellDataComponent? cells)
    {
        if (_reservationCells == cells) return;
        ReleaseSharedReservations();
        _reservationCells = cells;
        _reservationScope = cells is null ? null : ReservationScopes.GetValue(cells, _ => new());
        RebuildReservations();
    }

    private bool SharedClaimAvailable(string worker, Vector2I cell)
        => _reservationScope is null || (!_reservationScope.Workers.ContainsKey(worker) && !_reservationScope.Cells.ContainsKey(cell));

    private bool SharedCellAvailable(Vector2I cell, string job)
        => _reservationScope is null || !_reservationScope.Cells.TryGetValue(cell, out var claim)
            || claim == (this, job);

    private void AddSharedReservation(GridJob job)
    {
        if (_reservationScope is null) return;
        _reservationScope.Workers.Add(job.ClaimedBy, (this, job.Id));
        _reservationScope.Cells.Add(job.ReservedCell, (this, job.Id));
    }

    private void ReleaseSharedReservation(GridJob job)
    {
        if (_reservationScope is null) return;
        if (_reservationScope.Workers.TryGetValue(job.ClaimedBy, out var worker) && worker == (this, job.Id))
            _reservationScope.Workers.Remove(job.ClaimedBy);
        if (_reservationScope.Cells.TryGetValue(job.ReservedCell, out var cell) && cell == (this, job.Id))
            _reservationScope.Cells.Remove(job.ReservedCell);
    }

    private void ReleaseSharedReservations()
    {
        if (_reservationScope is null) return;
        foreach (var claim in _workerClaims)
            if (_reservationScope.Workers.TryGetValue(claim.Key, out var owner) && owner == (this, claim.Value))
                _reservationScope.Workers.Remove(claim.Key);
        foreach (var claim in _workCells)
            if (_reservationScope.Cells.TryGetValue(claim.Key, out var owner) && owner == (this, claim.Value))
                _reservationScope.Cells.Remove(claim.Key);
    }
}
