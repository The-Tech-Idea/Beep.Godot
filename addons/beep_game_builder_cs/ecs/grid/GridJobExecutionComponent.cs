using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>World-owned execution of work at an already reached job site. Does not simulate travel.</summary>
[GlobalClass]
public partial class GridJobExecutionComponent : Node, ISaveable
{
    [Export] public NodePath JobQueuePath { get; set; } = new("");
    [Export] public NodePath ActorRegistryPath { get; set; } = new("");
    [Export] public NodePath GridPath { get; set; } = new("");
    [Export] public NodePath WorkClockPath { get; set; } = new("");
    [Export] public string SaveKey { get; set; } = "job_execution";
    [Signal] public delegate void ExecutionFinishedEventHandler(string workerId, string jobId, bool completed);
    [Signal] public delegate void ExecutionStateRestoredEventHandler();
    public int ExecutionCount => _executions.Count;
    public string GetWorkerJob(string workerId) => _executions.TryGetValue(workerId, out var entry) ? entry.Job : "";
    internal bool MatchesSources(GridJobQueueComponent? queue, GridProjectionComponent? grid) => _queue == queue && _grid == grid;
    internal bool UsesRegistry(ActorRegistryComponent registry) => _registry == registry;
    internal bool MatchesWork(string workerId, string jobId, float speed) =>
        _executions.TryGetValue(workerId, out var entry) && entry.Job == jobId && entry.Speed == speed;

    private sealed class Execution(string worker, string job, Vector2I cell, float speed)
    {
        public readonly string Worker = worker, Job = job;
        public readonly Vector2I Cell = cell;
        public readonly float Speed = speed;
        public double Pending, Started;
        public long Deadline;
    }
    private readonly Dictionary<string, Execution> _executions = new(StringComparer.Ordinal);
    private readonly Dictionary<long, string> _deadlines = new();
    private GridJobQueueComponent? _queue;
    private ActorRegistryComponent? _registry;
    private GridProjectionComponent? _grid;
    private GridWorkClockComponent? _clock;

    public override void _Ready()
    {
        _queue = GetNodeOrNull<GridJobQueueComponent>(JobQueuePath);
        _registry = GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath);
        _grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
        _clock = GridWorkClockComponent.FindFor(this, WorkClockPath);
        if (_registry is not null) _registry.ActorDestroyed += ActorDestroyed;
        if (_clock is not null) { _clock.TreeExiting += RetainAll; _clock.TreeEntered += ResumeAll; }
        AddToGroup(SaveableHelper.Group);
        SetProcess(false);
        ResumeAll();
    }

    public override void _ExitTree()
    {
        RetainAll();
        if (GodotObject.IsInstanceValid(_registry)) _registry!.ActorDestroyed -= ActorDestroyed;
        if (GodotObject.IsInstanceValid(_clock)) { _clock!.TreeExiting -= RetainAll; _clock.TreeEntered -= ResumeAll; }
        foreach (var entry in _executions.Values)
            if (GodotObject.IsInstanceValid(_queue)) _queue!.UnbindExecutor(entry.Job, this);
        RemoveFromGroup(SaveableHelper.Group);
        RequestReady();
    }

    public bool BeginWork(string workerId, string jobId, float workSpeed = 1)
    {
        if (_executions.ContainsKey(workerId) || !float.IsFinite(workSpeed) || workSpeed <= 0
            || !GodotObject.IsInstanceValid(_queue) || !GodotObject.IsInstanceValid(_clock) || !_clock!.IsInsideTree()) return false;
        var entry = new Execution(workerId, jobId, _queue!.GetReservedWorkCell(jobId), workSpeed);
        if (!CanExecute(entry) || !_queue.BindExecutor(jobId, workerId, this)) return false;
        _executions.Add(workerId, entry);
        Schedule(entry);
        return true;
    }

    private bool CanExecute(Execution entry, bool restoring = false)
    {
        if (!GodotObject.IsInstanceValid(_registry) || !GodotObject.IsInstanceValid(_queue)
            || !GodotObject.IsInstanceValid(_grid) || !_queue!.IsInsideTree() || !_registry!.IsInsideTree()) return false;
        var position = _registry.GetActorPosition(entry.Worker);
        if (!position.IsFinite() || _grid!.WorldToCell(position) != entry.Cell || !_queue.CanWorkerClaim(entry.Worker)
            || _queue.GetJobState(entry.Job) != GridJobQueueComponent.GridJobState.Claimed
            || _queue.GetJobClaimedBy(entry.Job) != entry.Worker || _queue.GetReservedWorkCell(entry.Job) != entry.Cell) return false;
        var actor = _registry.FindActor(entry.Worker);
        if (actor is null) return _registry.IsDormant(entry.Worker);
        var worker = EntityComponent.FindComponent<GridWorkerComponent>(actor.Body, false);
        return actor.IsActive && !actor.IsDead && (!actor.HasOrders || actor.IsCurrentJob(entry.Job))
            && (worker is not { CurrentJobId.Length: > 0 } || worker.UsesExecutor(this, entry.Job)
                || (restoring && worker.HasWorldExecution && worker.AcceptsExecutor(this)));
    }

    private double Elapsed(Execution entry) => entry.Deadline != 0 && GodotObject.IsInstanceValid(_clock)
        ? Math.Max(0, _clock!.ElapsedTurns - entry.Started) : 0;

    private void CancelDeadline(Execution entry)
    {
        if (GodotObject.IsInstanceValid(_clock)) _clock!.CancelScheduledWork(entry.Deadline);
        _deadlines.Remove(entry.Deadline);
        entry.Deadline = 0;
    }

    private void RetainAll()
    {
        foreach (var entry in _executions.Values) { entry.Pending += Elapsed(entry); CancelDeadline(entry); }
    }

    private void ResumeAll()
    {
        foreach (var entry in _executions.Values.ToArray())
            if (CanExecute(entry) && _queue!.BindExecutor(entry.Job, entry.Worker, this)) Schedule(entry);
            else StopWork(entry.Worker, false);
    }

    private void Schedule(Execution entry)
    {
        if (entry.Deadline != 0 || !IsInsideTree() || !GodotObject.IsInstanceValid(_clock) || !_clock!.IsInsideTree()) return;
        entry.Started = _clock.ElapsedTurns;
        // Recheck activity and ownership during long jobs, not only at completion.
        double turns = Math.Clamp(_queue!.GetJobRemainingTurns(entry.Job) / entry.Speed - entry.Pending, 0.000001, 0.25);
        entry.Deadline = _clock.ScheduleWork(this, turns, nameof(AdvanceExecution));
        if (entry.Deadline != 0) _deadlines.Add(entry.Deadline, entry.Worker);
    }

    public void AdvanceExecution(long requestId, double elapsedTurns)
    {
        if (!_deadlines.Remove(requestId, out var worker) || !_executions.TryGetValue(worker, out var entry)
            || entry.Deadline != requestId) return;
        double turns = entry.Pending + Elapsed(entry);
        entry.Pending = 0;
        entry.Deadline = 0;
        if (!CanExecute(entry)) { StopWork(worker, true); return; }
        var result = _queue!.AdvanceClaimedWorkCore(entry.Job, entry.Worker, entry.Cell,
            (float)Math.Min(float.MaxValue, turns), entry.Speed, this);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;
        if (!_executions.TryGetValue(worker, out var current) || current != entry) return;
        if (result == GridJobQueueComponent.WorkAdvanceResult.Progressed) Schedule(entry);
        else
        {
            StopWork(worker, result == GridJobQueueComponent.WorkAdvanceResult.Rejected);
            EmitSignal(SignalName.ExecutionFinished, worker, entry.Job, result == GridJobQueueComponent.WorkAdvanceResult.Completed);
        }
    }

    public bool StopWork(string workerId, bool releaseClaim = true)
    {
        if (!_executions.Remove(workerId, out var entry)) return false;
        if (releaseClaim && CanExecute(entry))
            _queue!.ReportProgress(entry.Job, (float)Math.Max(0,
                _queue.GetJobRemainingTurns(entry.Job) - (entry.Pending + Elapsed(entry)) * entry.Speed));
        CancelDeadline(entry);
        if (GodotObject.IsInstanceValid(_queue))
        {
            _queue!.UnbindExecutor(entry.Job, this);
            if (releaseClaim) _queue.ReleaseJob(entry.Job, workerId);
        }
        return true;
    }

    private void ActorDestroyed(string id) => StopWork(id, true);

    public Godot.Collections.Dictionary CaptureState()
    {
        var state = new Godot.Collections.Dictionary();
        foreach (var (worker, entry) in _executions)
            state[worker] = new Godot.Collections.Dictionary { ["job"] = entry.Job, ["cell"] = entry.Cell,
                ["speed"] = entry.Speed, ["pending"] = entry.Pending + Elapsed(entry) };
        return state;
    }

    public bool RestoreState(Godot.Collections.Dictionary state)
    {
        var restored = new List<Execution>();
        foreach (var pair in state)
        {
            if (pair.Key.VariantType != Variant.Type.String || !GridVariantReader.TryDictionary(pair.Value, out var saved)) return false;
            var entry = new Execution(pair.Key.AsString(), GridVariantReader.String(saved, "job", ""),
                GridVariantReader.Vector2I(saved, "cell", new(int.MinValue, int.MinValue)), GridVariantReader.Float(saved, "speed", 0));
            entry.Pending = GridVariantReader.Float(saved, "pending", 0);
            if (!float.IsFinite(entry.Speed) || entry.Speed <= 0 || !double.IsFinite(entry.Pending) || entry.Pending < 0
                || !CanExecute(entry, restoring: true) || !_queue!.CanBindExecutor(entry.Job, entry.Worker, this)) return false;
            restored.Add(entry);
        }
        foreach (var worker in _executions.Keys.ToArray()) StopWork(worker, false);
        foreach (var entry in restored)
        {
            if (!_queue!.BindExecutor(entry.Job, entry.Worker, this)) return false;
            _executions.Add(entry.Worker, entry);
            Schedule(entry);
        }
        EmitSignal(SignalName.ExecutionStateRestored);
        return true;
    }

    public void Save(GameBuilder.GameStateData state) { if (SaveKey.Length > 0) state.GameData[SaveKey] = CaptureState(); }
    public void Load(GameBuilder.GameStateData state)
    {
        if (state.GameData.TryGetValue(SaveKey, out var saved) && GridVariantReader.TryDictionary(saved, out var data)) RestoreState(data);
    }
}
