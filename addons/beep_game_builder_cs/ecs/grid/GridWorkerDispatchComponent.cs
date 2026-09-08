using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>Opt-in automatic job dispatch for dormant actors using shared travel and work services.</summary>
[GlobalClass]
public partial class GridWorkerDispatchComponent : Node, ISaveable
{
    [Export] public NodePath ActorRegistryPath { get; set; } = new("");
    [Export] public NodePath JobQueuePath { get; set; } = new("");
    [Export] public NodePath GridPath { get; set; } = new("");
    [Export] public NodePath TravelPath { get; set; } = new("");
    [Export] public NodePath ExecutionPath { get; set; } = new("");
    [Export] public NodePath WorkClockPath { get; set; } = new("");
    [Export] public string SaveKey { get; set; } = "worker_dispatch";
    [Export] public Godot.Collections.Array<string> AllowedJobKinds { get; set; } = new();
    [Export(PropertyHint.Range, "0.1,3600,0.1,or_greater")] public float FailedJobRetryTurns { get; set; } = 5;
    [Signal] public delegate void AssignmentFinishedEventHandler(string actorId, string jobId, bool completed, string reason);
    public int WorkerCount => _workers.Count;
    public int AssignmentCount => _workers.Values.Count(worker => worker.Job.Length > 0);
    public string GetWorkerJob(string id) => _workers.TryGetValue(id, out var worker) ? worker.Job : "";

    private sealed class Worker(float moveSpeed, float workSpeed)
    {
        public readonly float MoveSpeed = moveSpeed, WorkSpeed = workSpeed;
        public string Job = "";
        public bool Working;
        public readonly Dictionary<string, double> RetryDeadlines = new(StringComparer.Ordinal);
    }
    private readonly Dictionary<string, Worker> _workers = new(StringComparer.Ordinal);
    private ActorRegistryComponent? _registry;
    private GridJobQueueComponent? _queue;
    private GridProjectionComponent? _grid;
    private GridActorTravelComponent? _travel;
    private GridJobExecutionComponent? _execution;
    private GridWorkClockComponent? _clock;
    private bool _dispatching;

    public override void _Ready()
    {
        _registry = GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath);
        _queue = GetNodeOrNull<GridJobQueueComponent>(JobQueuePath);
        _grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
        _travel = GetNodeOrNull<GridActorTravelComponent>(TravelPath);
        _execution = GetNodeOrNull<GridJobExecutionComponent>(ExecutionPath);
        _clock = GridWorkClockComponent.FindFor(this, WorkClockPath);
        if (_travel is not null) _travel.TravelFinished += TravelFinished;
        if (_execution is not null) _execution.ExecutionFinished += WorkFinished;
        if (_registry is not null) _registry.ActorDestroyed += ActorDestroyed;
        if (_clock is not null) _clock.WorkTick += Tick;
        AddToGroup(SaveableHelper.Group);
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_travel)) _travel!.TravelFinished -= TravelFinished;
        if (GodotObject.IsInstanceValid(_execution)) _execution!.ExecutionFinished -= WorkFinished;
        if (GodotObject.IsInstanceValid(_registry)) _registry!.ActorDestroyed -= ActorDestroyed;
        if (GodotObject.IsInstanceValid(_clock)) _clock!.WorkTick -= Tick;
        foreach (var id in _workers.Keys.ToArray()) UnregisterWorker(id);
        RemoveFromGroup(SaveableHelper.Group);
        RequestReady();
    }

    private bool Ready => GodotObject.IsInstanceValid(this) && IsInsideTree()
        && GodotObject.IsInstanceValid(_registry) && _registry!.IsInsideTree()
        && GodotObject.IsInstanceValid(_queue) && _queue!.IsInsideTree()
        && GodotObject.IsInstanceValid(_grid) && _grid!.IsInsideTree()
        && GodotObject.IsInstanceValid(_travel) && _travel!.IsInsideTree()
        && GodotObject.IsInstanceValid(_execution) && _execution!.IsInsideTree()
        && GodotObject.IsInstanceValid(_clock) && _clock!.IsInsideTree()
        && _travel.MatchesSources(_registry, _grid) && _execution.MatchesSources(_queue, _grid)
        && _execution.UsesRegistry(_registry);

    public bool RegisterWorker(string actorId, float worldUnitsPerTurn, float workSpeed = 1)
    {
        if (!Ready || _workers.ContainsKey(actorId) || !_registry!.GetActorPosition(actorId).IsFinite()
            || !_queue!.CanWorkerClaim(actorId) || !float.IsFinite(worldUnitsPerTurn) || worldUnitsPerTurn <= 0
            || !float.IsFinite(workSpeed) || workSpeed <= 0) return false;
        _workers.Add(actorId, new(worldUnitsPerTurn, workSpeed));
        return true;
    }

    public bool UnregisterWorker(string actorId)
    {
        if (!_workers.Remove(actorId, out var worker)) return false;
        ReleaseAssignment(actorId, worker);
        return true;
    }

    private void ActorDestroyed(string id) => UnregisterWorker(id);
    private void Tick(float turns)
    {
        if (!float.IsFinite(turns) || turns <= 0 || !Ready) return;
        foreach (var worker in _workers.Values)
            foreach (var job in worker.RetryDeadlines.Keys.ToArray())
            {
                if (worker.RetryDeadlines[job] <= _clock!.ElapsedTurns || !_queue!.HasJob(job))
                    worker.RetryDeadlines.Remove(job);
            }
        DispatchJobs();
    }

    public void DispatchJobs()
    {
        if (!Ready || _dispatching) return;
        _dispatching = true;
        try
        {
            foreach (var (id, worker) in _workers.ToArray())
            {
                if (!Ready) return;
                if (!_workers.TryGetValue(id, out var current) || current != worker) continue;
                if (worker.Job.Length > 0)
                {
                    if (!_queue!.CanWorkerClaim(id) || _queue.GetJobState(worker.Job) != GridJobQueueComponent.GridJobState.Claimed
                        || _queue.GetJobClaimedBy(worker.Job) != id
                        || (worker.Working ? _execution!.GetWorkerJob(id) != worker.Job : !_travel!.IsTravelling(id)))
                        Finish(id, worker, false, "assignment_invalidated");
                    continue;
                }
                // Loaded actors keep their own controls and commands; this service never steals those jobs.
                if (!_registry!.IsDormant(id) || _execution!.GetWorkerJob(id).Length > 0 || _travel!.IsTravelling(id)
                    || _queue!.FindClaimedJobId(id).Length > 0) continue;
                string job = _queue.ClaimNextJobExcluding(id, _grid!.WorldToCell(_registry.GetActorPosition(id)),
                    AllowedJobKinds, worker.RetryDeadlines.Keys.ToHashSet(StringComparer.Ordinal));
                if (job.Length == 0) continue;
                // Claim signals may remove this worker or the dispatcher.
                if (!Ready || !_workers.TryGetValue(id, out current) || current != worker)
                {
                    if (GodotObject.IsInstanceValid(_queue)) _queue!.ReleaseJob(job, id);
                    continue;
                }
                if (!_queue.BindDispatcher(job, id, this)) continue;
                worker.Job = job;
                worker.Working = false;
                if (!_travel.BeginTravel(id, _queue.GetReservedWorkCell(job), worker.MoveSpeed))
                    Finish(id, worker, false, "travel_rejected");
            }
        }
        finally { _dispatching = false; }
    }

    private void TravelFinished(string id, bool arrived, string reason)
    {
        if (!_workers.TryGetValue(id, out var worker) || worker.Job.Length == 0 || worker.Working) return;
        if (!arrived || !Ready) { Finish(id, worker, false, reason); return; }
        worker.Working = true;
        if (!_execution!.BeginWork(id, worker.Job, worker.WorkSpeed)) Finish(id, worker, false, "work_rejected");
    }

    private void WorkFinished(string id, string job, bool completed)
    {
        if (_workers.TryGetValue(id, out var worker) && worker.Working && worker.Job == job)
            Finish(id, worker, completed, completed ? "" : "work_rejected");
    }

    private void Finish(string id, Worker worker, bool completed, string reason)
    {
        string job = worker.Job;
        if (!completed && job.Length > 0 && GodotObject.IsInstanceValid(_clock))
            worker.RetryDeadlines[job] = _clock!.ElapsedTurns
                + (float.IsFinite(FailedJobRetryTurns) ? Math.Max(0.1, FailedJobRetryTurns) : 5);
        ReleaseAssignment(id, worker);
        if (GodotObject.IsInstanceValid(this) && IsInsideTree())
            EmitSignal(SignalName.AssignmentFinished, id, job, completed, reason);
    }

    private void ReleaseAssignment(string id, Worker worker)
    {
        string job = worker.Job;
        bool working = worker.Working;
        worker.Job = "";
        worker.Working = false;
        if (job.Length == 0) return;
        if (working && GodotObject.IsInstanceValid(_execution) && _execution!.GetWorkerJob(id) == job)
            _execution.StopWork(id);
        if (!working && GodotObject.IsInstanceValid(_travel)) _travel!.CancelTravel(id);
        if (GodotObject.IsInstanceValid(_queue))
        {
            _queue!.UnbindDispatcher(job, this);
            _queue.ReleaseJob(job, id);
        }
    }
}
