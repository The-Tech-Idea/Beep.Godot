using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    private bool _autoEnforceChunkBudget;
    [Export] public bool AutoEnforceChunkBudget
    {
        get => _autoEnforceChunkBudget;
        set
        {
            _autoEnforceChunkBudget = value;
            if (!value && _budgetSaveId != 0 && PendingSaveId == _budgetSaveId) CancelSave();
            if (IsInsideTree()) SetProcess(NeedsProcessing);
        }
    }
    [Export(PropertyHint.Range, "1,65536,1")] public int MaximumResidentChunks { get; set; } = 256;
    /// <summary>Optional exact record limit; zero disables it. Not a byte or total-process memory limit.</summary>
    [Export(PropertyHint.Range, "0,1048576,1024,or_greater")] public int MaximumResidentCells { get; set; }
    public int ResidentChunksOverBudget => GetNodeOrNull<GridCellDataComponent>(CellDataPath) is { } cells
        ? Math.Max(0, cells.StoredChunkCount - Math.Clamp(MaximumResidentChunks, 1, 65536)) : 0;
    public int ResidentCellsOverBudget => MaximumResidentCells > 0 && GetNodeOrNull<GridCellDataComponent>(CellDataPath) is { } cells
        ? Math.Max(0, cells.CellCount - MaximumResidentCells) : 0;
    private bool NeedsProcessing => IsBusy || AutoLoadPinnedChunks || AutoEnforceChunkBudget;
    private ulong _nextBudgetScan, _budgetServiceId;
    private string _budgetDirectory = "";
    private Vector2I? _budgetCandidate, _budgetCursor;
    private Vector2I? _budgetSavedCandidate;
    private long _budgetSaveId;
    private readonly Dictionary<Vector2I, ulong> _budgetRetryAt = new();

    public void ProcessChunkBudget()
    {
        if (!IsInsideTree() || Engine.IsEditorHint() || !AutoEnforceChunkBudget || IsBusy) return;
        ulong now = Time.GetTicksMsec();
        if (now < _nextBudgetScan) return;
        // 100 ms is the idle re-check. While over budget the pipeline runs continuously: a step
        // that succeeds makes the next one eligible at once (the end of this method, and
        // FinishBudgetSave), so a chunk is released in the frame its write publishes and the
        // next save starts the frame after. Measured on a 1024x1024 world, the fixed 100 ms wait
        // between every step was one of three reasons retirement managed 38 chunks in 15 s.
        _nextBudgetScan = now + 100;
        var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        if (!GodotObject.IsInstanceValid(cells) || !cells!.IsInsideTree() || cells.IsQueuedForDeletion()) return;
        if (_budgetServiceId != cells.GetInstanceId() || _budgetDirectory != ArchiveDirectory)
        {
            _budgetRetryAt.Clear();
            _budgetCandidate = _budgetCursor = null;
            _budgetSavedCandidate = null;
            _budgetServiceId = cells.GetInstanceId();
            _budgetDirectory = ArchiveDirectory;
        }
        bool cellPressure = ResidentCellsOverBudget > 0;
        if (ResidentChunksOverBudget == 0 && !cellPressure)
        {
            _budgetCandidate = null;
            _budgetRetryAt.Clear();
            return;
        }
        if (_budgetSavedCandidate is { } saved)
        {
            // A continuously edited chunk must not monopolize the writer while other idle chunks can retire.
            if (!IsChunkSaveCurrent(saved))
            {
                _budgetRetryAt[saved] = now + 2000;
                _budgetCandidate = null;
            }
            _budgetSavedCandidate = null;
        }
        var coordinates = new List<Vector2I>(cells.GetStoredChunks());
        coordinates.Sort((a, b) => a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        bool Eligible(Vector2I at) => (!_budgetRetryAt.TryGetValue(at, out ulong retry) || now >= retry)
            && cells.CanEvictChunk(at);
        Vector2I? chosen = _budgetCandidate is { } pending && coordinates.Contains(pending) && Eligible(pending) ? pending : null;
        int start = _budgetCursor is { } cursor ? coordinates.FindIndex(at => Before(cursor, at)) : 0;
        if (start < 0) start = 0;
        // Under record pressure, prefer a dense chunk within the bounded fair scan.
        // Keep a pending save candidate stable until its publication can be verified.
        bool scan = chosen is null;
        int largest = 0;
        for (int i = 0; scan && i < Math.Min(128, coordinates.Count); i++)
        {
            var at = coordinates[(start + i) % coordinates.Count];
            _budgetCursor = at;
            if (!Eligible(at)) continue;
            int count = cells.GetStoredChunkCellCount(at);
            if (count > largest) { chosen = at; largest = count; }
            if (!cellPressure) break;
        }
        _budgetCandidate = chosen;
        if (chosen is not { } coordinate) return;
        if (IsChunkSaveCurrent(coordinate))
        {
            if (EvictSavedChunk(coordinate)) _nextBudgetScan = now;
            else _budgetRetryAt[coordinate] = now + 2000;
            _budgetCandidate = null;
        }
        else
        {
            _budgetSaveId = RequestSaveChunk(coordinate);
            if (_budgetSaveId == 0) { _budgetRetryAt[coordinate] = now + 2000; _budgetCandidate = null; }
        }
    }

    private void FinishBudgetSave(long id, bool success)
    {
        if (id != _budgetSaveId) return;
        _budgetSaveId = 0;
        if (success)
        {
            _budgetSavedCandidate = _budgetCandidate;
            // Verify and release in this same frame, not at the next 100 ms tick.
            _nextBudgetScan = 0;
        }
        if (!success && _budgetCandidate is { } at)
        {
            _budgetRetryAt[at] = Time.GetTicksMsec() + 2000;
            _budgetCandidate = null;
        }
    }
}
