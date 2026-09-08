using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridWorkClockComponent
{
    private sealed record ScheduledWork(Node Owner, double Started, double Due, Callable Callback);
    private readonly Dictionary<long, ScheduledWork> _scheduledWork = new();
    private readonly Dictionary<Node, HashSet<long>> _ownerWork = new();
    private readonly PriorityQueue<long, (double Due, long Id)> _workDeadlines = new();
    private long _nextWorkId;
    private bool _dispatchingWork;
    public int ScheduledWorkCount => _scheduledWork.Count;
    [Export(PropertyHint.Range, "1,4096,1")] public int ScheduledWorkPerTick { get; set; } = 256;
    /// <summary>Cooperative dispatch budget. Zero disables timing for deterministic batch execution.
    /// An individual callback cannot be interrupted and may exceed this budget.</summary>
    [Export(PropertyHint.Range, "0,1000,0.1")] public double ScheduledWorkBudgetMilliseconds { get; set; } = 2;
    public double LastWorkDispatchMilliseconds { get; private set; }
    public int LastWorkDispatchCount { get; private set; }

    /// <summary>Schedules one callback in world turns. The callback receives request ID and actual elapsed turns.
    /// Owners leaving the tree cancel their work. Equal deadlines run in submission order.</summary>
    public long ScheduleWork(Node owner, double turns, StringName method)
    {
        if (!IsInsideTree() || Engine.IsEditorHint() || !GodotObject.IsInstanceValid(owner)
            || !owner.IsInsideTree() || owner.IsQueuedForDeletion() || owner.GetTree() != GetTree()
            || !double.IsFinite(turns) || turns <= 0 || method.IsEmpty || !owner.HasMethod(method)) return 0;
        double due = ElapsedTurns + turns;
        if (!double.IsFinite(due) || due <= ElapsedTurns) return 0;
        long id = ++_nextWorkId;
        _scheduledWork.Add(id, new(owner, ElapsedTurns, due, new Callable(owner, method)));
        if (!_ownerWork.TryGetValue(owner, out var ids)) _ownerWork.Add(owner, ids = new());
        ids.Add(id);
        _workDeadlines.Enqueue(id, (due, id));
        return id;
    }

    public bool CancelScheduledWork(long id)
    {
        if (!_scheduledWork.Remove(id, out var work)) return false;
        ForgetOwnerWork(work.Owner, id);
        CompactWorkDeadlines();
        return true;
    }

    private void CompactWorkDeadlines()
    {
        if (_workDeadlines.Count <= _scheduledWork.Count * 2 + 64) return;
        _workDeadlines.Clear();
        foreach (var (id, work) in _scheduledWork)
            _workDeadlines.Enqueue(id, (work.Due, id));
    }

    private void CancelOwnerWork(Node owner)
    {
        if (!_ownerWork.Remove(owner, out var retired)) return;
        foreach (long id in retired) _scheduledWork.Remove(id);
        CompactWorkDeadlines();
    }

    private void ForgetOwnerWork(Node owner, long id)
    {
        if (!_ownerWork.TryGetValue(owner, out var ids)) return;
        ids.Remove(id);
        if (ids.Count == 0) _ownerWork.Remove(owner);
    }

    private void DispatchScheduledWork()
    {
        if (_dispatchingWork) return;
        _dispatchingWork = true;
        ulong started = Time.GetTicksUsec();
        LastWorkDispatchCount = 0;
        double milliseconds = double.IsFinite(ScheduledWorkBudgetMilliseconds)
            ? Math.Clamp(ScheduledWorkBudgetMilliseconds, 0, 1000) : 2;
        double budgetUsec = milliseconds * 1000;
        // Reentrant time advances cannot repeatedly execute callbacks submitted
        // by callbacks in this dispatch. They remain due for the next work tick.
        long admitted = _nextWorkId;
        try
        {
            int budget = Math.Clamp(ScheduledWorkPerTick, 1, 4096);
            int examined = 0;
            while (budget-- > 0 && _workDeadlines.TryPeek(out long id, out var priority) && priority.Due <= ElapsedTurns)
            {
                if (id > admitted) break;
                // Admit at least one heap entry so small budgets cannot starve the queue.
                if (examined > 0 && budgetUsec > 0 && Time.GetTicksUsec() - started >= budgetUsec) break;
                examined++;
                _workDeadlines.Dequeue();
                if (!_scheduledWork.Remove(id, out var work)) continue;
                ForgetOwnerWork(work.Owner, id);
                if (!GodotObject.IsInstanceValid(work.Owner) || work.Owner.IsQueuedForDeletion()
                    || !work.Owner.IsInsideTree() || !work.Owner.HasMethod(work.Callback.Method)) continue;
                LastWorkDispatchCount++;
                work.Callback.Call(id, ElapsedTurns - work.Started);
                if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) break;
            }
        }
        finally
        {
            LastWorkDispatchMilliseconds = (Time.GetTicksUsec() - started) / 1000.0;
            _dispatchingWork = false;
        }
    }

    private void ClearScheduledWork()
    {
        _scheduledWork.Clear();
        _ownerWork.Clear();
        _workDeadlines.Clear();
    }
}
